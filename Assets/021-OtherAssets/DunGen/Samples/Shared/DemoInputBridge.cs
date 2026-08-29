#if ENABLE_INPUT_SYSTEM && HAS_UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;

namespace DunGen.Demo
{
	public enum InputState
	{
		None = 0,
		Pressed,
		Released,
		Held,
	}

	public interface IDemoInputBridge
	{
		bool GetToggleCamera();
		float GetZoomOffset();
		bool IsSprintHeld();
		bool GetToggleStats();
		bool GetToggleInputCapture();
		bool GetExit();
		Vector2 GetMoveAxis();
		Vector2 GetLookAxis();
		Vector2 GetMousePosition();
		InputState GetResetInputState();
		InputState GetLeftMouseInputState();
	}

	/// <summary>
	/// A simple wrapper around Unity's input system to allow for seamless switching between the old and new input systems.
	/// </summary>
	public class DemoInputBridge : IDemoInputBridge
	{
#if ENABLE_INPUT_SYSTEM && HAS_UNITY_INPUT_SYSTEM

		private readonly InputAction toggleCameraAction;
		private readonly InputAction moveAction;
		private readonly InputAction lookAction;
		private readonly InputAction zoomAction;
		private readonly InputAction shiftAction;
		private readonly InputAction resetAction;
		private readonly InputAction f1Action;
		private readonly InputAction exitAction;
		private readonly InputAction leftMouseAction;


		public DemoInputBridge()
		{
			toggleCameraAction = new InputAction("ToggleCamera", InputActionType.Button, binding: "<Keyboard>/c");
			moveAction = new InputAction("Move", InputActionType.Value);
			moveAction.AddCompositeBinding("2DVector")
					  .With("Up", "<Keyboard>/w")
					  .With("Down", "<Keyboard>/s")
					  .With("Left", "<Keyboard>/a")
					  .With("Right", "<Keyboard>/d");
			lookAction = new InputAction("Look", InputActionType.Value, binding: "<Mouse>/delta");
			zoomAction = new InputAction("Zoom", InputActionType.Value, binding: "<Mouse>/scroll/y");
			shiftAction = new InputAction("Sprint", InputActionType.Button, binding: "<Keyboard>/leftShift");
			resetAction = new InputAction("Reset", InputActionType.Button, binding: "<Keyboard>/r");
			f1Action = new InputAction("ToggleStats", InputActionType.Button, binding: "<Keyboard>/f1");
			exitAction = new InputAction("Exit", InputActionType.Button, binding: "<Keyboard>/escape");
			leftMouseAction = new InputAction("LeftMouse", InputActionType.Button, binding: "<Mouse>/leftButton");

			toggleCameraAction.Enable();
			moveAction.Enable();
			lookAction.Enable();
			zoomAction.Enable();
			shiftAction.Enable();
			resetAction.Enable();
			f1Action.Enable();
			exitAction.Enable();
			leftMouseAction.Enable();
		}

		public bool GetToggleCamera() => toggleCameraAction.WasPressedThisFrame();
		public Vector2 GetMoveAxis() => moveAction.ReadValue<Vector2>();
		public Vector2 GetLookAxis() => lookAction.ReadValue<Vector2>() * 0.05f;
		public Vector2 GetMousePosition() => Mouse.current.position.ReadValue();
		public float GetZoomOffset() => zoomAction.ReadValue<float>() * 0.001f;
		public bool IsSprintHeld() => shiftAction.IsPressed();
		public bool GetToggleStats() => f1Action.WasPressedThisFrame() && !shiftAction.IsPressed();
		public bool GetToggleInputCapture() => f1Action.WasPressedThisFrame() && shiftAction.IsPressed();
		public bool GetExit() => exitAction.WasPressedThisFrame();
		public InputState GetResetInputState()
		{
			if (resetAction.WasPressedThisFrame())
				return InputState.Pressed;
			if (resetAction.WasReleasedThisFrame())
				return InputState.Released;
			if (resetAction.IsPressed())
				return InputState.Held;

			return InputState.None;
		}
		public InputState GetLeftMouseInputState()
		{
			if (leftMouseAction.WasPressedThisFrame())
				return InputState.Pressed;
			if (leftMouseAction.WasReleasedThisFrame())
				return InputState.Released;
			if (leftMouseAction.IsPressed())
				return InputState.Held;

			return InputState.None;
		}
#else
		public bool GetToggleCamera() => Input.GetKeyDown(KeyCode.C);
		public Vector2 GetMoveAxis() => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
		public Vector2 GetLookAxis() => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
		public Vector2 GetMousePosition() => Input.mousePosition;
		public float GetZoomOffset() => Input.GetAxisRaw("Mouse ScrollWheel");
		public bool IsSprintHeld() => Input.GetKey(KeyCode.LeftShift);
		public bool GetToggleStats() => Input.GetKeyDown(KeyCode.F1) && !Input.GetKey(KeyCode.LeftShift);
		public bool GetToggleInputCapture() => Input.GetKeyDown(KeyCode.F1) && Input.GetKey(KeyCode.LeftShift);
		public bool GetExit() => Input.GetKeyDown(KeyCode.Escape);
		public InputState GetResetInputState()
		{
			if (Input.GetKeyDown(KeyCode.R))
				return InputState.Pressed;
			if (Input.GetKeyUp(KeyCode.R))
				return InputState.Released;
			if (Input.GetKey(KeyCode.R))
				return InputState.Held;

			return InputState.None;
		}
		public InputState GetLeftMouseInputState()
		{
			if (Input.GetMouseButtonDown(0))
				return InputState.Pressed;
			if (Input.GetMouseButtonUp(0))
				return InputState.Released;
			if (Input.GetMouseButton(0))
				return InputState.Held;

			return InputState.None;
		}
#endif
	}
}
