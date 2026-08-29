using DunGen.Culling.Shared;
using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace DunGen.Culling.Strategies
{
	[Serializable]
	[SubclassDisplay(DisplayName = "Portal Culling")]
	public class PortalCullingStrategy : ICullingStrategy
	{
		#region Nested Types

		public struct ConvexFrustum
		{
			public Plane[] Planes { get; set; }

			public bool Intersects(Bounds worldBounds) => GeometryUtility.TestPlanesAABB(Planes, worldBounds);

			/// <summary>
			/// Fills the provided plane array (must be length >= 6) with the camera frustum planes.
			/// Returns a ConvexFrustum referencing that array (no allocation).
			/// </summary>
			public static ConvexFrustum FromCamera(Camera camera, Plane[] target)
			{
				GeometryUtility.CalculateFrustumPlanes(camera, target);
				return new ConvexFrustum { Planes = target };
			}
		}

		private struct StackItem
		{
			public Room Room;
			public ConvexFrustum Frustum;
			public int Depth;

			public StackItem(Room room, ConvexFrustum frustum, int depth)
			{
				Room = room;
				Frustum = frustum;
				Depth = depth;
			}
		}

		#endregion

		public bool SupportsDebugDrawing => true;

		protected const int CameraPlaneCount = 6;
		protected const int MaxFrustumPlanes = 12; // 6 camera + up to 6 recent portal planes
		protected const int MaxStackDepth = 128; // Safety cap for internal stack buffer

		// Hysteresis to stabilize start-room selection and portal plane front/back tests near thresholds
		protected const float RoomBoundsHysteresis = 0.1f;
		protected const float PortalPlaneHysteresis = 0.05f;

		// Distance from portal plane within which both rooms are marked visible to avoid flicker
		protected const float PortalProximityDualRoomDistance = 0.2f;

		protected static readonly ProfilerMarker getVisibleRenderersPerfMarker = new ProfilerMarker("PortalCulling.GetVisibleRenderers");

		protected readonly List<Portal> drawPortals = new List<Portal>(64);
		protected readonly List<ConvexFrustum> doorFrustums = new List<ConvexFrustum>(64);
		protected ConvexFrustum cameraFrustum;

		protected Room lastStartRoom;

		// Cache buffers to avoid allocations
		private readonly Plane[] cameraPlanes = new Plane[CameraPlaneCount];
		private readonly Vector2[] screenQuad = new Vector2[4];
		private static readonly List<Plane> planeBuilder = new List<Plane>(32);
		private StackItem[] stackBuffer = new StackItem[32];
		private int stackCount;


		public virtual void OnEnable(CullingCamera camera) => CullingCamera.CullingGraph.GraphChanged += Reset;

		public virtual void OnDisable(CullingCamera camera) => CullingCamera.CullingGraph.GraphChanged -= Reset;

		protected virtual void Reset()
		{
			drawPortals.Clear();
			doorFrustums.Clear();
			planeBuilder.Clear();
			lastStartRoom = null;
		}

		private void StackPush(Room r, ConvexFrustum f, int d)
		{
			if (stackCount == stackBuffer.Length)
				Array.Resize(ref stackBuffer, stackBuffer.Length * 2);

			stackBuffer[stackCount++] = new StackItem(r, f, d);
		}

		private bool StackPop(out StackItem item)
		{
			if (stackCount == 0)
			{
				item = default;
				return false;
			}

			item = stackBuffer[--stackCount];

			return true;
		}

		public void GetVisibleRooms(Camera camera, IEnumerable<Room> rooms, ref HashSet<Room> visibleRooms)
		{
			getVisibleRenderersPerfMarker.Begin();

			visibleRooms.Clear();
			Room startRoom = null;
			var camPos = camera.transform.position + camera.transform.forward * camera.nearClipPlane;

			// Prefer previous start room if the camera is still within its slightly expanded bounds
			if (lastStartRoom != null)
			{
				var b = lastStartRoom.Bounds;
				b.Expand(RoomBoundsHysteresis);

				if (b.Contains(camPos))
					startRoom = lastStartRoom;
			}

			// Find the starting room based on camera position (with small hysteresis) if needed
			if (startRoom == null)
			{
				float bestScore = float.NegativeInfinity;
				Room best = null;

				foreach (var room in rooms)
				{
					var b = room.Bounds;
					b.Expand(RoomBoundsHysteresis);

					if (!b.Contains(camPos))
						continue;

					float score = -Vector3.SqrMagnitude(camPos - b.center);

					if (room == lastStartRoom)
						score += 1f;

					if (score > bestScore)
					{
						bestScore = score;
						best = room;
					}
				}

				startRoom = best;
			}

			// Camera is outside all rooms, return empty list
			if (startRoom == null)
			{
				getVisibleRenderersPerfMarker.End();
				return;
			}

			CollectVisibleRooms(camera, startRoom, ref visibleRooms);
			lastStartRoom = startRoom;
			getVisibleRenderersPerfMarker.End();
		}

		public void CollectVisibleRooms(Camera camera, Room startRoom, ref HashSet<Room> outRooms,
			float minScreenAreaPx = 8f, int maxDepth = 64)
		{
			outRooms.Clear();
			drawPortals.Clear();
			doorFrustums.Clear();

			cameraFrustum = ConvexFrustum.FromCamera(camera, cameraPlanes);

			stackCount = 0;
			StackPush(startRoom, cameraFrustum, 0);

			var cameraPos = camera.transform.position;

			while (StackPop(out var item))
			{
				var room = item.Room;
				var frustum = item.Frustum;
				int depth = item.Depth;

				if (room == null || outRooms.Contains(room))
					continue;

				if (!frustum.Intersects(room.Bounds))
					continue;

				outRooms.Add(room);

				if (depth >= maxDepth)
					continue;

				var portals = room.Portals;

				if (portals == null)
					continue;

				for (int pIndex = 0; pIndex < portals.Length; pIndex++)
				{
					var portal = portals[pIndex];

					if (portal == null || portal.To == null)
						continue;

					var door = portal.DoorComponent;

					if (door != null && !door.IsOpen)
						continue;

					var plane = portal.GetPortalPlane();

					// If close to the portal plane, mark adjacent room visible regardless of side to avoid flickering when crossing the threshold
					bool forceDual = Mathf.Abs(plane.GetDistanceToPoint(cameraPos)) <= Mathf.Max(PortalProximityDualRoomDistance, camera.nearClipPlane);

					if (forceDual)
					{
						if (!outRooms.Contains(portal.To))
							StackPush(portal.To, frustum, depth + 1);

						drawPortals.Add(portal);
						continue;
					}

					// Only see the front of the portal (with a small hysteresis to avoid flicker on the plane)
					if (plane.GetDistanceToPoint(cameraPos) > PortalPlaneHysteresis)
						continue;

					// Clip/visibility test: portal quad must intersect current frustum
					var quad = portal.GetWorldQuad();
					if (!PortalQuadIntersectsFrustum(frustum, quad))
						continue;

					// Quick screen-space area test to drop tiny slivers
					if (ProjectedAreaTooSmall(camera, quad, minScreenAreaPx, screenQuad))
						continue;

					// Build child frustum by adding portal edge planes + the portal plane
					if (!TryBuildChildFrustum(cameraPos, frustum, quad, out var child))
						continue;

					// Early reject: child frustum vs neighbour room bounds
					if (!child.Intersects(portal.To.Bounds))
						continue;

					StackPush(portal.To, child, depth + 1);
					drawPortals.Add(portal);
					doorFrustums.Add(child);
				}
			}
		}

		protected bool TryBuildChildFrustum(Vector3 camPos, ConvexFrustum parent, Vector3[] quadCW, out ConvexFrustum child)
		{
			planeBuilder.Clear();
			var parentPlanes = parent.Planes;

			for (int i = 0; i < parentPlanes.Length; i++)
				planeBuilder.Add(parentPlanes[i]);

			var pp = new Plane(quadCW[0], quadCW[1], quadCW[2]);
			if (pp.GetSide(camPos))
				pp = new Plane(-pp.normal, -pp.distance);

			var centre = (quadCW[0] + quadCW[1] + quadCW[2] + quadCW[3]) * 0.25f;
			var anchor = centre + pp.normal * 0.01f;

			for (int i = 0; i < 4; i++)
			{
				var a = quadCW[i];
				var b = quadCW[(i + 1) & 3];

				// Degenerate if edge is almost colinear with the vectors from camera
				if (NearlyColinear(camPos, a, b))
				{
					child = default;
					return false;
				}

				var p = new Plane();
				p.Set3Points(camPos, b, a);

				if (!p.GetSide(anchor))
					p = new Plane(-p.normal, -p.distance);

				if (!IsValidFast(ref p))
				{
					child = default;
					return false;
				}

				planeBuilder.Add(p);
			}

			if (!pp.GetSide(anchor))
				pp = new Plane(-pp.normal, -pp.distance);

			if (!IsValidFast(ref pp))
			{
				child = default;
				return false;
			}

			planeBuilder.Add(pp);

			// Prune plane count to avoid explosive growth over long chains.
			PrunePlanes(planeBuilder);

			// Allocate new plane array sized exactly
			var arr = new Plane[planeBuilder.Count];

			for (int i = 0; i < planeBuilder.Count; i++)
				arr[i] = planeBuilder[i];

			child = new ConvexFrustum { Planes = arr };

			// Ensure all planes are valid
			for (int i = 0; i < arr.Length; i++)
				if (!IsValidFast(ref arr[i]))
				{
					child = default;
					return false;
				}

			return true;
		}

		private static void PrunePlanes(List<Plane> planes)
		{
			if (planes == null)
				return;

			if (planes.Count <= MaxFrustumPlanes)
				return;

			// Keep the original camera frustum planes (first 6) and the most recent ones
			int keepCamera = Mathf.Min(CameraPlaneCount, planes.Count);
			int remainingCapacity = MaxFrustumPlanes - keepCamera;
			int startRecent = Mathf.Max(keepCamera, planes.Count - remainingCapacity);
			int originalCount = planes.Count;

			// Copy needed planes into temp segment (reuse list by removing others)
			// Build list of kept planes
			var kept = new Plane[MaxFrustumPlanes]; int k = 0;

			for (int i = 0; i < keepCamera; i++)
				kept[k++] = planes[i];

			for (int i = startRecent; i < originalCount && k < MaxFrustumPlanes; i++)
				kept[k++] = planes[i];

			planes.Clear();

			for (int i = 0; i < k; i++)
				planes.Add(kept[i]);
		}

		private static bool PortalQuadIntersectsFrustum(ConvexFrustum frustum, Vector3[] quad)
		{
			var planes = frustum.Planes;
			for (int i = 0; i < planes.Length; i++)
			{
				int outside = 0;
				var p = planes[i];

				for (int v = 0; v < 4; v++)
				{
					if (p.GetDistanceToPoint(quad[v]) < 0f)
						outside++;
				}
				if (outside == 4)
					return false;
			}

			return true;
		}

		protected static bool ProjectedAreaTooSmall(Camera cam, Vector3[] quad, float minAreaPixels, Vector2[] screen)
		{
			bool anyBehindNear = false;

			for (int i = 0; i < 4; i++)
			{
				var p = cam.WorldToScreenPoint(quad[i]);

				if (p.z <= cam.nearClipPlane + 0.0001f)
					anyBehindNear = true;

				screen[i].x = p.x; screen[i].y = p.y;
			}

			if (anyBehindNear)
				return false;

			float area = 0f;
			for (int i = 0; i < 4; i++)
			{
				var a = screen[i];
				var b = screen[(i + 1) & 3];
				area += (a.x * b.y - b.x * a.y);
			}

			area = area < 0 ? -area * 0.5f : area * 0.5f;
			return area < minAreaPixels;
		}

		public void DebugDraw()
		{
			foreach (var portal in drawPortals)
			{
				var quadVerts = portal.GetWorldQuad();

				Gizmos.color = Color.green;
				Gizmos.DrawLine(quadVerts[0], quadVerts[1]);
				Gizmos.DrawLine(quadVerts[1], quadVerts[2]);
				Gizmos.DrawLine(quadVerts[2], quadVerts[3]);
				Gizmos.DrawLine(quadVerts[3], quadVerts[0]);
			}

			FrustumDebug.Draw(cameraFrustum.Planes, Color.cyan, duration: 0f, faceColour: new Color(0f, 1f, 1f, 0.08f));

			int frustumCount = Mathf.Min(doorFrustums.Count, 8);
			for (int i = 0; i < frustumCount; i++)
				FrustumDebug.Draw(doorFrustums[i].Planes, Color.magenta, duration: 0f, faceColour: new Color(1f, 0f, 1f, 0.08f));
		}

		protected static bool NearlyColinear(Vector3 o, Vector3 a, Vector3 b)
		{
			var va = a - o;
			var vb = b - o;
			var c = Vector3.Cross(va, vb);

			return c.sqrMagnitude < 1e-10f;
		}

		protected static bool IsValidFast(ref Plane p)
		{
			var n = p.normal;
			float sq = n.x * n.x + n.y * n.y + n.z * n.z;

			return !float.IsNaN(n.x) && !float.IsInfinity(n.x) &&
				   !float.IsNaN(n.y) && !float.IsInfinity(n.y) &&
				   !float.IsNaN(n.z) && !float.IsInfinity(n.z) &&
				   !float.IsNaN(p.distance) && !float.IsInfinity(p.distance) &&
				   sq > 1e-10f;
		}

		protected static bool IsValid(Plane p) => IsValidFast(ref p);
		protected static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
	}
}