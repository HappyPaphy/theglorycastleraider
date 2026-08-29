using DunGen.Culling.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen.Culling.Strategies
{
	[Serializable]
	[SubclassDisplay(DisplayName = "Adjacency Culling")]
	public class AdjacencyCullingStrategy : ICullingStrategy
	{
		public bool SupportsDebugDrawing => false;

		/// <summary>
		/// The character whose position is used to determine which rooms are visible
		/// </summary>
		public DungenCharacter Character;

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

		protected DungenCharacter previousCharacter;
		protected Room currentRoom;
		protected readonly HashSet<Room> cachedVisibleRooms = new HashSet<Room>();
		protected readonly List<Door> trackedDoors = new List<Door>();
		protected bool isDirty = true;


		public virtual void OnEnable(CullingCamera camera)
		{
			isDirty = true;
			previousCharacter = null;

			// If no character has been specified, try to find one in the camera's parents
			if (Character == null)
				Character = camera.GetComponentInParent<DungenCharacter>();

			// If we still don't have a character, log an error
			if (Character == null)
				Debug.LogError($"[Culling Camera] Adjacency culling strategy must have a character assigned");

			CullingCamera.CullingGraph.GraphChanged += OnCullingGraphChanged;
		}

		public virtual void OnDisable(CullingCamera camera)
		{
			foreach(var door in trackedDoors)
				door.OnDoorStateChanged -= OnDoorStateChanged;

			trackedDoors.Clear();
			cachedVisibleRooms.Clear();

			CullingCamera.CullingGraph.GraphChanged -= OnCullingGraphChanged;
		}

		private void OnCullingGraphChanged() => isDirty = true;

		public void GetVisibleRooms(Camera camera, IEnumerable<Room> rooms, ref HashSet<Room> visibleRooms)
		{
			if (Character != previousCharacter)
				UpdateCharacter();

			if (isDirty)
			{
				UpdateDungeonRepresentation();
				CollectVisibleRooms();
			}

			foreach(var room in cachedVisibleRooms)
				visibleRooms.Add(room);
		}

		protected void UpdateCharacter()
		{
			if(previousCharacter != null)
				previousCharacter.OnTileChanged -= OnCurrentTileChanged;

			previousCharacter = Character;

			currentRoom = CullingCamera.CullingGraph.Rooms
				.Where(r => r.Tile == Character.CurrentTile)
				.FirstOrDefault();

			CollectVisibleRooms();

			if (Character != null)
				Character.OnTileChanged += OnCurrentTileChanged;
		}

		protected virtual void UpdateDungeonRepresentation()
		{
			foreach(var door in trackedDoors)
				door.OnDoorStateChanged -= OnDoorStateChanged;

			trackedDoors.Clear();
			cachedVisibleRooms.Clear();

			var allDoors = CullingCamera.CullingGraph.Rooms.SelectMany(r => r.Portals)
				.Where(p => p.DoorComponent != null)
				.Select(p => p.DoorComponent)
				.Distinct();

			foreach(var door in allDoors)
			{
				door.OnDoorStateChanged += OnDoorStateChanged;
				trackedDoors.Add(door);
			}

			isDirty = false;
		}

		protected virtual void OnDoorStateChanged(Door door, bool isOpen) => CollectVisibleRooms();

		protected virtual void CollectVisibleRooms()
		{
			cachedVisibleRooms.Clear();

			if (Character == null || currentRoom == null)
				return;

			var openSet = new Queue<(Room room, int depth)>();
			var closedSet = new HashSet<Room>();
			openSet.Enqueue((currentRoom, 0));

			while (openSet.Count > 0)
			{
				(var room, int depth) = openSet.Dequeue();
				closedSet.Add(room);

				if (depth > AdjacentTileDepth)
					continue;

				cachedVisibleRooms.Add(room);

				foreach (var portal in room.Portals)
				{
					var neighbour = portal.To;

					if(closedSet.Contains(neighbour))
						continue;

					if (CullBehindClosedDoors && portal.DoorComponent != null && !portal.DoorComponent.IsOpen)
						continue;

					openSet.Enqueue((neighbour, depth + 1));
				}
			}
		}

		protected void OnCurrentTileChanged(DungenCharacter character, Tile previousTile, Tile newTile)
		{
			currentRoom = CullingCamera.CullingGraph.Rooms
				.Where(r => r.Tile == newTile)
				.FirstOrDefault();

			CollectVisibleRooms();
		}

		public void DebugDraw() { }
	}
}
