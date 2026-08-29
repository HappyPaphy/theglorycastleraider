using UnityEngine;

namespace DunGen.TilePlacement
{
	/// <summary>
	/// Defines a contract for retrieving a tile template based on a specified tile prefab
	/// </summary>
	public interface ITileTemplateProvider
	{
		TileProxy GetTileTemplate(GameObject tilePrefab);
	}
}