﻿using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WaterWash {
	public class Sprayer : MonoBehaviour {
		[SerializeField] WaterParticles waterParticles;
		[SerializeField] Transform waterPack;
		[SerializeField, Min(0)] float consumptionRate = 1;
		[SerializeField, Min(0)] float rechargeRate = 1;
		[SerializeField, Min(0)] int maxWaterLevel = 100;

		[HideInInspector] public bool canRecharge;

		[Header("Hover Nozzle")]
		[SerializeField, Min(0)] float maxHoverTime = 20f;
		[SerializeField, Min(0)] float maxHoverHeight = 15f;
		[SerializeField] float hoverVelocity = 5f;
		[SerializeField] bool hoverIsActivated = false;

		InputAction sprayAction;
		InputAction interactAction;
		PlayerMovement playerMovement;

		//Would like to make int, but it gets weird with scaling by 0-1 input and deltaTime
		float waterLevel;
		bool isSpraying;

		float waterPackBaseHeight;
		float waterPackBaseY;
		float hoverTimeLeft;

		void Awake() {
			var playerInput = GetComponent<PlayerInput>();
			sprayAction = playerInput.actions["Spray"];
			interactAction = playerInput.actions["Interact"];
			playerMovement = GetComponent<PlayerMovement>();
			interactAction.performed += ctx =>
			{
				hoverIsActivated = !hoverIsActivated;
			};

			waterLevel = maxWaterLevel;
			waterPackBaseHeight = waterPack.localScale.y;
			waterPackBaseY = waterPack.localPosition.y;
			hoverTimeLeft = maxHoverTime;
		}

        void OnEnable()
        {
            interactAction.Enable();
        }

        void OnDisable()
        {
            interactAction?.Disable();
        }

        void Update() {
			float sprayInput = sprayAction.ReadValue<float>();

			if (canRecharge && !isSpraying && waterLevel < maxWaterLevel) {
				waterLevel = Mathf.Min(maxWaterLevel, waterLevel + sprayInput * rechargeRate * Time.deltaTime);
			} else {
				waterParticles.SetEmissionRate(waterLevel > 0 ? sprayInput : 0);
				waterLevel = Mathf.Max(0, waterLevel - sprayInput * consumptionRate * Time.deltaTime);
				isSpraying = sprayInput > 0 && waterLevel > 0;
				if (hoverIsActivated && isSpraying &&  hoverTimeLeft > 0)
				{
					hoverTimeLeft -= Time.deltaTime;
					playerMovement.Hover();
				}
				else
				{
					RestartTimer();
				}
			}
		}

		void LateUpdate() {
			Vector3 scale = waterPack.localScale;
			scale.y = waterPackBaseHeight * waterLevel / maxWaterLevel;
			waterPack.localScale = scale;

			Vector3 position = waterPack.localPosition;
			position.y = waterPackBaseY - (waterPackBaseHeight - scale.y) / 2;
			waterPack.localPosition = position;
		}

		//Very temporary debugging, could go in the inspector
		void OnGUI() {
			int y = Screen.height / 2;
			Utility.AddGUILabel(ref y, $"Water level: {waterLevel:F1}");
			Utility.AddGUILabel(ref y, $"Can recharge: {canRecharge}");
			Utility.AddGUILabel(ref y, $"hoverIsActivated: {hoverIsActivated}");
			Utility.AddGUILabel(ref y, $"hoverTimeLeft: {hoverTimeLeft}");
		}

		void RestartTimer() => hoverTimeLeft = maxHoverTime;
	}
}