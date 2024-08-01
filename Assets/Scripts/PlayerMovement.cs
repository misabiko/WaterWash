using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour {
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

	CharacterController controller;
	InputAction moveAction;
	InputAction jumpAction;

	Vector3 velocity;
	//Not null if the player is holding jump
	float? jumpStartTime;

	void Awake() {
		controller = GetComponent<CharacterController>();

		var playerInput = GetComponent<PlayerInput>();
		moveAction = playerInput.actions["Move"];
		jumpAction = playerInput.actions["Jump"];
	}

	void Update() {
		//Could move this with the rest of velocity.y stuff?
		if (controller.isGrounded && velocity.y < 0) {
			velocity.y = 0f;
			jumpStartTime = null;
		}

		var moveInput = moveAction.ReadValue<Vector2>();
		var hVel = new Vector3(velocity.x, 0f, velocity.z);
		if (moveInput != Vector2.zero) {
			Vector3 camForward = Camera.main.transform.forward;
			camForward.y = 0;
			Vector3 moveDirection = camForward * moveInput.y + Camera.main.transform.right * moveInput.x;
			if (moveDirection.sqrMagnitude > 1)
				moveDirection.Normalize();

			//TODO Vector2?
			Vector3 clampedHVel = Vector3.ClampMagnitude(hVel + moveDirection.normalized * acceleration * Time.deltaTime, moveSpeed * moveDirection.magnitude);
			velocity.x = clampedHVel.x;
			velocity.z = clampedHVel.z;

			//TODO Properly rotate
			if (moveDirection != Vector3.zero)
				transform.forward = moveDirection;
		} else {
			if (hVel.sqrMagnitude < deceleration * deceleration * Time.deltaTime * Time.deltaTime) {
				velocity.x = 0f;
				velocity.z = 0f;
			} else {
				Vector3 deceleratedHVel = hVel - hVel.normalized * deceleration * Time.deltaTime;
				velocity.x = deceleratedHVel.x;
				velocity.z = deceleratedHVel.z;
			}
		}

		if (jumpStartTime != null) {
			if (!jumpAction.IsPressed() || Time.time - jumpStartTime > longJumpDuration)
				jumpStartTime = null;
			else
				velocity.y += longJumpForce * Time.deltaTime;
		} else if (jumpAction.WasPressedThisFrame() && controller.isGrounded) {
			//Setting velY to a jumpVelocity vs adding a jumpForce and adding the gravity afterward?
			//The latter is physically correct, and would support changing the gravity, but I feel weird about the impulse being multiplied by deltaTime (either on jumpForce or gravity)
			velocity.y = jumpForce;
			jumpStartTime = Time.time;
		}

		velocity.y += Physics.gravity.y * Time.deltaTime;

		controller.Move(velocity * Time.deltaTime);
	}

	Vector2 HorizontalVelocity() => new(velocity.x, velocity.z);

	void OnGUI() {
		GUI.Label(new Rect(10, 10, Screen.width, 20), $"Velocity: {velocity}");
		GUI.Label(new Rect(10, 30, Screen.width, 20), $"HVel: {HorizontalVelocity().magnitude:F2} m/s");
	}
}