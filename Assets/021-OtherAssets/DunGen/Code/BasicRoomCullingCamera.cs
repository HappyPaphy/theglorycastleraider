#define RENDER_PIPELINE

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if RENDER_PIPELINE
using UnityEngine.Rendering;
#endif

namespace DunGen
{
	[AddComponentMenu("")] // Hide in menu
	[Obsolete("BasicRoomCullingCamera has been deprecated. Please use CullingCamera with the 'Adjacency Culling' strategy instead. This component will be removed in future versions.")]
	public class BasicRoomCullingCamera : MonoBehaviour
	{
		#region Helpers

		protected struct RendererData
		{
			public Renderer Renderer;
			public bool Enabled;


			public RendererData(Renderer renderer, bool enabled)
			{
				Renderer = renderer;
				Enabled = enabled;
			}
		}

		protected struct LightData
		{
			public Light Light;
			public bool Enabled;


			public LightData(Light light, bool enabled)
			{
				Light = light;
				Enabled = enabled;
			}
		}

		protected struct ReflectionProbeData
		{
			public ReflectionProbe Probe;
			public bool Enabled;


			public ReflectionProbeData(ReflectionProbe probe, bool enabled)
			{
				Probe = probe;
				Enabled = enabled;
			}
		}

		#endregion

		/// <summary>
		/// Determines how deep a tile must be before it's culled.
		/// 0: Only the current tile is visible
		/// 1: Only the current tile and all of its immediate neighbours are visible
		/// 2: Same as 1 but all of their neighbours are also visible
		/// ... etc
		/// </summary>
		public int AdjacentTileDepth = 1;
		/// <summary>
		/// If true, any tiles behind a closed door will be culled even if they're in range
		/// </summary>
		public bool CullBehindClosedDoors = true;
		/// <summary>
		/// The target object to use (defaults to this object). For third-person games,
		/// this would be the player character
		/// </summary>
		public Transform TargetOverride = null;
		/// <summary>
		/// Is culling enabled in the scene view of the editor?
		/// </summary>
		public bool CullInEditor = false;
		/// <summary>
		/// Should we cull light components?
		/// </summary>
		public bool CullLights = true;


		public bool IsReady { get; protected set; }

		protected bool isCulling;
		protected bool isDirty;
		protected DungeonGenerator generator;
		protected Tile currentTile;
		protected List<Dungeon> dungeons = new List<Dungeon>();
		protected List<Tile> allTiles = new List<Tile>();
		protected List<Door> allDoors = new List<Door>();
		protected List<Tile> visibleTiles = new List<Tile>();
		protected Dictionary<Tile, List<RendererData>> rendererVisibilities = new Dictionary<Tile, List<RendererData>>();
		protected Dictionary<Tile, List<LightData>> lightVisibilities = new Dictionary<Tile, List<LightData>>();
		protected Dictionary<Tile, List<ReflectionProbeData>> reflectionProbeVisibilities = new Dictionary<Tile, List<ReflectionProbeData>>();
		protected Dictionary<Door, List<RendererData>> doorRendererVisibilities = new Dictionary<Door, List<RendererData>>();


		protected virtual void Awake()
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

		protected virtual void OnDestroy()
		{
			if (generator != null)
				generator.OnGenerationComplete -= OnDungeonGenerationComplete;
		}

		protected virtual void OnEnable()
		{
#if RENDER_PIPELINE
			if (RenderPipelineManager.currentPipeline != null)
			{
				RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
				RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

				return;
			}
#endif

			Camera.onPreCull += EnableCulling;
			Camera.onPostRender += DisableCulling;
		}

		protected virtual void OnDisable()
		{
#if RENDER_PIPELINE
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
			RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
#endif

			Camera.onPreCull -= EnableCulling;
			Camera.onPostRender -= DisableCulling;
		}

#if RENDER_PIPELINE
		private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
		{
			EnableCulling(camera);
		}

		private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
		{
			DisableCulling(camera);
		}
#endif

