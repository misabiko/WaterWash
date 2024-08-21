using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WaterWash {
	[RequireComponent(typeof(ParticleSystem))]
	public class WaterParticles : MonoBehaviour {
		[SerializeField] PlayerInput playerInput;
		[SerializeField, Min(0)] int brushSize = 15;

		new ParticleSystem particleSystem;
		List<ParticleCollisionEvent> collisionEvents;
		ParticleSystem.EmissionModule emission;
		float baseEmissionRate;

		void Awake() {
			particleSystem = GetComponent<ParticleSystem>();
			collisionEvents = new List<ParticleCollisionEvent>();
			emission = particleSystem.emission;
			baseEmissionRate = emission.rateOverTime.constant;
			emission.rateOverTime = 0;
		}

		public void SetEmissionRate(float rate) => emission.rateOverTime = rate * baseEmissionRate;

		void OnParticleCollision(GameObject other) {
			int numCollisionEvents = particleSystem.GetCollisionEvents(other, collisionEvents);
			for (int i = 0; i < numCollisionEvents; ++i) {
				ParticleCollisionEvent collisionEvent = collisionEvents[i];
				if (other.gameObject.GetComponent<Paintable>() is {} paintable) {
					Physics.Raycast(collisionEvent.intersection + collisionEvent.normal / 2, -collisionEvent.normal, out RaycastHit hitInfo, 1f);
					if (hitInfo.collider == collisionEvent.colliderComponent) {
						Vector2 textureCoord = hitInfo.textureCoord;
						var centerPixel = new Vector2Int(
							(int)(textureCoord.x * paintable.DirtMaskTexture.width),
							(int)(textureCoord.y * paintable.DirtMaskTexture.height)
						);
						//Would be nice if we could use particle size for brush size
						int currentBrushSize = Random.Range(brushSize / 2, brushSize);
						for (int x = -currentBrushSize; x <= currentBrushSize; ++x) {
							for (int y = -currentBrushSize; y <= currentBrushSize; ++y) {
								var pixelPos = new Vector2Int(
									Mathf.Clamp(centerPixel.x + x, 0, paintable.DirtMaskTexture.width),
									Mathf.Clamp(centerPixel.y + y, 0, paintable.DirtMaskTexture.height)
								);


								//TODO Smooth brush
								// float alpha = 1f - Vector2.Distance(centerPixel, pixelPos) / currentBrushSize;
								// //TODO Use GetPixels instead of GetPixel every time
								// Color pixelColor = paintable.DirtMaskTexture.GetPixel(pixelPos.x, pixelPos.y);
								// pixelColor.a = Mathf.Clamp01(pixelColor.a + (draw ? alpha : -alpha));

								if (Vector2.Distance(pixelPos, centerPixel) > currentBrushSize)
									continue;

								paintable.DirtMaskTexture.SetPixel(pixelPos.x, pixelPos.y, Color.clear);
							}
						}

						paintable.DirtMaskTexture.Apply();
					}
				}
			}
		}
	}
}