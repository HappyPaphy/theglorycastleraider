using DunGen.Common;
using DunGen.Tags;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace DunGen
{
	/// <summary>
	/// Represents a proxy for a doorway within a tile, providing access to its spatial properties, connection state,
	/// and associated metadata.
	/// </summary>
	public sealed class DoorwayProxy
	{
		/// <summary>
		/// Is this doorway connected to another doorway?
		/// </summary>
		public bool Used { get { return ConnectedDoorway != null; } }

		/// <summary>
		/// The tile proxy instance that this doorway belongs to
		/// </summary>
		public TileProxy TileProxy { get; private set; }

		/// <summary>
		/// A stable doorway index that corresponds to the doorway's position within the tile prefab's doorway collection
		/// </summary>
		public int Index { get; private set; }

		/// <summary>
		/// The doorway socket type of this doorway
		/// </summary>
		public DoorwaySocket Socket { get; private set; }

		/// <summary>
		/// The actual doorway component on the tile prefab
		/// </summary>
		public Doorway DoorwayComponent { get; private set; }

		/// <summary>
		/// The local position of this doorway relative to the tile's origin
		/// </summary>
		public Vector3 LocalPosition { get; private set; }

		/// <summary>
		/// The local rotation of this doorway relative to the tile's origin
		/// </summary>
		public Quaternion LocalRotation { get; private set; }

		/// <summary>
		/// Which doorway this doorway is connected to, or null if unconnected
		/// </summary>
		public DoorwayProxy ConnectedDoorway { get; private set; }

		/// <summary>
		/// The forward direction of this doorway in world space
		/// </summary>
		public Vector3 Forward { get { return (TileProxy.Placement.Rotation * LocalRotation) * Vector3.forward; } }

		/// <summary>
		/// The up direction of this doorway in world space
		/// </summary>
		public Vector3 Up { get { return (TileProxy.Placement.Rotation * LocalRotation) * Vector3.up; } }

		/// <summary>
		/// Gets the world-space position of the tile as a 3D vector.
		/// </summary>
		public Vector3 Position { get { return TileProxy.Placement.Transform.MultiplyPoint(LocalPosition); } }

		/// <summary>
		/// A collection of tags that belong to this doorway
		/// </summary>
		public TagContainer Tags { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the current instance is disabled. Disabled doorways are not
		/// considered for connections during dungeon generation.
		/// </summary>
		public bool IsDisabled { get; internal set; }

		/// <summary>
		/// Is this doorway a designated entrance of the owning tile?
		/// </summary>
		public bool IsEntrance { get; }

		/// <summary>
		/// Is this doorway a designated exit of the owning tile?
		/// </summary>
		public bool IsExit { get; }


		public DoorwayProxy(TileProxy tileProxy, DoorwayProxy other)
		{
			TileProxy = tileProxy;
			Index = other.Index;
			Socket = other.Socket;
			DoorwayComponent = other.DoorwayComponent;
			LocalPosition = other.LocalPosition;
			LocalRotation = other.LocalRotation;
			Tags = new TagContainer(other.Tags);
			IsEntrance = other.IsEntrance;
			IsExit = other.IsExit;
		}

		public DoorwayProxy(TileProxy tileProxy, int index, Doorway doorwayComponent, Vector3 localPosition, Quaternion localRotation, bool isEntrance, bool isExit)
		{
			TileProxy = tileProxy;
			Index = index;
			Socket = doorwayComponent.Socket;
			DoorwayComponent = doorwayComponent;
			LocalPosition = localPosition;
			LocalRotation = localRotation;
			Tags = new TagContainer(doorwayComponent.Tags);

			IsEntrance = isEntrance;
			IsExit = isExit;
		}

		public static void Connect(DoorwayProxy a, DoorwayProxy b)
		{
			Debug.Assert(a.ConnectedDoorway == null, "Doorway 'a' is already connected to something");
			Debug.Assert(b.ConnectedDoorway == null, "Doorway 'b' is already connected to something");

			a.ConnectedDoorway = b;
			b.ConnectedDoorway = a;
		}

		public void Disconnect()
		{
			if (ConnectedDoorway == null)
				return;

			ConnectedDoorway.ConnectedDoorway = null;
			ConnectedDoorway = null;
		}
	}

	/// <summary>
	/// Represents a proxy for a tile prefab, providing access to important metadata used during dungeon generation.
	/// This class is spawned by the dungeon generator instead of using the actual tile prefabs directly.
	/// </summary>
	public sealed class TileProxy
	{
		/// <summary>
		/// The actual tile prefab to spawn when the dungeon is realized
		/// </summary>
		public GameObject Prefab { get; private set; }

		/// <summary>
		/// The tile component on the prefab
		/// </summary>
		public Tile PrefabTile { get; private set; }

		/// <summary>
		/// Information about where this tile instance is placed in the dungeon
		/// </summary>
		public TilePlacementData Placement { get; internal set; }

		/// <summary>
		/// A collection of all entrance doorways on this tile
		/// </summary>
		public List<DoorwayProxy> Entrances { get; private set; }

		/// <summary>
		/// A collection of all exit doorways on this tile
		/// </summary>
		public List<DoorwayProxy> Exits { get; private set; }

		/// <summary>
		/// A collection of all doorways on this tile
		/// </summary>
		public ReadOnlyCollection<DoorwayProxy> Doorways { get; private set; }

		/// <summary>
		/// A collection of all doorways on this tile that are currently used (connected to another tile)
		/// </summary>
		public IEnumerable<DoorwayProxy> UsedDoorways { get { return doorways.Where(d => d.Used); } }

		/// <summary>
		/// A collection of all doorways on this tile that are currently unused (not connected to another tile)
		/// </summary>
		public IEnumerable<DoorwayProxy> UnusedDoorways { get { return doorways.Where(d => !d.Used); } }

		/// <summary>
		/// A collection of tags that belong to this tile
		/// </summary>
		public TagContainer Tags { get; private set; }

		/// <summary>
		/// Is this placed tile instance required? If true, the generator will avoid removing it (for example, during branch pruning).
		/// Backtracking may still remove required tiles if no valid layout can be found as it is assumed that the required tile will
		/// be re-added later.
		/// </summary>
		public bool IsRequired { get; set; }

		private readonly List<DoorwayProxy> doorways = new List<DoorwayProxy>();


		public TileProxy(TileProxy existingTile)
		{
			Prefab = existingTile.Prefab;
			PrefabTile = existingTile.PrefabTile;
			Placement = new TilePlacementData(existingTile.Placement);
			Tags = new TagContainer(existingTile.Tags);
			IsRequired = existingTile.IsRequired;

			// Copy proxy doorways
			Doorways = new ReadOnlyCollection<DoorwayProxy>(doorways);
			Entrances = new List<DoorwayProxy>(existingTile.Entrances.Count);
			Exits = new List<DoorwayProxy>(existingTile.Exits.Count);

			foreach (var existingDoorway in existingTile.doorways)
			{
				var doorway = new DoorwayProxy(this, existingDoorway);
				doorways.Add(doorway);

				if (existingTile.Entrances.Contains(existingDoorway))
					Entrances.Add(doorway);

				if (existingTile.Exits.Contains(existingDoorway))
					Exits.Add(doorway);
			}
		}

		public TileProxy(GameObject prefab, Func<Doorway, int, bool> allowedDoorwayPredicate = null)
		{
			Prefab = prefab;
			PrefabTile = prefab.GetComponent<Tile>();

			if (PrefabTile == null)
				PrefabTile = prefab.AddComponent<Tile>();

			Placement = new TilePlacementData();
			Tags = new TagContainer(PrefabTile.Tags);

			// Add proxy doorways
			Doorways = new ReadOnlyCollection<DoorwayProxy>(doorways);
			Entrances = new List<DoorwayProxy>();
			Exits = new List<DoorwayProxy>();

			var allDoorways = prefab.GetComponentsInChildren<Doorway>();
			var rootTransform = prefab.transform;
			var inverseRotation = Quaternion.Inverse(rootTransform.rotation);

			for (int i = 0; i < allDoorways.Length; i++)
			{
				var doorway = allDoorways[i];

				Vector3 localPosition = rootTransform.InverseTransformPoint(doorway.transform.position);
				Quaternion localRotation = inverseRotation * doorway.transform.rotation;

				bool isEntrance = PrefabTile.Entrances.Contains(doorway);
				bool isExit = PrefabTile.Exits.Contains(doorway);
				var proxyDoorway = new DoorwayProxy(this, i, doorway, localPosition, localRotation, isEntrance, isExit);
				doorways.Add(proxyDoorway);

				if (isEntrance)
					Entrances.Add(proxyDoorway);
				if (isExit)
					Exits.Add(proxyDoorway);

				if (allowedDoorwayPredicate != null && !allowedDoorwayPredicate(doorway, i))
					proxyDoorway.IsDisabled = true;
			}

			// Calculate bounds if missing
			if (!PrefabTile.HasValidBounds)
				PrefabTile.RecalculateBounds();

			Placement.LocalBounds = PrefabTile.Placement.LocalBounds;
		}

		public void PositionBySocket(DoorwayProxy myDoorway, DoorwayProxy otherDoorway)
		{
			Quaternion targetRotation = Quaternion.LookRotation(-otherDoorway.Forward, otherDoorway.Up);
			Placement.Rotation = targetRotation * Quaternion.Inverse(Quaternion.Inverse(Placement.Rotation) * (Placement.Rotation * myDoorway.LocalRotation));

			Vector3 targetPosition = otherDoorway.Position;
			Placement.Position = targetPosition - (myDoorway.Position - Placement.Position);
		}

		public bool IsOverlapping(TileProxy other, float maxOverlap)
		{
			return UnityUtil.AreBoundsOverlapping(Placement.Bounds, other.Placement.Bounds, maxOverlap);
		}

		public bool IsOverlappingOrOverhanging(TileProxy other, AxisDirection upDirection, float maxOverlap)
		{
			return UnityUtil.AreBoundsOverlappingOrOverhanging(Placement.Bounds, other.Placement.Bounds, upDirection, maxOverlap);
		}
	}
}
