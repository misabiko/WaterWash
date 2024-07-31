using UnityEngine;

public class Paintable : MonoBehaviour {
	public Texture2D DirtMaskTexture { get; private set; }

	void Start() {
		var material = GetComponent<Renderer>().material;
		var originalDirtMask = material.GetTexture(DirtMask) as Texture2D;
		DirtMaskTexture = new Texture2D(originalDirtMask.width, originalDirtMask.height);
		// DirtMaskTexture.SetPixels(originalDirtMask.GetPixels());
		for (int x = 0; x < DirtMaskTexture.width; ++x) {
			for (int y = 0; y < DirtMaskTexture.height; ++y) {
				DirtMaskTexture.SetPixel(x, y, Color.black);
			}
		}
		DirtMaskTexture.Apply();
		material.SetTexture(DirtMask, DirtMaskTexture);
	}
	
	static readonly int DirtMask = Shader.PropertyToID("_DirtMask");
}