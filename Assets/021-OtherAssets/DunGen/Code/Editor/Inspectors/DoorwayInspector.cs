using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Linq;
using System;
using DunGen.Weighting;

namespace DunGen.Editor
{
	[CustomEditor(typeof(Doorway))]
	[CanEditMultipleObjects]
	public class DoorwayInspector : UnityEditor.Editor
	{
		#region Constants

		private static readonly GUIContent socketGroupLabel = new GUIContent("Socket", "Determines if two doorways can connect. By default, only doorways with matching socket groups can be connected to one another");
		private static readonly GUIContent hideConditionalObjectsLabel = new GUIContent("Hide Conditional Objects?", "If checked, any in-scene door or blocked objects will be hidden for the purpose of reducing clutter. Has no effect on the runtime results");
		private static readonly GUIContent connectorSceneObjectsLabel = new GUIContent("Scene Objects", "In-scene objects to be KEPT when the doorway is in use (connected). Objects are kept on both sides of the doorway");
		private static readonly GUIContent blockerSceneObjectsLabel = new GUIContent("Scene Objects", "In-scene objects to be REMOVED when the doorway is in use (connected)");
		private static readonly GUIContent priorityLabel = new GUIContent("Priority", "When two doorways are connected, the one with the higher priority will have their door prefab used");
		private static readonly GUIContent doorPrefabLabel = new GUIContent("Random Prefab Weights", "When this doorway is in use (connected), a single prefab will be spawned from this list (and the connected doorway) at random");
		private static readonly GUIContent blockerPrefabLabel = new GUIContent("Random Prefab Weights", "When this doorway is NOT in use (unconnected), a single prefab will be spawned from this list (and the connected doorway) at random");
		private static readonly GUIContent avoidRotationLabel = new GUIContent("Avoid Rotation?", "If checked, the placed prefab will NOT be oriented to match the doorway");
		private static readonly GUIContent prefabPositionOffsetLabel = new GUIContent("Position Offset", "An optional position offset to apply when spawning this prefab, relative to the doorway's transform");
		private static readonly GUIContent prefabRotationOffsetLabel = new GUIContent("Rotation Offset", "An optional rotation offset to apply when spawning this prefab, reltative to the doorway's transform");
		private static readonly GUIContent connectorsLabel = new GUIContent("Connectors", "In-scene objects and prefabs used when the doorway is in use (connected)");
		private static readonly GUIContent blockersLabel = new GUIContent("Blockers", "In-scene objects and prefabs used when the doorway is not in use (not connected)");
		private static readonly GUIContent tagsLabel = new GUIContent("Tags", "A collection of tags that can be used in code to define custom connection logic (see DoorwayPairFinder.CustomConnectionRules)");

		#endregion

		private SerializedProperty socketProp;
		private SerializedProperty hideConditionalObjectsProp;
		private SerializedProperty priorityProp;
		private SerializedProperty avoidDoorPrefabRotationProp;
		private SerializedProperty doorPrefabPositionOffsetProp;
		private SerializedProperty doorPrefabRotationOffsetProp;
		private SerializedProperty avoidBlockerPrefabRotationProp;
		private SerializedProperty blockerPrefabPositionOffsetProp;
		private SerializedProperty blockerPrefabRotationOffsetProp;
		private SerializedProperty tagsProp;
		private SerializedProperty connectorPrefabs;
		private SerializedProperty blockerPrefabs;
		private ReorderableList connectorSceneObjectsList;
		private ReorderableList blockerSceneObjectsList;


