using DunGen.Async;
using DunGen.Generation;
using DunGen.Placement;
using DunGen.Weighting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen.TilePlacement
{
	/// <summary>
	/// Defines a contract for building a realized dungeon from a proxy dungeon
	/// </summary>
	public interface IDungeonBuilder
	{
		IEnumerator BuildDungeon(GenerationContext context, Dungeon dungeon, TileInstanceSource tileInstanceSource);
	}

	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class DungeonBuilder : IDungeonBuilder
	{
		public virtual IEnumerator BuildDungeon(GenerationContext context, Dungeon dungeon, TileInstanceSource tileInstanceSource)
		{
			var settings = context.Request.Settings;
			var attachmentSettings = context.Request.AttachmentSettings;

			var proxyToTileMap = new Dictionary<TileProxy, Tile>();

			dungeon.DungeonFlow = context.Request.Settings.DungeonFlow;
			dungeon.TileInstanceSource = tileInstanceSource;

			// We're attaching to a previous dungeon
			if (attachmentSettings != null &&
				attachmentSettings.TileProxy != null)
			{
				// We need to manually inject the dummy TileProxy used to connect to a Tile in the previous dungeon
				var attachmentProxy = attachmentSettings.TileProxy;
				var attachmentTile = attachmentSettings.GetAttachmentTile();
				proxyToTileMap[attachmentProxy] = attachmentTile;
				dungeon.AttachmentTile = attachmentTile;

				// We also need to manually process the doorway in the other dungeon
				var usedDoorwayProxy = attachmentProxy.UsedDoorways.First();
				var usedDoorway = attachmentTile.AllDoorways[usedDoorwayProxy.Index];

				usedDoorway.ProcessDoorwayObjects(true, context.RandomStream);

				attachmentTile.UsedDoorways.Add(usedDoorway);
				attachmentTile.UnusedDoorways.Remove(usedDoorway);
			}

			foreach (var tileProxy in context.ProxyDungeon.AllTiles)
			{
				// Instantiate & re-position tile
				var tileObj = tileInstanceSource.SpawnTile(tileProxy.PrefabTile, tileProxy.Placement.Position, tileProxy.Placement.Rotation);

				// Add tile to lists
				var tile = tileObj.GetComponent<Tile>();
				tile.Dungeon = dungeon;
				tile.Placement = new TilePlacementData(tileProxy.Placement);
				tile.Prefab = tileProxy.Prefab;
				proxyToTileMap[tileProxy] = tile;
				dungeon.AddTile(tile);

				// Now that the tile is actually attached to the root object, we need to update our transform to match
				tile.Placement.SetPositionAndRotation(tileObj.transform.position, tileObj.transform.rotation);

				ConfigureTriggerVolume(tile, settings.TriggerPlacement, settings.TileTriggerLayer);

				// Process doorways
				var allDoorways = tileObj.GetComponentsInChildren<Doorway>();

				foreach (var doorway in allDoorways)
				{
					if (tile.AllDoorways.Contains(doorway))
						continue;

					doorway.Tile = tile;
					doorway.placedByGenerator = true;
					doorway.HideConditionalObjects = false;

					tile.AllDoorways.Add(doorway);
				}

				foreach (var doorwayProxy in tileProxy.UsedDoorways)
				{
					var doorway = allDoorways[doorwayProxy.Index];
					tile.UsedDoorways.Add(doorway);

					doorway.ProcessDoorwayObjects(true, context.RandomStream);
				}

				foreach (var doorwayProxy in tileProxy.UnusedDoorways)
				{
					var doorway = allDoorways[doorwayProxy.Index];
					tile.UnusedDoorways.Add(doorway);

					doorway.ProcessDoorwayObjects(false, context.RandomStream);
				}

				// Let the user know a new tile has been instantiated
				dungeon.NotifyTileInstantiated(in context, tile);

				yield return YieldSignal.Work;
			}

			// Add doorway connections
			foreach (var proxyConn in context.ProxyDungeon.Connections)
			{
				var tileA = proxyToTileMap[proxyConn.A.TileProxy];
				var tileB = proxyToTileMap[proxyConn.B.TileProxy];

				var doorA = tileA.AllDoorways[proxyConn.A.Index];
				var doorB = tileB.AllDoorways[proxyConn.B.Index];

				doorA.ConnectedDoorway = doorB;
				doorB.ConnectedDoorway = doorA;

				var connection = new DoorwayConnection(doorA, doorB);
				dungeon.AddConnection(connection);

				TrySpawnDoorPrefab(dungeon, connection, context.RandomStream, out var door);
			}

			dungeon.Finalise();
		}

		protected virtual void ConfigureTriggerVolume(Tile tile, TriggerPlacementMode placementMode, int tileTriggerLayer)
		{
			if(placementMode == TriggerPlacementMode.None)
				return;

			switch(placementMode)
			{
				case TriggerPlacementMode.ThreeDimensional:
					{
						if(!tile.TryGetComponent<BoxCollider>(out var triggerVolume))
							triggerVolume = tile.gameObject.AddComponent<BoxCollider>();

						triggerVolume.center = tile.Placement.LocalBounds.center;
						triggerVolume.size = tile.Placement.LocalBounds.size;
						triggerVolume.isTrigger = true;

						break;
					}

				case TriggerPlacementMode.TwoDimensional:
					{
						if (!tile.TryGetComponent<BoxCollider2D>(out var triggerVolume))
							triggerVolume = tile.gameObject.AddComponent<BoxCollider2D>();

						triggerVolume.offset = tile.Placement.LocalBounds.center;
						triggerVolume.size = tile.Placement.LocalBounds.size;
						triggerVolume.isTrigger = true;

						break;
					}

				default:
					throw new ArgumentOutOfRangeException(nameof(placementMode), placementMode, "Trigger placement mode not supported");
			}

			tile.gameObject.layer = tileTriggerLayer;
		}

		protected virtual bool TrySpawnDoorPrefab(Dungeon dungeon, DoorwayConnection connection, RandomStream randomStream, out Door newDoor)
		{
			newDoor = null;

			// This door already has a prefab instance placed, exit early
			if (connection.A.HasDoorPrefabInstance || connection.B.HasDoorPrefabInstance)
				return false;

			var context = new WeightedTable<GameObject>.SelectionContext
			{
				AllowNullSelection = false,
				IsOnMainPath = connection.A.Tile.Placement.IsOnMainPath,
				NormalizedPathDepth = connection.A.Tile.Placement.NormalizedPathDepth,
				NormalizedBranchDepth = connection.A.Tile.Placement.NormalizedBranchDepth,
			};

			// Select doorway and prefab to use
			if (!SelectDoorPrefab(connection, randomStream, context, out var chosenDoorway, out var chosenDoorPrefab))
				return false;

			// Spawn door prefab if valid
			if (chosenDoorway != null && chosenDoorPrefab != null)
			{
				GameObject door = SpawnAndPositionDoorPrefab(chosenDoorway, chosenDoorPrefab);
				newDoor = DungeonUtil.AddAndSetupDoorComponent(dungeon, door, chosenDoorway);

				connection.A.SetUsedPrefab(door);
				connection.B.SetUsedPrefab(door);

				return true;
			}

			return false;
		}

		protected virtual GameObject SpawnAndPositionDoorPrefab(Doorway chosenDoorway, GameObject chosenDoorPrefab)
		{
			GameObject door = GameObject.Instantiate(chosenDoorPrefab, chosenDoorway.transform);
			door.transform.localPosition = chosenDoorway.DoorPrefabPositionOffset;

			if (chosenDoorway.AvoidRotatingDoorPrefab)
				door.transform.rotation = Quaternion.Euler(chosenDoorway.DoorPrefabRotationOffset);
			else
				door.transform.localRotation = Quaternion.Euler(chosenDoorway.DoorPrefabRotationOffset);

			return door;
		}

		protected virtual bool SelectDoorPrefab(DoorwayConnection connection, RandomStream randomStream, WeightedTable<GameObject>.SelectionContext context, out Doorway chosenDoorway, out GameObject chosenDoorPrefab)
		{
			chosenDoorway = null;
			chosenDoorPrefab = null;

			bool doorwayAHasEntries = connection.A.ConnectorPrefabs.GetTotalWeight(context) > 0f;
			bool doorwayBHasEntries = connection.B.ConnectorPrefabs.GetTotalWeight(context) > 0f;

			// No doorway has a prefab to place, exit early
			if (!doorwayAHasEntries && !doorwayBHasEntries)
				return false;

			// If both doorways have door prefabs..
			if (doorwayAHasEntries && doorwayBHasEntries)
			{
				// ..A is selected if its priority is greater than or equal to B..
				if (connection.A.DoorPrefabPriority >= connection.B.DoorPrefabPriority)
					chosenDoorway = connection.A;
				// .. otherwise, B is chosen..
				else
					chosenDoorway = connection.B;
			}
			// ..if only one doorway has a prefab, use that one
			else
				chosenDoorway = (doorwayAHasEntries) ? connection.A : connection.B;

			return chosenDoorway.ConnectorPrefabs.TrySelectRandom(out chosenDoorPrefab, randomStream, context) && chosenDoorPrefab != null;
		}
	}
}