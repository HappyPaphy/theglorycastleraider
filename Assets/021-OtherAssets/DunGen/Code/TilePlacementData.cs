using DunGen.Graph;
using System;
using UnityEngine;

namespace DunGen
{
	/// <summary>
	/// A container for all of the information about a tile's positioning in the generated dungeon
	/// </summary>
	[Serializable]
	public sealed class TilePlacementData
	{
		/// <summary>
		/// Gets the depth of this tile in the dungeon along the main path
		/// </summary>
		public int PathDepth
		{
			get { return pathDepth; }
			set { pathDepth = value; }
		}
		/// <summary>
		/// Gets the normalized depth (0.0-1.0) of this tile in the dungeon along the main path
		/// </summary>
		public float NormalizedPathDepth
		{
			get { return normalizedPathDepth; }
			set { normalizedPathDepth = value; }
		}
		/// <summary>
		/// Gets the depth of this tile in the dungeon along the branch it's on
		/// </summary>
		public int BranchDepth
		{
			get { return branchDepth; }
			set { branchDepth = value; }
		}
		/// <summary>
		/// Gets the normalized depth (0.0-1.0) of this tile in the dungeon along the branch it's on
		/// </summary>
		public float NormalizedBranchDepth
		{
			get { return normalizedBranchDepth; }
			set { normalizedBranchDepth = value; }
		}
		/// <summary>
		/// An ID representing which branch the tile belongs to. All tiles on the same branch will share an ID.
		/// Will be -1 for tiles not on a branch
		/// </summary>
		public int BranchId
		{
			get { return branchId;  }
			set { branchId = value; }
		}
		/// <summary>
		/// Whether or not this tile lies on the dungeon's main path
		/// </summary>
		public bool IsOnMainPath
		{
			get { return isOnMainPath; }
			set { isOnMainPath = value; }
		}

		/// <summary>
		/// The boundaries of this tile
		/// </summary>
		public Bounds Bounds
		{
			get => worldBounds;
			private set => worldBounds = value;
		}

		/// <summary>
		/// The local boundaries of this tile
		/// </summary>
		public Bounds LocalBounds
		{
			get { return localBounds; }
			set
			{
				localBounds = value;
				RecalculateTransform();
			}
		}

		public TilePlacementParameters PlacementParameters
		{
			get { return placementParameters; }
			set { placementParameters = value; }
		}

		public GraphNode GraphNode => placementParameters.Node;

		public GraphLine GraphLine =>  placementParameters.Line;

		public DungeonArchetype Archetype => placementParameters.Archetype;

		public TileSet TileSet
		{
			get { return tileSet; }
			set { tileSet = value; }
		}

		public Vector3 Position
		{
			get { return position; }
			set
			{
				position = value;
				RecalculateTransform();
			}
		}
		public Quaternion Rotation
		{
			get { return rotation; }
			set
			{
				rotation = value;
				RecalculateTransform();
			}
		}
		public Matrix4x4 Transform { get; private set; }

		/// <summary>
		/// Gets the depth of this tile. Returns the branch depth if on a branch path, otherwise, returns the main path depth
		/// </summary>
		public int Depth { get { return (isOnMainPath) ? pathDepth : branchDepth; } }

		/// <summary>
		/// Gets the normalized depth (0-1) of this tile. Returns the branch depth if on a branch path, otherwise, returns the main path depth
		/// </summary>
		public float NormalizedDepth { get { return (isOnMainPath) ? normalizedPathDepth : normalizedBranchDepth; } }

		/// <summary>
		/// Data about how this tile was injected, or null if it was not placed using tile injection
		/// </summary>
		public InjectedTile InjectionData { get; set; }


		[SerializeField]
		private int pathDepth;
		[SerializeField]
		private float normalizedPathDepth;
		[SerializeField]
		private int branchDepth;
		[SerializeField]
		private float normalizedBranchDepth;
		[SerializeField]
		private int branchId = -1;
		[SerializeField]
		private bool isOnMainPath;
		[SerializeField]
		private Bounds localBounds;
		[SerializeField]
		private Bounds worldBounds;
		[SerializeField]
		private TilePlacementParameters placementParameters;
		[SerializeField]
		private TileSet tileSet;
		[SerializeField]
		private Vector3 position = Vector3.zero;
		[SerializeField]
		private Quaternion rotation = Quaternion.identity;


		public TilePlacementData()
		{
			RecalculateTransform();
		}

		public TilePlacementData(TilePlacementData copy)
		{
			PathDepth = copy.PathDepth;
			NormalizedPathDepth = copy.NormalizedPathDepth;
			BranchDepth = copy.BranchDepth;
			NormalizedBranchDepth = copy.NormalizedDepth;
			BranchId = copy.BranchId;
			IsOnMainPath = copy.IsOnMainPath;
			LocalBounds = copy.LocalBounds;
			Transform = copy.Transform;
			PlacementParameters = copy.PlacementParameters;
			TileSet = copy.TileSet;
			InjectionData = copy.InjectionData;

			position = copy.position;
			rotation = copy.rotation;

			RecalculateTransform();
		}

		public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
		{
			this.position = position;
			this.rotation = rotation;

			RecalculateTransform();
		}

		private void RecalculateTransform()
		{
			Transform = Matrix4x4.TRS(position, rotation, Vector3.one);
			Bounds = Transform.TransformBounds(localBounds);
		}
	}
}