		private void OnEnable()
		{
			socketProp = serializedObject.FindProperty("socket");
			hideConditionalObjectsProp = serializedObject.FindProperty("hideConditionalObjects");
			priorityProp = serializedObject.FindProperty(nameof(Doorway.DoorPrefabPriority));
			avoidDoorPrefabRotationProp = serializedObject.FindProperty(nameof(Doorway.AvoidRotatingDoorPrefab));
			doorPrefabPositionOffsetProp = serializedObject.FindProperty(nameof(Doorway.DoorPrefabPositionOffset));
			doorPrefabRotationOffsetProp = serializedObject.FindProperty(nameof(Doorway.DoorPrefabRotationOffset));
			avoidBlockerPrefabRotationProp = serializedObject.FindProperty(nameof(Doorway.AvoidRotatingBlockerPrefab));
			blockerPrefabPositionOffsetProp = serializedObject.FindProperty(nameof(Doorway.BlockerPrefabPositionOffset));
			blockerPrefabRotationOffsetProp = serializedObject.FindProperty(nameof(Doorway.BlockerPrefabRotationOffset));
			tagsProp = serializedObject.FindProperty(nameof(Doorway.Tags));
			connectorPrefabs = serializedObject.FindProperty(nameof(Doorway.ConnectorPrefabs));
			blockerPrefabs = serializedObject.FindProperty(nameof(Doorway.BlockerPrefabs));

			connectorSceneObjectsList = new ReorderableList(serializedObject, serializedObject.FindProperty(nameof(Doorway.ConnectorSceneObjects)), true, true, true, true);
			connectorSceneObjectsList.drawElementCallback = (rect, index, isActive, isFocused) => DrawGameObject(connectorSceneObjectsList, rect, index, true);
			connectorSceneObjectsList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, new GUIContent($"{connectorSceneObjectsLabel.text} ({connectorSceneObjectsList.count})", connectorSceneObjectsLabel.tooltip));

