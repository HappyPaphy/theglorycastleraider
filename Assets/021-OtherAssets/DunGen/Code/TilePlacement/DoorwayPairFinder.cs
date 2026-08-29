using DunGen.Pooling;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.TilePlacement
{
	[Serializable, SubclassDisplay(displayName: "Default")]
	public class DoorwayPairFinder : IDoorwayPairFinder
	{
		#region Statics

		public static readonly List<TileConnectionRule> CustomConnectionRules = new List<TileConnectionRule>();
		public static readonly List<CandidateTileRule> CandidateTileRules = new List<CandidateTileRule>();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void ResetStatics()
		{
			CustomConnectionRules.Clear();
			CandidateTileRules.Clear();
		}

		private static int CompareConnectionRules(TileConnectionRule a, TileConnectionRule b)
		{
			return b.Priority.CompareTo(a.Priority);
		}

		private static int CompareCandidateTileRules(CandidateTileRule a, CandidateTileRule b)
		{
			return b.Priority.CompareTo(a.Priority);
		}

		public static void SortCustomRules()
		{
			CustomConnectionRules.Sort(CompareConnectionRules);
			CandidateTileRules.Sort(CompareCandidateTileRules);
		}

		#endregion

		#region Helpers

		protected readonly struct PairingContext
		{
			public readonly PairingRequest Request;
			public readonly bool Straighten;
			public readonly Vector3? PathDir;
			public readonly IReadOnlyList<KeyedCandidateTile> Candidates;

			public PairingContext(PairingRequest request, bool straighten, Vector3? pathDir, IReadOnlyList<KeyedCandidateTile> candidates)
			{
				Request = request; Straighten = straighten; PathDir = pathDir; Candidates = candidates;
			}
		}

		#endregion

		// Pre-calculate cosine of angle epsilon for performance
		protected const float AngleEpsilonDeg = 1f;
		protected static readonly float CosEpsilon = Mathf.Cos(AngleEpsilonDeg * Mathf.Deg2Rad);


		public virtual void GetDoorwayPairs(PairingRequest request, ref Queue<DoorwayPair> results)
		{
			using (CollectionPool.List<KeyedCandidateTile>.Get(out var candidates))
			{
				var candidateTileBuilder = request.GenerationContext.Services.CandidateTileBuilder;
				candidateTileBuilder.BuildCandidates(request, ref candidates, CandidateTileRules);

				var (straighten, direction) = ComputeStraightening(in request);
				var context = new PairingContext(request, straighten, direction, candidates);

				using (CollectionPool.List<DoorwayPair>.Get(out var potentialPairs))
				{
					if (request.PreviousTile == null)
						CollectPairs_FirstTile(in context, ref potentialPairs);
					else
						CollectPairs_SubsequentTiles(in context, ref potentialPairs);

					int desired = SelectTopAndSort(request, ref potentialPairs);

					for (int i = 0; i < desired; i++)
						results.Enqueue(potentialPairs[i]);
				}
			}
		}

		protected (bool straighted, Vector3? direction) ComputeStraightening(in PairingRequest request)
		{
			// Can't straighten if there's no previous tile
			if (request.PreviousTile == null)
				return (false, null);

			// Find the appropriate straighten settings to use
			PathStraighteningSettings straighteningSettings = null;

			if (request.PlacementParameters.Archetype != null)
				straighteningSettings = request.PlacementParameters.Archetype.StraighteningSettings;
			else if (request.PlacementParameters.Node != null)
			{
				straighteningSettings = request.PlacementParameters.Node.StraighteningSettings;

				// Until branch paths are supported on nodes, we should just set these manually
				// to avoid any potential situation where they were somehow set incorrectly
				straighteningSettings.CanStraightenMainPath = true;
				straighteningSettings.CanStraightenBranchPaths = false;
			}

			// Exit early if we have no settings to work with
			if (straighteningSettings == null)
				return (false, null);

			// Apply any overrides to the global settings
			straighteningSettings = PathStraighteningSettings.GetFinalValues(straighteningSettings, request.DungeonFlow.GlobalStraighteningSettings);

			// Ignore main path based on user settings
			if (request.IsOnMainPath && !straighteningSettings.CanStraightenMainPath)
				return (false, null);

			// Ignore branch paths based on user settings
			if (!request.IsOnMainPath && !straighteningSettings.CanStraightenBranchPaths)
				return (false, null);

			// Random chance to straighten the connection
			bool tryStraighten = request.GenerationContext.RandomStream.NextDouble() < straighteningSettings.StraightenChance;

			// Calculate current path direction
			if (tryStraighten)
			{
				if (request.IsOnMainPath)
				{
					float pathDepth = request.PreviousTile.Placement.PathDepth;

					// Find the doorway we entered through and return its forward direction
					foreach (var doorway in request.PreviousTile.UsedDoorways)
					{
						var connectedTile = doorway.ConnectedDoorway.TileProxy;

						// We entered through this doorway if its connected Tile has a lower path depth than the current tile
						if (connectedTile.Placement.PathDepth < pathDepth)
							return (true, -doorway.Forward);
					}
				}
				else
				{
					// We can't calculate a path direction for the first tile in a branch
					if (request.PreviousTile.Placement.IsOnMainPath)
						return (false, null);

					float branchDepth = request.PreviousTile.Placement.BranchDepth;

					// Find the doorway we entered through and return its forward direction
					foreach (var doorway in request.PreviousTile.UsedDoorways)
					{
						var connectedTile = doorway.ConnectedDoorway.TileProxy;

						// We entered through this doorway if its connected Tile is on the main path or has a lower path depth than the current tile
						if (connectedTile.Placement.IsOnMainPath || connectedTile.Placement.BranchDepth < branchDepth)
							return (true, -doorway.Forward);
					}
				}
			}

			return (false, null);
		}

		protected int SelectTopAndSort(PairingRequest request, ref List<DoorwayPair> potentialPairs)
		{
			int total = potentialPairs.Count;
			int desired = request.MaxPairingAttempts.HasValue ? Math.Min(total, request.MaxPairingAttempts.Value) : total;

			// If we only need a small subset, we can do a partial selection instead of full sort
			// If desired < 40% of total, use selection
			if (desired < total && total > 0 && desired * 5 < total * 2)
			{
				PartialSelectTop(ref potentialPairs, desired);
				potentialPairs.Sort(0, desired, DoorwayPairComparer.Instance); // Sort top subset
			}
			else
				potentialPairs.Sort(DoorwayPairComparer.Instance);

			return desired;
		}

		// Quickselect-like partial selection (keeps top 'count' in front, unordered internally until sorted)
		protected static void PartialSelectTop(ref List<DoorwayPair> list, int count)
		{
			using (CollectionPool.List<DoorwayPair>.Get(out var heap))
			{
				for (int i = 0; i < list.Count; i++)
				{
					var item = list[i];

					if (heap.Count < count)
					{
						heap.Add(item);

						if (heap.Count == count)
							BuildMinHeap(heap);
					}
					else if (IsHigherPriority(item, heap[0]))
					{
						heap[0] = item;
						SiftDown(heap, 0);
					}
				}

				// Copy heap into front of original list
				for (int i = 0; i < count; i++)
					list[i] = heap[i];
			}
		}

		protected static bool IsHigherPriority(in DoorwayPair a, in DoorwayPair b)
		{
			// Lower 'key' is higher priority
			if (a.TileKey < b.TileKey)
				return true;
			if (a.TileKey > b.TileKey)
				return false;

			// Higher doorway weight as tie-breaker
			return a.DoorwayWeight > b.DoorwayWeight;
		}

		protected static void BuildMinHeap(List<DoorwayPair> heap)
		{
			for (int i = (heap.Count >> 1) - 1; i >= 0; i--)
				SiftDown(heap, i);
		}

		protected static void SiftDown(List<DoorwayPair> heap, int i)
		{
			int count = heap.Count;

			while (true)
			{
				int left = (i << 1) + 1;

				if (left >= count)
					return;

				int right = left + 1;
				int smallest = left;

				if (right < count && !IsHigherPriority(heap[right], heap[left]) && IsHigherPriority(heap[left], heap[right]))
					smallest = right;

				if (IsHigherPriority(heap[smallest], heap[i]) || heap[i].TileKey == heap[smallest].TileKey && heap[i].DoorwayWeight <= heap[smallest].DoorwayWeight)
					return;

				var tmp = heap[i];
				heap[i] = heap[smallest];
				heap[smallest] = tmp;
				i = smallest;
			}
		}

		protected virtual void CollectPairs_SubsequentTiles(in PairingContext context, ref List<DoorwayPair> results)
		{
			var request = context.Request;

			bool requiresSpecificExit = request.PreviousTile.Exits.Count > 0;

			// Pre-compute tile templates and apply the allowed-tile filter once per candidate,
			// rather than repeating the delegate call and predicate check for every doorway pairing.
			using (CollectionPool.List<(KeyedCandidateTile entry, TileProxy template)>.Get(out var resolvedCandidates))
			{
				foreach (var entry in context.Candidates)
				{
					var template = request.GetTileTemplateDelegate(entry.Candidate.TilePrefab);

					if (request.IsTileAllowedPredicate != null && !request.IsTileAllowedPredicate(request.PreviousTile, template))
						continue;

					resolvedCandidates.Add((entry, template));
				}

				foreach (var previousDoorway in request.PreviousTile.UnusedDoorways)
				{
					if (previousDoorway.IsDisabled)
						continue;

					// If the previous tile must use a specific exit and this door isn't one of them, skip it
					if (requiresSpecificExit && !previousDoorway.IsExit)
						continue;

					var previousDoorwayForward = previousDoorway.Forward;

					foreach (var (entry, nextTile) in resolvedCandidates)
					{
						bool requiresSpecificEntrance = nextTile.Entrances.Count > 0;
						bool singleForcedExit = nextTile.Exits.Count == 1;

						foreach (var nextDoorway in nextTile.Doorways)
						{
							// If the next tile must use a specific entrance and this door isn't one of them, skip it
							if (requiresSpecificEntrance && !nextDoorway.IsEntrance)
								continue;

							// Skip if doorway is the only designated exit (prevent locking future expansion)
							if (singleForcedExit && nextDoorway.IsExit)
								continue;

							if (IsValidDoorwayPairing(in context, previousDoorway, nextDoorway, request.PreviousTile, nextTile, previousDoorwayForward, out var doorwayWeight))
								results.Add(new DoorwayPair(request.PreviousTile, previousDoorway, nextTile, nextDoorway, entry.Candidate.TileSet, entry.Key, doorwayWeight));
						}
					}
				}
			}
		}

		protected virtual void CollectPairs_FirstTile(in PairingContext context, ref List<DoorwayPair> results)
		{
			foreach (var entry in context.Candidates)
			{
				var nextTile = context.Request.GetTileTemplateDelegate(entry.Candidate.TilePrefab);

				if (context.Request.IsTileAllowedPredicate != null && !context.Request.IsTileAllowedPredicate(context.Request.PreviousTile, nextTile))
					continue;

				foreach (var nextDoorway in nextTile.Doorways)
				{
					var proposedConnection = new ProposedConnection(context.Request.GenerationContext.ProxyDungeon, null, nextTile, null, nextDoorway);
					float doorwayWeight = CalculateConnectionWeight(in context, proposedConnection);

					results.Add(new DoorwayPair(null, null, nextTile, nextDoorway, entry.Candidate.TileSet, entry.Key, doorwayWeight));
				}
			}
		}

		protected virtual bool IsValidDoorwayPairing(in PairingContext context, DoorwayProxy previousDoorway, DoorwayProxy nextDoorway, TileProxy previousTile, TileProxy nextTile, Vector3 previousDoorwayForward, out float weight)
		{
			weight = 0.0f;

			var request = context.Request;
			var proposedConnection = new ProposedConnection(request.GenerationContext.ProxyDungeon, previousTile, nextTile, previousDoorway, nextDoorway);

			// Enforce facing-direction
			Vector3? forcedDirection = null;
			bool disallowRotation = false;

			// Global override takes precedence over tile-level setting if it has been set..
			if (request.AllowRotation.HasValue)
				disallowRotation = !request.AllowRotation.Value;
			// .. otherwise, fall back to tile-level setting on the tile to be placed
			else if(nextTile != null)
				disallowRotation = !nextTile.PrefabTile.AllowRotation;

			// Always enforce facing direction for vertical doorways
			if (Vector3.Dot(previousDoorwayForward, request.UpVector) >= CosEpsilon)
				forcedDirection = -request.UpVector;
			else if (Vector3.Dot(previousDoorwayForward, -request.UpVector) >= CosEpsilon)
				forcedDirection = request.UpVector;
			else if (disallowRotation)
				forcedDirection = -previousDoorwayForward;

			// We have a forced direction and the next doorway doesn't face it
			if (forcedDirection.HasValue && Vector3.Dot(forcedDirection.Value, nextDoorway.Forward) < CosEpsilon)
				return false;

			// Enforce connection rules
			if (!request.DungeonFlow.CanDoorwaysConnect(proposedConnection))
				return false;

			weight = CalculateConnectionWeight(in context, proposedConnection);
			request.DungeonFlow.ModifyConnectionWeight(proposedConnection, ref weight);

			return weight > 0.0f;
		}

		protected virtual float CalculateConnectionWeight(in PairingContext context, ProposedConnection connection)
		{
			// Assign a random weight initially
			float weight = (float)context.Request.GenerationContext.RandomStream.NextDouble();

			// Heavily weight towards doorways that keep the dungeon flowing in the same direction
			if (context.Straighten)
			{
				// Compare exit doorway direction to the current path direction
				float dot = Vector3.Dot(context.PathDir.Value, connection.PreviousDoorway.Forward);

				// If we're heading in the wrong direction, return a weight of 0
				if (dot < 0.99f)
					weight = 0.0f;
			}

			return weight;
		}
	}
}
