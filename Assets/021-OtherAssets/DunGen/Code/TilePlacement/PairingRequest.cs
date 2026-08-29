using DunGen.Generation;
using DunGen.Graph;
using DunGen.Weighting;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.TilePlacement
{
	public delegate bool TileMatchDelegate(TileProxy previousTile, TileProxy potentialNextTile);
	public delegate TileProxy GetTileTemplateDelegate(GameObject prefab);

	public sealed class PairingRequest
	{
		public GenerationContext GenerationContext;
		public List<WeightedEntry<GameObject>> TileWeights;
		public TileProxy PreviousTile;
		public int? MaxPairingAttempts;
		public bool IsOnMainPath;
		public float NormalizedPathDepth;
		public float NormalizedBranchDepth;
		public TilePlacementParameters PlacementParameters;
		public bool? AllowRotation;
		public Vector3 UpVector;
		public TileMatchDelegate IsTileAllowedPredicate;
		public GetTileTemplateDelegate GetTileTemplateDelegate;
		public DungeonFlow DungeonFlow;
	}
}