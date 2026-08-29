namespace DunGen.Analysis
{
	public interface IGenerationAnalysisModule
	{
		void OnAnalysisStarted(AnalysisResults results);
		void OnDungeonGenerated(AnalysisResults results, Dungeon dungeon, GenerationStats stats);
		void OnDungeonGenerationFailed(AnalysisResults results, GenerationStats stats);
		void OnAnalysisEnded(AnalysisResults results);
	}
}