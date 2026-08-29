using DunGen.Graph;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DunGen.Analysis.Modules
{
	/// <summary>
	/// An analysis module that performs validation checks on generated dungeons to ensure they meet
	/// the criteria defined by the dungeon flow and other settings. Useful for identifying issues
	/// with dungeon generation.
	/// </summary>
	[Serializable, SubclassDisplay(displayName: "Dungeon Validation")]
	public class ValidationAnalysisModule : IGenerationAnalysisModule
	{
		/// <summary>
		/// If true, warnings will be enabled in addition to errors. Warnings indicate when something
		/// does not strictly violate the dungeon flow rules, but may still be undesirable.
		/// For example, a branch count being lower than expected is considered a warning as there's
		/// no way to guarantee that the minimum number of branches will be met during generation.
		/// </summary>
		public bool EnableWarnings = false;

		protected DungeonFlow dungeonFlow;
		protected AnalysisResults results;
		protected Dungeon dungeon;


		public virtual void OnAnalysisStarted(AnalysisResults results) { }

		public virtual void OnDungeonGenerated(AnalysisResults results, Dungeon dungeon, GenerationStats stats)
		{
			// Cache references for use in validation methods
			this.results = results;
			this.dungeon = dungeon;
			dungeonFlow = dungeon.DungeonFlow;

			ValidateArchetypeUniqueness(); // Ensure that if an archetype requires uniqueness, it is only used once
			ValidateMainPathLength(); // Ensure main path length is within expected range
			ValidateBranchCount(); // Ensure branch count meets the criteria defined by the branch mode
			ValidateBranchLengths(); // Ensure branch lengths are within expected ranges
			ValidateBranchPruning(); // Ensure branch-tip tiles that should have been pruned are not present
			ValidateBranchStartAndCapTiles(); // Ensure branch start and cap tiles are valid according to their archetypes
			ValidateGlobalProps(); // Ensure global props are present in expected quantities
		}

		protected virtual void ValidateArchetypeUniqueness()
		{
			var seenLines = new HashSet<GraphLine>();
			var seenArchetypes = new HashSet<DungeonArchetype>();

			foreach (var tile in dungeon.AllTiles)
			{
				var graphLine = tile.Placement.GraphLine;

				if (graphLine == null)
					continue;

				seenLines.Add(graphLine);

				var archetype = tile.Placement.Archetype;

				if(seenArchetypes.Contains(archetype) && archetype.Unique)
					results.Error($"Dungeon archetype uniqueness validation failed. Archetype '{archetype.name}' is marked as unique but is used multiple times in the dungeon.", archetype);
				else
					seenArchetypes.Add(archetype);
			}
		}

		protected virtual void ValidateBranchLengths()
		{
			foreach(var branch in dungeon.Branches)
			{
				var startTile = branch.Tiles[0];
				var archetype = startTile.Placement.Archetype;

				var expectedBranchLength = archetype.BranchingDepth;
				int actualBranchLength = branch.Tiles.Count;

				if (actualBranchLength > expectedBranchLength.Max)
					results.Error($"Dungeon branch length validation failed for branch with index '{branch.Index}'. Archetype expects the branch length to be less than {expectedBranchLength.Max}, but got {actualBranchLength}.", startTile);
				if (EnableWarnings && actualBranchLength < expectedBranchLength.Min)
					results.Warning($"Dungeon branch length warning for branch with index '{branch.Index}'. Archetype expects the branch length to be at least {expectedBranchLength.Min}, but got {actualBranchLength}.", startTile);
			}
		}

		protected virtual void ValidateGlobalProps()
		{
			var allGlobalProps = dungeon.gameObject.GetComponentsInChildren<GlobalProp>(false)
				.Where(x => x.gameObject.activeInHierarchy)
				.GroupBy(x => x.PropGroupID)
				.ToDictionary(g => g.Key, g => g.ToList());

			foreach (var globalPropSpec in dungeonFlow.GlobalProps)
			{
				var expectedPropCount = globalPropSpec.Count;
				int actualPropCount = 0;

				if (allGlobalProps.TryGetValue(globalPropSpec.ID, out var props))
					actualPropCount = props.Count;

				if (actualPropCount < expectedPropCount.Min || actualPropCount > expectedPropCount.Max)
					results.Error($"Dungeon global prop validation failed for global prop with ID '{globalPropSpec.ID}'. Expected between {expectedPropCount.Min} and {expectedPropCount.Max}, but got {actualPropCount}");
			}
		}

		protected virtual void ValidateBranchStartAndCapTiles()
		{
			foreach (var branch in dungeon.Branches)
			{
				var branchStart = branch.Tiles[0];
				var branchTip = branch.Tiles[branch.Tiles.Count - 1];
				var archetype = branchStart.Placement.Archetype;

				// Check branch start tiles
				if (archetype.BranchStartType == BranchCapType.InsteadOf && archetype.GetHasValidBranchStartTiles())
				{
					var validStartTiles = archetype.BranchStartTileSets
						.SelectMany(x => x.Tiles.Entries)
						.Select(x => x.Value)
						.ToArray();

					if (!validStartTiles.Contains(branchStart.Prefab))
						results.Error($"Branch start validation failed. '{branchStart.Prefab.name}' appears at the start of a branch, but it's archetype '{archetype.name}' forbids this", archetype);
				}

				// Check branch cap tiles
				if (archetype.BranchCapType == BranchCapType.InsteadOf && archetype.GetHasValidBranchCapTiles())
				{
					var validCapTiles = archetype.BranchCapTileSets
						.SelectMany(x => x.Tiles.Entries)
						.Select(x => x.Value)
						.ToArray();

					if (EnableWarnings && !validCapTiles.Contains(branchTip.Prefab))
						results.Warning($"Branch cap validation failed. '{branchTip.Prefab.name}' appears at the tip of a branch, but it's archetype '{archetype.name}' forbids this", archetype);
				}
			}
		}

		protected virtual void ValidateBranchPruning()
		{
			if (dungeonFlow.BranchPruneTags.Count == 0)
				return;

			foreach (var branch in dungeon.Branches)
			{
				var branchTip = branch.Tiles[branch.Tiles.Count - 1];

				if (dungeonFlow.ShouldPruneBranchTipWithTags(branchTip.Tags))
					results.Error($"Dungeon branch pruning validation failed. Branch-end tile '{branchTip.name}' should have been pruned based on its tags, but was not.", branchTip);
			}
		}

		protected virtual void ValidateMainPathLength()
		{
			var expectedMainPathLength = dungeonFlow.Length;
			int actualMainPathLength = dungeon.MainPathTiles.Count;

			if (actualMainPathLength < expectedMainPathLength.Min || actualMainPathLength > expectedMainPathLength.Max)
				results.Error($"Dungeon main path length validation failed. Expected length between {expectedMainPathLength.Min} and {expectedMainPathLength.Max}, but got {actualMainPathLength}.", dungeonFlow);
		}

		protected virtual void ValidateBranchCount()
		{
			switch (dungeonFlow.BranchMode)
			{
				case BranchMode.Local:
					ValidateBranchCount_Local();
					break;
				case BranchMode.Section:
					ValidateBranchCount_Section();
					break;
				case BranchMode.Global:
					ValidateBranchCount_Global();
					break;
				default:
					throw new ArgumentOutOfRangeException($"No validation implemented for {nameof(BranchMode)}.{dungeonFlow.BranchMode}");
			}
		}

		protected virtual void ValidateBranchCount_Local()
		{
			var branchCounts = new Dictionary<Tile, int>();

			// Gather branch counts per attachment tile
			foreach (var branch in dungeon.Branches)
			{
				var startTile = branch.Tiles[0];

				Tile attachmentPoint = null;
				foreach (var doorway in startTile.UsedDoorways)
				{
					var otherTile = doorway.ConnectedDoorway.Tile;

					if (otherTile.Placement.IsOnMainPath)
					{
						attachmentPoint = otherTile;
						break;
					}
				}

				if (attachmentPoint == null)
				{
					results.Error($"Could not determine attachment point for branch starting at tile '{startTile.name}'.", dungeonFlow);
					continue;
				}

				if (!branchCounts.ContainsKey(attachmentPoint))
					branchCounts[attachmentPoint] = 0;

				branchCounts[attachmentPoint]++;
			}

			// Validate branch counts for each tile
			foreach (var kvp in branchCounts)
			{
				var tile = kvp.Key;
				int actualBranchCount = kvp.Value;
				var expectedBranchCount = tile.Placement.Archetype.BranchCount;

				if (actualBranchCount > expectedBranchCount.Max)
					results.Error($"Dungeon branch count validation failed for tile '{tile.name}'. Expected a maximum of {expectedBranchCount.Max} branches on the tile, but got {actualBranchCount}.", tile);
				if (EnableWarnings && actualBranchCount < expectedBranchCount.Min)
					results.Warning($"Dungeon branch count warning for tile '{tile.name}'. Expected a minimum of {expectedBranchCount.Min} branches on the tile, but got {actualBranchCount}.", tile);
			}
		}

		protected virtual void ValidateBranchCount_Section()
		{
			var lineData = new Dictionary<GraphLine, (DungeonArchetype Archetype, List<Dungeon.Branch> Branches)>();

			// Gather branch data per graph line
			foreach (var branch in dungeon.Branches)
			{
				var startTile = branch.Tiles[0];
				var line = startTile.Placement.GraphLine;

				// We're only interested in branches spawned from lines
				if (line == null)
					continue;

				// Create line data entry if it doesn't exist
				if (!lineData.TryGetValue(line, out var data))
				{
					data = (startTile.Placement.Archetype, new List<Dungeon.Branch>());
					lineData[line] = data;
				}

				data.Branches.Add(branch);
			}

			// Validate branch counts for each line section
			foreach (var kvp in lineData)
			{
				var (archetype, branches) = kvp.Value;

				var expectedBranchCount = archetype.BranchCount;
				int actualBranchCount = branches.Count;

				if (actualBranchCount > expectedBranchCount.Max)
					results.Error($"Dungeon branch count validation failed for line in archetype '{archetype.name}'. Expected a maximum of {expectedBranchCount.Max} branches on the section, but got {actualBranchCount}.", dungeonFlow);
				if (EnableWarnings && actualBranchCount < expectedBranchCount.Min)
					results.Warning($"Dungeon branch count warning for line in archetype '{archetype.name}'. Expected a minimum of {expectedBranchCount.Min} branches on the section, but got {actualBranchCount}.", dungeonFlow);
			}
		}

		protected virtual void ValidateBranchCount_Global()
		{
			var expectedBranchCount = dungeonFlow.BranchCount;
			int actualBranchCount = dungeon.Branches.Count;

			if (actualBranchCount > expectedBranchCount.Max)
				results.Error($"Dungeon global branch count validation failed. Expected a maximum of {expectedBranchCount.Max} branches across the entire dungeon, but got {actualBranchCount}.", dungeonFlow);
			if (EnableWarnings && actualBranchCount < expectedBranchCount.Min)
				results.Warning($"Dungeon global branch count warning. Expected a minimum of {expectedBranchCount.Min} branches across the entire dungeon, but got {actualBranchCount}.", dungeonFlow);
		}

		public virtual void OnDungeonGenerationFailed(AnalysisResults results, GenerationStats stats) { }

		public virtual void OnAnalysisEnded(AnalysisResults results) { }
	}
}