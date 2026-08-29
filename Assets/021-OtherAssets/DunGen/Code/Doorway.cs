using DunGen.Tags;
using DunGen.Weighting;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using DunGen.Versioning;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DunGen
{
	/// <summary>
	/// A component to handle doorway placement and behaviour
	/// </summary>
	[AddComponentMenu("DunGen/Doorway")]
	[HelpURL("https://dungen-docs.aegongames.com/core-concepts/doorways/")]
	public class Doorway : VersionedMonoBehaviour
	{
		public override int LatestVersion => 2;
		public override int DataVersion { get => fileVersion; set => fileVersion = value; }

		public bool HasSocketAssigned { get { return socket != null; } }

		/// <summary>
		/// The socket this doorway uses. Allows you to use different sized doorways and have them connect correctly
		/// </summary>
		public DoorwaySocket Socket
		{
			get
			{
				if (socket == null)
				{
					if (cachedDefaultSocket == null)
						cachedDefaultSocket = DunGenSettings.Instance.DefaultSocket;

					return cachedDefaultSocket;
				}
				else
					return socket;
			}
		}
		/// <summary>
		/// When placing a door prefab, the doorway with the higher priority will have their prefab used
		/// </summary>
		public int DoorPrefabPriority;
		/// <summary>
		/// If true, the chosen Door prefab will not be oriented to match the rotation of the doorway it is placed on
		/// </summary>
		public bool AvoidRotatingDoorPrefab;
		/// <summary>
		/// An optional position offset to apply when spawning a door (connector) prefab, relative to the doorway's transform
		/// </summary>
		public Vector3 DoorPrefabPositionOffset;
		/// <summary>
		/// An optional rotation offset to apply when spawning a door (connector) prefab, relative to the doorway's transform
		/// </summary>
		public Vector3 DoorPrefabRotationOffset;
		/// <summary>
		/// When this doorway is in use, a prefab will be picked at random from this list and is spawned at the doorway location - one per doorways pair (connection)
		/// </summary>
		public WeightedTable<GameObject> ConnectorPrefabs = new WeightedTable<GameObject>();
		/// <summary>
		/// When this doorway is in use, objects in this list will remain in the scene, otherwise, they are destroyed
		/// </summary>
		[FormerlySerializedAs("AddWhenInUse")]
		public List<GameObject> ConnectorSceneObjects = new List<GameObject>();
		/// <summary>
		/// If true, the chosen Blocker prefab will not be oriented to match the rotation of the doorway it is placed on
		/// </summary>
		public bool AvoidRotatingBlockerPrefab;
		/// <summary>
		/// An optional position offset to apply when spawning a blocker prefab, relative to the doorway's transform
		/// </summary>
		public Vector3 BlockerPrefabPositionOffset;
		/// <summary>
		/// An optional rotation offset to apply when spawning a blocker prefab, relative to the doorway's transform
		/// </summary>
		public Vector3 BlockerPrefabRotationOffset;
		/// <summary>
		/// When this doorway is NOT in use, a prefab will be picked at random from this list and is spawned at the doorway location - one per doorway
		/// </summary>
		public WeightedTable<GameObject> BlockerPrefabs = new WeightedTable<GameObject>();
		/// <summary>
		/// When this doorway is NOT in use, objects in this list will remain in the scene, otherwise, they are destroyed
		/// </summary>
		[FormerlySerializedAs("AddWhenNotInUse")]
		public List<GameObject> BlockerSceneObjects = new List<GameObject>();
		/// <summary>
		/// A collection of tags for this doorway. These can be used in code with DoorwayPairFinder.CustomConnectionRules for custom connection logic
		/// </summary>
		public TagContainer Tags = new TagContainer();
		/// <summary>
		/// The Tile that this doorway belongs to
		/// </summary>
		public Tile Tile { get { return tile; } internal set { tile = value; } }
		/// <summary>
		/// The ID of the key used to unlock this door. -1 when the door is not locked
		/// </summary>
		public int LockID = -1;
		/// <summary>
		/// Gets the lock status of the door
		/// </summary>
		public bool IsLocked { get { return LockID >= 0; } }
		/// <summary>
		/// Does this doorway have a prefab object placed as a door?
		/// </summary>
		public bool HasDoorPrefabInstance { get { return doorPrefabInstance != null; } }
		/// <summary>
		/// The prefab that has been placed as a door for this doorway
		/// </summary>
		public GameObject UsedDoorPrefabInstance { get { return doorPrefabInstance; } }
		/// <summary>
		/// The Door component that has been assigned to the door prefab instance (if any)
		/// </summary>
		public Door DoorComponent { get { return doorComponent; } }
		/// <summary>
		/// The dungeon that this doorway belongs to
		/// </summary>
		public Dungeon Dungeon { get; internal set; }
		/// <summary>
		/// The doorway that this is connected to
		/// </summary>
		public Doorway ConnectedDoorway { get { return connectedDoorway; } internal set { connectedDoorway = value; } }
		/// <summary>
		/// Allows for hiding of any GameObject in the "AddWhenInUse" and "AddWhenNotInUse" lists - used to remove clutter at design-time; should not be used at runtime
		/// </summary>
		public bool HideConditionalObjects
		{
			get { return hideConditionalObjects; }
			set
			{
				hideConditionalObjects = value;

				foreach (var obj in ConnectorSceneObjects)
					if (obj != null)
						obj.SetActive(!hideConditionalObjects);

				foreach (var obj in BlockerSceneObjects)
					if (obj != null)
						obj.SetActive(!hideConditionalObjects);
			}
		}

		#region Legacy Properties

#pragma warning disable 0414
		[SerializeField]
		[FormerlySerializedAs("SocketGroup")]
		private DoorwaySocketType socketGroup_obsolete = (DoorwaySocketType)(-1);

		[SerializeField]
		[FormerlySerializedAs("DoorPrefabs")]
		private List<GameObject> doorPrefabs_obsolete = new List<GameObject>();

		[SerializeField]
		[FormerlySerializedAs("BlockerPrefabs")]
		private List<GameObject> blockerPrefabs_obsolete = new List<GameObject>();

		[Obsolete("Obsolete in 2.19. Use ConnectorPrefabs property instead")]
		public List<GameObjectWeight> ConnectorPrefabWeights = new List<GameObjectWeight>();

		[Obsolete("Obsolete in 2.19. Use BlockerPrefabs property instead")]
		public List<GameObjectWeight> BlockerPrefabWeights = new List<GameObjectWeight>();

#pragma warning restore 0414

		#endregion

		[SerializeField]
		private DoorwaySocket socket = null;
		[SerializeField]
		private GameObject doorPrefabInstance;
		[SerializeField]
		private Door doorComponent;
		[SerializeField]
		private Tile tile;
		[SerializeField]
		private Doorway connectedDoorway;
		[SerializeField]
		private bool hideConditionalObjects;
		[SerializeField]
		private GameObject spawnedBlockerPrefab;
		[SerializeField]
		private int fileVersion;

		private DoorwaySocket cachedDefaultSocket;
		internal bool placedByGenerator;



		protected void OnValidate()
		{
			if (socket == null)
				socket = DunGenSettings.Instance.DefaultSocket;
		}

		protected override void Reset()
		{
			base.Reset();

			socket = DunGenSettings.Instance.DefaultSocket;
		}

		public void SetUsedPrefab(GameObject doorPrefabInstance)
		{
			this.doorPrefabInstance = doorPrefabInstance;

			if (doorPrefabInstance != null)
				doorComponent = doorPrefabInstance.GetComponent<Door>();
		}

		internal void RemoveUsedPrefab()
		{
			if (doorPrefabInstance != null)
				UnityUtil.Destroy(doorPrefabInstance);

			doorPrefabInstance = null;
		}

#if UNITY_EDITOR
		private void OnDrawGizmos()
		{
			if (!placedByGenerator)
				DebugDraw();
		}

		internal void DebugDraw()
		{
			Vector2 size = Socket.Size;
			Vector2 halfSize = size * 0.5f;

			bool isValidPlacement = true;
			Color doorwayColour = Color.white;

			isValidPlacement = ValidateTransform(out var localTileBounds, out bool isAxisAligned, out bool isEdgePositioned);

			if (isValidPlacement)
				doorwayColour = EditorConstants.DoorRectColourValid;
			else if (!isAxisAligned)
				doorwayColour = EditorConstants.DoorRectColourError;
			else
				doorwayColour = EditorConstants.DoorRectColourWarning;


			// Draw Forward Vector
			float lineLength = Mathf.Min(size.x, size.y);

			Gizmos.color = EditorConstants.DoorDirectionColour;
			Gizmos.DrawLine(transform.position + transform.up * halfSize.y, transform.position + transform.up * halfSize.y + transform.forward * lineLength);


			// Draw Up Vector
			Gizmos.color = EditorConstants.DoorUpColour;
			Gizmos.DrawLine(transform.position + transform.up * halfSize.y, transform.position + transform.up * size.y);


			// Draw Rectangle
			Gizmos.color = doorwayColour;
			Vector3 topLeft = transform.position - (transform.right * halfSize.x) + (transform.up * size.y);
			Vector3 topRight = transform.position + (transform.right * halfSize.x) + (transform.up * size.y);
			Vector3 bottomLeft = transform.position - (transform.right * halfSize.x);
			Vector3 bottomRight = transform.position + (transform.right * halfSize.x);

			Gizmos.DrawLine(topLeft, topRight);
			Gizmos.DrawLine(topRight, bottomRight);
			Gizmos.DrawLine(bottomRight, bottomLeft);
			Gizmos.DrawLine(bottomLeft, topLeft);


			// Draw position correction line
			if (!isValidPlacement)
			{
				GetTileRoot(out var _, out var tile);

				// Projected position is meaningless if the Doorway isn't attached to a Tile
				if (tile != null)
				{
					Vector3 projectedPosition = ProjectPositionToTileBounds(localTileBounds);

					Gizmos.color = Color.red;
					Gizmos.DrawLine(transform.position, projectedPosition);
				}
			}
		}
#endif

		private void GetTileRoot(out GameObject tileRoot, out Tile tileComponent)
		{
			tileComponent = GetComponentInParent<Tile>();

#if UNITY_EDITOR
			// We might need to walk up the transform hierarchy manually
			if (tileComponent == null)
			{
				Transform current = transform;

				while (current != null)
				{
					tileComponent = current.GetComponent<Tile>();

					if (tileComponent != null)
						break;

					current = current.parent;
				}
			}
#endif

			if (tileComponent != null)
				tileRoot = tileComponent.gameObject;
			else
				tileRoot = transform.root.gameObject;
		}

		public bool ValidateTransform(out Bounds localTileBounds, out bool isAxisAligned, out bool isEdgePositioned)
		{
			GetTileRoot(out var tileRoot, out var tile);

			// The Doorway isn't attached to a Tile, it can never be valid
			if (tileRoot == null || tile == null)
			{
				localTileBounds = new Bounds();
				isAxisAligned = false;
				isEdgePositioned = false;
				return false;
			}

			isAxisAligned = true;
			isEdgePositioned = true;

			if (tile != null && tile.OverrideAutomaticTileBounds)
				localTileBounds = tile.TileBoundsOverride;
			else
				localTileBounds = tile.Placement.LocalBounds;

			if (!UnityUtil.IsVectorAxisAligned(transform.forward))
				isAxisAligned = false;

			Vector3 projectedPosition = ProjectPositionToTileBounds(localTileBounds);

			if ((projectedPosition - transform.position).magnitude > 0.1f)
				isEdgePositioned = false;

			return isAxisAligned && isEdgePositioned;
		}

		public void TrySnapToCorrectedTransform()
		{
			if (ValidateTransform(out var localTileBounds, out _, out _))
				return;

			Vector3 correctedForward = UnityUtil.GetCardinalDirection(transform.forward, out _);

			transform.forward = correctedForward;
			transform.position = ProjectPositionToTileBounds(localTileBounds);
		}

		public Vector3 ProjectPositionToTileBounds(Bounds localTileBounds)
		{
			GetTileRoot(out var tileRoot, out var tile);

			var worldSpaceBounds = tileRoot.transform.TransformBounds(localTileBounds);

			Vector3 correctedForward = UnityUtil.GetCardinalDirection(transform.forward, out var magnitude);
			Vector3 offsetFromBoundsCenter = transform.position - worldSpaceBounds.center;

			// Calculate correction distance along forward vector (snap to edge)
			float currentForwardDistance = Vector3.Dot(correctedForward, offsetFromBoundsCenter);
			float extentForwardDistance = Vector3.Dot(magnitude < 0 ? -correctedForward : correctedForward, worldSpaceBounds.extents);
			float forwardCorrectionDistance = extentForwardDistance - currentForwardDistance;

			Vector3 targetPosition = transform.position;
			targetPosition += correctedForward * forwardCorrectionDistance;

			// Once we're positioned on the correct side of the bounding box based on the forward vector
			// of the doorway, clamp the position to keep it restrained within the bounds along the other axes
			targetPosition = UnityUtil.ClampVector(targetPosition, worldSpaceBounds.min, worldSpaceBounds.max);

			return targetPosition;
		}

		internal void ResetInstanceData()
		{
			if (spawnedBlockerPrefab != null)
				DestroyImmediate(spawnedBlockerPrefab);

			if (doorPrefabInstance != null)
				DestroyImmediate(doorPrefabInstance);

			connectedDoorway = null;
		}

		public void ProcessDoorwayObjects(bool isDoorwayInUse, RandomStream randomStream)
		{
			foreach (var obj in BlockerSceneObjects)
			{
				if (obj != null)
					obj.SetActive(!isDoorwayInUse);
			}

			foreach (var obj in ConnectorSceneObjects)
			{
				if (obj != null)
					obj.SetActive(isDoorwayInUse);
			}

			if (isDoorwayInUse)
			{
				if (spawnedBlockerPrefab != null)
					DestroyImmediate(spawnedBlockerPrefab);
			}
			else
			{
				var selectionContext = new WeightedTable<GameObject>.SelectionContext()
				{
					AllowImmediateRepeats = true,
					AllowNullSelection = true,
					IsOnMainPath = Tile != null && Tile.Placement.IsOnMainPath,
					NormalizedPathDepth = Tile != null ? Tile.Placement.NormalizedPathDepth : 0f,
					NormalizedBranchDepth = Tile != null ? Tile.Placement.NormalizedBranchDepth : 0f,
				};

				// Select a blocker prefab at random and spawn it as a child of the doorway
				if (BlockerPrefabs.TrySelectRandom(out var chosenPrefab, randomStream, selectionContext))
				{
					spawnedBlockerPrefab = Instantiate(chosenPrefab);
					spawnedBlockerPrefab.transform.parent = gameObject.transform;
					spawnedBlockerPrefab.transform.localPosition = BlockerPrefabPositionOffset;
					spawnedBlockerPrefab.transform.localScale = Vector3.one;

					if (AvoidRotatingBlockerPrefab)
						spawnedBlockerPrefab.transform.rotation = Quaternion.Euler(BlockerPrefabRotationOffset);
					else
						spawnedBlockerPrefab.transform.localRotation = Quaternion.Euler(BlockerPrefabRotationOffset);
				}
			}
		}

		protected override void OnMigrate()
		{
#pragma warning disable 0618

			// Convert old object lists to weighted lists
			if (DataVersion < 1)
			{
				foreach (var obj in doorPrefabs_obsolete)
					ConnectorPrefabWeights.Add(new GameObjectWeight(obj));

				foreach (var obj in blockerPrefabs_obsolete)
					BlockerPrefabWeights.Add(new GameObjectWeight(obj));

				doorPrefabs_obsolete.Clear();
				blockerPrefabs_obsolete.Clear();
			}

			// Migrate to new weighted table structure for doorway prefabs
			if (DataVersion < 2)
			{
				foreach (var weight in ConnectorPrefabWeights)
					ConnectorPrefabs.Entries.Add(new WeightedEntry<GameObject>()
					{
						Value = weight.GameObject,
						MainPathWeight = weight.Weight,
						BranchPathWeight = weight.Weight,
					});

				foreach (var weight in BlockerPrefabWeights)
					BlockerPrefabs.Entries.Add(new WeightedEntry<GameObject>()
					{
						Value = weight.GameObject,
						MainPathWeight = weight.Weight,
						BranchPathWeight = weight.Weight,
					});
			}

#pragma warning restore 0618
		}
	}
}