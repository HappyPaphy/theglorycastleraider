using DunGen.Culling.Shared;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Culling.Strategies
{
	[Serializable]
	[SubclassDisplay(DisplayName = "Frustum Culling")]
	public class FrustumCullingStrategy : ICullingStrategy
	{
		public bool SupportsDebugDrawing => true;

		private bool hasValidFrustumPlanes;
		private readonly Plane[] frustumPlanes = new Plane[6];


		public void OnEnable(CullingCamera camera) { }

		public void OnDisable(CullingCamera camera) { }

		public void GetVisibleRooms(Camera camera, IEnumerable<Room> rooms, ref HashSet<Room> visibleRooms)
		{
			visibleRooms.Clear();
			hasValidFrustumPlanes = false;

			if (camera == null || rooms == null)
				return;

			GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
			hasValidFrustumPlanes = true;

			foreach (var room in rooms)
			{
				if (room == null)
					continue;

				// Test if the room's world-space bounds intersect the camera frustum
				if (GeometryUtility.TestPlanesAABB(frustumPlanes, room.Bounds))
					visibleRooms.Add(room);
			}
		}

		public void DebugDraw()
		{
			if(hasValidFrustumPlanes)
				FrustumDebug.Draw(frustumPlanes, Color.cyan, duration: 0f, faceColour: new Color(0f, 1f, 1f, 0.08f));
		}
	}
}
