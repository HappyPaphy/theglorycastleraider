namespace DunGen.Pooling
{
	/// <summary>
	/// Defines a contract for managing a pool of TileProxy instances to enable efficient reuse and resource management
	/// </summary>
	public interface ITileProxyPool
	{
		TileProxy GetTileProxy(TileProxy tileTemplate);
		void ReturnTileProxy(TileProxy tileProxy);
	}
}