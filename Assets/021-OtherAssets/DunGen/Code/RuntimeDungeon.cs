using DunGen.Generation;
using UnityEngine;

namespace DunGen
{
	[AddComponentMenu("DunGen/Runtime Dungeon")]
	[HelpURL("https://dungen-docs.aegongames.com/core-concepts/dungeon-generator/")]
	public class RuntimeDungeon : MonoBehaviour
	{
		public DungeonGenerator Generator = new DungeonGenerator();
		public bool GenerateOnStart = true;
		public GameObject Root;


		protected virtual void Start()
		{
			if (GenerateOnStart)
				Generate();
		}

		public void Generate(DungeonGenerationRequest request = null)
		{
			if (Root != null)
				Generator.Root = Root;

			if (!Generator.IsGenerating)
			{
				request ??= new DungeonGenerationRequest(Generator.Settings);
				Generator.Generate(request);
			}
		}

		private void OnDrawGizmos()
		{
			if (Generator == null)
				return;

			var debugSettings = Generator.Settings.DebugRenderSettings;

			if (debugSettings.Enabled && debugSettings.ShowCollision)
				Generator.CollisionManager?.Broadphase?.DrawDebug();
		}
	}
}
