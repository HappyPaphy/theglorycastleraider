using UnityEngine;

namespace DunGen.TileBounds
{
	/// <summary>
	/// Provides a component that allows overriding the default bounds calculation on a tile-by-tile basis
	/// </summary>
	/// <remarks>Attach this component to a Tile GameObject to specify a custom bounds calculator for this specific Tile.
	/// Bounds calculations can be overridden globally with the BoundsCalculator property in the project settings</remarks>
	[AddComponentMenu("DunGen/Tile Bounds Override")]
	[DisallowMultipleComponent]
	[HelpURL("https://www.aegongames.com/dungen-documentation/core-concepts/tiles/#tile-bounds")]
	public class TileBoundsOverride : MonoBehaviour, ITileBoundsCalculator
	{
		[SerializeReference]
		[SubclassSelector(allowNone: false)]
		public ITileBoundsCalculator BoundsCalculator = new DefaultTileBoundsCalculator();


		public Bounds CalculateLocalBounds(GameObject tileRoot)
		{
			return BoundsCalculator.CalculateLocalBounds(tileRoot);
		}
	}
}