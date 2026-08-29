using DunGen.Demo;
using UnityEngine;

namespace DunGen.Demo2D
{
	sealed class PlayerController2D : MonoBehaviour
	{
		public float MovementSpeed = 6f;

		private CircleCollider2D playerCollider;
		private RaycastHit2D[] hitBuffer;
		private DungeonGenerator dungeonGenerator;
		private IDemoInputBridge inputBridge;


		private void Start()
		{
			playerCollider = GetComponent<CircleCollider2D>();
			hitBuffer = new RaycastHit2D[10];
			inputBridge = new DemoInputBridge();

			var gen = UnityUtil.FindObjectByType<DunGen.Demo.Generator>();
			dungeonGenerator = gen.DungeonGenerator.Generator;

			dungeonGenerator.OnGenerationStatusChanged += OnGeneratorStatusChanged;
		}

		private void OnDestroy()
		{
			dungeonGenerator.OnGenerationStatusChanged -= OnGeneratorStatusChanged;
		}

		private void OnGeneratorStatusChanged(DungeonGenerator generator, GenerationStatus status)
		{
			transform.position = Vector3.zero;
		}

		private void Update()
		{
			Vector2 input = inputBridge.GetMoveAxis();

			if (input.sqrMagnitude > 1f)
				input.Normalize();

			Vector3 direction = new Vector3(input.x, input.y, 0f);
			float distance = MovementSpeed * Time.deltaTime;
			Vector3 motion = direction * distance;

			int hitCount = playerCollider.Cast(direction, hitBuffer, distance);

			for (int i = 0; i < hitCount; i++)
			{
				var hit = hitBuffer[i];

				if(hit.collider.isTrigger)
					continue;

				motion = direction * hit.distance;
				break;
			}

			transform.position += motion;
		}
	}
}
