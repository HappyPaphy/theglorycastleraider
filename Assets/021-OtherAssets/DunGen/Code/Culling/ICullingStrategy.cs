using DunGen.Culling.Shared;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Culling
{
	public interface ICullingStrategy
	{
		public bool SupportsDebugDrawing { get; }

		public void OnEnable(CullingCamera camera);
		public void OnDisable(CullingCamera camera);
		public void GetVisibleRooms(Camera camera, IEnumerable<Room> rooms, ref HashSet<Room> visibleRooms);
		public void DebugDraw();
	}
}
