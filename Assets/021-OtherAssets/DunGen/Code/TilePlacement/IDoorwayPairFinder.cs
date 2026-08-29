using System.Collections.Generic;

namespace DunGen.TilePlacement
{
	/// <summary>
	/// Defines a service for retrieving valid doorway pairs to create a connection between two tiles
	/// </summary>
	public interface IDoorwayPairFinder
	{
		void GetDoorwayPairs(PairingRequest request, ref Queue<DoorwayPair> results);
	}
}