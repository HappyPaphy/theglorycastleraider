using DunGen.Generation;
using DunGen.Pooling;
using DunGen.Weighting;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen.TilePlacement
{
	/// <summary>
	/// Provides the default implementation for placing tiles within a procedural dungeon generation context.
	/// </summary>
	/// <remarks>The TilePlacer class is responsible for selecting, positioning, and validating tiles during dungeon
	/// generation. It determines which tiles can be placed based on configuration settings, tile constraints, and
	/// placement rules. This class can be subclassed to customize tile placement behaviour for different generation
	/// strategies.</remarks>
	[Serializable, SubclassDisplay(displayName: "Default")]
	public class TilePlacer : ITilePlacer
	{
		#region Helpers

		public readonly struct PendingTileInjection
		{
			public readonly InjectedTile Tile;
			public readonly int Index;


			public PendingTileInjection(InjectedTile tile, int index)
			{
				Tile = tile;
				Index = index;
			}

			public void Commit(GenerationContext context, TileProxy createdTile)
			{
				createdTile.Placement.InjectionData = Tile;

				if (Tile.IsRequired)
					createdTile.IsRequired = true;

				context.InjectedTiles[createdTile] = Tile;
				context.TilesPendingInjection.RemoveAt(Index);

				if (createdTile.Placement.IsOnMainPath)
					context.TargetLength++;
			}
		}

		#endregion

		protected GenerationContext context;


		public virtual TileProxy AddTile(GenerationContext context, TilePlacementRequest request)
		{
			this.context = context;

			var settings = context.Request.Settings;
			var attachmentTile = request.AttachTo;

			bool isOnMainPath = request.IsOnMainPath;
			bool isFirstTile = attachmentTile == null;
			float pathDepth = (isOnMainPath) ? request.NormalizedDepth : request.AttachTo.Placement.PathDepth / (context.TargetLength - 1f);
			float branchDepth = (isOnMainPath) ? 0 : request.NormalizedDepth;

			// If we're attaching to an existing dungeon, generate a dummy attachment point
			if (isFirstTile && context.Request.AttachmentSettings != null)
			{
				var rootTransform = context.Dungeon != null ? context.Dungeon.transform.parent : null;
				var attachmentProxy = context.Request.AttachmentSettings.GenerateAttachmentProxy(rootTransform, settings.UpDirection.ToVector3(), context.RandomStream);
				attachmentTile = attachmentProxy;
			}

			// See if we have a pending tile injection at this point
			var pendingTileInjection = GetPendingTileInjection(request);

			// Select appropriate tile weights
			IEnumerable<WeightedEntry<GameObject>> chanceEntries = null;

			if (pendingTileInjection.HasValue)
				chanceEntries = new List<WeightedEntry<GameObject>>(pendingTileInjection.Value.Tile.TileSet.Tiles.Entries);
			else if (request.CandidateTileEntries != null && request.CandidateTileEntries.Any())
				chanceEntries = request.CandidateTileEntries.Where(p => p != null).Distinct();

			// No candidate tiles available
			if (chanceEntries == null || !chanceEntries.Any())
			{
				context.TilePlacementResults.Add(new NoCandidateTilesResult(attachmentTile));
				return null;
			}

			// Leave the decision to allow rotation up to the new tile by default
			bool? allowRotation = null;

			// Apply constraint overrides
			if (settings.OverrideAllowTileRotation)
				allowRotation = settings.AllowTileRotation;

			var tileTemplateProvider = context.Services.TileTemplateProvider;
			int? maxPairingAttempts = settings.UseMaximumPairingAttempts ? (int?)settings.MaxPairingAttempts : null;

			var pairingRequest = new PairingRequest()
			{
				DungeonFlow = settings.DungeonFlow,
				GenerationContext = context,
				PlacementParameters = request.PlacementParameters,
				GetTileTemplateDelegate = tileTemplateProvider.GetTileTemplate,
				MaxPairingAttempts = maxPairingAttempts,
				IsOnMainPath = isOnMainPath,
				NormalizedPathDepth = pathDepth,
				NormalizedBranchDepth = branchDepth,
				PreviousTile = attachmentTile,
				UpVector = settings.UpDirection.ToVector3(),
				AllowRotation = allowRotation,
				TileWeights = new List<WeightedEntry<GameObject>>(chanceEntries),
				IsTileAllowedPredicate = IsTileAllowed
			};

			var doorwayPairFinder = context.Services.DoorwayPairFinder;

			TilePlacementResult lastTileResult = null;
			TileProxy createdTile = null;

			using (CollectionPool.Queue<DoorwayPair>.Get(out var pairsToTest))
			{
				doorwayPairFinder.GetDoorwayPairs(pairingRequest, ref pairsToTest);

				if (pairsToTest.Count == 0)
					context.TilePlacementResults.Add(new NoMatchingDoorwayPlacementResult(attachmentTile));

				while (pairsToTest.Count > 0)
				{
					var pair = pairsToTest.Dequeue();

					lastTileResult = TryPlaceTile(pair, request, out createdTile);

					if (lastTileResult is SuccessPlacementResult)
						break;
					else
						context.TilePlacementResults.Add(lastTileResult);
				}
			}

			// Successfully placed the tile
			if (lastTileResult is SuccessPlacementResult)
			{
				// We've successfully injected the tile, so we can remove it from the pending list now
				if (pendingTileInjection.HasValue)
					pendingTileInjection.Value.Commit(context, createdTile);

				return createdTile;
			}
			else
				return null;
		}

		protected virtual PendingTileInjection? GetPendingTileInjection(TilePlacementRequest request)
		{
			bool isOnMainPath = request.IsOnMainPath;
			bool isPlacingSpecificRoom = isOnMainPath && (request.PlacementParameters.Archetype == null);
			float pathDepth = (isOnMainPath) ? request.NormalizedDepth : request.AttachTo.Placement.PathDepth / (context.TargetLength - 1f);
			float branchDepth = (isOnMainPath) ? 0 : request.NormalizedDepth;

			if (context.TilesPendingInjection != null && !isPlacingSpecificRoom)
			{
				for (int i = 0; i < context.TilesPendingInjection.Count; i++)
				{
					var injectedTile = context.TilesPendingInjection[i];

					if (injectedTile.ShouldInjectTileAtPoint(isOnMainPath, pathDepth, branchDepth))
						return new PendingTileInjection(injectedTile, i);
				}
			}

			return null;
		}

		protected virtual bool IsTileAllowed(TileProxy previousTile, TileProxy potentialNextTile)
		{
			var settings = context.Request.Settings;

			bool isImmediateRepeat = previousTile != null && (potentialNextTile.Prefab == previousTile.Prefab);
			var repeatMode = TileRepeatMode.Allow;

			if (settings.OverrideRepeatMode)
				repeatMode = settings.RepeatMode;
			else if (potentialNextTile != null)
				repeatMode = potentialNextTile.PrefabTile.RepeatMode;

			bool allowTile = repeatMode switch
			{
				TileRepeatMode.Allow => true,
				TileRepeatMode.DisallowImmediate => !isImmediateRepeat,
				TileRepeatMode.Disallow => !context.ProxyDungeon.AllTiles.Where(t => t.Prefab == potentialNextTile.Prefab).Any(),
				_ => throw new NotImplementedException("TileRepeatMode " + repeatMode + " is not implemented"),
			};

			return allowTile;
		}

		protected virtual TilePlacementResult TryPlaceTile(DoorwayPair pair, TilePlacementRequest request, out TileProxy tile)
		{
			if (pair.PreviousTile != null && pair.PreviousDoorway == null)
				throw new Exception("Previous tile has no doorway, but was included in the doorway pairings. This should never happen.");

			var settings = context.Request.Settings;

			tile = null;

			var toTemplate = pair.NextTemplate;
			var fromDoorway = pair.PreviousDoorway;

			if (toTemplate == null)
				return new NullTemplatePlacementResult();

			var proxyPool = context.Services.TileProxyPool;
			var collisionService = context.Services.CollisionService;

			int toDoorwayIndex = pair.NextTemplate.Doorways.IndexOf(pair.NextDoorway);
			tile = proxyPool.GetTileProxy(toTemplate);
			tile.Placement.IsOnMainPath = request.IsOnMainPath;
			tile.Placement.PlacementParameters = request.PlacementParameters;
			tile.Placement.TileSet = pair.NextTileSet;

			if (fromDoorway != null)
			{
				// Move the proxy object into position
				var toProxyDoor = tile.Doorways[toDoorwayIndex];
				tile.PositionBySocket(toProxyDoor, fromDoorway);

			}

			Bounds proxyBounds = tile.Placement.Bounds;

			// Check if the new tile is outside of the valid bounds
			if (settings.RestrictDungeonToBounds && !settings.TilePlacementBounds.Contains(proxyBounds))
			{
				proxyPool.ReturnTileProxy(tile);
				return new OutOfBoundsPlacementResult(toTemplate);
			}

			// Check if the new tile is colliding with any other
			bool isColliding = collisionService.IsCollidingWithAnyTile(settings.UpDirection, tile, fromDoorway?.TileProxy);

			if (isColliding)
			{
				proxyPool.ReturnTileProxy(tile);
				return new TileIsCollidingPlacementResult(toTemplate);
			}

			if (tile.Placement.IsOnMainPath)
			{
				if (pair.PreviousTile != null)
					tile.Placement.PathDepth = pair.PreviousTile.Placement.PathDepth + 1;
			}
			else
			{
				tile.Placement.PathDepth = pair.PreviousTile.Placement.PathDepth;
				tile.Placement.BranchDepth = (pair.PreviousTile.Placement.IsOnMainPath) ? 0 : pair.PreviousTile.Placement.BranchDepth + 1;
			}

			var toDoorway = tile.Doorways[toDoorwayIndex];

			if (fromDoorway != null)
				context.ProxyDungeon.MakeConnection(fromDoorway, toDoorway);

			context.ProxyDungeon.AddTile(tile);
			collisionService.AddTile(tile);

			return new SuccessPlacementResult();
		}
	}
}