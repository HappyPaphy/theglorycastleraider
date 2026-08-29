using DunGen.Pooling;
using UnityEngine;

namespace DunGen.Placement
{
	public delegate void TileInstanceSpawnedDelegate(Tile tilePrefab, Tile tileInstance, bool fromPool);
	public delegate void TileInstanceDespawnedDelegate(Tile tileInstance);

	/// <summary>
	/// A class responsible for the spawning and despawning of tiles
	/// </summary>
	public class TileInstanceSource
	{
		public event TileInstanceSpawnedDelegate TileInstanceSpawned;
		public event TileInstanceDespawnedDelegate TileInstanceDespawned;

		protected readonly BucketedObjectPool<Tile, Tile> tilePool;
		protected bool enableTilePooling;
		protected GameObject dungeonRoot;
		protected Transform tilePoolRoot;
		protected TilePoolPreloader tilePoolPreloader;


		public TileInstanceSource()
		{
			tilePool = new BucketedObjectPool<Tile, Tile>(
				objectFactory: template =>
				{
					var tileObj = Object.Instantiate(template);

					if (tileObj.TryGetComponent<Tile>(out var tile))
						tile.RefreshTileEventReceivers();

					return tileObj;
				});
		}

		/// <summary>
		/// Initialises the TileInstanceSource
		/// </summary>
		/// <param name="enableTilePooling">Should tile pooling be used?</param>
		/// <param name="dungeonRoot">The root GameObject of the dungeon</param>
		public virtual void Initialise(bool enableTilePooling, GameObject dungeonRoot)
		{
			this.enableTilePooling = enableTilePooling;
			this.dungeonRoot = dungeonRoot;

			if (!enableTilePooling)
				return;

			// Try to find a TilePoolPreloader in the scene, and pre-warm the pool with its contents
			if (tilePoolPreloader == null)
				TryPreloadTilePool();

			// Create a GameObject to parent all pooled tiles to
			if (tilePoolRoot == null)
			{
				var tilePoolObj = new GameObject("Tile Pool");
				tilePoolObj.SetActive(false);
				tilePoolRoot = tilePoolObj.transform;
			}
		}

		protected void TryPreloadTilePool()
		{
			tilePoolPreloader = UnityUtil.FindObjectByType<TilePoolPreloader>();

			if(tilePoolPreloader == null)
				return;

			// Make the tile preloader the pool root
			tilePoolPreloader.gameObject.SetActive(false);
			tilePoolRoot = tilePoolPreloader.transform;

			// Insert each preloaded tile into the correct bucket in the pool
			foreach (var entry in tilePoolPreloader.Entries)
			{
				var prefab = entry.TilePrefab;

				if (prefab == null)
					continue;

				var instances = tilePoolPreloader.GetTileInstancesForPrefab(prefab);

				if(instances == null)
					continue;

				foreach (var instance in instances)
				{
					// The tile should be active as the tile pool root itself is inactive
					instance.gameObject.SetActive(true);

					instance.RefreshTileEventReceivers();
					tilePool.InsertObject(prefab, instance);
				}
			}
		}

		/// <summary>
		/// Spawns a tile at the specified position and rotation. If pooling is enabled, the tile will
		/// be taken from the pool. If pooling is disabled, or the pool is empty, a new tile will be instantiated
		/// </summary>
		/// <param name="tilePrefab">Tile prefab to spawn</param>
		/// <param name="localPosition">Local-space position</param>
		/// <param name="localRotation">Local-space rotation</param>
		/// <returns>The spawned tile instance</returns>
		public virtual Tile SpawnTile(Tile tilePrefab, Vector3 localPosition, Quaternion localRotation)
		{
#if UNITY_EDITOR
			if (!UnityEditor.EditorApplication.isPlaying)
				enableTilePooling = false;
#endif

			if (enableTilePooling)
			{
				bool fromPool = tilePool.TryTakeObject(tilePrefab, out var tile);
				var tileTransform = tile.transform;
				tileTransform.parent = dungeonRoot.transform;
				tileTransform.localPosition = localPosition;
				tileTransform.localRotation = localRotation;

				tile.TileSpawned();
				TileInstanceSpawned?.Invoke(tilePrefab, tile, fromPool);

				return tile;
			}
			else
			{
				GameObject tileObj;

#if UNITY_EDITOR
			if(UnityEditor.EditorApplication.isPlaying)
				tileObj = GameObject.Instantiate(tilePrefab.gameObject, dungeonRoot.transform);
			else
				tileObj = UnityEditor.PrefabUtility.InstantiatePrefab(tilePrefab.gameObject, dungeonRoot.transform) as GameObject;
#else
			tileObj = GameObject.Instantiate(tilePrefab.gameObject, dungeonRoot.transform);
#endif

				tileObj.transform.localPosition = localPosition;
				tileObj.transform.localRotation = localRotation;

				if (!tileObj.TryGetComponent<Tile>(out var tile))
					tile = tileObj.AddComponent<Tile>();

				tile.RefreshTileEventReceivers();
				tile.TileSpawned();
				TileInstanceSpawned?.Invoke(tilePrefab, tile, false);

				return tile;
			}
		}

		/// <summary>
		/// Despawns a tile. If pooling is enabled, the tile will be returned to the pool.
		/// If pooling is disabled, the tile will be destroyed
		/// </summary>
		/// <param name="tileInstance">The tile instance to despawn</param>
		public virtual void DespawnTile(Tile tileInstance)
		{
			bool returnedToPool = false;

			// Try to return the tile to the pool
			if (enableTilePooling)
			{
				returnedToPool = tilePool.ReturnObject(tileInstance);

				if (returnedToPool)
				{
					tileInstance.transform.parent = tilePoolRoot;
					tileInstance.TileDespawned();
					TileInstanceDespawned?.Invoke(tileInstance);
				}
			}

			// If we can't return the tile to the pool, or pooling was disabled, destroy the tile instead
			if (!returnedToPool)
			{
				tileInstance.TileDespawned();
				TileInstanceDespawned?.Invoke(tileInstance);
				GameObject.DestroyImmediate(tileInstance.gameObject);
			}
		}
	}
}
