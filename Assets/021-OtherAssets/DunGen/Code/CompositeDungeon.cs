using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DunGen
{
	/// <summary>
	/// Represents a group of dungeons that have been attached to one another.
	/// </summary>
	public class CompositeDungeon
	{
		private readonly List<Dungeon> dungeons = new List<Dungeon>();
		public ReadOnlyCollection<Dungeon> Dungeons => dungeons.AsReadOnly();

		public event Action<Dungeon> DungeonAdded;
		public event Action<Dungeon> DungeonRemoved;


		public void AddDungeon(Dungeon dungeon)
		{
			if (dungeon == null || dungeons.Contains(dungeon))
				return;

			dungeons.Add(dungeon);
			DungeonAdded?.Invoke(dungeon);
		}

		public void RemoveDungeon(Dungeon dungeon)
		{
			if (dungeon == null || !dungeons.Contains(dungeon))
				return;

			dungeons.Remove(dungeon);
			DungeonRemoved?.Invoke(dungeon);
		}

		public void Clear()
		{
			var oldDungeons = dungeons.ToArray();

			dungeons.Clear();

			foreach(var dungeon in oldDungeons)
				DungeonRemoved?.Invoke(dungeon);
		}
	}
}
