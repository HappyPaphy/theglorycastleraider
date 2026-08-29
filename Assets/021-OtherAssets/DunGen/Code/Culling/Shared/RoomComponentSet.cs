using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace DunGen.Culling.Shared
{
	public sealed class RoomComponentSet<T> : IEnumerable<T> where T : Component
	{
		/// <summary>
		/// Components contained within the hierarchy of the room
		/// </summary>
		public ReadOnlyCollection<T> Hierarchy
		{
			get
			{
				hierarchyReadonly ??= new List<T>(hierarchy).AsReadOnly();
				return hierarchyReadonly;
			}
		}

		/// <summary>
		/// Additional components to be controlled by the culling system that are not part of
		/// the room's hierarchy. This is useful for runtime-spawned objects that should be culled with the room
		/// </summary>
		public ReadOnlyCollection<T> Additional
		{
			get
			{
				additionalReadonly ??= new List<T>(additional).AsReadOnly();
				return additionalReadonly;
			}
		}

		/// <summary>
		/// Contains all components in the room, including both those in the hierarchy and any additional components
		/// </summary>
		public ReadOnlyCollection<T> All
		{
			get
			{
				allReadonly ??= new List<T>(all).AsReadOnly();
				return allReadonly;
			}
		}

		public event Action Changed;

		private readonly HashSet<T> hierarchy = new HashSet<T>();
		private readonly HashSet<T> additional = new HashSet<T>();
		private readonly HashSet<T> all = new HashSet<T>();

		private ReadOnlyCollection<T> hierarchyReadonly;
		private ReadOnlyCollection<T> additionalReadonly;
		private ReadOnlyCollection<T> allReadonly;


		public bool AddAdditional(T component)
		{
			if (component == null || !additional.Add(component))
				return false;

			RebuildAllAndNotify();
			return true;
		}

		public bool RemoveAdditional(T component)
		{
			if (component == null || !additional.Remove(component))
				return false;

			RebuildAllAndNotify();
			return true;
		}

		public bool ClearAdditional()
		{
			if (additional.Count == 0)
				return false;

			additional.Clear();
			RebuildAllAndNotify();
			return true;
		}

		public bool RefreshFromHierarchy(GameObject root)
		{
			hierarchy.Clear();

			if (root != null)
			{
				foreach (var component in root.GetComponentsInChildren<T>(true))
				{
					if (component != null)
						hierarchy.Add(component);
				}
			}

			return RebuildAllAndNotify();
		}

		private bool RebuildAllAndNotify()
		{
			var changed = false;

			if (all.Count != hierarchy.Count + additional.Count)
				changed = true;

			if (!changed)
			{
				foreach (var item in hierarchy)
				{
					if (!all.Contains(item))
					{
						changed = true;
						break;
					}
				}
			}

			if (!changed)
			{
				foreach (var item in additional)
				{
					if (!all.Contains(item))
					{
						changed = true;
						break;
					}
				}
			}

			all.Clear();

			foreach (var item in hierarchy)
				all.Add(item);

			foreach (var item in additional)
				all.Add(item);

			hierarchyReadonly = null;
			additionalReadonly = null;
			allReadonly = null;

			if (changed)
				Changed?.Invoke();

			return changed;
		}

		#region IEnumerable<T> Implementation

		public IEnumerator<T> GetEnumerator() => All.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => All.GetEnumerator();

		#endregion
	}
}