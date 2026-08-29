using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Pooling
{
	[Serializable]
	public sealed class TilePoolPreloaderEntry
	{
		public Tile TilePrefab = null;
		public int Count = 1;
	}

	/// <summary>
	/// This class is used to create a collection of tiles in the scene to pre-load the tile pool
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DunGen/Pooling/Tile Pool Pre-loader")]
	[HelpURL("https://dungen-docs.aegongames.com/optimization/tile-pooling/#pre-warming-the-pool")]
	public class TilePoolPreloader : MonoBehaviour
	{
		#region Nested Types

		[Serializable]
		public sealed class SpawnedTileInstances
		{
			public Tile TilePrefab;
			public List<Tile> Instances = new List<Tile>();
		}

		#endregion

		public List<TilePoolPreloaderEntry> Entries = new List<TilePoolPreloaderEntry>();

		[SerializeField]
		private List<SpawnedTileInstances> spawnedTileInstances = new List<SpawnedTileInstances>();


		public void ClearSpawnedInstances()
		{
			foreach(var entry in spawnedTileInstances)
			{
				foreach (var instance in entry.Instances)
					if(instance != null)
						DestroyImmediate(instance.gameObject);
			}

			spawnedTileInstances.Clear();
		}

		public IEnumerable<Tile> GetTileInstancesForPrefab(Tile prefab)
		{
			if (prefab == null)
				return null;

			var entry = spawnedTileInstances.Find(x => x.TilePrefab == prefab);

			if (entry != null)
				return entry.Instances;

			return null;
		}

		public bool HasSpawnedInstances() => spawnedTileInstances.Count > 0;

		public void RefreshTileInstances()
		{
			// Remove instances for tiles we're no longer interested in
			for (int i = spawnedTileInstances.Count - 1; i >= 0; i--)
			{
				var entry = spawnedTileInstances[i];
				bool shouldExist = entry.TilePrefab != null && Entries.Exists(x => x.TilePrefab == entry.TilePrefab);

				if (!shouldExist)
				{
					foreach (var instance in entry.Instances)
						if (instance != null)
							DestroyImmediate(instance.gameObject);

					spawnedTileInstances.RemoveAt(i);
				}
			}

			// Add or update instances for tiles we're interested in
			foreach (var entry in Entries)
			{
				if(entry.TilePrefab == null)
					continue;

				var spawnedTileInstance = spawnedTileInstances.Find(x => x.TilePrefab == entry.TilePrefab);

				if (spawnedTileInstance == null)
				{
					spawnedTileInstance = new SpawnedTileInstances { TilePrefab = entry.TilePrefab };
					spawnedTileInstances.Add(spawnedTileInstance);
				}

				// Clear invalid instances
				spawnedTileInstance.Instances.RemoveAll(x => x == null);

				int existingCount = spawnedTileInstance.Instances.Count;

				// Not enough instances, create more
				if (existingCount < entry.Count)
				{
					int instancesToCreate = entry.Count - existingCount;

					for (int i = 0; i < instancesToCreate; i++)
					{
						var instance = Instantiate(entry.TilePrefab, transform);
						instance.gameObject.SetActive(false);
						spawnedTileInstance.Instances.Add(instance);
					}
				}
				// Too many instances, destroy the excess
				else if(existingCount > entry.Count)
				{
					for (int i = existingCount - 1; i >= entry.Count; i--)
					{
						var instance = spawnedTileInstance.Instances[i];
						spawnedTileInstance.Instances.RemoveAt(i);

						if (instance != null)
							DestroyImmediate(instance.gameObject);
					}
				}
			}
		}
	}
}
