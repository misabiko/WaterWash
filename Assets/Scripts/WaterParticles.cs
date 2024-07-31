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
					for (int x = -brushSize; x <= brushSize; ++x) {
						for (int y = -brushSize; y <= brushSize; ++y) {
							int pixelX = Mathf.Clamp((int)(textureCoord.x * paintable.DirtMaskTexture.width) + x, 0, paintable.DirtMaskTexture.width);
							int pixelY = Mathf.Clamp((int)(textureCoord.y * paintable.DirtMaskTexture.height) + y, 0, paintable.DirtMaskTexture.height);


							paintable.DirtMaskTexture.SetPixel(pixelX, pixelY, draw ? Color.black : Color.clear);
						}
					}
					paintable.DirtMaskTexture.Apply();
				}
			}
		}
	}
}