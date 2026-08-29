using DunGen.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen
{
	/// <summary>
	/// Used to determine how the number of branches are calculated
	/// </summary>
	public enum BranchMode
	{
		/// <summary>
		/// Branch count is calculated per-tile using the Archetype's BranchCount property
		/// </summary>
		Local,
		/// <summary>
		/// Branch count is calculated across the entire dungeon using the DungeonFlow's BranchCount property
		/// </summary>
		Global,
		/// <summary>
		/// Branch count is calculated for each section of the dungeon flow graph, using the Archetype's BranchCount property
		/// </summary>
		Section,
	}

	public static class BranchCountHelper
	{
		public static void ComputeBranchCounts(DungeonFlow dungeonFlow, RandomStream randomStream, DungeonProxy proxyDungeon, ref int[] mainPathBranches)
		{
			switch (dungeonFlow.BranchMode)
			{
				case BranchMode.Local:
					ComputeBranchCountsLocal(randomStream, proxyDungeon, ref mainPathBranches);
					break;

				case BranchMode.Global:
					ComputeBranchCountsGlobal(dungeonFlow, randomStream, proxyDungeon, ref mainPathBranches);
					break;

				case BranchMode.Section:
					ComputeBranchCountsPerSection(randomStream, proxyDungeon, ref mainPathBranches);
					break;

				default:
					throw new NotImplementedException(string.Format("{0}.{1} is not implemented", typeof(BranchMode).Name, dungeonFlow.BranchMode));
			}
		}

		private static void ComputeBranchCountsLocal(RandomStream randomStream, DungeonProxy proxyDungeon, ref int[] mainPathBranches)
		{
			for (int i = 0; i < mainPathBranches.Length; i++)
			{
				var tile = proxyDungeon.MainPathTiles[i];

				if (tile.Placement.Archetype == null)
					continue;

				int branchCount = tile.Placement.Archetype.BranchCount.GetRandom(randomStream);
				branchCount = Mathf.Min(branchCount, tile.UnusedDoorways.Count());

				mainPathBranches[i] = branchCount;
			}
		}

		private static void ComputeBranchCountsPerSection(RandomStream randomStream, DungeonProxy proxyDungeon, ref int[] mainPathBranches)
		{
			var sectionBranchCounts = new Dictionary<GraphLine, int>();

			// Determine how many branches should appear per section
			foreach (var tile in proxyDungeon.MainPathTiles)
			{
				var section = tile.Placement.GraphLine;

				if (section != null && !sectionBranchCounts.ContainsKey(section))
				{
					var archetype = tile.Placement.Archetype;
					sectionBranchCounts.Add(section, archetype.BranchCount.GetRandom(randomStream));
				}
			}

			// Distribute branches per-section
			foreach (var pair in sectionBranchCounts)
			{
				var section = pair.Key;
				int sectionBranchCount = pair.Value;

				// Find and cache the number of unused doorways there are per tile within this section
				var tileDoorwayCounts = new Dictionary<int, int>();

				for (int i = 0; i < proxyDungeon.MainPathTiles.Count; i++)
				{
					var tile = proxyDungeon.MainPathTiles[i];

					if (tile.Placement.GraphLine != section)
						continue;

					tileDoorwayCounts[i] = tile.UnusedDoorways.Count();
				}

				// Calculate the total number of unused doorways within this section
				int totalDoorwayCount = tileDoorwayCounts.Sum(x => x.Value);
				float[] tileWeights = new float[tileDoorwayCounts.Count];

				// Calculate a weight value for each tile in the section
				for (int i = 0; i < tileDoorwayCounts.Count; i++)
				{
					int mainPathIndex = tileDoorwayCounts.Keys.ElementAt(i);
					int tileDoorwayCount = tileDoorwayCounts.Values.ElementAt(i);

					// The proportion of branches that should belong to this tile
					float tileWeight = tileDoorwayCount / (float)totalDoorwayCount;
					tileWeights[i] = tileWeight;
				}

				// Distribute the branches for this section across all tiles within the section,
				// weighted by the number of available doorways each tile has
				int[] assignedDoorwayCounts = DistributeByWeights(sectionBranchCount, tileWeights);

				for (int i = 0; i < assignedDoorwayCounts.Length; i++)
				{
					int tileMainPathIndex = tileDoorwayCounts.Keys.ElementAt(i);
					mainPathBranches[tileMainPathIndex] = assignedDoorwayCounts[i];
				}
			}
		}

		private static void ComputeBranchCountsGlobal(DungeonFlow dungeonFlow, RandomStream randomStream, DungeonProxy proxyDungeon, ref int[] mainPathBranches)
		{
			int globalBranchCount = dungeonFlow.BranchCount.GetRandom(randomStream);
			int totalBranchableRooms = proxyDungeon.MainPathTiles.Count(t => t.Placement.Archetype != null && t.UnusedDoorways.Any());
			float branchesPerTile = globalBranchCount / (float)totalBranchableRooms;

			float branchChance = branchesPerTile;
			int branchesRemaining = globalBranchCount;

			for (int i = 0; i < mainPathBranches.Length; i++)
			{
				if (branchesRemaining <= 0)
					break;

				var tile = proxyDungeon.MainPathTiles[i];

				if (tile.Placement.Archetype == null || !tile.UnusedDoorways.Any())
					continue;

				int availableDoorways = tile.UnusedDoorways.Count();

				// Add as many guaranteed branches as needed when branchChance is > 100%
				int branchCount = Mathf.FloorToInt(branchChance);
				branchCount = Mathf.Min(branchCount, availableDoorways, tile.Placement.Archetype.BranchCount.Max, branchesRemaining);

				branchChance -= branchCount;
				availableDoorways -= branchCount;

				// Randomly choose to add a branch to this tile
				bool tileSupportsMoreBranches = branchCount < availableDoorways && branchCount < branchesRemaining;

				if (tileSupportsMoreBranches)
				{
					bool addNewBranch = randomStream.NextDouble() < branchChance;

					if (addNewBranch)
					{
						branchCount++;
						branchChance = 0f;
					}
				}

				branchChance += branchesPerTile;
				branchesRemaining -= branchCount;

				mainPathBranches[i] = branchCount;
			}
		}

		/// <summary>
		/// Distributes a whole number of elements into discrete chunks based on provided per-chunk weights
		/// </summary>
		/// <param name="count">The number of things to distribute</param>
		/// <param name="weights">The weight values for each bucket</param>
		/// <returns>The number of elements per bucket</returns>
		private static int[] DistributeByWeights(int count, float[] weights)
		{
			int Round(double x)
			{
				return (int)(x >= 0 ? x + 0.5 : x - 0.5);
			};

			if (weights == null)
				throw new ArgumentNullException(nameof(weights));
			else if (weights.Length <= 0)
				throw new ArgumentOutOfRangeException(nameof(weights), "Empty weights");

			double sum = weights.Sum(x => (double)x);

			if (sum == 0)
				throw new ArgumentException("Weights must not sum to 0", nameof(weights));

			int[] result = new int[weights.Length];
			double diff = 0;

			for (int i = 0; i < weights.Length; ++i)
			{
				double v = count * (double)(weights[i]) / sum;
				int value = Round(v);
				diff += v - value;

				if (diff >= 0.5)
				{
					value += 1;
					diff -= 1;
				}
				else if (diff <= -0.5)
				{
					value -= 1;
					diff += 1;
				}

				result[i] = value;
			}

			return result;
		}
	}
}