			blockerSceneObjectsList = new ReorderableList(serializedObject, serializedObject.FindProperty(nameof(Doorway.BlockerSceneObjects)), true, true, true, true);
			blockerSceneObjectsList.drawElementCallback = (rect, index, isActive, isFocused) => DrawGameObject(blockerSceneObjectsList, rect, index, true);
			blockerSceneObjectsList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, new GUIContent($"{blockerSceneObjectsLabel.text} ({blockerSceneObjectsList.count})", blockerSceneObjectsLabel.tooltip));
		}

		private void DrawGameObject(ReorderableList list, Rect rect, int index, bool requireSceneObject)
		{
			rect = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);

			EditorGUI.BeginChangeCheck();

			var element = list.serializedProperty.GetArrayElementAtIndex(index);
			var newObject = EditorGUI.ObjectField(rect, element.objectReferenceValue, typeof(GameObject), requireSceneObject);
			bool isValidEntry = true;

			if (newObject != null)
			{
				bool isAsset = EditorUtility.IsPersistent(newObject);
				isValidEntry = isAsset != requireSceneObject;
			}

			if (EditorGUI.EndChangeCheck() && isValidEntry)
				element.objectReferenceValue = newObject;
		}

		public override void OnInspectorGUI()
		{
			var doorways = targets.OfType<Doorway>();
			serializedObject.Update();

			if (socketProp.objectReferenceValue == null)
				socketProp.objectReferenceValue = DunGenSettings.Instance.DefaultSocket;

			EditorGUILayout.PropertyField(socketProp, socketGroupLabel);

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(hideConditionalObjectsProp, hideConditionalObjectsLabel);
			if (EditorGUI.EndChangeCheck())
			{
				foreach(var d in doorways)
					d.HideConditionalObjects = hideConditionalObjectsProp.boolValue;
			}

			EditorGUILayout.Space();
			EditorGUILayout.Space();

			EditorGUI.indentLevel++;

			// Connectors
			EditorGUILayout.BeginVertical("box");

			priorityProp.isExpanded = EditorGUILayout.Foldout(priorityProp.isExpanded, connectorsLabel, true);
			if (priorityProp.isExpanded)
			{
				EditorGUILayout.PropertyField(priorityProp, priorityLabel);
				EditorGUILayout.PropertyField(avoidDoorPrefabRotationProp, avoidRotationLabel);

				EditorGUILayout.PropertyField(doorPrefabPositionOffsetProp, prefabPositionOffsetLabel);
				EditorGUILayout.PropertyField(doorPrefabRotationOffsetProp, prefabRotationOffsetLabel);

				EditorGUILayout.Space();

				EditorGUILayout.BeginVertical(); // We create a group here so the whole list is a drag and drop target
				EditorGUILayout.PropertyField(connectorPrefabs, doorPrefabLabel);
				EditorGUILayout.EndVertical();

				HandlePropDragAndDrop(GUILayoutUtility.GetLastRect(), false, true, (doorway, obj) => doorway.ConnectorPrefabs.Entries.Add(new WeightedEntry<GameObject>(obj)));

				EditorGUILayout.Space();

				EditorGUILayout.BeginVertical(); // We create a group here so the whole list is a drag and drop target
				connectorSceneObjectsList.DoLayoutList();
				EditorGUILayout.EndVertical();

				HandlePropDragAndDrop(GUILayoutUtility.GetLastRect(), true, false, (doorway, obj) => doorway.ConnectorSceneObjects.Add(obj));
			}

			EditorGUILayout.EndVertical();

			// Blockers
			EditorGUILayout.BeginVertical("box");

			avoidBlockerPrefabRotationProp.isExpanded = EditorGUILayout.Foldout(avoidBlockerPrefabRotationProp.isExpanded, blockersLabel, true);
			if (avoidBlockerPrefabRotationProp.isExpanded)
			{
				EditorGUILayout.PropertyField(avoidBlockerPrefabRotationProp, avoidRotationLabel);

				EditorGUILayout.PropertyField(blockerPrefabPositionOffsetProp, prefabPositionOffsetLabel);
				EditorGUILayout.PropertyField(blockerPrefabRotationOffsetProp, prefabRotationOffsetLabel);

				EditorGUILayout.Space();

				EditorGUILayout.BeginVertical(); // We create a group here so the whole list is a drag and drop target
				EditorGUILayout.PropertyField(blockerPrefabs, blockerPrefabLabel);
				EditorGUILayout.EndVertical();

				HandlePropDragAndDrop(GUILayoutUtility.GetLastRect(), false, true, (doorway, obj) => doorway.BlockerPrefabs.Entries.Add(new WeightedEntry<GameObject>(obj)));


				EditorGUILayout.Space();

				EditorGUILayout.BeginVertical(); // We create a group here so the whole list is a drag and drop target
				blockerSceneObjectsList.DoLayoutList();
				EditorGUILayout.EndVertical();

				HandlePropDragAndDrop(GUILayoutUtility.GetLastRect(), true, false, (doorway, obj) => doorway.BlockerSceneObjects.Add(obj));
			}

			EditorGUILayout.EndVertical();
			EditorGUI.indentLevel--;

			EditorGUILayout.PropertyField(tagsProp, tagsLabel);

			serializedObject.ApplyModifiedProperties();



			bool isPlacementInvalid = false;

			// Check if any of the doorways have an invalid transform
			foreach (var doorway in doorways)
			{
				if (!doorway.ValidateTransform(out _, out _, out _))
				{
					isPlacementInvalid = true;
					break;
				}
			}

			// Show a warning message if the doorway(s) appear to be placed incorrectly and offer to fix the issue
			if (isPlacementInvalid)
			{
				EditorGUILayout.Space(20);
				EditorGUILayout.HelpBox("The doorway placement may not be correct. Doorways should be:\n\n- Facing away from the tile\n- Rotated to align with a world axis\n- Positioned at the edge of the tile's bounding box\n\nIf the doorway works as expected this message can be ignored, otherwise you can press the button below to try to automatically fix any placement issues\n", MessageType.Warning, true);
				EditorGUILayout.Space();

				if (GUILayout.Button(new GUIContent("Fix Doorway Placement")))
				{
					Undo.RecordObjects(doorways.Select(d => d.transform).ToArray(), "Snap Doorway");

					foreach (var doorway in doorways)
						doorway.TrySnapToCorrectedTransform();

					Undo.FlushUndoRecordObjects();
				}
			}
		}

		private void HandlePropDragAndDrop(Rect dragTargetRect, bool allowSceneObjects, bool allowAssetObjects, Action<Doorway, GameObject> addGameObject)
		{
			var evt = Event.current;
			var doorways = targets.OfType<Doorway>();

			if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
			{
				var validGameObjects = EditorUtil.GetValidGameObjects(DragAndDrop.objectReferences, allowSceneObjects, allowAssetObjects);

				if (dragTargetRect.Contains(evt.mousePosition) && validGameObjects.Any())
				{
					DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

					if (evt.type == EventType.DragPerform)
					{
						Undo.RecordObjects(doorways.ToArray(), "Modify Doorway");
						DragAndDrop.AcceptDrag();

						foreach (var doorway in doorways)
							foreach (var dragObject in validGameObjects)
								addGameObject(doorway, dragObject);

						Undo.FlushUndoRecordObjects();
					}
				}
			}
		}
	}
}