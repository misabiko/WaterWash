using UnityEngine;

public class Paintable : MonoBehaviour {
	public Texture2D DirtMaskTexture { get; private set; }

	void Start() {
		Material material = GetComponent<Renderer>().material;
		var mainTexture = material.GetTexture(MainTex) as Texture2D;
		DirtMaskTexture = new Texture2D(mainTexture.width, mainTexture.height);

		for (int x = 0; x < DirtMaskTexture.width; ++x)
			for (int y = 0; y < DirtMaskTexture.height; ++y)
				DirtMaskTexture.SetPixel(x, y, Color.black);
		DirtMaskTexture.Apply();

		material.SetTexture(DirtMask, DirtMaskTexture);
	}

	static readonly int DirtMask = Shader.PropertyToID("_DirtMask");
	static readonly int MainTex = Shader.PropertyToID("_MainTex");
}