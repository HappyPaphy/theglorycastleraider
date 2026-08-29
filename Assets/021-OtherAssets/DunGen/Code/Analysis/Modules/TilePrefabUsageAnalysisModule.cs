using DunGen.Graph;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Analysis.Modules
{
	[Serializable, SubclassDisplay(displayName: "Tile Prefab Usage")]
	public sealed class TilePrefabUsageAnalysisModule : IGenerationAnalysisModule
	{
		public bool AlwaysShowTileSetInName = false;


		public void OnAnalysisStarted(AnalysisResults results) { }

		public void OnDungeonGenerated(AnalysisResults results, Dungeon dungeon, GenerationStats stats)
		{
			// Count the number of instances for each tile prefab used in the dungeon
			var tileCounts = new Dictionary<(GameObject Prefab, TileSet TileSet), int>();

			foreach (var tile in dungeon.AllTiles)
			{
				var key = (tile.Prefab, tile.Placement.TileSet);

				if (tileCounts.ContainsKey(key))
					tileCounts[key]++;
				else
					tileCounts[key] = 1;
			}

			// Check for duplicate prefab names across different TileSets
			HashSet<string> duplicatePrefabNames = null;

			if (!AlwaysShowTileSetInName)
				duplicatePrefabNames = GetDuplicateNames(dungeon.DungeonFlow);

			// Report the results
			foreach (var kvp in tileCounts)
			{
				var key = kvp.Key;
				string tileName;

				// Use TileSet name to disambiguate if there are duplicate prefab names
				if (AlwaysShowTileSetInName || duplicatePrefabNames.Contains(key.Prefab.name))
					tileName = $"{key.Prefab.name} [{key.TileSet.name}]";
				else
					tileName = key.Prefab.name;

				results.AddValue($"Tiles.PrefabInstances.{tileName}", kvp.Value);
			}
		}

		private HashSet<string> GetDuplicateNames(DungeonFlow dungeonFlow)
		{
			// This has to be done across the entire DungeonFlow, not just the tiles used in this dungeon instance
			// because an individual dungeon generation might not use all available TileSets, leading to some
			// names that appear unique but actually aren't.
			var allTileSets = dungeonFlow.GetUsedTileSets();
			var allTilePrefabs = new List<GameObject>();

			foreach (var tileSet in allTileSets)
			{
				foreach (var tileEntry in tileSet.Tiles.Entries)
					allTilePrefabs.Add(tileEntry.Value);
			}

			var encounteredPrefabNames = new HashSet<string>();
			var duplicatePrefabNames = new HashSet<string>();

			foreach (var tile in allTilePrefabs)
			{
				string prefabName = tile.name;

				if (encounteredPrefabNames.Contains(prefabName))
					duplicatePrefabNames.Add(prefabName);
				else
					encounteredPrefabNames.Add(prefabName);
			}

			return duplicatePrefabNames;
		}

		public void OnDungeonGenerationFailed(AnalysisResults results, GenerationStats stats) { }

		public void OnAnalysisEnded(AnalysisResults results) { }
	}
}