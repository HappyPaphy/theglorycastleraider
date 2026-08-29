using System;

namespace DunGen.Analysis.Modules
{
	[Serializable, SubclassDisplay(displayName: "Path Lengths")]
	public sealed class PathLengthsAnalysisModule : IGenerationAnalysisModule
	{
		public void OnAnalysisStarted(AnalysisResults results) { }

		public void OnDungeonGenerated(AnalysisResults results, Dungeon dungeon, GenerationStats stats)
		{
			results.AddValue("Paths.Length.MainPath", dungeon.MainPathTiles.Count);

			foreach(var branch in dungeon.Branches)
				results.AddValue("Paths.Length.BranchPaths", branch.Tiles.Count);
		}

		public void OnDungeonGenerationFailed(AnalysisResults results, GenerationStats stats) { }

		public void OnAnalysisEnded(AnalysisResults results) { }
	}
}