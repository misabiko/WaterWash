using UnityEngine;

namespace WaterWash {
	public class Player : MonoBehaviour {
		PlayerMovement playerMovement;
		Vector3 startPoint;
		Vector3 startRotation;

		void Awake() {
			playerMovement = GetComponent<PlayerMovement>();
			startPoint = transform.position;
			startRotation = transform.eulerAngles;
		}

		void TeleportToStart() => playerMovement.Teleport(startPoint, startRotation);

		void OnTriggerEnter(Collider other) {
			if (other.gameObject.layer == LayerMask.NameToLayer("DeathPlane"))
				TeleportToStart();
		}
	}
}