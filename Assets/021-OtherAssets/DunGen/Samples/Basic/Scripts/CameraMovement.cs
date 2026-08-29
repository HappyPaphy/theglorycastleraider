using UnityEngine;

namespace DunGen.Demo
{
	public class CameraMovement : MonoBehaviour
	{
		public float MovementSpeed = 100;

		private IDemoInputBridge inputBridge;


		private void Start()
		{
			inputBridge = new DemoInputBridge();
			var runtimeDungeon = UnityUtil.FindObjectByType<RuntimeDungeon>();

			if (runtimeDungeon != null)
				transform.forward = -runtimeDungeon.Generator.Settings.UpDirection.ToVector3();
		}

		private void Update()
		{
			Vector2 movementInput = inputBridge.GetMoveAxis();
			Vector3 direction = Vector3.zero;

			direction += transform.up * movementInput.y;
			direction += transform.right * movementInput.x;

			direction.Normalize();

			Vector3 offset = MovementSpeed * Time.deltaTime * direction;
			if (inputBridge.IsSprintHeld())
				offset *= 2;

			float zoom = inputBridge.GetZoomOffset();
			offset += 100 * MovementSpeed * Time.deltaTime * zoom * transform.forward;

			transform.position += offset;
		}
	}
}