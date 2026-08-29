using System;

namespace DunGen.Analysis.Modules
{
	[Serializable, SubclassDisplay(displayName: "Generation Stats")]
	public sealed class GenerationStatsAnalysisModule : IGenerationAnalysisModule
	{
		public void OnAnalysisStarted(AnalysisResults results) { }

		public void OnDungeonGenerated(AnalysisResults results, Dungeon dungeon, GenerationStats stats)
		{
			// Tile Counts
			results.AddValue("Tiles.Count.MainPath", stats.MainPathRoomCount);
			results.AddValue("Tiles.Count.BranchPath", stats.BranchPathRoomCount);
			results.AddValue("Tiles.Count.Total", stats.TotalRoomCount);

			// Branch Stats
			results.AddValue("Branches.MaxDepth", stats.MaxBranchDepth);
			results.AddValue("Branches.PrunedTiles", stats.PrunedBranchTileCount);
			results.AddValue("Branches.Count", dungeon.Branches.Count);

			// Generation Stats
			results.AddValue("Generation.Retries", stats.TotalRetries);
			results.AddValue("Generation.Time.Total", stats.TotalTime, "ms");

			// Generation Steps
			foreach (var step in GenerationAnalysis.MeasurableSteps)
				results.AddValue($"Generation.Time.Step.{step}", stats.GetGenerationStepTime(step), "ms");
		}

		public void OnDungeonGenerationFailed(AnalysisResults results, GenerationStats stats) { }

		public void OnAnalysisEnded(AnalysisResults results) { }
	}
}