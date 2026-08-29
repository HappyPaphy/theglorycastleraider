using DunGen.Common;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace DunGen.Collision
{
	public class DungeonCollisionManager
	{
		private static readonly ProfilerMarker initPerfMarker = new ProfilerMarker("DungeonCollisionManager.Initialize");
		private static readonly ProfilerMarker preCachePerfMarker = new ProfilerMarker("DungeonCollisionManager.PreCacheCounds");
		private static readonly ProfilerMarker addTilePerMarker = new ProfilerMarker("DungeonCollisionManager.AddTile");
		private static readonly ProfilerMarker removeTilePerfMarker = new ProfilerMarker("DungeonCollisionManager.RemoveTile");
		private static readonly ProfilerMarker collisionBroadPhasePerfMarker = new ProfilerMarker("DungeonCollisionManager.BroadPhase");
		private static readonly ProfilerMarker collisionNarrowPhasePerfMarker = new ProfilerMarker("DungeonCollisionManager.NarrowPhase");

		public DungeonCollisionSettings Settings { get; set; }
		public ICollisionBroadphase Broadphase { get; private set; }

		private readonly List<Bounds> cachedBoundsWorldSpace = new List<Bounds>();
		private readonly List<TileProxy> tiles = new List<TileProxy>();
		private List<Bounds> boundsToCheck = new List<Bounds>();
		private Transform rootTransform;

		/// <summary>
		/// Initializes the collision manager. This should be done at the beginning of the dungeon generation process
		/// </summary>
		/// <param name="dungeonGenerator">The dungeon generator we're initializing for</param>
		public virtual void Initialize(DungeonGenerator dungeonGenerator)
		{
			using (initPerfMarker.Auto())
			{
				rootTransform = dungeonGenerator.Root != null ? dungeonGenerator.Root.transform : null;
				Clear();
				PreCacheBounds(dungeonGenerator);
				InitializeBroadphase(dungeonGenerator);
			}
		}

		private Bounds ApplyDungeonTransform(Bounds bounds)
		{
			if (rootTransform == null)
				return bounds;

			// Transform the bounds to world space using the root transform
			var center = rootTransform.TransformPoint(bounds.center);
			var size = rootTransform.TransformVector(bounds.size);
			return new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
		}

		protected virtual void Clear()
		{
			tiles.Clear();
			cachedBoundsWorldSpace.Clear();
			boundsToCheck.Clear();
		}

		protected virtual void PreCacheBounds(DungeonGenerator dungeonGenerator)
		{
			using (preCachePerfMarker.Auto())
			{
				cachedBoundsWorldSpace.Clear();

				// Multi-dungeon collision support
				var mode = dungeonGenerator.Settings.CollisionSettings.MultiDungeonCollisionMode;
				if (mode == MultiDungeonCollisionMode.AllDungeons)
				{
					// Add tiles from all dungeons
					foreach (var dungeon in UnityUtil.FindObjectsByType<Dungeon>())
						foreach (var tile in dungeon.AllTiles)
							cachedBoundsWorldSpace.Add(tile.Placement.Bounds);
				}
				else if (mode == MultiDungeonCollisionMode.ConnectedDungeons && dungeonGenerator.CompositeDungeon != null)
				{
					// Add tiles from all dungeons in the composite (except the current one)
					foreach (var dungeon in dungeonGenerator.CompositeDungeon.Dungeons)
					{
						if (dungeon != dungeonGenerator.CurrentDungeon)
						{
							foreach (var tile in dungeon.AllTiles)
								cachedBoundsWorldSpace.Add(tile.Placement.Bounds);
						}
					}
				}

				// Add all additional collision bounds to the cache
				foreach (var bounds in Settings.AdditionalCollisionBounds)
					cachedBoundsWorldSpace.Add(bounds);

				// Avoid collisions with the tile we've attached the dungeon to (if any)
				if (dungeonGenerator.AttachmentSettings != null &&
					dungeonGenerator.AttachmentSettings.TileProxy != null)
				{
					cachedBoundsWorldSpace.Add(dungeonGenerator.AttachmentSettings.TileProxy.Placement.Bounds);
				}
			}
		}

		protected virtual void InitializeBroadphase(DungeonGenerator dungeonGenerator)
		{
			var broadphaseSettings = DunGenSettings.Instance.BroadphaseSettings;

			if (broadphaseSettings == null)
			{
				Broadphase = null;
				return;
			}

			Broadphase = broadphaseSettings.Create();
			Broadphase.Init(broadphaseSettings, dungeonGenerator);

			// Add all cached bounds to the broad phase
			foreach (var bounds in cachedBoundsWorldSpace)
				Broadphase.Insert(bounds);
		}

		/// <summary>
		/// Adds a tile to the collision manager
		/// </summary>
		/// <param name="tile">The tile to add</param>
		public virtual void AddTile(TileProxy tile)
		{
			using (addTilePerMarker.Auto())
			{
				tiles.Add(tile);
				Broadphase?.Insert(ApplyDungeonTransform(tile.Placement.Bounds));
			}
		}

		/// <summary>
		/// Removed a tile from the collision manager
		/// </summary>
		/// <param name="tile">The tile to remove</param>
		public virtual void RemoveTile(TileProxy tile)
		{
			using (removeTilePerfMarker.Auto())
			{
				tiles.Remove(tile);
				Broadphase?.Remove(ApplyDungeonTransform(tile.Placement.Bounds));
			}
		}

		/// <summary>
		/// Checks if a tile is colliding with any other tiles in the dungeon
		/// </summary>
		/// <param name="upDirection">The up direction for the dungeon</param>
		/// <param name="prospectiveNewTile">The new tile we'd like to spawn</param>
		/// <param name="previousTile">The tile we're trying to attach to</param>
		/// <returns>True if any blocking collision occurs</returns>
		public virtual bool IsCollidingWithAnyTile(AxisDirection upDirection, TileProxy prospectiveNewTile, TileProxy previousTile)
		{
			bool isColliding = false;

			using (collisionBroadPhasePerfMarker.Auto())
			{
				UpdateBoundsToCheck(prospectiveNewTile, previousTile);
			}

			using (collisionNarrowPhasePerfMarker.Auto())
			{
				var prospectiveBounds = ApplyDungeonTransform(prospectiveNewTile.Placement.Bounds);

				// If we have a previous tile, check for collisions with it first so we don't have to check all other tiles.
				if (previousTile != null)
				{
					var previousBounds = ApplyDungeonTransform(previousTile.Placement.Bounds);
					boundsToCheck.Remove(previousBounds);

					if (Settings.DisallowOverhangs)
					{
						if (UnityUtil.AreBoundsOverlappingOrOverhanging(prospectiveBounds, previousBounds, upDirection, Settings.OverlapThreshold))
							isColliding = true;
					}
					else
					{
						if (UnityUtil.AreBoundsOverlapping(prospectiveBounds, previousBounds, Settings.OverlapThreshold))
							isColliding = true;
					}
				}

				if (!isColliding)
				{
					for (int i = 0; i < boundsToCheck.Count; i++)
					{
						var bounds = boundsToCheck[i];
						float maxOverlap = -Settings.Padding;

						if (Settings.DisallowOverhangs)
						{
							if (UnityUtil.AreBoundsOverlappingOrOverhanging(prospectiveBounds, bounds, upDirection, maxOverlap))
							{
								isColliding = true;
								break;
							}
						}
						else
						{
							if (UnityUtil.AreBoundsOverlapping(prospectiveBounds, bounds, maxOverlap))
							{
								isColliding = true;
								break;
							}
						}
					}
				}
			}

			// Process custom collision predicate if there is one
			if (Settings.AdditionalCollisionsPredicate != null)
				isColliding = Settings.AdditionalCollisionsPredicate(ApplyDungeonTransform(prospectiveNewTile.Placement.Bounds), isColliding);

			return isColliding;
		}

		protected virtual void UpdateBoundsToCheck(TileProxy prospectiveNewTile, TileProxy previousTile)
		{
			boundsToCheck.Clear();

			if (Broadphase != null)
				Broadphase.Query(ApplyDungeonTransform(prospectiveNewTile.Placement.Bounds), ref boundsToCheck);
			else
			{
				foreach (var tile in tiles)
					if (tile != previousTile)
						boundsToCheck.Add(ApplyDungeonTransform(tile.Placement.Bounds));

				boundsToCheck.AddRange(cachedBoundsWorldSpace);
			}
		}
	}
}
