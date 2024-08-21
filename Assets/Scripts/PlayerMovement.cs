using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour {
	[SerializeField] CinemachineInputAxisController camInputAxisController;
	[SerializeField] CinemachineCamera sprayAimCamera;
	[SerializeField] Transform sprayTransform;
	[SerializeField] Animator animator;

	[Header("Parameters")]
	[SerializeField, Min(0)] float runSpeed = 5;
	[SerializeField, Min(0)] float crouchSpeed = 2;
	//I say jumpForce, but it's really acceleration, with mass of 1
	[SerializeField, Min(0)] float jumpForce = 5;
	[SerializeField, Min(0)] float crouchBackFlipJumpForce = 20;
	[SerializeField, Min(0)] float crouchBackFlipHVel = 0.5f;
	[SerializeField, Min(0)] float crouchLongJumpForce = 12;
	[SerializeField, Min(0)] float crouchLongJumpHVel = 6;
	[SerializeField, Min(0)] float plungeYForce = 12;
	[SerializeField, Min(0)] float plungeHVel = 10;
	[SerializeField, Min(0)] float longJumpDuration = 0.5f;
	[SerializeField, Min(0)] float longJumpForce = 3;
	[SerializeField, Min(0)] float acceleration = 5;
	[SerializeField, Min(0)] float deceleration = 1;
	[SerializeField, Min(0)] float maxAngleToStartMoving = 45;
	[SerializeField, Min(0)] float stoppedRotationSpeed = 700;
	[SerializeField, Min(0)] float movingRotationSpeed = 350;

	[SerializeField, Min(0)] float aimRotateSpeed = 1;
	//Unity doesn't have a MaxAttribute...
	[SerializeField] float minYSprayAngle = -1;
	[SerializeField, Min(0)] float maxYSprayAngle = 1;

	[Header("Wall Slide/Jump")]
	[SerializeField] LayerMask wallSlideMask;
	[SerializeField, Min(0)] float wallSlideDetectionDistance = 0.25f;
	[SerializeField, Range(0, 1)] float wallMaxSlopiness = 15;
	[SerializeField, Min(0)] float wallSlideMaxHorizontalAngle = 40;
	[SerializeField, Min(0)] float wallSlideFriction = 1;
	[SerializeField] bool horizontalWallSlide;
	[SerializeField, Min(0)] float horizontalWallSlideDeceleration = 5;
	[SerializeField, Min(0)] float propulsedHorizontalVelocity = 1;
	[SerializeField, Min(0)] float propulsedAcceleration = 5;
	[SerializeField, Min(0)] float propulsedDeceleration = 10;

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
	JumpType? jumpType;

	float wallSlopiness;	//Debugging
	float wallHorizontalAngle;	//Debugging
	float? moveAngle;	//Debugging

	static readonly int AnimRunSpeed = Animator.StringToHash("RunSpeed");
	static readonly int AnimJumping = Animator.StringToHash("Jumping");
	static readonly int AnimGrounded = Animator.StringToHash("Grounded");
	static readonly int AnimPushingOnWall = Animator.StringToHash("PushingOnWall");
	static readonly int AnimCrouching = Animator.StringToHash("Crouching");

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
			if (aimAction.WasPressedThisFrame())
				sprayAimCamera.gameObject.SetActive(true);
			else if (aimAction.WasReleasedThisFrame())
				sprayAimCamera.gameObject.SetActive(false);

			//TODO Fix aim movement
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
			jumpType = null;
		}

		pushingOnWall = false;
		bool crouching = controller.isGrounded && crouchAction.IsPressed();

		float usedMoveSpeed = crouching ? crouchSpeed : runSpeed;

		var moveInput = moveAction.ReadValue<Vector2>();
		Vector3 hVel = Utility.GetHorizontal(velocity);
		if (moveInput != Vector2.zero) {
			//Could add cam.up to avoid locking when looking straight up/down
			Vector3 camForward = Camera.main!.transform.forward;
			camForward = Utility.GetHorizontal(camForward);
			Vector3 moveDirection = camForward * moveInput.y + Camera.main.transform.right * moveInput.x;
			if (moveDirection.sqrMagnitude > 1)
				moveDirection.Normalize();

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
						animator.SetBool(AnimJumping, jumpType == JumpType.WallJump);
					}
				}
			}

			//Horizontal Movement
			moveAngle = Vector2.Angle(Utility.GetHorizontal2D(moveDirection), Utility.GetHorizontal2D(transform.forward));
			if ((hVel.sqrMagnitude > 0 || moveAngle < maxAngleToStartMoving) && !pushingOnWall) {
				float maxSpeed = WasPropulsed
					? Mathf.Infinity
					: usedMoveSpeed * moveDirection.magnitude;
				float usedAcceleration = WasPropulsed ? propulsedAcceleration : acceleration;

				hVel = Vector3.Project(hVel + transform.forward * (moveDirection.magnitude * usedAcceleration * Time.deltaTime), transform.forward);
				hVel = Vector3.ClampMagnitude(hVel, maxSpeed);
				velocity.SetHorizontal(hVel);
			}

			//Rotation
			//TODO Reenable rotation (and normal movement) a few seconds after wall jump
			//TODO Faster turn around anim/rotation when near 180°
			if (!aimAction.IsPressed() && !pushingOnWall && !WasPropulsed) {
				float rotationSpeed = hVel.sqrMagnitude > 0 ? movingRotationSpeed : stoppedRotationSpeed;
				transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(moveDirection), rotationSpeed * Time.deltaTime);
			}
		} else
			moveAngle = null;

		//Horizontal deceleration
		if (moveInput == Vector2.zero || WasPropulsed || (pushingOnWall && horizontalWallSlide)) {
			float usedDeceleration;
			if (WasPropulsed)
				usedDeceleration = propulsedDeceleration;
			else if (pushingOnWall)
				usedDeceleration = horizontalWallSlideDeceleration;
			else
				usedDeceleration = deceleration;

			//If velocity is almost zero, set it to zero
			if (hVel.sqrMagnitude < usedDeceleration * usedDeceleration * Time.deltaTime * Time.deltaTime) {
				velocity.SetHorizontal(0);
			} else {
				hVel -= hVel.normalized * (usedDeceleration * Time.deltaTime);
				velocity.SetHorizontal(hVel);
			}
		}

		//Jumping
		if (plungeAction.WasPressedThisFrame() && jumpType != JumpType.Plunge/* && (controller.isGrounded || pushingOnWall)*/) {
			velocity.y = plungeYForce;
			jumpType = JumpType.Plunge;
			velocity.SetHorizontal(transform.forward * plungeHVel);
			animator.Play("Plunge");
			animator.SetBool(AnimJumping, true);
			jumpStartTime = null;
		} else if (jumpStartTime != null && jumpType == JumpType.Normal) {
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
				jumpType = JumpType.WallJump;
				velocity.SetHorizontal(transform.forward * propulsedHorizontalVelocity);
			} else if (crouching) {
				if (hVel == Vector3.zero) {
					velocity.y = crouchBackFlipJumpForce;
					jumpType = JumpType.CrouchBackFlip;
					velocity.SetHorizontal(-transform.forward * crouchBackFlipHVel);
					animator.Play("CrouchBackFlip");
				} else {
					velocity.y = crouchLongJumpForce;
					jumpType = JumpType.CrouchLongJump;
					velocity.SetHorizontal(transform.forward * crouchLongJumpHVel);
					animator.Play("CrouchLongJump");
				}
			} else {
				velocity.y = jumpForce;
				jumpType = JumpType.Normal;
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
	}

	//Not great name, basically when not full air control, like wall jump or getting launched from cannon
	bool WasPropulsed => jumpType
		is JumpType.WallJump
		or JumpType.CrouchBackFlip
		or JumpType.CrouchLongJump
		or JumpType.Plunge;

	//Very temporary debugging, could go in the inspector
	void OnGUI() {
		int y = 10;
		AddGUILabel(ref y, $"Velocity: {velocity}");
		AddGUILabel(ref y, $"HVel: {(Utility.GetHorizontal2D(velocity)).magnitude:F2} m/s");
		AddGUILabel(ref y, $"Move Angle: {moveAngle:F1}");
		AddGUILabel(ref y, $"Jump type: {jumpType}");
		AddGUILabel(ref y, $"Pushing on wall: {pushingOnWall}");
		AddGUILabel(ref y, $"Wall Slope: {wallSlopiness}");
		AddGUILabel(ref y, $"Wall H Angle: {wallHorizontalAngle}");
	}

	static void AddGUILabel(ref int y, string text) {
		GUI.Label(new Rect(10, y, Screen.width, 20), text);
		y += 20;
	}

	void DrawArrow(Vector3 direction, Color color) {
		Vector3 bottom = transform.position + Vector3.down * controller.height / 2;
		Debug.DrawLine(bottom, bottom + direction, color);
	}
}

enum JumpType {
	Normal,
	CrouchBackFlip,
	CrouchLongJump,
	Plunge,
	SpinJump,
	WallJump,
}