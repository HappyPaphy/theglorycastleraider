using DunGen.Graph;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen
{
	public struct ProxyDoorwayConnection
	{
		public DoorwayProxy A { get; private set; }
		public DoorwayProxy B { get; private set; }


		public ProxyDoorwayConnection(DoorwayProxy a, DoorwayProxy b)
		{
			A = a;
			B = b;
		}
	}

	public sealed class DungeonProxy
	{
		public List<TileProxy> AllTiles = new List<TileProxy>();
		public List<TileProxy> MainPathTiles = new List<TileProxy>();
		public List<TileProxy> BranchPathTiles = new List<TileProxy>();
		public List<ProxyDoorwayConnection> Connections = new List<ProxyDoorwayConnection>();

		private Transform visualsRoot;
		private Dictionary<TileProxy, GameObject> tileVisuals = new Dictionary<TileProxy, GameObject>();


		public DungeonProxy(Transform debugVisualsRoot = null)
		{
			visualsRoot = debugVisualsRoot;
		}

		public void ClearDebugVisuals()
		{
			var instances = tileVisuals.Values.ToArray();

			foreach (var instance in instances)
				GameObject.DestroyImmediate(instance);

			tileVisuals.Clear();
		}

		public void MakeConnection(DoorwayProxy a, DoorwayProxy b)
		{
			Debug.Assert(a != null && b != null);
			Debug.Assert(a != b);
			Debug.Assert(!a.Used && !b.Used);

			DoorwayProxy.Connect(a, b);
			var conn = new ProxyDoorwayConnection(a, b);
			Connections.Add(conn);
		}

		public void RemoveLastConnection()
		{
			Debug.Assert(Connections.Any(), "No connections to remove");

			RemoveConnection(Connections.Last());
		}

		public void RemoveConnectionsToTile(TileProxy tile)
		{
			for (int i = Connections.Count - 1; i >= 0; i--)
			{
				var connection = Connections[i];

				if (connection.A.TileProxy == tile || connection.B.TileProxy == tile)
					RemoveConnection(connection);
			}
		}

		public void RemoveConnection(ProxyDoorwayConnection connection)
		{
			connection.A.Disconnect();
			Connections.Remove(connection);
		}

		public void CalculateDepths()
		{
			// Main Path
			int mainPathLength = MainPathTiles.Count;

			for (int i = 0; i < mainPathLength; i++)
			{
				var tile = MainPathTiles[i];

				tile.Placement.PathDepth = i;
				tile.Placement.NormalizedPathDepth = mainPathLength > 1 ? (float)i / (mainPathLength - 1) : 1f;

				tile.Placement.BranchDepth = 0;
				tile.Placement.NormalizedBranchDepth = 0f;
			}

			// Branch Paths
			var branches = new Dictionary<int, List<TileProxy>>();

			// Gather branch tiles
			foreach (var tile in BranchPathTiles)
			{
				int branchIndex = tile.Placement.BranchId;

				if (!branches.TryGetValue(branchIndex, out var branchTileList))
				{
					branchTileList = new List<TileProxy>();
					branches[branchIndex] = branchTileList;
				}

				branchTileList.Add(tile);
			}

			// Update depths for each branch
			foreach(var branch in branches.Values)
			{
				int branchLength = branch.Count;

				for (int i = 0; i < branchLength; i++)
				{
					var tile = branch[i];

					tile.Placement.BranchDepth = i;
					tile.Placement.NormalizedBranchDepth = branchLength > 1 ? (float)i / (branchLength - 1) : 1f;

					tile.Placement.NormalizedPathDepth = (float)tile.Placement.PathDepth / (mainPathLength - 1);
				}
			}
		}

		internal void AddTile(TileProxy tile)
		{
			AllTiles.Add(tile);

			if (tile.Placement.IsOnMainPath)
				MainPathTiles.Add(tile);
			else
				BranchPathTiles.Add(tile);

			if(visualsRoot != null)
			{
				var tileObj = GameObject.Instantiate(tile.Prefab, visualsRoot);
				tileObj.name = $"DEBUG_VISUALS_{tile.Prefab.name}";
				tileObj.transform.localPosition = tile.Placement.Position;
				tileObj.transform.localRotation = tile.Placement.Rotation;

				tileVisuals[tile] = tileObj;
			}
		}

		internal void RemoveTile(TileProxy tile)
		{
			AllTiles.Remove(tile);

			if (tile.Placement.IsOnMainPath)
				MainPathTiles.Remove(tile);
			else
				BranchPathTiles.Remove(tile);

			if (tileVisuals.TryGetValue(tile, out var tileInstance))
			{
				GameObject.DestroyImmediate(tileInstance);
				tileVisuals.Remove(tile);
			}
		}

		internal void ConnectOverlappingDoorways(float globalChance, DungeonFlow dungeonFlow, RandomStream randomStream)
		{
			const float epsilon = 0.00001f;
			var doorways = AllTiles.SelectMany(t => t.UnusedDoorways).ToArray();
			float cellSize = 1f;

			// Build spatial grid
			var grid = new Dictionary<Vector3Int, List<DoorwayProxy>>();
			foreach (var doorway in doorways)
			{
				var pos = doorway.Position;

				var cell = new Vector3Int(
					Mathf.FloorToInt(pos.x / cellSize),
					Mathf.FloorToInt(pos.y / cellSize),
					Mathf.FloorToInt(pos.z / cellSize)
				);

				if (!grid.TryGetValue(cell, out var list))
					grid[cell] = list = new List<DoorwayProxy>();

				list.Add(doorway);
			}

			var checkedPairs = new HashSet<(DoorwayProxy, DoorwayProxy)>();

			foreach (var doorway in doorways)
			{
				var pos = doorway.Position;

				var cell = new Vector3Int(
					Mathf.FloorToInt(pos.x / cellSize),
					Mathf.FloorToInt(pos.y / cellSize),
					Mathf.FloorToInt(pos.z / cellSize)
				);

				// Check this cell and all adjacent cells
				for (int dx = -1; dx <= 1; dx++)
				for (int dy = -1; dy <= 1; dy++)
				for (int dz = -1; dz <= 1; dz++)
				{
					var neighbourCell = cell + new Vector3Int(dx, dy, dz);

					if (grid.TryGetValue(neighbourCell, out var candidates))
					{
						foreach (var other in candidates)
						{
							if (doorway == other || doorway.TileProxy == other.TileProxy)
								continue;

							if (doorway.Used || other.Used)
								continue;

							// Avoid duplicate checks
							if (checkedPairs.Contains((doorway, other)) || checkedPairs.Contains((other, doorway)))
								continue;

							checkedPairs.Add((doorway, other));

							float distanceSqrd = (doorway.Position - other.Position).sqrMagnitude;

							if (distanceSqrd >= epsilon)
								continue;

							var proposedConnection = new ProposedConnection(this, doorway.TileProxy, other.TileProxy, doorway, other);

							// These doors cannot be connected due to their sockets or other connection rules
							if (!dungeonFlow.CanDoorwaysConnect(proposedConnection))
								continue;

							if (dungeonFlow.RestrictConnectionToSameSection)
							{
								bool tilesAreOnSameLineSegment = doorway.TileProxy.Placement.GraphLine == other.TileProxy.Placement.GraphLine;

								// The tiles are not on a line segment
								if (doorway.TileProxy.Placement.GraphLine == null)
									tilesAreOnSameLineSegment = false;

								if (!tilesAreOnSameLineSegment)
									continue;
							}

							float chance = globalChance;

							// Allow tiles to override the global connection chance
							// If both tiles want to override the connection chance, use the lowest value
							if (doorway.TileProxy.PrefabTile.OverrideConnectionChance && other.TileProxy.PrefabTile.OverrideConnectionChance)
								chance = Mathf.Min(doorway.TileProxy.PrefabTile.ConnectionChance, other.TileProxy.PrefabTile.ConnectionChance);
							else if (doorway.TileProxy.PrefabTile.OverrideConnectionChance)
								chance = doorway.TileProxy.PrefabTile.ConnectionChance;
							else if (other.TileProxy.PrefabTile.OverrideConnectionChance)
								chance = other.TileProxy.PrefabTile.ConnectionChance;

							// There is no chance to connect these doorways
							if (chance <= 0f)
								continue;

							if (randomStream.NextDouble() < chance)
								MakeConnection(doorway, other);
						}
					}
				}
			}
		}
	}
}
