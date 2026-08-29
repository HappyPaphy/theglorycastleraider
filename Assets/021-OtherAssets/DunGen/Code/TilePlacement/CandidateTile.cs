using UnityEngine;

namespace DunGen.TilePlacement
{
	/// <summary>
	/// An option for a tile that can be placed at a given location.
	/// </summary>
	public sealed class CandidateTile
	{
		public GameObject TilePrefab;
		public float Weight;
		public TileSet TileSet;

		public CandidateTile(GameObject tilePrefab, float weight, TileSet tileSet)
		{
			TilePrefab = tilePrefab;
			Weight = weight;
			TileSet = tileSet;
		}
	}

	/// <summary>
	/// Represents a candidate tile associated with a numeric key used for sorting or ranking operations.
	/// </summary>
	public readonly struct KeyedCandidateTile
	{
		public readonly CandidateTile Candidate;
		public readonly double Key;

		public KeyedCandidateTile(CandidateTile candidate, double key)
		{
			Candidate = candidate;
			Key = key;
		}
	}
}