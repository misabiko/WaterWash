using UnityEngine;

namespace WaterWash {
	public class Paintable : MonoBehaviour {
		public Texture2D DirtMaskTexture { get; private set; }
		//TODO Make it actual pixel per meter
		const int PixelPerMeter = 2;

		void Start() {
			Material material = GetComponent<Renderer>().material;
			var mainTexture = material.GetTexture(MainTex) as Texture2D;

			float scale = Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
			int pixelSize = (int)(scale * PixelPerMeter);
			// print(scale + " → " + pixelSize);
			DirtMaskTexture = new Texture2D(pixelSize, pixelSize);

			for (int x = 0; x < DirtMaskTexture.width; ++x)
				for (int y = 0; y < DirtMaskTexture.height; ++y)
					DirtMaskTexture.SetPixel(x, y, Color.black);
			DirtMaskTexture.Apply();

			material.SetTexture(DirtMask, DirtMaskTexture);
		}

		static readonly int DirtMask = Shader.PropertyToID("_DirtMask");
		static readonly int MainTex = Shader.PropertyToID("_MainTex");
	}
}