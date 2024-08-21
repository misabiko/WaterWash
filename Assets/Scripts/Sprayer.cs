using UnityEngine;
using UnityEngine.InputSystem;

public class Sprayer : MonoBehaviour {
	[SerializeField] WaterParticles waterParticles;
	[SerializeField, Min(0)] float consumptionRate = 1;
	[SerializeField, Min(0)] float rechargeRate = 1;
	[SerializeField, Min(0)] int maxWaterLevel = 100;

	[HideInInspector] public bool canRecharge;

	InputAction sprayAction;
	//Would like to make int, but it gets weird with scaling by 0-1 input and deltaTime
	float waterLevel;
	bool isSpraying;

	void Awake() {
		sprayAction = GetComponent<PlayerInput>().actions["Spray"];
		waterLevel = maxWaterLevel;
	}

	void Update() {
		float sprayInput = sprayAction.ReadValue<float>();

		if (canRecharge && !isSpraying && waterLevel < maxWaterLevel) {
			waterLevel = Mathf.Min(maxWaterLevel, waterLevel + sprayInput * rechargeRate * Time.deltaTime);
		} else {
			waterParticles.SetEmissionRate(waterLevel > 0 ? sprayInput : 0);
			waterLevel = Mathf.Max(0, waterLevel - sprayInput * consumptionRate * Time.deltaTime);
			isSpraying = sprayInput > 0 && waterLevel > 0;
		}
	}

	//Very temporary debugging, could go in the inspector
	void OnGUI() {
		int y = Screen.height / 2;
		Utility.AddGUILabel(ref y, $"Water level: {waterLevel:F1}");
		Utility.AddGUILabel(ref y, $"Can recharge: {canRecharge}");
	}
}