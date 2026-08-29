using DunGen.Common;

namespace DunGen.Collision
{
	public interface IDungeonCollisionService
	{
		void SetCollisionManager(DungeonCollisionManager manager);
		void AddTile(TileProxy tile);
		void RemoveTile(TileProxy tile);
		bool IsCollidingWithAnyTile(AxisDirection upDirection, TileProxy prospectiveNewTile, TileProxy previousTile);
	}
}