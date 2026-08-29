using DunGen.Generation;
using DunGen.Graph;
using DunGen.Placement;
using DunGen.Tags;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace DunGen
{
	public delegate void DungeonTileInstantiatedDelegate(Dungeon dungeon, Tile newTile, int currentTileCount, int totalTileCount);

	public class Dungeon : MonoBehaviour
	{
		public static event DungeonTileInstantiatedDelegate TileInstantiated;

		#region Nested Types

		[Serializable]
		public sealed class Branch
		{
			[SerializeField]
			private int index;

			[SerializeField]
			private List<Tile> tiles = new List<Tile>();

			[NonSerialized]
			private ReadOnlyCollection<Tile> readOnlyTiles;

			public int Index => index;

			public ReadOnlyCollection<Tile> Tiles => readOnlyTiles ?? (readOnlyTiles = tiles.AsReadOnly());

			public Branch(int index, List<Tile> tiles)
			{
				this.index = index;
				this.tiles = tiles ?? new List<Tile>();
			}
		}

		#endregion

		#region Legacy Properties

		[Obsolete("Use 'DebugRenderSettings' instead")]
		public bool DebugRender = false;

		#endregion

		/// <summary>
		/// World-space bounding box of the entire dungeon
		/// </summary>
		public Bounds Bounds { get; protected set; }

		/// <summary>
		/// The dungeon flow asset used to generate this dungeon
		/// </summary>
		public DungeonFlow DungeonFlow
		{
			get => dungeonFlow;
			set => dungeonFlow = value;
		}

		public Tile AttachmentTile
		{
			get => attachmentTile;
			set => attachmentTile = value;
		}

		public DebugRenderSettings DebugRenderSettings { get; set; } = new DebugRenderSettings();

		public ReadOnlyCollection<Tile> AllTiles { get; }
		public ReadOnlyCollection<Tile> MainPathTiles { get; }
		public ReadOnlyCollection<Tile> BranchPathTiles { get; }
		public ReadOnlyCollection<GameObject> Doors { get; }
		public ReadOnlyCollection<DoorwayConnection> Connections { get; }
		public ReadOnlyCollection<Branch> Branches { get; }
		public DungeonGraph ConnectionGraph { get; private set; }

		public TileInstanceSource TileInstanceSource { get; internal set; }

		[SerializeField]
		private DungeonFlow dungeonFlow;
		[SerializeField]
		private List<Tile> allTiles = new List<Tile>();
		[SerializeField]
		private List<Tile> mainPathTiles = new List<Tile>();
		[SerializeField]
		private List<Tile> branchPathTiles = new List<Tile>();
		[SerializeField]
		private List<GameObject> doors = new List<GameObject>();
		[SerializeField]
		private List<DoorwayConnection> connections = new List<DoorwayConnection>();
		[SerializeField]
		private Tile attachmentTile;
		[SerializeField]
		private List<Branch> branches = new List<Branch>();


		public Dungeon()
		{
			AllTiles = new ReadOnlyCollection<Tile>(allTiles);
			MainPathTiles = new ReadOnlyCollection<Tile>(mainPathTiles);
			BranchPathTiles = new ReadOnlyCollection<Tile>(branchPathTiles);
			Doors = new ReadOnlyCollection<GameObject>(doors);
			Connections = new ReadOnlyCollection<DoorwayConnection>(connections);
			Branches = new ReadOnlyCollection<Branch>(branches);
		}

		private void Start()
		{
			// If there are already tiles and the connection graph isn't initialised yet,
			// this script is likely already present in the scene (from generating the dungeon in-editor).
			// We just need to finalise the dungeon info from data we already have available
			if (allTiles.Count > 0 && ConnectionGraph == null)
				Finalise();
		}

		public IEnumerable<Tile> FindTilesWithTag(Tag tag) => allTiles.Where(t => t.Tags.HasTag(tag));

		public IEnumerable<Tile> FindTilesWithAnyTag(params Tag[] tags) => allTiles.Where(t => t.Tags.HasAnyTag(tags));

		public IEnumerable<Tile> FindTilesWithAllTags(params Tag[] tags) => allTiles.Where(t => t.Tags.HasAllTags(tags));


		#region Builder Methods

		public void AddTile(Tile tile)
		{
			allTiles.Add(tile);

			if (tile.Placement.IsOnMainPath)
				mainPathTiles.Add(tile);
			else
				branchPathTiles.Add(tile);
		}

		public void AddConnection(DoorwayConnection connection)
		{
			connections.Add(connection);
		}

		public void AddDoor(GameObject door)
		{
			if (door != null && !doors.Contains(door.gameObject))
				doors.Add(door.gameObject);
		}

		public void NotifyTileInstantiated(in GenerationContext context, Tile newTile)
		{
			TileInstantiated?.Invoke(this, newTile, allTiles.Count, context.ProxyDungeon.AllTiles.Count);
		}

		public void Finalise()
		{
			var additionalTiles = new List<Tile>();

			if (attachmentTile != null)
				additionalTiles.Add(attachmentTile);

			ConnectionGraph = new DungeonGraph(this, additionalTiles);
			Bounds = UnityUtil.CombineBounds(allTiles.Select(x => x.Placement.Bounds).ToArray());
			GatherBranches();
		}

		#endregion

		/// <summary>
		/// Gathers all branches into lists for easy access
		/// </summary>
		private void GatherBranches()
		{
			var branchTiles = new Dictionary<int, List<Tile>>();

			// Gather branch tiles
			foreach (var branchTile in branchPathTiles)
			{
				int branchIndex = branchTile.Placement.BranchId;

				if (!branchTiles.TryGetValue(branchIndex, out var branchTileList))
				{
					branchTileList = new List<Tile>();
					branchTiles[branchIndex] = branchTileList;
				}

				branchTileList.Add(branchTile);
			}

			// Create branch objects
			foreach (var kvp in branchTiles)
			{
				int index = kvp.Key;
				var tiles = kvp.Value;

				branches.Add(new Branch(index, tiles));
			}
		}

		public void Clear()
		{
			Clear(TileInstanceSource.DespawnTile);
		}

		public void Clear(Action<Tile> destroyTileDelegate)
		{
			// Destroy all tiles
			foreach (var tile in allTiles)
				destroyTileDelegate(tile);

			// Destroy anything else attached to this dungeon
			for (int i = transform.childCount - 1; i >= 0; i--)
			{
				GameObject child = transform.GetChild(i).gameObject;
				UnityUtil.Destroy(child);
			}

			allTiles.Clear();
			mainPathTiles.Clear();
			branchPathTiles.Clear();
			doors.Clear();
			connections.Clear();
			branches.Clear();
			attachmentTile = null;
		}

		public Doorway GetConnectedDoorway(Doorway doorway)
		{
			foreach (var conn in connections)
				if (conn.A == doorway)
					return conn.B;
				else if (conn.B == doorway)
					return conn.A;

			return null;
		}

		public void OnDrawGizmos()
		{
			if (!DebugRenderSettings.Enabled)
				return;

			if (DebugRenderSettings.ShowPathColours)
				DebugDrawPathColours();
		}

		private void DebugDrawPathColours()
		{
			Color mainPathStartColour = Color.red;
			Color mainPathEndColour = Color.green;
			Color branchPathStartColour = Color.blue;
			Color branchPathEndColour = new Color(0.5f, 0, 0.5f);
			float boundsBoxOpacity = 0.75f;

			foreach (var tile in allTiles)
			{
				Bounds bounds = tile.Placement.Bounds;
				bounds.size = bounds.size * 1.01f;

				Color tileColour = (tile.Placement.IsOnMainPath) ?
									Color.Lerp(mainPathStartColour, mainPathEndColour, tile.Placement.NormalizedDepth) :
									Color.Lerp(branchPathStartColour, branchPathEndColour, tile.Placement.NormalizedDepth);

				tileColour.a = boundsBoxOpacity;
				Gizmos.color = tileColour;

				Gizmos.DrawCube(bounds.center, bounds.size);
			}
		}
	}
}
