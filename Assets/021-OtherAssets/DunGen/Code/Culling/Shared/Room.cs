using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen.Culling.Shared
{
	public class Room
	{
		/// <summary>
		/// The tile toom is based on
		/// </summary>
		public Tile Tile { get; }

		/// <summary>
		/// World space bounds of the room
		/// </summary>
		public Bounds Bounds { get; private set; }

		/// <summary>
		/// Portals (doorways) in the room
		/// </summary>
		public Portal[] Portals { get; private set; }

		public RoomComponentSet<Renderer> Renderers { get; } = new RoomComponentSet<Renderer>();
		public RoomComponentSet<Light> Lights { get; } = new RoomComponentSet<Light>();
		public RoomComponentSet<ReflectionProbe> ReflectionProbes { get; } = new RoomComponentSet<ReflectionProbe>();

		public event Action<Room> ComponentsChanged;

		#region Legacy

		[Obsolete("Deprecated in 2.19.5. Use Renderers.AddAdditional instead")]
		public readonly List<Renderer> AdditionalRenderers = new List<Renderer>();

		[Obsolete("Deprecated in 2.19.5. Use ComponentsChanged instead")]
#pragma warning disable CS0067 // Unused event
		public event Action<Room> RenderersChanged;
#pragma warning restore CS0067

		[Obsolete("Deprecated in 2.19.5. Use RefreshComponents() instead")]
		public void RefreshRenderers() => RefreshComponents();

		[Obsolete("Deprecated in 2.19.5. No longer necessary as subscribers are now automatically notified when component sets change")]
		public void Refresh() { }

		#endregion


		public Room(Tile tile)
		{
			Tile = tile;

			Renderers.Changed += OnComponentSetChanged;
			Lights.Changed += OnComponentSetChanged;
			ReflectionProbes.Changed += OnComponentSetChanged;

			RefreshBounds();
			RefreshComponents();
		}

		private void OnComponentSetChanged() => NotifyChanged();

		public void RefreshBounds()
		{
			Bounds = Tile.Bounds;
		}

		/// <summary>
		/// Refreshes the list of portals in this room by getting all used doorways from the tile and finding the connected rooms from the provided list
		/// </summary>
		/// <param name="allRooms">A complete set of rooms in the scene</param>
		public void RefreshPortals(List<Room> allRooms)
		{
			Portals = Tile.UsedDoorways.Select(d => new Portal()
			{
				From = this,
				To = allRooms.FirstOrDefault(x => x.Tile == d.ConnectedDoorway.Tile),
				DoorComponent = d.DoorComponent,
				Transform = d.transform,
				Size = d.Socket.Size,
			}).ToArray();

			foreach(var portal in Portals)
				portal.RefreshRenderers();
		}

		/// <summary>
		/// Refreshes the list of renderers in this room by getting all child renderers of the tile root
		/// </summary>
		public void RefreshComponents()
		{
			// Unsubscribe from the changed events to avoid triggering them during the refresh
			Renderers.Changed -= OnComponentSetChanged;
			Lights.Changed -= OnComponentSetChanged;
			ReflectionProbes.Changed -= OnComponentSetChanged;

			Renderers.RefreshFromHierarchy(Tile.gameObject);
			Lights.RefreshFromHierarchy(Tile.gameObject);
			ReflectionProbes.RefreshFromHierarchy(Tile.gameObject);

			if (Portals != null)
			{
				foreach (var portal in Portals)
					portal.RefreshRenderers();
			}

			// Re-subscribe to the changed events after the refresh is complete
			Renderers.Changed += OnComponentSetChanged;
			Lights.Changed += OnComponentSetChanged;
			ReflectionProbes.Changed += OnComponentSetChanged;

			NotifyChanged();
		}

		private void NotifyChanged() => ComponentsChanged?.Invoke(this);
	}
}
