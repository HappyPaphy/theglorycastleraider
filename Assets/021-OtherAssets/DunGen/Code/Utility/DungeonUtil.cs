using System.Collections.Generic;
using UnityEngine;

namespace DunGen
{
	public static class DungeonUtil
	{
		/// <summary>
		/// Adds a Door component to the selected doorPrefab if one doesn't already exist
		/// </summary>
		/// <param name="dungeon">The dungeon that this door belongs to</param>
		/// <param name="doorPrefab">The door prefab on which to apply the component</param>
		/// <param name="doorway">The doorway that this door belongs to</param>
		/// <returns>The door component that was configured (or added if none was present)</returns>
		public static Door AddAndSetupDoorComponent(Dungeon dungeon, GameObject doorPrefab, Doorway doorway)
		{
			if (!doorPrefab.TryGetComponent<Door>(out var door))
				door = doorPrefab.AddComponent<Door>();

			door.Dungeon = dungeon;
			door.DoorwayA = doorway;
			door.DoorwayB = doorway.ConnectedDoorway;
			door.TileA = doorway.Tile;
			door.TileB = doorway.ConnectedDoorway.Tile;

			dungeon.AddDoor(door.gameObject);

			return door;
		}
	}
}
