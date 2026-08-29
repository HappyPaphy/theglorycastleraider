using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DunGen
{
	public abstract class TilePlacementResult
	{
		public abstract string DisplayName { get; }

		public static string ProduceReport(IEnumerable<TilePlacementResult> results, int maxRetryAttempts)
		{
			var report = new StringBuilder();

			report.AppendLine($"=== Failed to generate dungeon {maxRetryAttempts} times ===");
			report.AppendLine($"This could indicate an issue with the way your tiles are set up.");
			report.AppendLine($"Here is a list of all the reasons tile placement failed while trying to generate the dungeon:\n");

			var groupedResults = results
				.GroupBy(r => r.GetType())
				.OrderByDescending(g => g.Count());

			foreach (var group in groupedResults)
				report.AppendLine($"\t- {group.First().DisplayName} (x{group.Count()})");

			return report.ToString();
		}
	}

	public abstract class TileTemplatePlacementResult : TilePlacementResult
	{
		/// <summary>
		/// The template used to create a the new tile.
		/// </summary>
		public GameObject TileTemplatePrefab { get; private set; }

		public TileTemplatePlacementResult(TileProxy tileTemplate)
		{
			if (tileTemplate != null)
				TileTemplatePrefab = tileTemplate.Prefab;
		}
	}

	/// <summary>
	/// The tile placement failed because the tile was outside of the bounding box specified in the dungeon generator settings.
	/// </summary>
	public sealed class OutOfBoundsPlacementResult : TileTemplatePlacementResult
	{
		public override string DisplayName => "Out-of-Bounds";

		public OutOfBoundsPlacementResult(TileProxy tileTemplate)
			: base(tileTemplate)
		{
		}
	}

	/// <summary>
	/// The tile placement failed because the tile was colliding with another tile.
	/// </summary>
	public sealed class TileIsCollidingPlacementResult : TileTemplatePlacementResult
	{
		public override string DisplayName => "Collision";

		public TileIsCollidingPlacementResult(TileProxy tileTemplate)
			: base(tileTemplate)
		{
		}
	}

	/// <summary>
	/// The tile placement failed because there was no valid tile template provided.
	/// </summary>
	public sealed class NullTemplatePlacementResult : TilePlacementResult
	{
		public override string DisplayName => "No Template";
	}

	/// <summary>
	/// The tile placement was successful and a new tile was created.
	/// </summary>
	public sealed class SuccessPlacementResult : TilePlacementResult
	{
		override public string DisplayName => "Success";
	}

	/// <summary>
	/// The tile placement failed because there was no valid doorway pairs were found.
	/// </summary>
	public sealed class NoMatchingDoorwayPlacementResult : TilePlacementResult
	{
		public override string DisplayName => "No Doorway Pairs";

		/// <summary>
		/// The tile that the placement was attempted from.
		/// </summary>
		public GameObject FromTilePrefab { get; private set; }

		public NoMatchingDoorwayPlacementResult(TileProxy fromTile)
		{
			if (fromTile != null)
				FromTilePrefab = fromTile.Prefab;
		}
	}

	/// <summary>
	/// The dungeon generation failed because a required injected tile could not be placed.
	/// </summary>
	public sealed class RequiredTileInjectionFailedResult : TilePlacementResult
	{
		public override string DisplayName => "Tile Injection Failed";

		public TileSet InjectedTileSet { get; private set; }

		public RequiredTileInjectionFailedResult(TileSet injectedTileSet)
		{
			InjectedTileSet = injectedTileSet;
		}
	}

	public sealed class NoCandidateTilesResult : TilePlacementResult
	{
		public override string DisplayName => "No Candidate Tiles";

		/// <summary>
		/// The tile that the placement was attempted from.
		/// </summary>
		public GameObject FromTilePrefab { get; private set; }

		public NoCandidateTilesResult(TileProxy fromTile)
		{
			if (fromTile != null)
				FromTilePrefab = fromTile.Prefab;
		}
	}

}
