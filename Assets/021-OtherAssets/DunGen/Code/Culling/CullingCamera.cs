using DunGen.Culling.Shared;
using DunGen.Culling.Strategies;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if RENDER_PIPELINE
using UnityEngine.Rendering;
#endif

namespace DunGen.Culling
{
	public enum VisibilityOverrideMode
	{
		Auto,
		ForceVisible,
		ForceHidden,
	}

	[Flags]
	public enum CullComponents
	{
		None = 0,

		Renderers = 1 << 0,
		Lights = 1 << 1,
		ReflectionProbes = 1 << 2,

		Everything = Renderers | Lights | ReflectionProbes,
	}

	[DisallowMultipleComponent]
	[AddComponentMenu("DunGen/Culling/Culling Camera")]
	[RequireComponent(typeof(Camera))]
	[HelpURL("https://dungen-docs.aegongames.com/optimization/culling/")]
	public class CullingCamera : MonoBehaviour
	{
		#region Statics

		public static CullingGraph CullingGraph { get; private set; } = new CullingGraph();
		public static IReadOnlyCollection<CullingCamera> ActiveCameras => activeCameras;

		protected static HashSet<CullingCamera> activeCameras = new HashSet<CullingCamera>();

		protected static void RegisterCamera(CullingCamera camera)
		{
			activeCameras.Add(camera);

			if (activeCameras.Count == 1)
			{
				DungeonGenerator.OnAnyDungeonGenerationComplete += OnAnyDungeonGenerationComplete;

				// Find any existing dungeons in the scene and add them to the culling graph
				foreach (var runtimeDungeon in UnityUtil.FindObjectsByType<RuntimeDungeon>())
				{
					if (runtimeDungeon.Generator.CompositeDungeon != null)
						CullingGraph.AddCompositeDungeon(runtimeDungeon.Generator.CompositeDungeon);
				}
			}

			if (activeCameras.Count > 1)
			{
				if (activeCameras.Any(c => !c.PerCameraCulling))
					Debug.LogWarning("Multiple CullingCamera components are active in the scene, and at least one is not using the per-camera culling. If you need multiple culling cameras active at once, they should all have per-camera culling enabled.", camera);
			}
		}

		protected static void UnregisterCamera(CullingCamera camera)
		{
			activeCameras.Remove(camera);

			if (activeCameras.Count == 0)
				DungeonGenerator.OnAnyDungeonGenerationComplete -= OnAnyDungeonGenerationComplete;
		}

