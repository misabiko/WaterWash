using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour {
	[SerializeField] CinemachineInputAxisController camInputAxisController;
	[SerializeField] CinemachineCamera sprayAimCamera;
	[SerializeField] Transform sprayTransform;

	[Header("Parameters")]
	//TODO Put attributes together
	[Min(0f)]
	[SerializeField] float moveSpeed = 5f;
	//I say jumpForce, but it's really acceleration, with mass of 1
	[Min(0f)]
	[SerializeField] float jumpForce = 5f;
	[Min(0f)]
	[SerializeField] float longJumpDuration = 0.5f;
	[Min(0f)]
	[SerializeField] float longJumpForce = 3f;
	[Min(0f)]
	[SerializeField] float acceleration = 5f;
	[Min(0f)]
	[SerializeField] float deceleration = 1f;

	[Min(0f)]
	[SerializeField] float aimRotateSpeed = 1f;
	//Unity doesn't have a MaxAttribute...
	[SerializeField] float minYSprayAngle = -1f;
	[Min(0f)]
	[SerializeField] float maxYSprayAngle = 1f;

	//[Header("")]
	[SerializeField] LayerMask wallSlideMask;
	[Min(0f)]
	[SerializeField] float wallSlideDetectionDistance = 0.25f;
	[Range(0f, 1f)]
	[SerializeField] float wallMaxSlopiness = 15f;
	[SerializeField, Min(0f)] float wallSlideMaxHorizontalAngle = 40f;
	[SerializeField, Min(0f)] float wallSlideFriction = 1f;
	//TODO Probably generalize "post-wall-jump" params to "limited aerial movement" or something
	[SerializeField, Min(0f)] float wallJumpHorizontalVelocity = 1f;
	[SerializeField, Min(0)] float wallJumpAcceleration = 5f;
	[SerializeField, Min(0)] float wallJumpDeceleration = 10f;

	CharacterController controller;
	InputAction moveAction;
	InputAction lookAction;
	InputAction jumpAction;
	InputAction aimAction;

	Vector3 velocity;
	//Not null if the player is holding jump
	float? jumpStartTime;
	bool pushingOnWall;
	//TODO Rename to "propulsed" or something? Would apply to getting shot from cannon
	bool didWallJump;

	float wallSlopiness;	//Debugging
	float wallHorizontalAngle;	//Debugging

	void Awake() {
		controller = GetComponent<CharacterController>();

		var playerInput = GetComponent<PlayerInput>();
		moveAction = playerInput.actions["Move"];
		lookAction = playerInput.actions["Look"];
		jumpAction = playerInput.actions["Jump"];
		aimAction = playerInput.actions["Aim"];
	}

	void Update() {
		if (aimAction.WasPressedThisFrame()) {
			// camInputAxisController.enabled = false;
			sprayAimCamera.gameObject.SetActive(true);
		} else if (aimAction.WasReleasedThisFrame()) {
			// camInputAxisController.enabled = true;
			sprayAimCamera.gameObject.SetActive(false);
		}

		if (aimAction.IsPressed()) {
			var look = lookAction.ReadValue<Vector2>();
			transform.Rotate(Vector3.up, look.x * aimRotateSpeed * Time.deltaTime);
			sprayTransform.Rotate(Vector3.right, -look.y * aimRotateSpeed * Time.deltaTime);

			if (sprayTransform.localEulerAngles.x >= 180f)
				sprayTransform.localEulerAngles = new Vector3(Mathf.Max(sprayTransform.localEulerAngles.x, 360f + minYSprayAngle), 0, 0);
			else
				sprayTransform.localEulerAngles = new Vector3(Mathf.Min(sprayTransform.localEulerAngles.x, maxYSprayAngle), 0, 0);
		}

		//Could move this with the rest of velocity.y stuff?
		if (controller.isGrounded && velocity.y < 0) {
			velocity.y = 0f;
			jumpStartTime = null;
			didWallJump = false;
		}

		pushingOnWall = false;

		var moveInput = moveAction.ReadValue<Vector2>();
		Vector3 hVel = GetHorizontalVelocity(velocity);
		if (moveInput != Vector2.zero) {
			//TODO forward + up?
			Vector3 camForward = Camera.main.transform.forward;
			camForward = GetHorizontalVelocity(camForward);
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
					wallHorizontalAngle = Vector2.Angle(-(new Vector2(hit.normal.x, hit.normal.z)), new Vector2(rayDir.x, rayDir.z));

					if (wallSlopiness <= wallMaxSlopiness && wallHorizontalAngle <= wallSlideMaxHorizontalAngle) {
						pushingOnWall = true;
						Vector3 flatNormal = GetHorizontalVelocity(hit.normal).normalized;

						//TODO Wall slide
						Vector3 flatVel = Vector3.Project(velocity, -flatNormal);
						velocity.x = flatVel.x;
						velocity.z = flatVel.z;

						transform.forward = flatNormal;
					}
				}
			}

			if (!pushingOnWall) {
				float maxSpeed = didWallJump
					? Mathf.Infinity
					: moveSpeed * moveDirection.magnitude;
				float usedAcceleration = didWallJump ? wallJumpAcceleration : acceleration;
				//TODO(1) Vector2?
				hVel = Vector3.ClampMagnitude(
					hVel + moveDirection.normalized * usedAcceleration * Time.deltaTime,
					maxSpeed
				);
				velocity.x = hVel.x;
				velocity.z = hVel.z;
			}

			if (!aimAction.IsPressed() && !pushingOnWall && !didWallJump) {
				//TODO Properly rotate
				if (moveDirection != Vector3.zero)
					transform.forward = moveDirection;
			}
		}

		if (moveInput == Vector2.zero || didWallJump) {
			float usedDeceleration = didWallJump ? wallJumpDeceleration : deceleration;

			//If velocity is almost zero, set it to zero
			if (hVel.sqrMagnitude < usedDeceleration * usedDeceleration * Time.deltaTime * Time.deltaTime) {
				velocity.x = 0f;
				velocity.z = 0f;
			} else {
				hVel -= hVel.normalized * usedDeceleration * Time.deltaTime;
				velocity.x = hVel.x;
				velocity.z = hVel.z;
			}
		}

		if (jumpStartTime != null) {
			if (!jumpAction.IsPressed() || Time.time - jumpStartTime > longJumpDuration)
				jumpStartTime = null;
			else
				velocity.y += longJumpForce * Time.deltaTime;
		} else if (jumpAction.WasPressedThisFrame() && (controller.isGrounded || pushingOnWall)) {
			//Setting velY to a jumpVelocity vs adding a jumpForce and adding the gravity afterward?
			//The latter is physically correct, and would support changing the gravity, but I feel weird about the impulse being multiplied by deltaTime (either on jumpForce or gravity)
			velocity.y = jumpForce;
			jumpStartTime = Time.time;

			if (pushingOnWall) {
				Vector3 flatVel = transform.forward * wallJumpHorizontalVelocity;
				velocity.x = flatVel.x;
				velocity.z = flatVel.z;
				didWallJump = true;
			}
		}

		velocity.y += Physics.gravity.y * Time.deltaTime;
		if (pushingOnWall && velocity.y < 0)
			velocity.y = Mathf.Min(0f, velocity.y + wallSlideFriction * Time.deltaTime);

		controller.Move(velocity * Time.deltaTime);
	}

	//Could go in utility class
	static Vector3 GetHorizontalVelocity(Vector3 velocity) => new(velocity.x, 0f, velocity.z);

	void OnGUI() {
		int y = 10;
		AddGUILabel(ref y, $"Velocity: {velocity}");
		AddGUILabel(ref y, $"HVel: {(new Vector2(velocity.x, velocity.z)).magnitude:F2} m/s");
		AddGUILabel(ref y, $"Pushing on wall: {pushingOnWall}");
		AddGUILabel(ref y, $"Wall Slope: {wallSlopiness}");
		AddGUILabel(ref y, $"Wall H Angle: {wallHorizontalAngle}");
		AddGUILabel(ref y, $"Did wall jump: {didWallJump}");
	}

	static void AddGUILabel(ref int y, string text) {
		GUI.Label(new Rect(10, y, Screen.width, 20), text);
		y += 20;
	}
}