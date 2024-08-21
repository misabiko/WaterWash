using UnityEngine;

public static class Utility {
	//TODO Decide calling it "horizontal" or "flat"
	public static Vector3 GetHorizontal(Vector3 v) => new(v.x, 0f, v.z);
	public static Vector2 GetHorizontal2D(Vector3 v) => new(v.x, v.z);

	public static void SetHorizontal(this ref Vector3 v, Vector3 newHorizontal) {
		v.x = newHorizontal.x;
		v.z = newHorizontal.z;
	}

	public static void SetHorizontal(this ref Vector3 v, Vector2 newHorizontal) {
		v.x = newHorizontal.x;
		v.z = newHorizontal.y;
	}

	public static void SetHorizontal(this ref Vector3 v, float value) {
		v.x = value;
		v.z = value;
	}

	public static void AddGUILabel(ref int y, string text) {
		GUI.Label(new Rect(10, y, Screen.width, 20), text);
		y += 20;
	}
}