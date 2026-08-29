using System.Collections.Generic;

namespace DunGen.Generation
{
	public class GenerationFailureReport
	{
		public int MaxRetryAttempts { get; }
		public List<TilePlacementResult> TilePlacementResults { get; }


		public GenerationFailureReport(int maxRetryAttempts, List<TilePlacementResult> tilePlacementResults)
		{
			MaxRetryAttempts = maxRetryAttempts;
			TilePlacementResults = tilePlacementResults;
		}
	}
}
