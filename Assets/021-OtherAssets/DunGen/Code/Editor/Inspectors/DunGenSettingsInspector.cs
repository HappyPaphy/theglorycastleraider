using DunGen.Editor.Windows;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor.Inspectors
{
	[CustomEditor(typeof(DunGenSettings))]
	public sealed class DunGenSettingsInspector : UnityEditor.Editor
	{
		private static class Label
		{
			public static readonly GUIContent BoundsCalculator = new GUIContent("Tile Bounds Calculator", "The ITileBoundsCalculator implementation to use when calculating tile bounds");
			public static readonly GUIContent RecalculateTileBoundsOnSave = new GUIContent("Recalculate On Save", "If true, tile bounds will automatically be recalculated every time the tile is saved. Otherwise, bounds must be recalculated manually in the Tile component inspector");
			public static readonly GUIContent BroadphaseSettings = new GUIContent("Collision Broadphase", "The settings to use for broadphase collision detection");
			public static readonly GUIContent TilePooling = new GUIContent("Enable Tile Pooling", "If true, tile instances will be re-used instead of being destroyed, improving generation performance at the cost of increased memory consumption");
			public static readonly GUIContent DisplayFailureReportWindow = new GUIContent("Display Failure Report Window", "If true, a window will be displayed when a generation failure occurs, allowing you to inspect the failure report");
			public static readonly GUIContent CheckForRemovedFiles = new GUIContent("Check for Unused Files", "DunGen will check if any old files from previous versions are still present in the DunGen directory and will present a list of files to potentially delete");
			public static readonly GUIContent CleanDunGenDirectory = new GUIContent("Remove Unused Files", "Removes any DunGen files left over from previous versions that are no longer in use");
			public static readonly GUIContent CollisionThreshold = new GUIContent("Collision Threshold", "Small epsilon used when checking room bounds for overlap to improve cross-platform seed determinism. Keep low; increase only if identical seeds produce different dungeons across platforms. Higher values may cause rooms to visually overlap");
		}

		private SerializedProperty boundsCalculator;
		private SerializedProperty recalculateTileBoundsOnSave;
		private SerializedProperty broadphaseSettings;
		private SerializedProperty enableTilePooling;
		private SerializedProperty displayFailureReportWindow;
		private SerializedProperty checkForUnusedFiles;
		private SerializedProperty collisionThreshold;


		private void OnEnable()
		{
			boundsCalculator = serializedObject.FindProperty(nameof(DunGenSettings.BoundsCalculator));
			recalculateTileBoundsOnSave = serializedObject.FindProperty(nameof(DunGenSettings.RecalculateTileBoundsOnSave));
			broadphaseSettings = serializedObject.FindProperty(nameof(DunGenSettings.BroadphaseSettings));
			enableTilePooling = serializedObject.FindProperty(nameof(DunGenSettings.EnableTilePooling));
			displayFailureReportWindow = serializedObject.FindProperty(nameof(DunGenSettings.DisplayFailureReportWindow));
			checkForUnusedFiles = serializedObject.FindProperty(nameof(DunGenSettings.CheckForUnusedFiles));
			collisionThreshold = serializedObject.FindProperty(nameof(DunGenSettings.CollisionThreshold));
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			DrawGeneralProperties();
			EditorGUILayout.Space();
			DrawOptimizationProperties();
			EditorGUILayout.Space();
			DrawBoundsProperties();

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawGeneralProperties()
		{
			var obj = target as DunGenSettings;

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
				EditorGUILayout.LabelField("General", EditorStyles.boldLabel);

				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.ObjectField("Default Socket", obj.DefaultSocket, typeof(DoorwaySocket), false);
				EditorGUI.EndDisabledGroup();

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField(collisionThreshold, Label.CollisionThreshold);
				if(EditorGUI.EndChangeCheck())
					collisionThreshold.floatValue = Mathf.Max(0, collisionThreshold.floatValue);

				if(collisionThreshold.floatValue > 0.01f)
					EditorGUILayout.HelpBox("A high collision threshold may cause tiles to overlap. This value should be kept low and only increased to minimize determinism issues caused by floating-point precision differences across platforms/architecture", MessageType.Warning);

				EditorGUILayout.PropertyField(displayFailureReportWindow, Label.DisplayFailureReportWindow);
				EditorGUILayout.PropertyField(checkForUnusedFiles, Label.CheckForRemovedFiles);

				if (GUILayout.Button(Label.CleanDunGenDirectory))
					DunGenFolderCleaningWindow.CleanDunGenDirectory();
			}
			EditorGUILayout.EndVertical();
		}

		private void DrawOptimizationProperties()
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
				EditorGUILayout.LabelField("Optimization", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(broadphaseSettings, Label.BroadphaseSettings);
				EditorGUILayout.PropertyField(enableTilePooling, Label.TilePooling);

				if (enableTilePooling.boolValue)
					EditorGUILayout.HelpBox("Tile pooling requires special attention to how your tiles are structured. Please consult the documentation for more information", MessageType.Warning);

			}
			EditorGUILayout.EndVertical();
		}

		private void DrawBoundsProperties()
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
				EditorGUILayout.LabelField("Tile Bounds", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(boundsCalculator, Label.BoundsCalculator);
				EditorGUILayout.PropertyField(recalculateTileBoundsOnSave, Label.RecalculateTileBoundsOnSave);

				EditorGUILayout.Space();

				var rect = EditorGUILayout.GetControlRect();
				var buttonRect = rect;
				var dropdownRect = new Rect(buttonRect.xMax - 20, buttonRect.y, 20, buttonRect.height);
				buttonRect.width -= 20;

				if (GUI.Button(buttonRect, "Calculate Missing Tile Bounds"))
					AskUserToCacheBounds(false);

				if (EditorGUI.DropdownButton(dropdownRect, GUIContent.none, FocusType.Passive))
				{
					var menu = new GenericMenu();
					menu.AddItem(new GUIContent("Clear and Recalculate All Tile Bounds"), false, () => AskUserToCacheBounds(true));
					menu.DropDown(dropdownRect);
				}
			}
			EditorGUILayout.EndVertical();
		}

		private static void AskUserToCacheBounds(bool clearExisting)
		{
			string message = clearExisting
				? "This will clear and recalculate bounds for all Tile prefabs in your project that belong to a TileSet. This may take some time. Continue?"
				: "This will calculate missing bounds for Tile prefabs in your project that belong to a TileSet. This may take some time. Continue?";

			if (!EditorUtility.DisplayDialog("Calculate Tile Bounds", message, "Yes", "No"))
				return;

			IEnumerable<Tile> tiles = GetAllTilePrefabs();

			if (!clearExisting)
				tiles = tiles.Where(t => !t.HasValidBounds);

			if (tiles.Count() == 0)
				EditorUtility.DisplayDialog("Calculate Tile Bounds", "No tile bounds were missing", "OK");
			else
			{
				int cachedBoundsCount = CacheTileBounds(tiles, clearExisting);
				EditorUtility.DisplayDialog("Calculate Tile Bounds", $"Recalculated bounds for {cachedBoundsCount} tiles", "OK");
			}
		}

		private static HashSet<Tile> GetAllTilePrefabs()
		{
			var tiles = new HashSet<Tile>();

			// Find all the TileSets in the project
			// This is much faster than loading all prefabs in the project and checking if they have a Tile component
			var tileSetGuids = AssetDatabase.FindAssets("t:TileSet");

			foreach (var guid in tileSetGuids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var tileSet = AssetDatabase.LoadAssetAtPath<TileSet>(path);

				if(tileSet != null)
					tiles.UnionWith(tileSet.Tiles.Entries.Select(w => w.Value.GetComponent<Tile>()));
			}

			return tiles;
		}

		private static int CacheTileBounds(IEnumerable<Tile> tiles, bool force)
		{
			int updatedBoundsCount = 0;

			foreach (var tile in tiles)
			{
				bool boundsUpdated = tile.RecalculateBounds();

				if (boundsUpdated)
				{
					updatedBoundsCount++;
					EditorUtility.SetDirty(tile);
				}
			}

			return updatedBoundsCount;
		}
	}
}