		private void OnDungeonGenerationComplete(DungeonGenerator generator)
		{
			bool isAttachedDungeon = generator.AttachmentSettings != null &&
									 generator.AttachmentSettings.TileProxy != null;

			// Remove the last dungeon if we're not attaching to it
			if (!isAttachedDungeon && dungeons.Count > 0)
				RemoveDungeon(dungeons[dungeons.Count - 1]);

			AddDungeon(generator.CurrentDungeon);
		}

		protected virtual void EnableCulling(Camera camera)
		{
			SetCullingEnabled(camera, true);
		}

		protected virtual void DisableCulling(Camera camera)
		{
			SetCullingEnabled(camera, false);
		}

		protected void SetCullingEnabled(Camera camera, bool enabled)
		{
			if (!IsReady || camera == null)
				return;

			bool cullThisCameras = camera.gameObject == gameObject;

#if UNITY_EDITOR
			if (CullInEditor)
			{
				var sceneCameras = UnityEditor.SceneView.GetAllSceneCameras();

				if (sceneCameras != null && sceneCameras.Contains(camera))
					cullThisCameras = true;
			}
#endif

			if (cullThisCameras)
				SetIsCulling(enabled);
		}

		protected virtual void LateUpdate()
		{
			if (!IsReady)
				return;

			Transform target = (TargetOverride != null) ? TargetOverride : transform;
			bool hasPositionChanged = currentTile == null || !currentTile.Bounds.Contains(target.position);

			if (hasPositionChanged)
			{
				// Update current tile
				foreach (var tile in allTiles)
				{
					if (tile == null)
						continue;

					if (tile.Bounds.Contains(target.position))
					{
						currentTile = tile;
						break;
					}
				}

				isDirty = true;
			}

			if (isDirty)
			{
				UpdateCulling();

				// Update the list of renderers for tiles about to be culled
				foreach (var tile in allTiles)
					if (tile != null && !visibleTiles.Contains(tile))
						UpdateRendererList(tile);
			}
		}

		protected void UpdateRendererList(Tile tile)
		{
			// Renderers
			if (!rendererVisibilities.TryGetValue(tile, out List<RendererData> renderers))
				rendererVisibilities[tile] = renderers = new List<RendererData>();
			else
				renderers.Clear();

			foreach (var renderer in tile.GetComponentsInChildren<Renderer>())
				renderers.Add(new RendererData(renderer, renderer.enabled));


			// Lights
			if (CullLights)
			{
				if (!lightVisibilities.TryGetValue(tile, out List<LightData> lights))
					lightVisibilities[tile] = lights = new List<LightData>();
				else
					lights.Clear();

				foreach (var light in tile.GetComponentsInChildren<Light>())
					lights.Add(new LightData(light, light.enabled));
			}

			// Reflection Probes
			if (!reflectionProbeVisibilities.TryGetValue(tile, out List<ReflectionProbeData> probes))
				reflectionProbeVisibilities[tile] = probes = new List<ReflectionProbeData>();
			else
				probes.Clear();

			foreach (var probe in tile.GetComponentsInChildren<ReflectionProbe>())
				probes.Add(new ReflectionProbeData(probe, probe.enabled));
		}

		protected void SetIsCulling(bool isCulling)
		{
			this.isCulling = isCulling;

			for (int i = 0; i < allTiles.Count; i++)
			{
				var tile = allTiles[i];

				if (visibleTiles.Contains(tile))
					continue;

				// Renderers
				if (rendererVisibilities.TryGetValue(tile, out List<RendererData> rendererData))
				{
					foreach (var r in rendererData) // Removed null check on r.Renderer because it was expensive. Shouldn't be necessary
						if (r.Renderer != null)
							r.Renderer.enabled = isCulling ? false : r.Enabled;
				}

				// Lights
				if (CullLights)
				{
					if (lightVisibilities.TryGetValue(tile, out List<LightData> lightData))
					{
						foreach (var l in lightData)
							if (l.Light != null)
								l.Light.enabled = isCulling ? false : l.Enabled;
					}
				}

				// Reflection Probes
				if (reflectionProbeVisibilities.TryGetValue(tile, out List<ReflectionProbeData> probeData))
				{
					foreach (var p in probeData)
						if (p.Probe != null)
							p.Probe.enabled = isCulling ? false : p.Enabled;
				}
			}

			foreach (var door in allDoors)
			{
				bool isVisible = visibleTiles.Contains(door.DoorwayA.Tile) || visibleTiles.Contains(door.DoorwayB.Tile);

				if (doorRendererVisibilities.TryGetValue(door, out List<RendererData> rendererData))
				{
					foreach (var r in rendererData)
						if (r.Renderer != null)
							r.Renderer.enabled = isCulling ? isVisible : r.Enabled;
				}
			}
		}

