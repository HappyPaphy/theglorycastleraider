using System.Linq;
using System.Text;
using UnityEngine;

namespace DunGen.Demo
{
	[RequireComponent(typeof(CharacterController))]
	public class PlayerController : MonoBehaviour
	{
		public float MinYaw = -360;
		public float MaxYaw = 360;
		public float MinPitch = -60;
		public float MaxPitch = 60;
		public float LookSensitivity = 1;

		public float MoveSpeed = 10;
		public float TurnSpeed = 90;

		public bool IsControlling { get { return isControlling; } }
		public Camera ActiveCamera { get { return isControlling ? playerCamera : overheadCamera; } }

		protected CharacterController movementController;
		protected Camera playerCamera;
		protected Camera overheadCamera;
		protected bool isControlling;
		protected bool isInputCaptured;
		protected float yaw;
		protected float pitch;
		protected Generator gen;
		protected Vector3 velocity;
		protected IDemoInputBridge inputBridge;


		protected virtual void Start()
		{
			movementController = GetComponent<CharacterController>();
			playerCamera = GetComponentInChildren<Camera>();
			gen = UnityUtil.FindObjectByType<Generator>();
			overheadCamera = GameObject.Find("Overhead Camera").GetComponent<Camera>();
			inputBridge = new DemoInputBridge();

			isControlling = true;
			isInputCaptured = true;
			ToggleControl();

			gen.DungeonGenerator.Generator.OnGenerationStatusChanged += OnGenerationStatusChanged;
			gen.GetAdditionalText = GetAdditionalScreenText;
		}

		protected virtual void OnDestroy()
		{
			gen.DungeonGenerator.Generator.OnGenerationStatusChanged -= OnGenerationStatusChanged;
			gen.GetAdditionalText = null;
		}

		private void GetAdditionalScreenText(StringBuilder infoText)
		{
			infoText.AppendLine("Press 'C' to switch between camera modes");
		}

		protected virtual void OnGenerationStatusChanged(DungeonGenerator generator, GenerationStatus status)
		{
			if (status == GenerationStatus.Complete)
			{
				FrameDungeonWithCamera();

				// Teleport the player to start if we're generating a new dungeon (not appending to an existing one)
				if (generator.CompositeDungeon.Dungeons.Count < 2)
				{
					transform.position = new Vector3(0, 1, 7); // Hard-coded spawn position
					velocity = Vector3.zero;
				}
			}
		}

		protected virtual void Update()
		{
			// Repeatedly frame the dungeon while the generation process is running
			var generator = gen.DungeonGenerator.Generator;

			if (generator.IsGenerating && generator.Settings.GenerateAsynchronously && generator.Settings.PauseBetweenRooms > 0f)
				FrameDungeonWithCamera();

			if(inputBridge.GetToggleInputCapture())
				SetInputCaptured(!isInputCaptured);

			if (!isInputCaptured)
				return;

			if (inputBridge.GetToggleCamera())
				ToggleControl();

			if (isControlling)
			{
				Vector2 movementInput = inputBridge.GetMoveAxis();
				Vector2 lookInput = inputBridge.GetLookAxis();

				Vector3 direction = Vector3.zero;
				direction += transform.forward * movementInput.y;
				direction += transform.right * movementInput.x;

				direction.Normalize();

				if (movementController.isGrounded)
					velocity = Vector3.zero;
				else
					velocity += (9.81f * 10) * Time.deltaTime * -transform.up; // Gravity

				direction += velocity * Time.deltaTime;
				movementController.Move(MoveSpeed * Time.deltaTime * direction);

				// Camera Look
				yaw += lookInput.x * LookSensitivity;
				pitch += lookInput.y * LookSensitivity;

				yaw = ClampAngle(yaw, MinYaw, MaxYaw);
				pitch = ClampAngle(pitch, MinPitch, MaxPitch);

				transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up);
				playerCamera.transform.localRotation = Quaternion.AngleAxis(pitch, -Vector3.right);
			}
		}

		protected float ClampAngle(float angle) => ClampAngle(angle, 0, 360);

		protected float ClampAngle(float angle, float min, float max)
		{
			if (angle < -360)
				angle += 360;
			if (angle > 360)
				angle -= 360;

			return Mathf.Clamp(angle, min, max);
		}

		protected void ToggleControl()
		{
			isControlling = !isControlling;

			overheadCamera.gameObject.SetActive(!isControlling);
			playerCamera.gameObject.SetActive(isControlling);

			overheadCamera.transform.position = new Vector3(transform.position.x, overheadCamera.transform.position.y, transform.position.z);

			UpdateCursorState();

			if (!isControlling)
				FrameDungeonWithCamera();
		}

		protected void SetInputCaptured(bool captured)
		{
			isInputCaptured = captured;
			UpdateCursorState();
		}

		protected void UpdateCursorState()
		{
			Cursor.lockState = (isControlling && isInputCaptured) ? CursorLockMode.Locked : CursorLockMode.None;
			Cursor.visible = !(isControlling && isInputCaptured);
		}

		protected void FrameDungeonWithCamera()
		{
			var allDungeons = UnityUtil.FindObjectsByType<Dungeon>()
				.Select(x => x.gameObject)
				.ToArray();

			FrameObjectsWithCamera(allDungeons);
		}

		protected void FrameObjectsWithCamera(params GameObject[] gameObjects)
		{
			if (gameObjects == null || gameObjects.Length == 0)
				return;

			bool hasBounds = false;
			Bounds bounds = new Bounds();

			foreach (var obj in gameObjects)
			{
				var objBounds = UnityUtil.CalculateObjectBounds(obj, false, false);

				if (!hasBounds)
				{
					bounds = objBounds;
					hasBounds = true;
				}
				else
					bounds.Encapsulate(objBounds);
			}

			if (!hasBounds)
				return;

			float radius = Mathf.Max(bounds.size.x, bounds.size.z);

			float distance = radius / Mathf.Sin(overheadCamera.fieldOfView / 2);
			distance = Mathf.Abs(distance);

			Vector3 position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z);
			position += gen.DungeonGenerator.Generator.Settings.UpDirection.ToVector3() * distance;

			overheadCamera.transform.position = position;
		}
	}
}