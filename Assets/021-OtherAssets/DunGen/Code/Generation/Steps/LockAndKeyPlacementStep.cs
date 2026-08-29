using DunGen.LockAndKey;
using DunGen.Weighting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen.Generation.Steps
{
	/// <summary>
	/// Represents a generation step that places locks and corresponding keys within a dungeon layout, ensuring that locked
	/// doorways are properly assigned and that keys are distributed in accessible locations.
	/// </summary>
	[Serializable]
	[SubclassDisplay(displayName: "Default")]
	public class LockAndKeyPlacementStep : IGenerationStep
	{
		public virtual IEnumerator Execute(GenerationContext context)
		{
			var dungeon = context.Dungeon;
			var settings = context.Request.Settings;

			if(settings.DungeonFlow.KeyManager == null)
				yield break;

			var nodes = dungeon.ConnectionGraph.Nodes.Select(x => x.Tile.Placement.GraphNode).Where(x => { return x != null; }).Distinct().ToArray();
			var lines = dungeon.ConnectionGraph.Nodes.Select(x => x.Tile.Placement.GraphLine).Where(x => { return x != null; }).Distinct().ToArray();

			Dictionary<Doorway, Key> lockedDoorways = new Dictionary<Doorway, Key>();

			// Lock doorways on nodes
			foreach (var node in nodes)
			{
				foreach (var l in node.Locks)
				{
					var tile = dungeon.AllTiles
						.Where(x => { return x.Placement.GraphNode == node; })
						.FirstOrDefault();

					var connections = dungeon.ConnectionGraph.Nodes
						.Where(x => { return x.Tile == tile; })
						.FirstOrDefault()
						.Connections;

					Doorway entrance = null;
					Doorway exit = null;

					foreach (var conn in connections)
					{
						if (conn.DoorwayA.Tile == tile)
							exit = conn.DoorwayA;
						else if (conn.DoorwayB.Tile == tile)
							entrance = conn.DoorwayB;
					}

					var key = node.Graph.KeyManager.GetKeyByID(l.ID);

					if (entrance != null && (node.LockPlacement & NodeLockPlacement.Entrance) == NodeLockPlacement.Entrance)
						lockedDoorways.Add(entrance, key);

					if (exit != null && (node.LockPlacement & NodeLockPlacement.Exit) == NodeLockPlacement.Exit)
						lockedDoorways.Add(exit, key);
				}
			}

			// Lock doorways on lines
			foreach (var line in lines)
			{
				var doorways = dungeon.ConnectionGraph.Connections.Where(x =>
				{
					var tileSet = x.DoorwayA.Tile.Placement.TileSet;

					if (tileSet == null)
						return false;

					bool isDoorwayAlreadyLocked = lockedDoorways.ContainsKey(x.DoorwayA) || lockedDoorways.ContainsKey(x.DoorwayB);
					bool doorwayHasLockPrefabs = tileSet.LockPrefabs.Count > 0;

					return x.DoorwayA.Tile.Placement.GraphLine == line &&
							x.DoorwayB.Tile.Placement.GraphLine == line &&
							!isDoorwayAlreadyLocked &&
							doorwayHasLockPrefabs;

				}).Select(x => x.DoorwayA)
				.ToList();

				if (doorways.Count == 0)
					continue;

				foreach (var l in line.Locks)
				{
					int lockCount = l.Range.GetRandom(context.RandomStream);
					lockCount = Mathf.Clamp(lockCount, 0, doorways.Count);

					for (int i = 0; i < lockCount; i++)
					{
						if (doorways.Count == 0)
							break;

						var doorway = doorways[context.RandomStream.Next(0, doorways.Count)];
						doorways.Remove(doorway);

						if (lockedDoorways.ContainsKey(doorway))
							continue;

						var key = line.Graph.KeyManager.GetKeyByID(l.ID);
						lockedDoorways.Add(doorway, key);
					}
				}
			}

			// Lock doorways on injected tiles
			foreach (var tile in dungeon.AllTiles)
			{
				if (tile.Placement.InjectionData != null && tile.Placement.InjectionData.IsLocked)
				{
					var validLockedDoorways = new List<Doorway>();

					foreach (var doorway in tile.UsedDoorways)
					{
						bool isDoorwayAlreadyLocked = lockedDoorways.ContainsKey(doorway) || lockedDoorways.ContainsKey(doorway.ConnectedDoorway);
						bool doorwayHasLockPrefabs = tile.Placement.TileSet.LockPrefabs.Count > 0;
						bool isEntranceDoorway = tile.GetEntranceDoorway() == doorway;

						if (!isDoorwayAlreadyLocked &&
							doorwayHasLockPrefabs &&
							isEntranceDoorway)
						{
							validLockedDoorways.Add(doorway);
						}
					}

					if (validLockedDoorways.Any())
					{
						var doorway = validLockedDoorways.First();
						var key = settings.DungeonFlow.KeyManager.GetKeyByID(tile.Placement.InjectionData.LockID);

						lockedDoorways.Add(doorway, key);
					}
				}
			}

			var locksToRemove = new List<Doorway>();
			var usedSpawnComponents = new List<IKeySpawner>();

			foreach (var pair in lockedDoorways)
			{
				var doorway = pair.Key;
				var key = pair.Value;
				var possibleSpawnTiles = new List<Tile>();

				foreach (var t in dungeon.AllTiles)
				{
					if (t.Placement.NormalizedPathDepth >= doorway.Tile.Placement.NormalizedPathDepth)
						continue;

					bool canPlaceKey = false;

					if (t.Placement.GraphNode != null && t.Placement.GraphNode.Keys.Where(x => { return x.ID == key.ID; }).Count() > 0)
						canPlaceKey = true;
					else if (t.Placement.GraphLine != null && t.Placement.GraphLine.Keys.Where(x => { return x.ID == key.ID; }).Count() > 0)
						canPlaceKey = true;

					if (!canPlaceKey)
						continue;

					possibleSpawnTiles.Add(t);
				}

				var possibleSpawnComponents = possibleSpawnTiles
					.SelectMany(x => x.GetComponentsInChildren<Component>()
					.OfType<IKeySpawner>())
					.Except(usedSpawnComponents)
					.Where(x => x.CanSpawnKey(settings.DungeonFlow.KeyManager, key))
					.ToArray();

				GameObject lockedDoorPrefab = null;

				if (possibleSpawnComponents.Any())
					lockedDoorPrefab = TryGetRandomLockedDoorPrefab(context, doorway, key, settings.DungeonFlow.KeyManager);

				if (!possibleSpawnComponents.Any() || lockedDoorPrefab == null)
					locksToRemove.Add(doorway);
				else
				{
					doorway.LockID = key.ID;

					var keySpawnParameters = new KeySpawnParameters(key, settings.DungeonFlow.KeyManager, dungeon);

					int keysToSpawn = key.KeysPerLock.GetRandom(context.RandomStream);
					keysToSpawn = Math.Min(keysToSpawn, possibleSpawnComponents.Length);

					for (int i = 0; i < keysToSpawn; i++)
					{
						int chosenSpawnerIndex = context.RandomStream.Next(0, possibleSpawnComponents.Length);
						var keySpawner = possibleSpawnComponents[chosenSpawnerIndex];

						keySpawnParameters.OutputSpawnedKeys.Clear();
						keySpawner.SpawnKey(keySpawnParameters);

						foreach (var receiver in keySpawnParameters.OutputSpawnedKeys)
							receiver.OnKeyAssigned(key, settings.DungeonFlow.KeyManager);

						usedSpawnComponents.Add(keySpawner);
					}

					LockDoorway(context, doorway, lockedDoorPrefab, key, settings.DungeonFlow.KeyManager);
				}
			}

			foreach (var doorway in locksToRemove)
			{
				doorway.LockID = -1;
				lockedDoorways.Remove(doorway);
			}
		}

		protected virtual GameObject TryGetRandomLockedDoorPrefab(GenerationContext context, Doorway doorway, Key key, KeyManager keyManager)
		{
			var placement = doorway.Tile.Placement;
			var selectionContext = new WeightedTable<GameObject>.SelectionContext
			{
				IsOnMainPath = placement.IsOnMainPath,
				NormalizedPathDepth = placement.NormalizedPathDepth,
				NormalizedBranchDepth = placement.NormalizedBranchDepth,
				PreviouslyChosen = null,
				AllowImmediateRepeats = true,
				AllowNullSelection = false,
			};

			var prefabs = doorway.Tile.Placement.TileSet.LockPrefabs.Where(x =>
			{
				if (x == null || x.Prefabs == null)
					return false;

				if (!x.Prefabs.HasAnyValidEntries(selectionContext))
					return false;

				var lockSocket = x.Socket;

				if (lockSocket == null)
					return true;
				else
					return DoorwaySocket.CanSocketsConnect(lockSocket, doorway.Socket);

			}).Select(x => x.Prefabs)
			.ToArray();

			if (prefabs.Length == 0)
				return null;

			var table = prefabs[context.RandomStream.Next(0, prefabs.Length)];

			if (table.TrySelectRandom(out var chosenPrefab, context.RandomStream, selectionContext))
				return chosenPrefab;
			else
				return null;
		}

		protected virtual void LockDoorway(GenerationContext context, Doorway doorway, GameObject doorPrefab, Key key, KeyManager keyManager)
		{
			GameObject doorObj = GameObject.Instantiate(doorPrefab, doorway.transform);

			DungeonUtil.AddAndSetupDoorComponent(context.Dungeon, doorObj, doorway);

			// Remove any existing door prefab that may have been placed as we'll be replacing it with a locked door
			doorway.RemoveUsedPrefab();

			// Set this locked door as the current door prefab
			doorway.SetUsedPrefab(doorObj);
			doorway.ConnectedDoorway.SetUsedPrefab(doorObj);

			foreach (var keylock in doorObj.GetComponentsInChildren<Component>().OfType<IKeyLock>())
				keylock.OnKeyAssigned(key, keyManager);
		}
	}
}