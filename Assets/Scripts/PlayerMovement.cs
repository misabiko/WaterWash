using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour {
	[Min(0f)]
	[SerializeField] float moveSpeed = 5f;
	[Min(0f)]
	[SerializeField] float jumpForce = 5f;
	[Min(0f)]
	[SerializeField] float acceleration = 5f;
	[Min(0f)]
	[SerializeField] float deceleration = 1f;

	CharacterController controller;
	InputAction moveAction;
	InputAction jumpAction;

	Vector3 velocity;

	void Awake() {
		controller = GetComponent<CharacterController>();

		var playerInput = GetComponent<PlayerInput>();
		moveAction = playerInput.actions["Move"];
		jumpAction = playerInput.actions["Jump"];
	}

	void Update() {
		if (controller.isGrounded && velocity.y < 0)
			velocity.y = 0f;

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

		if (jumpAction.triggered && controller.isGrounded)
			velocity.y = jumpForce;
		else
			velocity.y += Physics.gravity.y * Time.deltaTime;

		controller.Move(velocity * Time.deltaTime);
	}

	Vector2 HorizontalVelocity() => new(velocity.x, velocity.z);

	void OnGUI() {
		GUI.Label(new Rect(10, 10, Screen.width, 20), $"Velocity: {velocity}");
		GUI.Label(new Rect(10, 30, Screen.width, 20), $"HVel: {HorizontalVelocity().magnitude:F2} m/s");
	}
}