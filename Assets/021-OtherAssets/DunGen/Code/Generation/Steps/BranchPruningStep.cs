using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DunGen.Generation.Steps
{
	/// <summary>
	/// Represents a dungeon generation step that prunes branches from the dungeon layout, trimming the ends of branches
	/// based on specified pruning tags. This can be used to avoid having unfitting tiles (such as corridors) at
	/// the ends of branches.
	/// </summary>
	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class BranchPruningStep : IGenerationStep
	{
		protected virtual Stack<TileProxy> GatherBranchTips(List<TileProxy> branchPathTiles)
		{
			var branchTips = new Stack<TileProxy>();

			foreach (var tile in branchPathTiles)
			{
				var connectedTiles = tile.UsedDoorways.Select(d => d.ConnectedDoorway.TileProxy);

				// If we're not connected to another tile with a higher branch depth, this is a branch tip
				if (!connectedTiles.Any(t => t.Placement.BranchDepth > tile.Placement.BranchDepth))
					branchTips.Push(tile);
			}

			return branchTips;
		}

		protected virtual bool IsRequiredTile(TileProxy tile)
		{
			return tile.IsRequired;
		}

		protected virtual bool ShouldPruneTile(TileProxy tile, DungeonGeneratorSettings settings)
		{
			return settings.DungeonFlow.ShouldPruneBranchTipWithTags(tile.PrefabTile.Tags);
		}

		protected virtual void OnTilePruned(GenerationContext context, TileProxy prunedTile, TileProxy newBranchTip) { }

		public virtual IEnumerator Execute(GenerationContext context)
		{
			var settings = context.Request.Settings;

			// If there are no prune tags, there's nothing to do here
			if (settings.DungeonFlow.BranchPruneTags.Count == 0)
				yield break;

			var collisionService = context.Services.CollisionService;
			var branchTips = GatherBranchTips(context.ProxyDungeon.BranchPathTiles);

			while (branchTips.Count > 0)
			{
				var tile = branchTips.Pop();
				bool shouldPruneTile = !IsRequiredTile(tile) && ShouldPruneTile(tile, settings);

				if (shouldPruneTile)
				{
					// Find the preceding tile
					var precedingTileConnection = tile.UsedDoorways
						.Select(d => d.ConnectedDoorway)
						.Where(d => d.TileProxy.Placement.IsOnMainPath || d.TileProxy.Placement.BranchDepth < tile.Placement.BranchDepth)
						.Select(d => new ProxyDoorwayConnection(d, d.ConnectedDoorway))
						.First();

					// Remove tile and connection
					context.ProxyDungeon.RemoveTile(tile);
					collisionService.RemoveTile(tile);
					context.ProxyDungeon.RemoveConnection(precedingTileConnection);
					context.GenerationStats.PrunedBranchTileCount++;

					var precedingTile = precedingTileConnection.A.TileProxy;
					var newBranchTip = precedingTile.Placement.IsOnMainPath ? null : precedingTile;

					// The preceding tile is the new tip of this branch
					if (newBranchTip != null)
						branchTips.Push(precedingTile);

					OnTilePruned(context, tile, newBranchTip);
				}
			}
		}
	}
}