using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen
{
	[AddComponentMenu("")] // Hide in menu
	[Obsolete("AdjacentRoomCulling has been deprecated. Please use CullingCamera with the 'Adjacency Culling' strategy instead. This component will be removed in future versions.")]
	public class AdjacentRoomCulling : MonoBehaviour
	{
		public delegate void VisibilityChangedDelegate(Tile tile, bool visible);

		/// <summary>
		/// How deep from the current room should tiles be considered visible
		/// 0 = Only the current tile
		/// 1 = The current tile and all its neighbours
		/// 2 = The current tile, all its neighbours, and all THEIR neighbours
		/// etc...
		/// </summary>
		public int AdjacentTileDepth = 1;

		/// <summary>
		/// If true, tiles behind a closed door will be culled, even if they're within <see cref="AdjacentTileDepth"/>
		/// </summary>
		public bool CullBehindClosedDoors = true;

		/// <summary>
		/// If set, this transform will be used as the vantage point that rooms should be culled from.
		/// Useful for third person games where you want to cull from the character's position, not the camera
		/// </summary>
		public Transform TargetOverride = null;

		/// <summary>
		/// Whether culling should handle any components that start disabled
		/// </summary>
		public bool IncludeDisabledComponents = false;

		/// <summary>
		/// A set of override values for specific renderers.
		/// By default, this script will overwrite any renderer.enabled values we might set in
		/// gameplay code. This property lets us tell the culling that we want to override the
		/// visibility values its setting
		/// </summary>
		[NonSerialized]
		public Dictionary<Renderer, bool> OverrideRendererVisibilities = new Dictionary<Renderer, bool>();

		/// <summary>
		/// A set of override values for specific lights.
		/// By default, this script will overwrite any light.enabled values we might set in
		/// gameplay code. This property lets us tell the culling that we want to override the
		/// visibility values its setting
		/// </summary>
		[NonSerialized]
		public Dictionary<Light, bool> OverrideLightVisibilities = new Dictionary<Light, bool>();

		/// <summary>
		/// True when a dungeon has been assigned and we're ready to start culling
		/// </summary>
		public bool Ready { get; protected set; }
		public event VisibilityChangedDelegate TileVisibilityChanged;

		protected List<Dungeon> dungeons = new List<Dungeon>();
		protected List<Tile> allTiles = new List<Tile>();
		protected List<Door> allDoors = new List<Door>();
		protected List<Tile> oldVisibleTiles = new List<Tile>();
		protected List<Tile> visibleTiles = new List<Tile>();
		protected Dictionary<Tile, bool> tileVisibilities = new Dictionary<Tile, bool>();
		protected Dictionary<Tile, List<Renderer>> tileRenderers = new Dictionary<Tile, List<Renderer>>();
		protected Dictionary<Tile, List<Light>> lightSources = new Dictionary<Tile, List<Light>>();
		protected Dictionary<Tile, List<ReflectionProbe>> reflectionProbes = new Dictionary<Tile, List<ReflectionProbe>>();
		protected Dictionary<Door, List<Renderer>> doorRenderers = new Dictionary<Door, List<Renderer>>();

		protected Transform targetTransform { get { return (TargetOverride != null) ? TargetOverride : transform; } }
		private bool dirty;
		private DungeonGenerator generator;
		private Tile currentTile;
		private Queue<Tile> tilesToSearch;
		private List<Tile> searchedTiles;


		protected virtual void OnEnable()
		{
			var runtimeDungeon = UnityUtil.FindObjectByType<RuntimeDungeon>();

			if (runtimeDungeon != null)
			{
				generator = runtimeDungeon.Generator;
				generator.OnGenerationComplete += OnDungeonGenerationComplete;

				if (generator.Status == GenerationStatus.Complete)
					AddDungeon(generator.CurrentDungeon);
			}
		}

		protected virtual void OnDisable()
		{
			if (generator != null)
				generator.OnGenerationComplete -= OnDungeonGenerationComplete;

			ClearAllDungeons();
		}

		public virtual void SetDungeon(Dungeon newDungeon)
		{
			if (newDungeon == null)
				return;

			ClearAllDungeons();
			AddDungeon(newDungeon);
		}

		public virtual void AddDungeon(Dungeon dungeon)
		{
			if (dungeon == null || dungeons.Contains(dungeon))
				return;

			dungeons.Add(dungeon);

			var dungeonTiles = new List<Tile>(dungeon.AllTiles);
			var dungeonDoors = new List<Door>(GetAllDoorsInDungeon(dungeon));

			allTiles.AddRange(dungeonTiles);
			allDoors.AddRange(dungeonDoors);

			UpdateRendererLists(dungeonTiles, dungeonDoors);

			foreach (var tile in dungeonTiles)
				SetTileVisibility(tile, false);

			foreach (var door in dungeonDoors)
			{
				door.OnDoorStateChanged += OnDoorStateChanged;
				SetDoorVisibility(door, false);
			}

			Ready = true;
			dirty = true;
		}

		private void RemoveNullKeys<TKey, TValue>(ref Dictionary<TKey, TValue> dictionary)
		{
			var keysToRemove = dictionary.Keys
				.Where(key => key == null)
				.ToArray();

			foreach (var key in keysToRemove)
			{
				if (dictionary.ContainsKey(key))
					dictionary.Remove(key);
			}
		}

		public virtual void RemoveDungeon(Dungeon dungeon)
		{
			if (dungeon == null || !dungeons.Contains(dungeon))
				return;

			dungeons.Remove(dungeon);

			// Clear any leftover data from destroyed dungeons
			allTiles.RemoveAll(x => !x);
			visibleTiles.RemoveAll(x => !x);
			allDoors.RemoveAll(x => !x);
			RemoveNullKeys(ref tileVisibilities);
			RemoveNullKeys(ref tileRenderers);
			RemoveNullKeys(ref lightSources);
			RemoveNullKeys(ref reflectionProbes);
			RemoveNullKeys(ref doorRenderers);

			foreach (var tile in dungeon.AllTiles)
			{
				SetTileVisibility(tile, true);
				allTiles.Remove(tile);
				tileVisibilities.Remove(tile);
				tileRenderers.Remove(tile);
				lightSources.Remove(tile);
				reflectionProbes.Remove(tile);
				visibleTiles.Remove(tile);
				oldVisibleTiles.Remove(tile);
			}

			foreach (var doorObj in dungeon.Doors)
			{
				if(doorObj == null)
					continue;

				if (!doorObj.TryGetComponent<Door>(out var door))
					continue;

				SetDoorVisibility(door, true);
				door.OnDoorStateChanged -= OnDoorStateChanged;
				allDoors.Remove(door);
				doorRenderers.Remove(door);
			}

			if (allTiles.Count == 0)
				Ready = false;
		}

		public virtual void ClearAllDungeons()
		{
			Ready = false;

			foreach (var door in allDoors)
			{
				if (door != null)
					door.OnDoorStateChanged -= OnDoorStateChanged;
			}

			dungeons.Clear();
			allTiles.Clear();
			visibleTiles.Clear();
			allDoors.Clear();
			oldVisibleTiles.Clear();

			tileVisibilities.Clear();
			tileRenderers.Clear();
			lightSources.Clear();
			reflectionProbes.Clear();
			doorRenderers.Clear();
		}

		public virtual bool IsTileVisible(Tile tile)
		{
			bool visibility;
			if (tileVisibilities.TryGetValue(tile, out visibility))
				return visibility;
			else
				return false;
		}

		protected IEnumerable<Door> GetAllDoorsInDungeon(Dungeon dungeon)
		{
			foreach (var doorObj in dungeon.Doors)
			{
				if (doorObj == null)
					continue;

				var door = doorObj.GetComponent<Door>();
				if (door != null)
					yield return door;
			}
		}

		protected virtual void OnDoorStateChanged(Door door, bool isOpen)
		{
			dirty = true;
		}

		protected virtual void OnDungeonGenerationComplete(DungeonGenerator generator)
		{
			bool isAttachedDungeon = generator.AttachmentSettings != null &&
						 generator.AttachmentSettings.TileProxy != null;

			// Remove the last dungeon if we're not attaching to it
			if (!isAttachedDungeon && dungeons.Count > 0)
				RemoveDungeon(dungeons[dungeons.Count - 1]);

			AddDungeon(generator.CurrentDungeon);
		}

		protected virtual void LateUpdate()
		{
			if (!Ready)
				return;

			var oldTile = currentTile;

			// If currentTile doesn't exist, we need to first look for a dungeon,
			// then search every tile to find one that encompasses this GameObject
			if (currentTile == null)
				currentTile = FindCurrentTile();
			// If currentTile does exist, but we're not in it, we can perform a
			// breadth-first search radiating from currentTile. Assuming the player
			// is likely to be in an adjacent room, this should be much quicker than
			// testing every tile in the dungeon
			else if (!currentTile.Bounds.Contains(targetTransform.position))
				currentTile = SearchForNewCurrentTile();

			if (currentTile != oldTile)
				dirty = true;

			if (dirty)
				RefreshVisibility();

			dirty = false;
		}

		protected virtual void RefreshVisibility()
		{
			var temp = visibleTiles;
			visibleTiles = oldVisibleTiles;
			oldVisibleTiles = temp;

			UpdateVisibleTiles();

			// Hide any tiles that are no longer visible
			foreach (var tile in oldVisibleTiles)
				if (!visibleTiles.Contains(tile))
					SetTileVisibility(tile, false);

			// Show tiles that are newly visible
			foreach (var tile in visibleTiles)
				if (!oldVisibleTiles.Contains(tile))
					SetTileVisibility(tile, true);

			oldVisibleTiles.Clear();
			RefreshDoorVisibilities();
		}

		protected virtual void RefreshDoorVisibilities()
		{
			foreach (var door in allDoors)
			{
				bool visible = visibleTiles.Contains(door.DoorwayA.Tile) || visibleTiles.Contains(door.DoorwayB.Tile);
				SetDoorVisibility(door, visible);
			}
		}

		protected virtual void SetDoorVisibility(Door door, bool visible)
		{
			if (doorRenderers.TryGetValue(door, out List<Renderer> renderers))
			{
				for (int i = renderers.Count - 1; i >= 0; i--)
				{
					var renderer = renderers[i];
					if (renderer == null)
					{
						renderers.RemoveAt(i);
						continue;
					}

					// Check for overridden renderer visibility
					if (OverrideRendererVisibilities.TryGetValue(renderer, out bool visibleOverride))
						renderer.enabled = visibleOverride;
					else
						renderer.enabled = visible;
				}
			}
		}

		protected virtual void UpdateVisibleTiles()
		{
			visibleTiles.Clear();

			if (currentTile != null)
				visibleTiles.Add(currentTile);

			int processTileStart = 0;

			// Add neighbours down to RoomDepth (0 = just tiles containing characters, 1 = plus adjacent tiles, etc)
			for (int i = 0; i < AdjacentTileDepth; i++)
			{
				int processTileEnd = visibleTiles.Count;

				for (int t = processTileStart; t < processTileEnd; t++)
				{
					var tile = visibleTiles[t];

					// Get all connections to adjacent tiles
					foreach (var doorway in tile.UsedDoorways)
					{
						var adjacentTile = doorway.ConnectedDoorway.Tile;

						if(adjacentTile == null)
							continue;

						// Skip the tile if it's already visible
						if (visibleTiles.Contains(adjacentTile))
							continue;

						// No need to add adjacent rooms to the visible list when the door between them is closed
						if (CullBehindClosedDoors)
						{
							var door = doorway.DoorComponent;
							if (door != null && door.ShouldCullBehind)
								continue;
						}

						visibleTiles.Add(adjacentTile);
					}
				}

				processTileStart = processTileEnd;
			}
		}

		protected virtual void SetTileVisibility(Tile tile, bool visible)
		{
			tileVisibilities[tile] = visible;

			// Renderers
			if (tileRenderers.TryGetValue(tile, out List<Renderer> renderers))
			{
				for (int i = renderers.Count - 1; i >= 0; i--)
				{
					var renderer = renderers[i];
					if (renderer == null)
					{
						renderers.RemoveAt(i);
						continue;
					}

					// Check for overridden renderer visibility
					if (OverrideRendererVisibilities.TryGetValue(renderer, out bool visibleOverride))
						renderer.enabled = visibleOverride;
					else
						renderer.enabled = visible;
				}
			}

			// Lights
			if (lightSources.TryGetValue(tile, out List<Light> lights))
			{
				for (int i = lights.Count - 1; i >= 0; i--)
				{
					var light = lights[i];
					if (light == null)
					{
						lights.RemoveAt(i);
						continue;
					}

					// Check for overridden light visibility
					if (OverrideLightVisibilities.TryGetValue(light, out bool visibleOverride))
						light.enabled = visibleOverride;
					else
						light.enabled = visible;
				}
			}

			// Reflection Probes
			if (reflectionProbes.TryGetValue(tile, out List<ReflectionProbe> probes))
			{
				for (int i = probes.Count - 1; i >= 0; i--)
				{
					var probe = probes[i];

					if (probe == null)
					{
						probes.RemoveAt(i);
						continue;
					}

					probe.enabled = visible;
				}
			}

			TileVisibilityChanged?.Invoke(tile, visible);
		}

		public virtual void UpdateRendererLists()
		{
			UpdateRendererLists(allTiles, allDoors);
		}

		protected void UpdateRendererLists(List<Tile> tiles, List<Door> doors)
		{
			foreach (var tile in tiles)
			{
				// Renderers
				if (!tileRenderers.TryGetValue(tile, out List<Renderer> renderers))
					tileRenderers[tile] = renderers = new List<Renderer>();
				else
					renderers.Clear();

				foreach (var renderer in tile.GetComponentsInChildren<Renderer>())
					if (IncludeDisabledComponents || (renderer.enabled && renderer.gameObject.activeInHierarchy))
						renderers.Add(renderer);

				// Lights
				if (!lightSources.TryGetValue(tile, out List<Light> lights))
					lightSources[tile] = lights = new List<Light>();
				else
					lights.Clear();

				foreach (var light in tile.GetComponentsInChildren<Light>())
					if (IncludeDisabledComponents || (light.enabled && light.gameObject.activeInHierarchy))
						lights.Add(light);

				// Reflection Probes
				if (!reflectionProbes.TryGetValue(tile, out List<ReflectionProbe> probes))
					reflectionProbes[tile] = probes = new List<ReflectionProbe>();
				else
					probes.Clear();

				foreach (var probe in tile.GetComponentsInChildren<ReflectionProbe>())
					if (IncludeDisabledComponents || (probe.enabled && probe.gameObject.activeInHierarchy))
						probes.Add(probe);
			}

			foreach (var door in doors)
			{
				List<Renderer> renderers = new List<Renderer>();
				doorRenderers[door] = renderers;

				foreach (var r in door.GetComponentsInChildren<Renderer>(true))
					if (IncludeDisabledComponents || (r.enabled && r.gameObject.activeInHierarchy))
						renderers.Add(r);
			}
		}

		protected Tile FindCurrentTile()
		{
			foreach (var tile in allTiles)
			{
				if(tile == null)
					continue;

				if (tile.Bounds.Contains(targetTransform.position))
					return tile;
			}

			return null;
		}

		protected Tile SearchForNewCurrentTile()
		{
			if (tilesToSearch == null)
				tilesToSearch = new Queue<Tile>();
			if (searchedTiles == null)
				searchedTiles = new List<Tile>();

			// Add all tiles adjacent to currentTile to the search queue
			foreach (var door in currentTile.UsedDoorways)
			{
				var adjacentTile = door.ConnectedDoorway.Tile;

				if (adjacentTile == null)
					continue;

				if (!tilesToSearch.Contains(adjacentTile))
					tilesToSearch.Enqueue(adjacentTile);
			}

			// Breadth-first search to find the tile which contains the player
			while (tilesToSearch.Count > 0)
			{
				var tile = tilesToSearch.Dequeue();

				if (tile.Bounds.Contains(targetTransform.position))
				{
					tilesToSearch.Clear();
					searchedTiles.Clear();
					return tile;
				}
				else
				{
					searchedTiles.Add(tile);

					foreach (var door in tile.UsedDoorways)
					{
						var adjacentTile = door.ConnectedDoorway.Tile;

						if(adjacentTile == null)
							continue;

						if (!tilesToSearch.Contains(adjacentTile) && !searchedTiles.Contains(adjacentTile))
							tilesToSearch.Enqueue(adjacentTile);
					}
				}
			}

			searchedTiles.Clear();
			return null;
		}
	}
}