using DunGen.Versioning;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Collision
{
	/// <summary>
	/// Used to add custom collision detection when deciding where to place tiles
	/// </summary>
	/// <param name="tileBounds">The tile bounds to check collisions for</param>
	/// <param name="isCollidingWithDungeon">If the tile is already deemed to be colliding the dungeon itself</param>
	/// <returns>True if the new tile bounds are blocked</returns>
	public delegate bool AdditionalCollisionsPredicate(Bounds tileBounds, bool isCollidingWithDungeon);

	public enum MultiDungeonCollisionMode
	{
		SelfOnly, // Only collide with other tiles in the same dungeon
		ConnectedDungeons, // Collide with other dungeons that are connected to this one
		AllDungeons, // Collide with all other dungeons
	}

	[Serializable]
	public class DungeonCollisionSettings : IVersionable
	{
		public int LatestVersion => 2;
		public int DataVersion { get => fileVersion; set => fileVersion = value; }
		public bool RequiresMigration => DataVersion < LatestVersion;

		#region Legacy Properties

		[Obsolete("Use MultiDungeonCollisionMode instead. This will be removed in later versions.")]
		public bool AvoidCollisionsWithOtherDungeons = false;

		#endregion

		/// <summary>
		/// If true, tiles will not be allowed to overhang other tiles
		/// </summary>
		public bool DisallowOverhangs = false;

		/// <summary>
		/// The maximum amount of overlap allowed between two connected tiles
		/// </summary>
		public float OverlapThreshold = 0.01f;

		/// <summary>
		/// The amount of padding to add to the bounds of each tile when checking for collisions
		/// </summary>
		public float Padding = 0.0f;

		/// <summary>
		/// An optional additional set of world-space bounds to test against when determining if a tile will collide or not.
		/// Useful for preventing the dungeon from being generated in specific areas
		/// </summary>
		public readonly List<Bounds> AdditionalCollisionBounds = new List<Bounds>();

		/// <summary>
		/// Specifies how collisions with other dungeons should be handled
		/// </summary>
		public MultiDungeonCollisionMode MultiDungeonCollisionMode = MultiDungeonCollisionMode.ConnectedDungeons;

		/// <summary>
		/// An optional predicate to test for additional collisions
		/// </summary>
		public AdditionalCollisionsPredicate AdditionalCollisionsPredicate;

		[SerializeField]
		private int fileVersion;


		public void Migrate()
		{
#pragma warning disable CS0618 // Type or member is obsolete
			// Multi-dungeon collision enum
			if (DataVersion < 2)
			{
				MultiDungeonCollisionMode = AvoidCollisionsWithOtherDungeons
					? MultiDungeonCollisionMode.AllDungeons
					: MultiDungeonCollisionMode.ConnectedDungeons;
			}
#pragma warning restore CS0618 // Type or member is obsolete
		}
	}
}
