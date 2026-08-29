using System;
using System.Collections;

namespace DunGen.Generation.Steps
{
	/// <summary>
	/// Represents a generation step that validates whether all required tile injections have been successfully completed
	/// in the current generation context.
	/// </summary>
	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class ValidateRequiredTilesStep : IGenerationStep
	{
		public virtual IEnumerator Execute(GenerationContext context)
		{
			if (!HasAllRequiredTiles(context))
				context.StepResult = GenerationStepResult.Failure("Missing required injected tile(s)");

			yield break;
		}

		protected virtual bool HasAllRequiredTiles(GenerationContext context)
		{
			bool isValid = true;

			// Make sure all required tile injections were successful
			foreach (var tileInjection in context.TilesPendingInjection)
			{
				if (tileInjection.IsRequired)
				{
					context.TilePlacementResults.Add(new RequiredTileInjectionFailedResult(tileInjection.TileSet));
					isValid = false;
				}
			}

			return isValid;
		}
	}
}