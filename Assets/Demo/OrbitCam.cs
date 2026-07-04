using UnityEngine;

namespace Seb.Fluid.Demo
{
	public class OrbitCam : MonoBehaviour
	{
		public float moveSpeed = 3;
		public float rotationSpeed = 220;
		public float zoomSpeed = 0.1f;
		public Vector3 pivot;
		Vector3 mousePosOld;
		bool hasFocusOld;
		public float focusDst = 1f;
		Vector3 lastCtrlPivot;

		float lastLeftClickTime = float.MinValue;
		private Vector2 rightClickPos;

		private Vector3 startPos;
		Quaternion startRot;

		void Start()
		{
			startPos = transform.position;
			startRot = transform.rotation;
		}

		void Update()
		{
			if (Application.isFocused != hasFocusOld)
			{
				hasFocusOld = Application.isFocused;
				mousePosOld = Input.mousePosition;
			}

			float dstWeight = Mathf.Max(transform.position.magnitude, 1f);

			// Always track mouse delta — if we skip it inside the guard the position
			// jumps the next time the mouse leaves the UI panel.
			Vector2 mouseMove = (Vector2)Input.mousePosition - (Vector2)mousePosOld;
			mousePosOld = Input.mousePosition;
			float mouseMoveX = mouseMove.x / Screen.width;
			float mouseMoveY = mouseMove.y / Screen.width;

			// Arrow keys always work (keyboard input, not captured by IMGUI)
			Vector3 move = Vector3.zero;
			float arrowStep = moveSpeed * Time.deltaTime;
			if (Input.GetKey(KeyCode.LeftShift)) arrowStep *= 3f;
			if (Input.GetKey(KeyCode.LeftArrow))  move -= transform.right * arrowStep;
			if (Input.GetKey(KeyCode.RightArrow)) move += transform.right * arrowStep;
			if (Input.GetKey(KeyCode.UpArrow))    move += transform.up    * arrowStep;
			if (Input.GetKey(KeyCode.DownArrow))  move -= transform.up    * arrowStep;
			transform.Translate(move, Space.World);

			// Scroll zoom always works (even when mouse is over the UI panel)
			float mouseScroll = Input.mouseScrollDelta.y;
			transform.Translate(Vector3.forward * mouseScroll * zoomSpeed * dstWeight);

			// Everything below is blocked while the mouse is over the UI panel
			bool uiBlocked = MixingSceneUI.MouseOverUI || GUIUtility.hotControl != 0;
			if (uiBlocked) return;

			// Reset view on double click
			if (Input.GetMouseButtonDown(0))
			{
				if (Time.time - lastLeftClickTime < 0.2f)
				{
					transform.position = startPos;
					transform.rotation = startRot;
				}
				lastLeftClickTime = Time.time;
				lastCtrlPivot = transform.position + transform.forward * focusDst;
			}

			// Middle button: pan
			if (Input.GetMouseButton(2))
			{
				Vector3 pan = Vector3.zero;
				pan += Vector3.up    * mouseMoveY * -moveSpeed * dstWeight;
				pan += Vector3.right * mouseMoveX * -moveSpeed * dstWeight;
				transform.Translate(pan);
			}

			// Left button: orbit
			if (Input.GetMouseButton(0))
			{
				Vector3 activePivot = Input.GetKey(KeyCode.LeftAlt) ? transform.position : pivot;
				if (Input.GetKey(KeyCode.LeftControl)) activePivot = lastCtrlPivot;
				transform.RotateAround(activePivot, transform.right, mouseMoveY * -rotationSpeed);
				transform.RotateAround(activePivot, Vector3.up,       mouseMoveX *  rotationSpeed);
			}

			// Right button: drag-zoom
			if (Input.GetMouseButtonDown(1)) rightClickPos = Input.mousePosition;
			if (Input.GetMouseButton(1))
			{
				Vector2 delta = (Vector2)Input.mousePosition - rightClickPos;
				rightClickPos = Input.mousePosition;
				float dragScroll = delta.magnitude
					* Mathf.Sign(Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? delta.x : -delta.y)
					/ Screen.width * zoomSpeed * 100;
				transform.Translate(Vector3.forward * dragScroll * zoomSpeed * dstWeight);
			}
		}

		void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.red;
		}
	}
}