		protected static void OnAnyDungeonGenerationComplete(DungeonGenerator generator, Dungeon dungeon)
		{
			if (generator.CompositeDungeon != null)
				CullingGraph.AddCompositeDungeon(generator.CompositeDungeon);
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void InitialiseStatics()
		{
			CullingGraph = new CullingGraph();
			activeCameras.Clear();
		}

		#endregion

		[SubclassSelector(allowNone: false)]
		[SerializeReference]
		public ICullingStrategy Strategy = new PortalCullingStrategy();
		public bool PerCameraCulling = false;
		public CullComponents ComponentsToCull = CullComponents.Everything;
		public bool DebugDraw = false;
		public Camera CameraComponent => cameraComponent;
		public CullingContext Context { get; private set; }

		protected Camera cameraComponent;
		protected bool hookedRenderPipeline;
		protected readonly Dictionary<Component, VisibilityOverrideMode> visibilityOverrides = new Dictionary<Component, VisibilityOverrideMode>();


		protected virtual void OnEnable()
		{
			cameraComponent = GetComponent<Camera>();

			if (Context == null)
				Context = new CullingContext(this);

			RegisterCamera(this);
			CullingGraph.RoomAdded += OnRoomAdded;
			CullingGraph.RoomChanged += OnRoomChanged;
			CullingGraph.GraphChanged += OnCullingGraphChanged;

#if RENDER_PIPELINE
			if (RenderPipelineManager.currentPipeline != null)
			{
				RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
				RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
				hookedRenderPipeline = true;
			}
#endif

			if (!hookedRenderPipeline)
			{
				Camera.onPreCull += OnCameraPreCull;
				Camera.onPostRender += OnCameraPostRender;
			}

			// Hide all rooms initially
			foreach (var room in CullingGraph.Rooms)
				ApplyRoomVisibility(room, false);

			ApplyAllPortalRendererVisibility(false);
			Strategy?.OnEnable(this);
		}

		protected virtual void OnDisable()
		{
			UnregisterCamera(this);

			CullingGraph.RoomAdded -= OnRoomAdded;
			CullingGraph.RoomChanged -= OnRoomChanged;
			CullingGraph.GraphChanged -= OnCullingGraphChanged;
			Context.ResetCaches();

#if RENDER_PIPELINE
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
			RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
#endif

			Camera.onPreCull -= OnCameraPreCull;
			Camera.onPostRender -= OnCameraPostRender;
			hookedRenderPipeline = false;

			if (!PerCameraCulling)
			{
				foreach (var room in CullingGraph.Rooms)
					ApplyRoomVisibility(room, true);
			}

			ApplyAllPortalRendererVisibility(true);
			Strategy?.OnDisable(this);
		}

		public void SetVisibilityOverride(Component component, VisibilityOverrideMode mode)
		{
			if (component == null)
				return;

			if (mode == VisibilityOverrideMode.Auto)
				visibilityOverrides.Remove(component);
			else
				visibilityOverrides[component] = mode;

			// Refresh the visibility of the component immediately
			if (CullingGraph.TryGetRoomByComponent(component, out var room))
			{
				bool isVisible = Context.IsRoomVisible(room);

				if (component is Renderer renderer)
				{
					if (ShouldCullComponents(CullComponents.Renderers))
						ApplyRendererVisibility(renderer, isVisible);
				}
				else if (component is Light light)
				{
					if(ShouldCullComponents(CullComponents.Lights))
						ApplyLightVisibility(light, isVisible);
				}
				else if (component is ReflectionProbe probe)
				{
					if(ShouldCullComponents(CullComponents.ReflectionProbes))
						ApplyProbeVisibility(probe, isVisible);
				}
				else
					Debug.LogWarning($"Component {component} is not a supported type for visibility overrides.", component);
			}
		}

		protected virtual void OnCullingGraphChanged()
		{
			foreach (var room in CullingGraph.Rooms)
				ApplyRoomVisibility(room, Context.IsRoomVisible(room));
		}

		protected bool ShouldCullComponents(CullComponents flag) => (ComponentsToCull & flag) == flag;

		protected void ApplyRoomVisibility(Room room, bool visible)
		{
			if (room == null)
				return;

			if (ShouldCullComponents(CullComponents.Renderers))
			{
				foreach (var renderer in room.Renderers.All)
					ApplyRendererVisibility(renderer, visible);
			}

			if (ShouldCullComponents(CullComponents.Lights))
			{
				foreach (var light in room.Lights.All)
					ApplyLightVisibility(light, visible);
			}

			if (ShouldCullComponents(CullComponents.ReflectionProbes))
			{
				foreach (var probe in room.ReflectionProbes.All)
					ApplyProbeVisibility(probe, visible);
			}
		}

		protected void ApplyPortalRendererVisibility(HashSet<Room> visibleRooms)
		{
			if (!ShouldCullComponents(CullComponents.Renderers))
				return;

			foreach (var room in CullingGraph.Rooms)
			{
				var portals = room.Portals;
				if (portals == null)
					continue;

				for (int i = 0; i < portals.Length; i++)
				{
					var portal = portals[i];
					if (portal == null || portal.Renderers == null)
						continue;

					bool visible = visibleRooms.Contains(portal.From) || visibleRooms.Contains(portal.To);

					foreach (var renderer in portal.Renderers)
						ApplyRendererVisibility(renderer, visible);
				}
			}
		}

		protected void ApplyAllPortalRendererVisibility(bool visible)
		{
			if (!ShouldCullComponents(CullComponents.Renderers))
				return;

			foreach (var room in CullingGraph.Rooms)
			{
				var portals = room.Portals;
				if (portals == null)
					continue;

				for (int i = 0; i < portals.Length; i++)
				{
					var portal = portals[i];
					if (portal == null || portal.Renderers == null)
						continue;

					foreach (var renderer in portal.Renderers)
						ApplyRendererVisibility(renderer, visible);
				}
			}
		}

		protected void ApplyComponentVisibilityOverride(Component component, ref bool visible)
		{
			if (!visibilityOverrides.TryGetValue(component, out var mode))
				return;

			switch (mode)
			{
				case VisibilityOverrideMode.ForceVisible:
					visible = true;
					break;
				case VisibilityOverrideMode.ForceHidden:
					visible = false;
					break;
			}
		}

		protected void ApplyRendererVisibility(Renderer renderer, bool visible)
		{
			if (renderer == null)
				return;

			ApplyComponentVisibilityOverride(renderer, ref visible);
			renderer.forceRenderingOff = !visible;
		}

		protected void ApplyLightVisibility(Light light, bool visible)
		{
			if (light == null)
				return;

			ApplyComponentVisibilityOverride(light, ref visible);
			light.enabled = visible;
		}

		protected void ApplyProbeVisibility(ReflectionProbe probe, bool visible)
		{
			if (probe == null)
				return;

			ApplyComponentVisibilityOverride(probe, ref visible);
			probe.enabled = visible;
		}

#if RENDER_PIPELINE
		protected void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
		{
			EnableCullingForCamera(camera);
		}

		protected void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
		{
			DisableCullingForCamera(camera);
		}
#endif

		protected virtual void OnCameraPreCull(Camera camera) => EnableCullingForCamera(camera);

		protected virtual void OnCameraPostRender(Camera camera) => DisableCullingForCamera(camera);

		protected virtual void EnableCullingForCamera(Camera camera)
		{
			if (!PerCameraCulling || camera != cameraComponent)
				return;

			Context.VisibleRooms.Clear();
			Strategy?.GetVisibleRooms(cameraComponent, CullingGraph.Rooms, ref Context.VisibleRooms);

			foreach (var room in Context.VisibleRooms)
				ApplyRoomVisibility(room, true);

			ApplyPortalRendererVisibility(Context.VisibleRooms);
		}

		protected virtual void DisableCullingForCamera(Camera camera)
		{
			if (!PerCameraCulling || camera != cameraComponent)
				return;

			foreach (var room in Context.VisibleRooms)
				ApplyRoomVisibility(room, false);

			ApplyAllPortalRendererVisibility(false);
		}

		protected virtual void LateUpdate()
		{
			if (PerCameraCulling)
				return;

			if (Strategy == null && Context.VisibleRooms.Count == CullingGraph.Rooms.Count)
				return; // No strategy set and all rooms are already visible

			// Swap the previous visible rooms with the current ones
			(Context.VisibleRooms, Context.PreviousVisibleRooms) = (Context.PreviousVisibleRooms, Context.VisibleRooms);
			Context.VisibleRooms.Clear();

			// Calculate the visible set of rooms
			if (Strategy != null)
				Strategy.GetVisibleRooms(cameraComponent, CullingGraph.Rooms, ref Context.VisibleRooms);
			else
			{
				// No strategy, so just make everything visible
				foreach (var room in CullingGraph.Rooms)
					Context.VisibleRooms.Add(room);
			}

			Context.NewlyVisibleRooms.Clear();
			Context.NewlyHiddenRooms.Clear();

			// Find newly visible rooms
			foreach (var room in Context.VisibleRooms)
			{
				if (!Context.PreviousVisibleRooms.Contains(room))
				{
					Context.NewlyVisibleRooms.Add(room);
					ApplyRoomVisibility(room, true);
				}
			}

			// Find newly hidden rooms
			foreach (var room in Context.PreviousVisibleRooms)
			{
				if (!Context.IsRoomVisible(room))
				{
					Context.NewlyHiddenRooms.Add(room);
					ApplyRoomVisibility(room, false);
				}
			}

			ApplyPortalRendererVisibility(Context.VisibleRooms);
		}

		// Gets the portals that are on the threshold of visibility (between a visible and hidden room)
		protected void GetThresholdPortals(HashSet<Room> visibleRooms, ref HashSet<Portal> thresholdPortals)
		{
			thresholdPortals.Clear();

			foreach (var room in visibleRooms)
			{
				foreach (var portal in room.Portals)
				{
					if (visibleRooms.Contains(portal.To))
						continue; // Adjacent room is already visible

					thresholdPortals.Add(portal);
				}
			}
		}

		private void OnRoomAdded(Room newRoom)
		{
			if (enabled && !PerCameraCulling)
				ApplyRoomVisibility(newRoom, false);
		}

		private void OnRoomChanged(Room room)
		{
			if (enabled && !PerCameraCulling)
				ApplyRoomVisibility(room, Context.IsRoomVisible(room));
		}

		protected virtual void OnDrawGizmos()
		{
			if (enabled && DebugDraw)
				Strategy?.DebugDraw();
		}
	}
}
