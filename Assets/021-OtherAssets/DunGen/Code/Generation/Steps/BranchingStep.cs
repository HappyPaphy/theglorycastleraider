using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DunGen.Generation.Steps
{
	/// <summary>
	/// Represents a generation step that creates branch paths extending from the main path in a dungeon layout
	/// </summary>
	/// <remarks>This step analyses each tile on the main path and, based on dungeon flow settings, generates one or
	/// more branching paths of configurable depth. Branches are constructed using appropriate tile sets for branch starts,
	/// caps, and intermediate segments, as defined by the tile archetype.</remarks>
	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class BranchingStep : IGenerationStep
	{
		/// <summary>
		/// What to do when path building fails. The default value is KeepPartialPath, which retains any successfully built portion of the path when a failure
		/// occurs. Adjust this value to control whether the builder discards or preserves partial results on failure.
		/// </summary>
		public PathBuilder.FailureBehaviour FailureBehaviour = PathBuilder.FailureBehaviour.KeepPartialPath;

		/// <summary>
		/// The maximum number of attempts to place a tile in a given slot before considering it a failure.
		/// </summary>
		public int MaxAttemptsPerSlot = 1;

		/// <summary>
		/// How many steps backwards the builder can backtrack when encountering placement issues.
		/// </summary>
		public int MaxBacktrackSlots = 5;

		/// <summary>
		/// The maximum number of backtracks allowed during path building. A value of 0 indicates no backtracking is allowed.
		/// When backtracking is enabled, the builder may revisit previous placements to find alternative configurations if it encounters
		/// placement issues further along the path.
		/// </summary>
		public int MaxTotalBacktracks = 0;


		public virtual IEnumerator Execute(GenerationContext context)
		{
			var settings = context.Request.Settings;

			int[] mainPathBranches = new int[context.ProxyDungeon.MainPathTiles.Count];
			BranchCountHelper.ComputeBranchCounts(settings.DungeonFlow, context.RandomStream, context.ProxyDungeon, ref mainPathBranches);

			int branchId = 0;

			for (int b = 0; b < mainPathBranches.Length; b++)
			{
				var mainPathTile = context.ProxyDungeon.MainPathTiles[b];
				int branchCount = mainPathBranches[b];

				// This tile was created from a graph node, there should be no branching
				if (mainPathTile.Placement.Archetype == null)
					continue;

				if (branchCount == 0)
					continue;

				for (int i = 0; i < branchCount; i++)
				{
					int branchDepth = mainPathTile.Placement.Archetype.BranchingDepth.GetRandom(context.RandomStream);
					TileProxy previousTile = mainPathTile;

					var pathBuilderOptions = new PathBuilder.OptionsBuilder()
						.OnFailure(FailureBehaviour)
						.MaxAttemptsPerSlot(MaxAttemptsPerSlot)
						.MaxBacktrackSlots(MaxBacktrackSlots)
						.MaxTotalBacktracks(MaxTotalBacktracks)
						.AttachTo(mainPathTile)
						.Build();

					var builder = new PathBuilder(pathBuilderOptions);

					for (int j = 0; j < branchDepth; j++)
					{
						List<TileSet> useableTileSets;

						// Branch start tiles
						if (j == 0 && mainPathTile.Placement.Archetype.GetHasValidBranchStartTiles())
						{
							if (mainPathTile.Placement.Archetype.BranchStartType == BranchCapType.InsteadOf)
								useableTileSets = mainPathTile.Placement.Archetype.BranchStartTileSets;
							else
								useableTileSets = mainPathTile.Placement.Archetype.TileSets.Concat(mainPathTile.Placement.Archetype.BranchStartTileSets).ToList();
						}
						// Branch cap tiles
						else if (j == (branchDepth - 1) && mainPathTile.Placement.Archetype.GetHasValidBranchCapTiles())
						{
							if (mainPathTile.Placement.Archetype.BranchCapType == BranchCapType.InsteadOf)
								useableTileSets = mainPathTile.Placement.Archetype.BranchCapTileSets;
							else
								useableTileSets = mainPathTile.Placement.Archetype.TileSets.Concat(mainPathTile.Placement.Archetype.BranchCapTileSets).ToList();
						}
						// Other tiles
						else
							useableTileSets = mainPathTile.Placement.Archetype.TileSets;

						float normalizedDepth = (branchDepth <= 1) ? 1 : j / (float)(branchDepth - 1);
						var candidateTiles = useableTileSets.SelectMany(t => t.Tiles.Entries);

						int localBranchDepth = j;
						float localNormalizedDepth = normalizedDepth;

						builder.ProposeSlot(new PathBuilder.SlotSpec(
							candidateEntries: candidateTiles,
							isOnMainPath: false,
							normalizedDepth: normalizedDepth,
							placementParameters: previousTile.Placement.PlacementParameters,
							onTilePlaced: (newTile) =>
							{
								newTile.Placement.BranchDepth = localBranchDepth;
								newTile.Placement.NormalizedBranchDepth = localNormalizedDepth;
								newTile.Placement.BranchId = branchId;
								newTile.Placement.PlacementParameters = previousTile.Placement.PlacementParameters;
								previousTile = newTile;
							}));
					}

					yield return builder.Build(context);
					branchId++;
				}
			}
		}
	}
}