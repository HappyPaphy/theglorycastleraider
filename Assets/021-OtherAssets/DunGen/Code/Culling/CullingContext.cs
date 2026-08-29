using DunGen.Culling.Shared;
using System.Collections.Generic;

namespace DunGen.Culling
{
	public class CullingContext
	{
		public CullingCamera Camera { get; private set; }
		public HashSet<Room> PreviousVisibleRooms = new HashSet<Room>();
		public HashSet<Room> VisibleRooms = new HashSet<Room>();
		public HashSet<Portal> ThresholdPortals = new HashSet<Portal>();
		public HashSet<Room> NewlyVisibleRooms = new HashSet<Room>();
		public HashSet<Room> NewlyHiddenRooms = new HashSet<Room>();


		public CullingContext(CullingCamera camera)
		{
			Camera = camera;
		}

		public void ResetCaches()
		{
			PreviousVisibleRooms.Clear();
			VisibleRooms.Clear();
			ThresholdPortals.Clear();
			NewlyVisibleRooms.Clear();
			NewlyHiddenRooms.Clear();
		}

		public bool IsRoomVisible(Room room)
		{
			return VisibleRooms.Contains(room);
		}
	}
}
