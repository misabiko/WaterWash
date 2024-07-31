using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ParticleSystem))]
public class WaterParticles : MonoBehaviour {
	[Min(0)]
	[SerializeField] int brushSize = 0;
	new ParticleSystem particleSystem;
	List<ParticleCollisionEvent> collisionEvents;

	void Awake() {
		particleSystem = GetComponent<ParticleSystem>();
		collisionEvents = new List<ParticleCollisionEvent>();
	}
	
	void Update() {
		if (Keyboard.current.spaceKey.wasPressedThisFrame) {
			draw = !draw;
			print(draw);
		}
	}

	bool draw = true;

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
					for (int x = -brushSize; x <= brushSize; ++x) {
						for (int y = -brushSize; y <= brushSize; ++y) {
							var pixelPos = new Vector2Int(
								Mathf.Clamp(centerPixel.x + x, 0, paintable.DirtMaskTexture.width),
								Mathf.Clamp(centerPixel.y + y, 0, paintable.DirtMaskTexture.height)
							);


							//TODO Smooth brush
							// float alpha = 1f - Vector2.Distance(centerPixel, pixelPos) / brushSize;
							// //TODO Use GetPixels instead of GetPixel every time
							// Color pixelColor = paintable.DirtMaskTexture.GetPixel(pixelPos.x, pixelPos.y);
							// pixelColor.a = Mathf.Clamp01(pixelColor.a + (draw ? alpha : -alpha));

							if (Vector2.Distance(pixelPos, centerPixel) > brushSize)
								continue;
							
							paintable.DirtMaskTexture.SetPixel(pixelPos.x, pixelPos.y, draw ? Color.black : Color.clear);
						}
					}
					paintable.DirtMaskTexture.Apply();
				}
			}
		}
	}
}