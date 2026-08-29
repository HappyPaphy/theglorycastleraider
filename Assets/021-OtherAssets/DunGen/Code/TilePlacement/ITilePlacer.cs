using DunGen.Generation;

namespace DunGen.TilePlacement
{
	/// <summary>
	/// Defines a contract for placing tile proxies within a generation context.
	/// </summary>
	public interface ITilePlacer
	{
		TileProxy AddTile(GenerationContext context, TilePlacementRequest request);
	}
}