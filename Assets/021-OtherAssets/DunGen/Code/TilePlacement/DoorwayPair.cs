using System.Collections.Generic;

namespace DunGen.TilePlacement
{
	public readonly struct DoorwayPair
	{
		public readonly TileProxy PreviousTile;
		public readonly DoorwayProxy PreviousDoorway;
		public readonly TileProxy NextTemplate;
		public readonly DoorwayProxy NextDoorway;
		public readonly TileSet NextTileSet;
		public readonly double TileKey;
		public readonly float DoorwayWeight;

		public DoorwayPair(TileProxy previousTile, DoorwayProxy previousDoorway, TileProxy nextTemplate, DoorwayProxy nextDoorway, TileSet nextTileSet, double tileKey, float doorwayWeight)
		{
			PreviousTile = previousTile;
			PreviousDoorway = previousDoorway;
			NextTemplate = nextTemplate;
			NextDoorway = nextDoorway;
			NextTileSet = nextTileSet;
			TileKey = tileKey;
			DoorwayWeight = doorwayWeight;
		}
	}

	public sealed class DoorwayPairComparer : IComparer<DoorwayPair>
	{
		public static readonly DoorwayPairComparer Instance = new DoorwayPairComparer();

		public int Compare(DoorwayPair a, DoorwayPair b)
		{
			// Lower tile keys should be sorted first, but higher doorway weights should be sorted first
			int tileCmp = a.TileKey.CompareTo(b.TileKey);
			return tileCmp != 0 ? tileCmp : b.DoorwayWeight.CompareTo(a.DoorwayWeight);
		}
	}
}