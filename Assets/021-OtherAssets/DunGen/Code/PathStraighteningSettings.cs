using System;
using UnityEngine;

namespace DunGen
{
	[Serializable]
	public class PathStraighteningSettings
	{
		/// <summary>
		/// Should we override the default straightening chance?
		/// </summary>
		public bool OverrideStraightenChance = false;
		/// <summary>
		/// The chance (0.0 - 1.0) that the next tile spawned will continue in the same direction as the previous tile
		/// </summary>
		[Range(0.0f, 1.0f)]		
		public float StraightenChance = 0.0f;
		/// <summary>
		/// Should we override the default straightening for the main path?
		/// </summary>
		public bool OverrideCanStraightenMainPath = false;
		/// <summary>
		/// Whether or not the main path should be straightened using <see cref="StraightenChance"/>
		/// </summary>
		public bool CanStraightenMainPath = true;
		/// <summary>
		/// Should we override the default straightening for branch paths?
		/// </summary>
		public bool OverrideCanStraightenBranchPaths = false;
		/// <summary>
		/// Whether or not branch paths should be straightened using <see cref="StraightenChance"/>
		/// </summary>
		public bool CanStraightenBranchPaths = false;


		/// <summary>
		/// Gets the final value of a property from a hierarchy of settings objects.
		/// </summary>
		/// <typeparam name="T">The type of property to get</typeparam>
		/// <param name="valueGetter">A delegate for getting the property from the settings object</param>
		/// <param name="overriddenGetter">A delegate for getting whether the property is overridden in the settings object</param>
		/// <param name="settingsHierarchy">The hierarchy of settings, starting with the leaf node and ending with the root</param>
		/// <returns>The overridden value, or the value from the root node if no overrides were applied</returns>
		public static T GetFinalValue<T>(Func<PathStraighteningSettings, T> valueGetter, Func<PathStraighteningSettings, bool> overriddenGetter, params PathStraighteningSettings[] settingsHierarchy)
		{
			// Loop through the hierarchy, starting at the leaf and going up to the root
			for (int i = 0; i < settingsHierarchy.Length; i++)
			{
				bool isRoot = i == settingsHierarchy.Length - 1;
				var settings = settingsHierarchy[i];

				// If we've reached the root, we should return its value
				if (isRoot)
					return valueGetter(settings);
				else
				{
					// If the current settings object has overridden the value, return it
					if (overriddenGetter(settings))
						return valueGetter(settings);
				}
			}

			return default;
		}

		/// <summary>
		/// Gets a new instance of <see cref="PathStraighteningSettings"/> with the final values from the hierarchy
		/// </summary>
		/// <param name="settingsHierarchy">The hierarchy of settings, starting with the leaf node and ending with the root</param>
		/// <returns>The overridden values, or the values from the root node if no overrides were applied</returns>
		public static PathStraighteningSettings GetFinalValues(params PathStraighteningSettings[] settingsHierarchy)
		{
			PathStraighteningSettings finalSettings = new PathStraighteningSettings
			{
				StraightenChance = GetFinalValue(s => s.StraightenChance, s => s.OverrideStraightenChance, settingsHierarchy),
				CanStraightenMainPath = GetFinalValue(s => s.CanStraightenMainPath, s => s.OverrideCanStraightenMainPath, settingsHierarchy),
				CanStraightenBranchPaths = GetFinalValue(s => s.CanStraightenBranchPaths, s => s.OverrideCanStraightenBranchPaths, settingsHierarchy)
			};

			return finalSettings;
		}
	}
}