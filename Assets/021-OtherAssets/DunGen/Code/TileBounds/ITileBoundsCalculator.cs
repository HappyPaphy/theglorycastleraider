using UnityEngine;

namespace DunGen.TileBounds
{
	public interface ITileBoundsCalculator
	{
		Bounds CalculateLocalBounds(GameObject tileRoot);
	}
}