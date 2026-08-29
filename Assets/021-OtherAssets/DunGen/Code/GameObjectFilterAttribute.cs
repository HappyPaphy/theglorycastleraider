using System;
using UnityEngine;

namespace DunGen
{
	/// <summary>
	/// Apply to a field of type `WeightedTable<GameObject>` to control whether scene objects and/or prefab assets are allowed.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public sealed class GameObjectFilterAttribute : PropertyAttribute
	{
		/// <summary>
		/// If true, allows selecting scene objects
		/// </summary>
		public bool AllowSceneObjects { get; }

		/// <summary>
		/// If true, allows selecting prefab assets
		/// </summary>
		public bool AllowPrefabAssets { get; }


		public GameObjectFilterAttribute(bool allowSceneObjects = true, bool allowPrefabAssets = true)
		{
			AllowSceneObjects = allowSceneObjects;
			AllowPrefabAssets = allowPrefabAssets;
		}
	}

	/// <summary>
	/// Apply to a field of type `WeightedTable<GameObject>` to control whether scene objects and/or prefab assets are allowed.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public sealed class GameObjectWeightFilterAttribute : PropertyAttribute
	{
		/// <summary>
		/// If true, allows selecting scene objects
		/// </summary>
		public bool AllowSceneObjects { get; }

		/// <summary>
		/// If true, allows selecting prefab assets
		/// </summary>
		public bool AllowPrefabAssets { get; }


		public GameObjectWeightFilterAttribute(bool allowSceneObjects = true, bool allowPrefabAssets = true)
		{
			AllowSceneObjects = allowSceneObjects;
			AllowPrefabAssets = allowPrefabAssets;
		}
	}
}