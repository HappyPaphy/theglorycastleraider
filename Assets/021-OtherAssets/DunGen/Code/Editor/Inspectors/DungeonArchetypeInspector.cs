using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
	[CustomEditor(typeof(DungeonArchetype))]
	public sealed class DungeonArchetypeInspector : UnityEditor.Editor
	{
		#region Labels

		// Headers
		private static readonly GUIContent GeneralHeader = new GUIContent("General");
		private static readonly GUIContent PathStraighteningHeader = new GUIContent("Path Straightening");
		private static readonly GUIContent TileSetsHeader = new GUIContent("Tile Sets", "A list of the Tile Sets which can be placed in a dungeon segment of this Archetype");
		private static readonly GUIContent BranchStartTilesHeader = new GUIContent("Branch Start Tiles");
		private static readonly GUIContent BranchCapTilesHeader = new GUIContent("Branch Cap Tiles");

		// Labels
		private static readonly GUIContent BranchingDepthLabel = new GUIContent("Branching Depth", "A range representing the min and max branch depth. This is how long a branch can be.");
		private static readonly GUIContent BranchCountLabel = new GUIContent("Branch Count", "A range representing the min and max number of branches an individual tile on the main path can have. Setting both to zero will result in a dungeon segment with no branching paths");
		private static readonly GUIContent BranchStartTypeLabel = new GUIContent("Branch Start Type", "The method used to combine Tile Sets in conjunction with the Tiles listed below");
		private static readonly GUIContent BranchStartTileSetsLabel = new GUIContent("Branch-Start Tile Sets", "A list of Tile Sets that can be used as the first tile on branch paths");
		private static readonly GUIContent BranchCapTypeLabel = new GUIContent("Branch Cap Type", "The method used to combine Tile Sets in conjunction with the Tiles listed below");
		private static readonly GUIContent BranchCapTileSetsLabel = new GUIContent("Branch-Cap Tile Sets", "A list of Tile Sets that can be used as caps on branch paths");
		private static readonly GUIContent UniqueLabel = new GUIContent("Unique?", "If checked, DunGen will try to only allow one segment of the dungeon to use this archetype. There will be duplicates if it's not possible to keep it unique");

		#endregion


		public override void OnInspectorGUI()
		{
			DungeonArchetype archetype = target as DungeonArchetype;

			if (archetype == null)
				return;

			serializedObject.Update();

			// General Section
			EditorGUILayout.BeginVertical("box");
			{
				EditorGUILayout.LabelField(GeneralHeader, EditorStyles.boldLabel);
				EditorUtil.DrawIntRange(BranchingDepthLabel, archetype.BranchingDepth);
				EditorUtil.DrawIntRange(BranchCountLabel, archetype.BranchCount);
				archetype.Unique = EditorGUILayout.Toggle(UniqueLabel, archetype.Unique);
			}
			EditorGUILayout.EndVertical();

			// Straightening Section
			EditorGUILayout.BeginVertical("box");
			{
				EditorGUILayout.LabelField(PathStraighteningHeader, EditorStyles.boldLabel);
				EditorUtil.DrawStraightenSettingsWithOverrides(serializedObject.FindProperty(nameof(DungeonArchetype.StraighteningSettings)), true);
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space();
			EditorGUILayout.Space();

			EditorUtil.DrawObjectList(TileSetsHeader, archetype.TileSets, GameObjectSelectionTypes.Prefab, target);

			EditorGUILayout.Space();
			EditorGUILayout.Space();

			DrawBranchStartTilesEditor(archetype);
			EditorGUILayout.Space();
			DrawBranchCapTilesEditor(archetype);

			if (GUI.changed)
				EditorUtility.SetDirty(archetype);

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawBranchStartTilesEditor(DungeonArchetype archetype)
		{
			EditorGUILayout.BeginVertical("box");
			EditorGUILayout.LabelField(BranchStartTilesHeader, EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("Specific tiles can be set to appear at the beginning of branch paths. The type can be used to control if these tiles are used \"instead of\" or \"as well as\" the standard tile sets listed above", MessageType.Info);

			archetype.BranchStartType = (BranchCapType)EditorGUILayout.EnumPopup(BranchStartTypeLabel, archetype.BranchStartType);
			EditorUtil.DrawObjectList(BranchStartTileSetsLabel, archetype.BranchStartTileSets, GameObjectSelectionTypes.Prefab, target);

			EditorGUILayout.EndVertical();
		}

		private void DrawBranchCapTilesEditor(DungeonArchetype archetype)
		{
			EditorGUILayout.BeginVertical("box");
			EditorGUILayout.LabelField(BranchCapTilesHeader, EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("Specific tiles can be set to appear at the end of branch paths. The cap type can be used to control if these tiles are used \"instead of\" or \"as well as\" the standard tile sets listed above", MessageType.Info);

			archetype.BranchCapType = (BranchCapType)EditorGUILayout.EnumPopup(BranchCapTypeLabel, archetype.BranchCapType);
			EditorUtil.DrawObjectList(BranchCapTileSetsLabel, archetype.BranchCapTileSets, GameObjectSelectionTypes.Prefab, target);

			EditorGUILayout.EndVertical();
		}
	}
}
