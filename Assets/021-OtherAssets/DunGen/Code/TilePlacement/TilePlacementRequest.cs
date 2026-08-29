using DunGen.Weighting;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.TilePlacement
{
	/// <summary>
	/// A request to place a new tile proxy in a proxy dungeon
	/// </summary>
	public sealed class TilePlacementRequest
	{
		/// <summary>
		/// The existing tile to attach the new tile to. If null, this is the first tile in the dungeon
		/// </summary>
		public TileProxy AttachTo;

		/// <summary>
		/// A collection of candidate tiles to choose from when placing the new tile
		/// </summary>
		public IEnumerable<WeightedEntry<GameObject>> CandidateTileEntries;

		/// <summary>
		/// Should this tile be placed on the main path? If false, it will be placed on a branch
		/// </summary>
		public bool IsOnMainPath;

		/// <summary>
		/// The normalized (0-1) depth along the path or branch that this tile is being placed at
		/// </summary>
		public float NormalizedDepth;

		/// <summary>
		/// Additional information about where a tile originated from the dungeon flow
		/// </summary>
		public TilePlacementParameters PlacementParameters;
	}
}