using DunGen.Common;
using System;

namespace DunGen.Collision
{
	/// <summary>
	/// Provides services for managing and querying tile-based collision detection.
	/// </summary>
	[Serializable, SubclassDisplay(displayName: "Default")]
	public class DungeonCollisionService : IDungeonCollisionService
	{
		public DungeonCollisionManager CollisionManager { get; private set; }


		public virtual void SetCollisionManager(DungeonCollisionManager manager) => CollisionManager = manager;

		public virtual void AddTile(TileProxy tile) => CollisionManager.AddTile(tile);

		public virtual void RemoveTile(TileProxy tile) => CollisionManager.RemoveTile(tile);

		public virtual bool IsCollidingWithAnyTile(AxisDirection upDirection, TileProxy prospectiveNewTile, TileProxy previousTile)
		{
			return CollisionManager.IsCollidingWithAnyTile(upDirection, prospectiveNewTile, previousTile);
		}
	}
}