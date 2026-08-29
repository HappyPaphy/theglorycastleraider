using DunGen.Pooling;
using DunGen.Tags;
using DunGen.TileBounds;
using DunGen.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace DunGen
{
	[AddComponentMenu("DunGen/Tile")]
	[DisallowMultipleComponent]
	[HelpURL("https://dungen-docs.aegongames.com/core-concepts/tiles/")]
	public class Tile : VersionedMonoBehaviour
	{
		public override int LatestVersion => 3;
		public override int DataVersion { get => fileVersion; set => fileVersion = value; }

		#region Legacy Properties

		// Legacy properties only exist to avoid breaking existing projects
		// Converting old data structures over to the new ones

		[SerializeField]
		[FormerlySerializedAs("AllowImmediateRepeats")]
		private bool allowImmediateRepeats = true;

		[SerializeField]
		[Obsolete("'Entrance' is no longer used. Please use the 'Entrances' list instead", false)]
		public Doorway Entrance;

		[SerializeField]
		[Obsolete("'Exit' is no longer used. Please use the 'Exits' list instead", false)]
		public Doorway Exit;

		#endregion

		/// <summary>
		/// Should this tile be allowed to rotate to fit in place?
		/// </summary>
		public bool AllowRotation = true;

		/// <summary>
		/// Should this tile be allowed to be placed next to another instance of itself?
		/// </summary>
		public TileRepeatMode RepeatMode = TileRepeatMode.Allow;

		/// <summary>
		/// Should the automatically generated tile bounds be overridden with a user-defined value?
		/// </summary>
		public bool OverrideAutomaticTileBounds = false;

		/// <summary>
		/// Optional tile bounds to override the automatically calculated tile bounds
		/// </summary>
		public Bounds TileBoundsOverride = new Bounds(Vector3.zero, Vector3.one);

		/// <summary>
		/// An optional collection of entrance doorways. DunGen will try to use one of these doorways as the entrance to the tile if possible
		/// </summary>
		public List<Doorway> Entrances = new List<Doorway>();

		/// <summary>
		/// An optional collection of exit doorways. DunGen will try to use one of these doorways as the exit to the tile if possible
		/// </summary>
		public List<Doorway> Exits = new List<Doorway>();

		/// <summary>
		/// Should this tile override the connection chance globally defined in the DungeonFlow?
		/// </summary>
		public bool OverrideConnectionChance = false;

		/// <summary>
		/// The overridden connection chance value. Only used if <see cref="OverrideConnectionChance"/> is true.
		/// If both tiles have overridden the connection chance, the lowest value is used
		/// </summary>
		public float ConnectionChance = 0f;

		/// <summary>
		/// A collection of tags for this tile. Can be used with the dungeon flow asset to restrict which
		/// tiles can be attached
		/// </summary>
		public TagContainer Tags = new TagContainer();

		/// <summary>
		/// The calculated world-space bounds of this Tile
		/// </summary>
		[HideInInspector]
		public Bounds Bounds { get { return transform.TransformBounds(Placement.LocalBounds); } }

		/// <summary>
		/// Information about the tile's position in the generated dungeon
		/// </summary>
		public TilePlacementData Placement
		{
			get { return placement; }
			internal set { placement = value; }
		}
		/// <summary>
		/// The dungeon that this tile belongs to
		/// </summary>
		public Dungeon Dungeon { get; internal set; }

		public List<Doorway> AllDoorways = new List<Doorway>();
		public List<Doorway> UsedDoorways = new List<Doorway>();
		public List<Doorway> UnusedDoorways = new List<Doorway>();
		public GameObject Prefab { get; internal set; }
		public bool HasValidBounds => Placement != null && Placement.LocalBounds.extents.sqrMagnitude > 0f;

		[SerializeField]
		private TilePlacementData placement;
		[SerializeField]
		private int fileVersion;

		private readonly List<ITileSpawnEventReceiver> spawnEventReceivers = new List<ITileSpawnEventReceiver>();


		public void RefreshTileEventReceivers()
		{
			spawnEventReceivers.Clear();
			GetComponentsInChildren(true, spawnEventReceivers);
		}

		internal void TileSpawned()
		{
			foreach (var receiver in spawnEventReceivers)
				receiver.OnTileSpawned(this);
		}

		internal void TileDespawned()
		{
			Dungeon = null;

			foreach (var doorway in AllDoorways)
				doorway.ResetInstanceData();

			placement.SetPositionAndRotation(Vector2.zero, Quaternion.identity);

			UsedDoorways.Clear();
			UnusedDoorways.Clear();

			foreach(var receiver in spawnEventReceivers)
				receiver.OnTileDespawned(this);
		}

		private void OnTriggerEnter(Collider other)
		{
			if (other == null)
				return;

			if (other.gameObject.TryGetComponent<DungenCharacter>(out var character))
				character.OnTileEntered(this);
		}

		private void OnTriggerEnter2D(Collider2D other)
		{
			if (other == null)
				return;

			if (other.gameObject.TryGetComponent<DungenCharacter>(out var character))
				character.OnTileEntered(this);
		}

		private void OnTriggerExit(Collider other)
		{
			if (other == null)
				return;

			if (other.gameObject.TryGetComponent<DungenCharacter>(out var character))
				character.OnTileExited(this);
		}
		private void OnTriggerExit2D(Collider2D other)
		{
			if (other == null)
				return;

			if (other.gameObject.TryGetComponent<DungenCharacter>(out var character))
				character.OnTileExited(this);
		}

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.red;
			Bounds? bounds = null;

			if (OverrideAutomaticTileBounds)
				bounds = transform.TransformBounds(TileBoundsOverride);
			else if (placement != null)
				bounds = Bounds;

			if (bounds.HasValue)
				Gizmos.DrawWireCube(bounds.Value.center, bounds.Value.size);
		}

		public IEnumerable<Tile> GetAdjacentTiles()
		{
			return UsedDoorways.Select(x => x.ConnectedDoorway.Tile).Distinct();
		}

		public bool IsAdjacentTo(Tile other)
		{
			foreach (var door in UsedDoorways)
				if (door.ConnectedDoorway.Tile == other)
					return true;

			return false;
		}

		public Doorway GetEntranceDoorway()
		{
			foreach (var doorway in UsedDoorways)
			{
				var connectedTile = doorway.ConnectedDoorway.Tile;

				if (Placement.IsOnMainPath)
				{
					if (connectedTile.Placement.IsOnMainPath && Placement.PathDepth > connectedTile.Placement.PathDepth)
						return doorway;
				}
				else
				{
					if (connectedTile.Placement.IsOnMainPath || Placement.Depth > connectedTile.Placement.Depth)
						return doorway;
				}
			}

			return null;
		}

		public Doorway GetExitDoorway()
		{
			foreach (var doorway in UsedDoorways)
			{
				var connectedTile = doorway.ConnectedDoorway.Tile;

				if (Placement.IsOnMainPath)
				{
					if (connectedTile.Placement.IsOnMainPath && Placement.PathDepth < connectedTile.Placement.PathDepth)
						return doorway;
				}
				else
				{
					if (!connectedTile.Placement.IsOnMainPath && Placement.Depth < connectedTile.Placement.Depth)
						return doorway;
				}
			}

			return null;
		}

		/// <summary>
		/// Retrieves the tile bounds calculator to use for this Tile.
		/// </summary>
		/// <remarks>If a component implementing <see cref="ITileBoundsCalculator"/> is attached to this object,
		/// it will be used instead of the global default.</remarks>
		/// <returns>An <see cref="ITileBoundsCalculator"/> instance that determines how tile bounds are calculated. Returns an
		/// override component if present; otherwise, returns the global default from settings.</returns>
		public ITileBoundsCalculator GetBoundsCalculator()
		{
			// Look for an override component first
			if(TryGetComponent<ITileBoundsCalculator>(out var boundsCalculator))
				return boundsCalculator;

			// Fallback to the global settings
			var settings = DunGenSettings.Instance;
			return settings.BoundsCalculator;
		}

		/// <summary>
		/// Recalculates the Tile's bounds based on the geometry inside the prefab
		/// </summary>
		/// <returns>True if the bounds changed when recalculated</returns>
		public bool RecalculateBounds()
		{
			Placement ??= new TilePlacementData();

			var oldBounds = Placement.LocalBounds;

			if (OverrideAutomaticTileBounds)
				Placement.LocalBounds = TileBoundsOverride;
			else
				Placement.LocalBounds = GetBoundsCalculator().CalculateLocalBounds(gameObject);

			var newBounds = Placement.LocalBounds;
			bool haveBoundsChanged = newBounds != oldBounds;

			// Let the user know that the tile's bounds are invalid
			if (newBounds.size.x <= 0f || newBounds.size.y <= 0f || newBounds.size.z <= 0f)
				Debug.LogError($"Tile prefab '{gameObject.name}' has automatic bounds that are zero or negative in size. The bounding volume for this tile will need to be manually defined.", gameObject);

			return haveBoundsChanged;
		}

		public void CopyBoundsFrom(Tile otherTile)
		{
			if (otherTile == null)
				return;

			if(Placement == null)
				Placement = new TilePlacementData();

			Placement.LocalBounds = otherTile.Placement.LocalBounds;
		}

		protected override void OnMigrate()
		{
#pragma warning disable 618

			// AllowImmediateRepeats (bool) -> TileRepeatMode (enum)
			if (DataVersion < 1)
				RepeatMode = (allowImmediateRepeats) ? TileRepeatMode.Allow : TileRepeatMode.DisallowImmediate;

			// Converted individual Entrance and Exit doorways to collections
			if (DataVersion < 2)
			{
				if (Entrances == null)
					Entrances = new List<Doorway>();

				if (Exits == null)
					Exits = new List<Doorway>();

				if (Entrance != null)
					Entrances.Add(Entrance);

				if(Exit != null)
					Exits.Add(Exit);

				Entrance = null;
				Exit = null;
			}

#pragma warning restore 618
		}
	}
}
