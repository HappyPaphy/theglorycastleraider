using DunGen.Graph;
using DunGen.Pooling;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DunGen.Editor.Inspectors
{
	[CustomEditor(typeof(TilePoolPreloader))]
	[CanEditMultipleObjects]
	public class TilePoolPreloaderInspector : UnityEditor.Editor
	{
		#region Constants

		public static class Label
		{
			public static readonly GUIContent ListHeader = new GUIContent("Tile Pool Entries", "How many of each tile prefab we want to pre-load into the pool");
			public static readonly GUIContent NoneElement = new GUIContent("Drag a Tile, TileSet, Archetype, or DungeonFlow here");
			public static readonly GUIContent ClearList = new GUIContent("Clear List", "Clears all entries from the list");
			public static readonly GUIContent SetAllCounts = new GUIContent("Set All Counts", "Sets the count value of every tile prefab in the list to the specified value");
			public static readonly GUIContent ApplyButton = new GUIContent("Apply");
			public static readonly GUIContent ListControls = new GUIContent("List Controls");
			public static readonly GUIContent ClearInstancesButton = new GUIContent("Clear Instances");
			public static readonly GUIContent RefreshInstancesButton = new GUIContent("Refresh Instances");
		}

		#endregion

		private static int setAllCountValue = 1;
		private static bool showListControls = false;

		private SerializedProperty entries;
		private ReorderableList reorderableList;


		private void OnEnable()
		{
			entries = serializedObject.FindProperty(nameof(TilePoolPreloader.Entries));

			reorderableList = new ReorderableList(serializedObject, entries, true, true, true, true);

			reorderableList.drawHeaderCallback = rect =>
			{
				EditorGUI.LabelField(rect, Label.ListHeader);
			};

			reorderableList.onAddCallback = list =>
			{
				var newElement = entries.GetArrayElementAtIndex(entries.arraySize++);
				var prefabProperty = newElement.FindPropertyRelative(nameof(TilePoolPreloaderEntry.TilePrefab));
				var countProperty = newElement.FindPropertyRelative(nameof(TilePoolPreloaderEntry.Count));

				prefabProperty.objectReferenceValue = null;
				countProperty.intValue = setAllCountValue;
			};

			reorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
			{
				var element = entries.GetArrayElementAtIndex(index);
				rect.y += 2;

				float countWidth = 100;
				float prefabWidth = rect.width - countWidth;

				// Draw tile prefab field
				EditorGUI.PropertyField(
					new Rect(rect.x, rect.y, prefabWidth, EditorGUIUtility.singleLineHeight),
					element.FindPropertyRelative(nameof(TilePoolPreloaderEntry.TilePrefab)),
					GUIContent.none);

				// Draw count field
				var countProperty = element.FindPropertyRelative(nameof(TilePoolPreloaderEntry.Count));
				countProperty.intValue = Mathf.Max(1, EditorGUI.IntField(
					new Rect(rect.x + prefabWidth, rect.y, countWidth, EditorGUIUtility.singleLineHeight),
					countProperty.intValue));
			};

			reorderableList.drawNoneElementCallback = rect =>
			{
				EditorGUI.LabelField(rect, Label.NoneElement, EditorStyles.centeredGreyMiniLabel);
			};
		}

		public override void OnInspectorGUI()
		{
			EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);
			{
				serializedObject.Update();

				DrawListControls();
				EditorGUILayout.Space();
				DrawPrefabList();
				EditorGUILayout.Space();
				DrawSpawnControls();

				serializedObject.ApplyModifiedProperties();
			}
			EditorGUI.EndDisabledGroup();
		}

		private void DrawListControls()
		{
			showListControls = EditorGUILayout.Foldout(showListControls, Label.ListControls, true);

			if (showListControls)
			{
				EditorGUI.indentLevel++;

				// Clear List
				if (GUILayout.Button(Label.ClearList))
				{
					entries.ClearArray();
					GUI.changed = true;
				}

				// Set all counts controls
				EditorGUILayout.BeginHorizontal();
				{
					setAllCountValue = Mathf.Max(1, EditorGUILayout.IntField(Label.SetAllCounts, setAllCountValue));

					if (GUILayout.Button(Label.ApplyButton, GUILayout.Width(60)))
					{
						for (int i = 0; i < entries.arraySize; i++)
						{
							var element = entries.GetArrayElementAtIndex(i);
							var countProperty = element.FindPropertyRelative(nameof(TilePoolPreloaderEntry.Count));
							countProperty.intValue = setAllCountValue;
						}

						GUI.changed = true;
					}
				}
				EditorGUILayout.EndHorizontal();

				EditorGUI.indentLevel--;
				EditorGUILayout.Space();
			}
		}

		private void DrawPrefabList()
		{
			// Handle drag and drop
			Rect dropRect = GUILayoutUtility.GetRect(0, reorderableList.GetHeight());
			reorderableList.DoList(dropRect);

			Event evt = Event.current;
			switch (evt.type)
			{
				case EventType.DragUpdated:
				case EventType.DragPerform:
					if (!dropRect.Contains(evt.mousePosition))
						break;

					DragAndDrop.visualMode = HasValidDragObject(DragAndDrop.objectReferences) ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

					if (evt.type == EventType.DragPerform)
					{
						DragAndDrop.AcceptDrag();

						foreach (Object draggedObject in DragAndDrop.objectReferences)
							TryAddDraggedObject(draggedObject);

						GUI.changed = true;
					}

					evt.Use();
					break;
			}
		}

		private void DrawSpawnControls()
		{
			var preloader = target as TilePoolPreloader;

			EditorGUI.BeginDisabledGroup(!preloader.HasSpawnedInstances());
			{
				if (GUILayout.Button(Label.ClearInstancesButton))
					preloader.ClearSpawnedInstances();
			}
			EditorGUI.EndDisabledGroup();

			if (GUILayout.Button(Label.RefreshInstancesButton))
			{
				int totalInstanceCount = 0;

				foreach(var entry in preloader.Entries)
				{
					if(entry.TilePrefab != null)
						totalInstanceCount += entry.Count;
				}

				if (totalInstanceCount > 0)
				{
					bool generateInstances;

					// Get confirmation from the user before spawning the tile instances
					generateInstances = EditorUtility.DisplayDialog(
						"Generating Tiles",
						$"{totalInstanceCount} tile instances will be created. Are you sure you want to continue?",
						"Continue",
						"Cancel");

					if (generateInstances)
						preloader.RefreshTileInstances();
				}
			}
		}

		private bool HasValidDragObject(Object[] draggedObjects)
		{
			foreach (var draggedObject in draggedObjects)
			{
				if (draggedObject is GameObject gameObject)
				{
					var prefabType = PrefabUtility.GetPrefabAssetType(gameObject);
					bool isPrefab = prefabType == PrefabAssetType.Regular || prefabType == PrefabAssetType.Variant;

					if (isPrefab && gameObject.TryGetComponent<Tile>(out var tile))
						return true;
				}
				else if (draggedObject is TileSet || draggedObject is DungeonArchetype || draggedObject is DungeonFlow)
					return true;
			}

			return false;
		}

		private void AddTile(Tile tilePrefab)
		{
			if(tilePrefab == null)
				return;

			// Check if this tile already exists in the list
			for (int i = 0; i < entries.arraySize; i++)
			{
				var element = entries.GetArrayElementAtIndex(i);
				var prefabProperty = element.FindPropertyRelative(nameof(TilePoolPreloaderEntry.TilePrefab));
				var existingTilePrefab = prefabProperty.objectReferenceValue as Tile;

				if (tilePrefab == existingTilePrefab)
					return; // Tile already exists, skip it
			}

			entries.InsertArrayElementAtIndex(entries.arraySize);

			var newElement = entries.GetArrayElementAtIndex(entries.arraySize - 1);
			newElement.FindPropertyRelative(nameof(TilePoolPreloaderEntry.TilePrefab)).objectReferenceValue = tilePrefab;
			newElement.FindPropertyRelative(nameof(TilePoolPreloaderEntry.Count)).intValue = 1;
		}

		private void AddTileSet(TileSet tileSet)
		{
			foreach (var entry in tileSet.Tiles.Entries)
			{
				if (entry == null || entry.Value == null)
					continue;

				if (entry.Value.TryGetComponent(out Tile tile))
					AddTile(tile);
			}
		}

		private void AddArchetype(DungeonArchetype archetype)
		{
			// Add regular tile sets
			foreach (var tileSet in archetype.TileSets)
				if (tileSet != null)
					AddTileSet(tileSet);

			// Add branch start tile sets
			foreach (var tileSet in archetype.BranchStartTileSets)
				if (tileSet != null)
					AddTileSet(tileSet);

			// Add branch end tile sets
			foreach (var tileSet in archetype.BranchCapTileSets)
				if (tileSet != null)
					AddTileSet(tileSet);
		}

		private void AddDungeonFlow(DungeonFlow flow)
		{
			// Add tile sets from nodes
			foreach(var node in flow.Nodes)
			{
				foreach(var tileSet in node.TileSets)
					if(tileSet != null)
						AddTileSet(tileSet);
			}

			// Add archetypes from lines
			foreach(var line in flow.Lines)
			{
				foreach(var archetype in line.DungeonArchetypes)
					if (archetype != null)
						AddArchetype(archetype);
			}

			// Add tile sets from injection rules
			foreach (var injectionRule in flow.TileInjectionRules)
			{
				if(injectionRule.TileSet != null)
					AddTileSet(injectionRule.TileSet);
			}
		}

		private void TryAddDraggedObject(Object draggedObject)
		{
			// Tile Prefab
			if (draggedObject is GameObject gameObject)
			{
				var prefabType = PrefabUtility.GetPrefabAssetType(gameObject);
				bool isPrefab = prefabType == PrefabAssetType.Regular || prefabType == PrefabAssetType.Variant;

				if (isPrefab && gameObject.TryGetComponent<Tile>(out var tile))
					AddTile(tile);
			}
			// TileSet
			else if (draggedObject is TileSet tileSet)
				AddTileSet(tileSet);
			// Archetype
			else if (draggedObject is DungeonArchetype archetype)
				AddArchetype(archetype);
			else if (draggedObject is DungeonFlow flow)
				AddDungeonFlow(flow);
		}
	}
}