		protected void UpdateCulling()
		{
			isDirty = false;
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

		public void SetDungeon(Dungeon newDungeon)
		{
			if (newDungeon == null)
				return;

			ClearAllDungeons();
			AddDungeon(newDungeon);
		}

		public void AddDungeon(Dungeon dungeon)
		{
			if (dungeon == null || dungeons.Contains(dungeon))
				return;

			var dungeonDoors = GetAllDoorsInDungeon(dungeon);

			dungeons.Add(dungeon);
			allTiles.AddRange(dungeon.AllTiles);
			allDoors.AddRange(dungeonDoors);

			foreach (var door in dungeonDoors)
			{
				var renderers = new List<RendererData>();
				doorRendererVisibilities[door] = renderers;

				foreach (var renderer in door.GetComponentsInChildren<Renderer>(true))
					renderers.Add(new RendererData(renderer, renderer.enabled));

				door.OnDoorStateChanged += OnDoorStateChanged;
			}

			IsReady = true;
			isDirty = true;
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

		public void RemoveDungeon(Dungeon dungeon)
		{
			if (dungeon == null || !dungeons.Contains(dungeon))
				return;

			dungeons.Remove(dungeon);

			// Clear any leftover data from destroyed dungeons
			allTiles.RemoveAll(x => !x);
			visibleTiles.RemoveAll(x => !x);
			allDoors.RemoveAll(x => !x);
			RemoveNullKeys(ref rendererVisibilities);
			RemoveNullKeys(ref lightVisibilities);
			RemoveNullKeys(ref reflectionProbeVisibilities);


			// Remove tiles
			foreach (var tile in dungeon.AllTiles)
			{
				if (tile == null)
					continue;

				if (allTiles.Contains(tile))
					allTiles.Remove(tile);

				if (visibleTiles.Contains(tile))
					visibleTiles.Remove(tile);

				if (rendererVisibilities.ContainsKey(tile))
					rendererVisibilities.Remove(tile);

				if (lightVisibilities.ContainsKey(tile))
					lightVisibilities.Remove(tile);

				if (reflectionProbeVisibilities.ContainsKey(tile))
					reflectionProbeVisibilities.Remove(tile);
			}

			// Remove doors
			foreach (var doorObj in dungeon.Doors)
			{
				if (doorObj == null)
					continue;

				if (!doorObj.TryGetComponent<Door>(out var door))
					continue;

				if (allDoors.Contains(door))
					allDoors.Remove(door);

				if (doorRendererVisibilities.ContainsKey(door))
					doorRendererVisibilities.Remove(door);

				door.OnDoorStateChanged -= OnDoorStateChanged;
			}
		}

		public void ClearAllDungeons()
		{
			IsReady = false;

			foreach (var door in allDoors)
			{
				if (door != null)
					door.OnDoorStateChanged -= OnDoorStateChanged;
			}

			dungeons.Clear();
			allTiles.Clear();
			visibleTiles.Clear();
			allDoors.Clear();

			rendererVisibilities.Clear();
			lightVisibilities.Clear();
			reflectionProbeVisibilities.Clear();
			doorRendererVisibilities.Clear();
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
			isDirty = true;
		}
	}
}