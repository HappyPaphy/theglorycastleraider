namespace DunGen.Pooling
{
	public interface ITileSpawnEventReceiver
	{
		void OnTileSpawned(Tile tile);
		void OnTileDespawned(Tile tile);
	}
}
