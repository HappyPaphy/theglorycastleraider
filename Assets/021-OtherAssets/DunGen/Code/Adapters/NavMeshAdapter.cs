using DunGen.Generation;

namespace DunGen.Adapters
{
	public abstract class NavMeshAdapter : BaseAdapter
	{
		#region Helpers

		public struct NavMeshGenerationProgress
		{
			public float Percentage;
			public string Description;
		}

		public delegate void OnNavMeshGenerationProgress(NavMeshGenerationProgress progress);

		#endregion

		public OnNavMeshGenerationProgress OnProgress;


		protected override void Run(GenerationContext context)
		{
			Generate(context.Dungeon);
		}

		public abstract void Generate(Dungeon dungeon);
	}
}