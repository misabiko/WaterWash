using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace WaterWash {
	[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
	public class PlayerMovement : MonoBehaviour {
		[SerializeField] CinemachineCamera mainCamera;
		[SerializeField] CinemachineCamera sprayAimCamera;
		[SerializeField] Transform sprayTransform;
		[SerializeField] Animator animator;

		[Header("Parameters")]
		[SerializeField, Min(0)] float runSpeed = 5;
		[SerializeField, Min(0)] float crouchSpeed = 2;
		//I say jumpForce, but it's really acceleration, with mass of 1
		[SerializeField, Min(0)] float jumpForce = 10;
		[SerializeField, Min(0)] float crouchBackFlipJumpForce = 20;
		[SerializeField, Min(0)] float crouchBackFlipHVel = 1.5f;
		[SerializeField, Min(0)] float crouchLongJumpForce = 12;
		[SerializeField, Min(0)] float crouchLongJumpHVel = 10;
		[SerializeField, Min(0)] float plungeYForce = 12;
		[SerializeField, Min(0)] float plungeHVel = 13;
		[SerializeField, Min(0)] float longJumpDuration = 0.5f;
		[SerializeField, Min(0)] float longJumpForce = 20;
		[SerializeField, Min(0)] float acceleration = 20;
		[SerializeField, Min(0)] float deceleration = 20;
		[SerializeField, Min(0)] float slidingDeceleration = 8;
		[SerializeField, Min(0)] float maxAngleToStartMoving = 45;
		[SerializeField, Min(0)] float stoppedRotationSpeed = 700;
		[SerializeField, Min(0)] float movingRotationSpeed = 500;
		[SerializeField, Min(0)] float slidingRotationSpeed = 50;

		[SerializeField, Min(0)] float aimRotateSpeed = 100;
		//Unity doesn't have a MaxAttribute...
		[SerializeField] float minYSprayAngle = -40;
		[SerializeField, Min(0)] float maxYSprayAngle = 30;

		[Header("Wall Slide/Jump")]
		[SerializeField] LayerMask wallSlideMask;
		[SerializeField, Min(0)] float wallSlideDetectionDistance = 1f;
		[SerializeField, Range(0, 1)] float wallMaxSlopiness = 0.4f;
		[SerializeField, Min(0)] float wallSlideMaxHorizontalAngle = 40;
		[SerializeField, Min(0)] float wallSlideFriction = 34;
		[SerializeField] bool horizontalWallSlide;
		[SerializeField, Min(0)] float horizontalWallSlideDeceleration = 1;
		[SerializeField, Min(0)] float propulsedHorizontalVelocity = 8;
		[SerializeField, Min(0)] float propulsedAcceleration = 3;
		[SerializeField, Min(0)] float propulsedDeceleration = 1;

		CharacterController controller;
		InputAction moveAction;
		InputAction lookAction;
		InputAction jumpAction;
		InputAction crouchAction;
		InputAction plungeAction;
		InputAction aimAction;

		Vector3 velocity;
		//Not null if the player is holding jump
		float? jumpStartTime;
		bool pushingOnWall;
		JumpTypes? _jumpType;

		JumpTypes? JumpType {
			get => _jumpType;
			set {
				_jumpType = value;
				if (value != null)
					sliding = value is JumpTypes.Plunge or JumpTypes.CrouchLongJump;
			}
		}

		// When landing from long jump/plunge
		bool sliding;

		float wallSlopiness;//Debugging
		float wallHorizontalAngle;//Debugging
		float? moveAngle;//Debugging
		float maxSpeed;//Debugging

		static readonly int AnimRunSpeed = Animator.StringToHash("RunSpeed");
		static readonly int AnimJumping = Animator.StringToHash("Jumping");
		static readonly int AnimGrounded = Animator.StringToHash("Grounded");
		static readonly int AnimPushingOnWall = Animator.StringToHash("PushingOnWall");
		static readonly int AnimCrouching = Animator.StringToHash("Crouching");
		static readonly int AnimSliding = Animator.StringToHash("Sliding");

		void Awake() {
			controller = GetComponent<CharacterController>();

			var playerInput = GetComponent<PlayerInput>();
			moveAction = playerInput.actions["Move"];
			lookAction = playerInput.actions["Look"];
			jumpAction = playerInput.actions["Jump"];
			crouchAction = playerInput.actions["Crouch"];
			plungeAction = playerInput.actions["Plunge"];
			aimAction = playerInput.actions["Aim"];
		}

		void Update() {
			{
				if (aimAction.WasPressedThisFrame()) {
					sprayAimCamera.gameObject.SetActive(true);
					//Eyeballed around 40 degrees between the camera and the spray
					sprayTransform.localEulerAngles = new Vector3(mainCamera.transform.localEulerAngles.x + 40f, 0, 0);
				}else if (aimAction.WasReleasedThisFrame())
					sprayAimCamera.gameObject.SetActive(false);

				if (aimAction.IsPressed()) {
					var look = lookAction.ReadValue<Vector2>();
					transform.Rotate(Vector3.up, look.x * aimRotateSpeed * Time.deltaTime);
					sprayTransform.Rotate(Vector3.right, -look.y * aimRotateSpeed * Time.deltaTime);

					if (sprayTransform.localEulerAngles.x >= 180f)
						sprayTransform.localEulerAngles = new Vector3(Mathf.Max(sprayTransform.localEulerAngles.x, 360f + minYSprayAngle), 0, 0);
					else
						sprayTransform.localEulerAngles = new Vector3(Mathf.Min(sprayTransform.localEulerAngles.x, maxYSprayAngle), 0, 0);
				}
			}

			//Could move this with the rest of velocity.y stuff?
			if (controller.isGrounded && velocity.y < 0) {
				velocity.y = 0f;
				jumpStartTime = null;
				JumpType = null;
			}

			pushingOnWall = false;
			bool crouching = controller.isGrounded && crouchAction.IsPressed();

			float usedMoveSpeed = crouching ? crouchSpeed : runSpeed;

			var moveInput = moveAction.ReadValue<Vector2>();
			//Could add cam.up to avoid locking when looking straight up/down
			Vector3 camForward = Utility.GetHorizontal(Camera.main!.transform.forward).normalized;
			Vector3 moveDirection = camForward * moveInput.y + Camera.main.transform.right * moveInput.x;
			if (moveDirection.sqrMagnitude > 1)
				moveDirection.Normalize();

			maxSpeed = WasPropulsed
				? Mathf.Infinity
				: usedMoveSpeed * moveDirection.magnitude;

			Vector3 hVel = Utility.GetHorizontal(velocity);
			if (moveInput != Vector2.zero) {
				//Wall slide detection
				if (!controller.isGrounded) {
					Vector3 rayDir = moveDirection.normalized;
					float rayDistance = controller.radius + wallSlideDetectionDistance;
					Debug.DrawLine(transform.position, transform.position + rayDir * rayDistance, Color.red);

					if (Physics.Raycast(transform.position, rayDir, out RaycastHit hit, rayDistance, wallSlideMask)) {
						//sloppy way to get the pitch angle of the wall, like its wall to slope ratio, as opposed to the angle of how much the player is pointing toward the wall
						wallSlopiness = Mathf.Abs(hit.normal.y);
						wallHorizontalAngle = Vector2.Angle(-(Utility.GetHorizontal2D(hit.normal)), Utility.GetHorizontal2D(rayDir));

						if (wallSlopiness <= wallMaxSlopiness && wallHorizontalAngle <= wallSlideMaxHorizontalAngle) {
							pushingOnWall = true;
							Vector3 flatNormal = Utility.GetHorizontal(hit.normal).normalized;

							if (!horizontalWallSlide) {
								//limit horizontal movement along wall's normal (aka no horizontal slide)
								velocity.SetHorizontal(Vector3.Project(velocity, -flatNormal));
							}

							transform.forward = flatNormal;

							animator.Play("WallSlide");
							//Not sure where I was going with that
							animator.SetBool(AnimJumping, JumpType == JumpTypes.WallJump);
						}
					}
				}

				//Horizontal Movement
				moveAngle = Vector2.Angle(Utility.GetHorizontal2D(moveDirection), Utility.GetHorizontal2D(transform.forward));
				if ((hVel.sqrMagnitude > 0 || moveAngle < maxAngleToStartMoving || aimAction.IsPressed()) && !pushingOnWall) {
					float usedAcceleration = WasPropulsed ? propulsedAcceleration : acceleration;
					Vector3 direction = aimAction.IsPressed() ? moveDirection.normalized : transform.forward;

					if (hVel.sqrMagnitude < maxSpeed * maxSpeed)
						hVel += direction * (moveDirection.magnitude * usedAcceleration * Time.deltaTime);
					hVel = Vector3.Project(hVel, direction);
					velocity.SetHorizontal(hVel);
				}

				//Rotation
				//TODO Reenable rotation (and normal movement) a few seconds after wall jump
				//TODO Faster turn around anim/rotation when near 180°
				if (!aimAction.IsPressed() && !pushingOnWall && !WasPropulsed) {
					float rotationSpeed;
					if (sliding)
						rotationSpeed = slidingRotationSpeed;
					else if (hVel.sqrMagnitude > 0)
						rotationSpeed = movingRotationSpeed;
					else
						rotationSpeed = stoppedRotationSpeed;
					transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(moveDirection), rotationSpeed * Time.deltaTime);
				}
			} else
				moveAngle = null;

			//Horizontal deceleration
			/*if (moveInput == Vector2.zero || WasPropulsed || (pushingOnWall && horizontalWallSlide))*/
			{
				float usedDeceleration;
				if (WasPropulsed)
					usedDeceleration = propulsedDeceleration;
				else if (pushingOnWall)
					usedDeceleration = horizontalWallSlideDeceleration;
				else if (sliding)
					usedDeceleration = slidingDeceleration;
				else
					usedDeceleration = deceleration;

				if (hVel.sqrMagnitude < Mathf.Pow(maxSpeed + usedDeceleration * Time.deltaTime, 2)) {
					hVel = Vector3.ClampMagnitude(hVel, maxSpeed);
					//bad workaround to avoiding case when maxSpeed is infinity (propulsed)
					if (controller.isGrounded)
						sliding = false;
				} else
					hVel -= hVel.normalized * (usedDeceleration * Time.deltaTime);

				velocity.SetHorizontal(hVel);
			}

			//Jumping
			if (plungeAction.WasPressedThisFrame() && JumpType != JumpTypes.Plunge/* && (controller.isGrounded || pushingOnWall)*/) {
				velocity.y = plungeYForce;
				JumpType = JumpTypes.Plunge;
				velocity.SetHorizontal(transform.forward * plungeHVel);
				animator.Play("Plunge");
				animator.SetBool(AnimJumping, true);
				jumpStartTime = null;
			} else if (jumpStartTime != null && JumpType == JumpTypes.Normal) {
				if (!jumpAction.IsPressed() || Time.time - jumpStartTime > longJumpDuration)
					jumpStartTime = null;
				else
					velocity.y += longJumpForce * Time.deltaTime;
			} else if (jumpAction.WasPressedThisFrame() && (controller.isGrounded || pushingOnWall)) {
				//Setting velY to a jumpVelocity vs adding a jumpForce and adding the gravity afterward?
				//The latter is physically correct, and would support changing the gravity, but I feel weird about the impulse being multiplied by deltaTime (either on jumpForce or gravity)
				jumpStartTime = Time.time;

				if (pushingOnWall) {
					velocity.y = jumpForce;
					JumpType = JumpTypes.WallJump;
					velocity.SetHorizontal(transform.forward * propulsedHorizontalVelocity);
				} else if (crouching) {
					if (hVel == Vector3.zero) {
						velocity.y = crouchBackFlipJumpForce;
						JumpType = JumpTypes.CrouchBackFlip;
						velocity.SetHorizontal(-transform.forward * crouchBackFlipHVel);
						animator.Play("CrouchBackFlip");
					} else {
						velocity.y = crouchLongJumpForce;
						JumpType = JumpTypes.CrouchLongJump;
						velocity.SetHorizontal(transform.forward * crouchLongJumpHVel);
						animator.Play("CrouchLongJump", -1, 0f);
					}
				} else {
					velocity.y = jumpForce;
					JumpType = JumpTypes.Normal;
					animator.Play("Jump");
				}

				animator.SetBool(AnimJumping, true);
			}

			velocity.y += Physics.gravity.y * Time.deltaTime;
			if (pushingOnWall && velocity.y < 0)
				velocity.y = Mathf.Min(0f, velocity.y + wallSlideFriction * Time.deltaTime);

			controller.Move(velocity * Time.deltaTime);

			//TODO Split velocity into horizontal and vertical until end of Update
			animator.SetFloat(AnimRunSpeed, Utility.GetHorizontal(velocity).magnitude / usedMoveSpeed);
			animator.SetBool(AnimGrounded, controller.isGrounded);
			if (controller.isGrounded)
				animator.SetBool(AnimJumping, false);
			animator.SetBool(AnimPushingOnWall, pushingOnWall);
			animator.SetBool(AnimCrouching, crouching);
			animator.SetBool(AnimSliding, sliding);
		}

		public void Teleport(Vector3 position, Vector3? rotation) {
			controller.enabled = false;

			//TODO Snap camera better
			transform.position = position;
			if (rotation is {} rot)
				transform.eulerAngles = rot;
			velocity = Vector3.zero;

			controller.enabled = true;
		}

		//Not great name, basically when not full air control, like wall jump or getting launched from cannon
		bool WasPropulsed
			=> JumpType
				is JumpTypes.WallJump
				or JumpTypes.CrouchBackFlip
				or JumpTypes.CrouchLongJump
				or JumpTypes.Plunge;

		//Very temporary debugging, could go in the inspector
		void OnGUI() {
			int y = 10;
			Utility.AddGUILabel(ref y, $"Velocity: {velocity}");
			Utility.AddGUILabel(ref y, $"HVel: {(Utility.GetHorizontal2D(velocity)).magnitude:F2} m/s");
			Utility.AddGUILabel(ref y, $"maxSpeed: {maxSpeed:F2} m/s");
			Utility.AddGUILabel(ref y, $"sliding: {sliding}");
			Utility.AddGUILabel(ref y, $"Move Angle: {moveAngle:F1}");
			Utility.AddGUILabel(ref y, $"Jump type: {JumpType}");
			Utility.AddGUILabel(ref y, $"Pushing on wall: {pushingOnWall}");
			Utility.AddGUILabel(ref y, $"Wall Slope: {wallSlopiness}");
			Utility.AddGUILabel(ref y, $"Wall H Angle: {wallHorizontalAngle}");
			Utility.AddGUILabel(ref y, $"Spray Angle: {(sprayTransform.localEulerAngles.x - 40f):F2}");
			Utility.AddGUILabel(ref y, $"Cam Angle: {mainCamera.transform.localEulerAngles.x:F2}");
			Utility.AddGUILabel(ref y, $"Spray Cam Angle: {sprayAimCamera.transform.localEulerAngles.x:F2}");
		}

		void DrawArrow(Vector3 direction, Color color) {
			Vector3 bottom = transform.position + Vector3.down * controller.height / 2;
			Debug.DrawLine(bottom, bottom + direction, color);
		}
	}

	enum JumpTypes {
		Normal,
		CrouchBackFlip,
		CrouchLongJump,
		Plunge,
		SpinJump,
		WallJump,
	}
}