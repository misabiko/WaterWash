using UnityEngine;

public class RechargeTrigger : MonoBehaviour {
	void OnTriggerEnter(Collider other) {
		if (other.GetComponent<Sprayer>() is {} sprayer)
			sprayer.canRecharge = true;
	}

	void OnTriggerExit(Collider other) {
		//Will break if player is inside overlapping triggers, could fix by replacing canRecharge bool by a list of triggers, but probably not worth prevention
		if (other.GetComponent<Sprayer>() is {} sprayer)
			sprayer.canRecharge = false;
	}
}