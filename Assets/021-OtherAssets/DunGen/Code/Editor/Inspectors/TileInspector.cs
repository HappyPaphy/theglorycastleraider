using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace DunGen.Editor
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Tile))]
	public class TileInspector : UnityEditor.Editor
	{
		#region Labels

		private static class Label
		{
			public static readonly GUIContent AllowRotation = new GUIContent("Allow Rotation", "If checked, this tile is allowed to be rotated by the dungeon gennerator. This setting can be overriden globally in the dungeon generator settings");
			public static readonly GUIContent RepeatMode = new GUIContent("Repeat Mode", "Determines how a tile is able to repeat throughout the dungeon. This setting can be overriden globally in the dungeon generator settings");
			public static readonly GUIContent OverrideAutomaticTileBounds = new GUIContent("Manual Tile Bounds", "DunGen automatically calculates a bounding volume for tiles. Check this option if you're having problems with the automatically generated bounds.");
			public static readonly GUIContent FitToTile = new GUIContent("Fit to Tile", "Uses DunGen's automatic bounds generating to try to fit the bounds to the tile.");
			public static readonly GUIContent Entrances = new GUIContent("Entrances", "If set, DunGen will always use one of these doorways as the entrance to this tile.");
			public static readonly GUIContent Exits = new GUIContent("Exits", "If set, DunGen will always use one of these doorways as the first exit from this tile");
			public static readonly GUIContent OverrideConnectionChance = new GUIContent("Override Connection Chance", "If checked, this tile will override the global connection chance set in the dungeon flow. If both tiles override the connection chance, the lowest value will be used");
			public static readonly GUIContent ConnectionChance = new GUIContent("Connection Chance", "The chance that this tile will be connected to an overlapping doorway");
			public static readonly GUIContent Tags = new GUIContent("Tags", "A set of user-defined tags that can be used with the dungeon flow to restrict tile connections or referenced in code to apply custom logic");
		}

		#endregion

		private SerializedProperty allowRotation;
		private SerializedProperty repeatMode;
		private SerializedProperty overrideAutomaticTileBounds;
		private SerializedProperty tileBoundsOverride;
		private SerializedProperty entrances;
		private SerializedProperty exits;
		private SerializedProperty overrideConnectionChance;
		private SerializedProperty connectionChance;
		private SerializedProperty tags;

		private BoxBoundsHandle overrideBoundsHandle;


		private void OnEnable()
		{
			allowRotation = serializedObject.FindProperty("AllowRotation");
			repeatMode = serializedObject.FindProperty("RepeatMode");
			overrideAutomaticTileBounds = serializedObject.FindProperty("OverrideAutomaticTileBounds");
			tileBoundsOverride = serializedObject.FindProperty("TileBoundsOverride");
			entrances = serializedObject.FindProperty("Entrances");
			exits = serializedObject.FindProperty("Exits");
			overrideConnectionChance = serializedObject.FindProperty("OverrideConnectionChance");
			connectionChance = serializedObject.FindProperty("ConnectionChance");
			tags = serializedObject.FindProperty("Tags");


			overrideBoundsHandle = new BoxBoundsHandle();
			overrideBoundsHandle.SetColor(Color.red);
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField(allowRotation, Label.AllowRotation);
			EditorGUILayout.PropertyField(repeatMode, Label.RepeatMode);

			EditorGUILayout.Space();

			// Tile Bounds Override
			EditorGUILayout.BeginVertical("box");

			EditorGUILayout.PropertyField(overrideAutomaticTileBounds, Label.OverrideAutomaticTileBounds);

			EditorGUI.BeginDisabledGroup(!overrideAutomaticTileBounds.boolValue);

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(tileBoundsOverride, GUIContent.none);

			if (GUILayout.Button(Label.FitToTile))
			{
				Undo.RecordObjects(targets, "Fit Bounds to Tile(s)");

				foreach (var t in targets)
				{
					var tile = t as Tile;

					if (tile == null)
						continue;

					var newBounds = tile.GetBoundsCalculator().CalculateLocalBounds(tile.gameObject);

					var so = new SerializedObject(tile);
					so.Update();

					var overrideBoundsProp = so.FindProperty(nameof(Tile.TileBoundsOverride));
					overrideBoundsProp.boundsValue = newBounds;

					so.ApplyModifiedProperties();
					EditorUtility.SetDirty(tile);
				}

				serializedObject.Update();
			}

			EditorGUI.EndDisabledGroup();
			EditorGUILayout.Space();
			EditorGUILayout.EndVertical();


			// Connection Chance Override
			EditorGUILayout.BeginVertical("box");

			EditorGUILayout.PropertyField(overrideConnectionChance, Label.OverrideConnectionChance);

			EditorGUI.BeginDisabledGroup(!overrideConnectionChance.boolValue);

			EditorGUILayout.Slider(connectionChance, 0f, 1f, Label.ConnectionChance);

			EditorGUI.EndDisabledGroup();
			EditorGUILayout.Space();
			EditorGUILayout.EndVertical();


			// Entrance & Exit doorways
			EditorGUILayout.BeginVertical("box");
			EditorGUILayout.HelpBox("You can optionally designate doorways as entrances or exits for this tile", MessageType.Info);

			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(entrances, Label.Entrances);
			EditorGUILayout.PropertyField(exits, Label.Exits);
			EditorGUI.indentLevel--;

			EditorGUILayout.Space();
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space();
			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(tags, Label.Tags);

			EditorGUILayout.Space();

			if (GUILayout.Button("Recalculate Bounds"))
			{
				Undo.RecordObjects(targets, "Recalculate Tile Bounds");

				foreach (var t in targets)
				{
					var tile = t as Tile;

					if (tile == null)
						continue;

					if (tile.RecalculateBounds())
						EditorUtility.SetDirty(tile);
				}

				serializedObject.Update();
			}

			serializedObject.ApplyModifiedProperties();
		}

		private void OnSceneGUI()
		{
			var tile = target as Tile;

			if (tile == null)
				return;

			// Create a temporary SerializedObject for this specific target
			using (var so = new SerializedObject(tile))
			{
				var overrideBoundsProp = so.FindProperty(nameof(Tile.OverrideAutomaticTileBounds));
				var boundsProp = so.FindProperty(nameof(Tile.TileBoundsOverride));

				// If the property setup is invalid or unchecked, exit
				if (overrideBoundsProp == null || !overrideBoundsProp.boolValue)
					return;

				// Sync handle to this specific object's bounds
				overrideBoundsHandle.center = boundsProp.boundsValue.center;
				overrideBoundsHandle.size = boundsProp.boundsValue.size;

				// Allow Unity to identify this handle uniquely
				int controlId = GUIUtility.GetControlID(FocusType.Passive);

				EditorGUI.BeginChangeCheck();

				using (new Handles.DrawingScope(tile.transform.localToWorldMatrix))
				{
					overrideBoundsHandle.DrawHandle();
				}

				if (EditorGUI.EndChangeCheck())
				{
					boundsProp.boundsValue = new Bounds(overrideBoundsHandle.center, overrideBoundsHandle.size);
					so.ApplyModifiedProperties();
				}
			}
		}
	}
}