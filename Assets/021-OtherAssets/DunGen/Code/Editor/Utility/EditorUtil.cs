using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
	public static class EditorUtil
	{
		public static IEnumerable<GameObject> GetValidGameObjects(IEnumerable<object> objects, bool allowSceneObjects, bool allowAssets)
		{
			foreach (var gameObject in objects.OfType<GameObject>())
			{
				bool isSceneObject;

#if UNITY_6000_4_OR_NEWER
				isSceneObject = gameObject.scene.handle.GetRawData() != 0;
#else
				isSceneObject = gameObject.scene.handle != 0;
#endif

				if ((isSceneObject && allowSceneObjects) || (!isSceneObject && allowAssets))
					yield return gameObject;
			}
		}

		/// <summary>
		/// Draws a simple GUI for an IntRange
		/// </summary>
		/// <param name="name">A descriptive label</param>
		/// <param name="range">The range to modify</param>
		public static void DrawIntRange(string name, IntRange range)
		{
			DrawIntRange(new GUIContent(name), range);
		}

		/// <summary>
		/// Draws a simple GUI for an IntRange
		/// </summary>
		/// <param name="name">A descriptive label</param>
		/// <param name="range">The range to modify</param>
		public static void DrawIntRange(GUIContent name, IntRange range)
		{
			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.PrefixLabel(name);
			GUILayout.FlexibleSpace();
			range.Min = EditorGUILayout.IntField(range.Min, InspectorConstants.IntFieldWidth);
			EditorGUILayout.LabelField("-", InspectorConstants.SmallWidth);
			range.Max = EditorGUILayout.IntField(range.Max, InspectorConstants.IntFieldWidth);

			EditorGUILayout.EndHorizontal();
		}

		/// <summary>
		/// Draws the GUI for a list of Unity.Object. Allows users to add/remove/modify a specific type
		/// deriving from Unity.Object (such as GameObject, or a Component type)
		/// </summary>
		/// <param name="header">A descriptive header</param>
		/// <param name="objects">The object list to edit</param>
		/// <param name="allowedSelectionTypes">The types of objects that are allowed to be selected</param>
		/// <typeparam name="T">The type of object in the list</typeparam>
		public static void DrawObjectList<T>(string header, IList<T> objects, GameObjectSelectionTypes allowedSelectionTypes, UnityEngine.Object owningObject) where T : UnityEngine.Object
		{
			DrawObjectList(new GUIContent(header), objects, allowedSelectionTypes, owningObject);
		}

		/// <summary>
		/// Draws the GUI for a list of Unity.Object. Allows users to add/remove/modify a specific type
		/// deriving from Unity.Object (such as GameObject, or a Component type)
		/// </summary>
		/// <param name="header">A descriptive header</param>
		/// <param name="objects">The object list to edit</param>
		/// <param name="allowedSelectionTypes">The types of objects that are allowed to be selected</param>
		/// <typeparam name="T">The type of object in the list</typeparam>
		public static void DrawObjectList<T>(GUIContent header, IList<T> objects, GameObjectSelectionTypes allowedSelectionTypes, UnityEngine.Object owningObject) where T : UnityEngine.Object
		{
			bool allowSceneSelection = (allowedSelectionTypes & GameObjectSelectionTypes.InScene) == GameObjectSelectionTypes.InScene;
			bool allowPrefabSelection = (allowedSelectionTypes & GameObjectSelectionTypes.Prefab) == GameObjectSelectionTypes.Prefab;

			GUILayout.BeginVertical("box");
			EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
			EditorGUI.indentLevel = 0;

			int toDeleteIndex = -1;
			GUILayout.BeginVertical("box");

			for (int i = 0; i < objects.Count; i++)
			{
				T obj = objects[i];
				EditorGUILayout.BeginHorizontal();

				T tempObj = (T)EditorGUILayout.ObjectField("", obj, typeof(T), allowSceneSelection);

				if (tempObj != null)
				{
					bool isAsset = EditorUtility.IsPersistent(tempObj);

					if ((isAsset && allowPrefabSelection) || (!isAsset && allowSceneSelection))
						objects[i] = tempObj;
				}
				else
					objects[i] = null;

				if (GUILayout.Button("x", EditorStyles.miniButton, InspectorConstants.SmallButtonWidth))
					toDeleteIndex = i;

				EditorGUILayout.EndHorizontal();
			}

			if (toDeleteIndex >= 0)
			{
				Undo.RecordObject(owningObject, "Delete Object Entry");
				objects.RemoveAt(toDeleteIndex);
				Undo.FlushUndoRecordObjects();
			}

			if (GUILayout.Button("Add New"))
			{
				Undo.RecordObject(owningObject, "Add Object Entry");
				objects.Add(default(T));
				Undo.FlushUndoRecordObjects();
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.EndVertical();


			// Handle dragging objects into the list
			var dragTargetRect = GUILayoutUtility.GetLastRect();
			var evt = Event.current;

			if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
			{
				bool isDraggingValidObjects = false;

				foreach (var obj in DragAndDrop.objectReferences)
				{
					if (obj is T)
					{
						isDraggingValidObjects = true;
						break;
					}
				}

				if (dragTargetRect.Contains(evt.mousePosition) && isDraggingValidObjects)
				{
					DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

					if (evt.type == EventType.DragPerform)
					{
						Undo.RecordObject(owningObject, "Drag Objects");
						DragAndDrop.AcceptDrag();

						foreach (var dragObject in DragAndDrop.objectReferences.OfType<T>())
							objects.Add(dragObject);

						EditorUtility.SetDirty(owningObject);
						Undo.FlushUndoRecordObjects();
					}
				}
			}
		}

		public static void DrawKey(Rect position, GUIContent label, KeyManager manager, ref int keyID)
		{
			if (manager == null)
				EditorGUI.LabelField(position, "<Missing Key Manager>");
			else
			{
				string[] keyNames = manager.Keys.Select(x => x.Name).ToArray();
				GUIContent[] keyLabels = keyNames.Select(x => new GUIContent(x)).ToArray();

				var key = manager.GetKeyByID(keyID);
				int nameIndex = EditorGUI.Popup(position, label, Array.IndexOf(keyNames, key.Name), keyLabels);
				keyID = manager.GetKeyByName(keyNames[nameIndex]).ID;
			}
		}

		public static void DrawKeyLayout(GUIContent label, KeyManager manager, ref int keyID)
		{
			if (manager == null)
				EditorGUILayout.LabelField("<Missing Key Manager>");
			else
			{
				string[] keyNames = manager.Keys.Select(x => x.Name).ToArray();
				GUIContent[] keyLabels = keyNames.Select(x => new GUIContent(x)).ToArray();

				var key = manager.GetKeyByID(keyID);
				int nameIndex = EditorGUILayout.Popup(label, Array.IndexOf(keyNames, key.Name), keyLabels);
				keyID = manager.GetKeyByName(keyNames[nameIndex]).ID;
			}
		}

		public static void DrawKeySelection(string label, KeyManager manager, List<KeyLockPlacement> keys, bool includeRange)
		{
			if (manager == null)
				return;

			EditorGUILayout.BeginVertical("box");
			EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

			int toDeleteIndex = -1;
			string[] keyNames = manager.Keys.Select(x => x.Name).ToArray();

			for (int i = 0; i < keys.Count; i++)
			{
				EditorGUILayout.BeginVertical("box");

				var key = manager.GetKeyByID(keys[i].ID);

				EditorGUILayout.BeginHorizontal();

				int nameIndex = EditorGUILayout.Popup(Array.IndexOf(keyNames, key.Name), keyNames);
				keys[i].ID = manager.GetKeyByName(keyNames[nameIndex]).ID;

				if (GUILayout.Button("x", EditorStyles.miniButton, InspectorConstants.SmallButtonWidth))
					toDeleteIndex = i;

				EditorGUILayout.EndHorizontal();

				if (includeRange)
					EditorUtil.DrawIntRange("Count", keys[i].Range);

				EditorGUILayout.EndVertical();
			}

			if (toDeleteIndex > -1)
				keys.RemoveAt(toDeleteIndex);

			if (GUILayout.Button("Add"))
				keys.Add(new KeyLockPlacement() { ID = manager.Keys[0].ID });

			EditorGUILayout.EndVertical();
		}

		public static void DrawStraightenSettings(SerializedProperty straightenSettingsProperty, bool showCheckboxes)
		{
			var straightenChanceProp = straightenSettingsProperty.FindPropertyRelative(nameof(PathStraighteningSettings.StraightenChance));
			EditorGUILayout.PropertyField(straightenChanceProp, new GUIContent("Straighten Chance", "The chance that the next tile spawned will continue in the same direction as the previous tile"));

			if (showCheckboxes)
			{
				var canStraightenMainPathProp = straightenSettingsProperty.FindPropertyRelative(nameof(PathStraighteningSettings.CanStraightenMainPath));
				var canStraightenBranchPathsProp = straightenSettingsProperty.FindPropertyRelative(nameof(PathStraighteningSettings.CanStraightenBranchPaths));
				EditorGUILayout.PropertyField(canStraightenMainPathProp, new GUIContent("Straighten Main Path", "Whether or not the main path should be straightened using StraightenChance"));
				EditorGUILayout.PropertyField(canStraightenBranchPathsProp, new GUIContent("Straighten Branch Paths", "Whether or not branch paths should be straightened using StraightenChance"));
			}
		}

		public static void DrawStraightenSettingsWithOverrides(SerializedProperty straightenSettingsProperty, bool showCheckboxes)
		{
			var overrideStraightenChanceProp = straightenSettingsProperty.FindPropertyRelative(nameof(PathStraighteningSettings.OverrideStraightenChance));
			var straightenChanceProp = straightenSettingsProperty.FindPropertyRelative(nameof(PathStraighteningSettings.StraightenChance));

			DrawOverrideProperty(overrideStraightenChanceProp, straightenChanceProp, new GUIContent("Straighten Chance", "The chance that the next tile spawned will continue in the same direction as the previous tile"));

			if (showCheckboxes)
			{
				var overrideMainPathProp = straightenSettingsProperty.FindPropertyRelative(nameof(PathStraighteningSettings.OverrideCanStraightenMainPath));
				var canStraightenMainPathProp = straightenSettingsProperty.FindPropertyRelative(nameof(PathStraighteningSettings.CanStraightenMainPath));

				var overrideBranchPathsProp = straightenSettingsProperty.FindPropertyRelative(nameof(PathStraighteningSettings.OverrideCanStraightenBranchPaths));
				var canStraightenBranchPathsProp = straightenSettingsProperty.FindPropertyRelative(nameof(PathStraighteningSettings.CanStraightenBranchPaths));

				DrawOverrideProperty(overrideMainPathProp, canStraightenMainPathProp, new GUIContent("Straighten Main Path", "Whether or not the main path should be straightened using StraightenChance"));
				DrawOverrideProperty(overrideBranchPathsProp, canStraightenBranchPathsProp, new GUIContent("Straighten Branch Paths", "Whether or not branch paths should be straightened using StraightenChance"));
			}
		}

		public static void DrawOverrideProperty(SerializedProperty overrideEnabledProp, SerializedProperty valueProp, GUIContent label)
		{
			if (overrideEnabledProp.propertyType != SerializedPropertyType.Boolean)
			{
				Debug.LogError("OverrideEnabled property must be a boolean");
				return;
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.PropertyField(overrideEnabledProp, GUIContent.none, GUILayout.Width(15));

				using (new EditorGUI.DisabledGroupScope(!overrideEnabledProp.boolValue))
				{
					EditorGUILayout.PropertyField(valueProp, label);
				}
			}
		}

		public static void ObjectField(Rect rect, SerializedProperty property, GUIContent label, Type objectType, bool allowSceneObjects, bool allowAssets)
		{
			EditorGUI.BeginChangeCheck();
			var newValue = EditorGUI.ObjectField(rect, label, property.objectReferenceValue, objectType, allowSceneObjects);

			if (EditorGUI.EndChangeCheck())
			{
				if (newValue == null)
					property.objectReferenceValue = newValue;
				else
				{
					bool isAsset = EditorUtility.IsPersistent(newValue);

					if (isAsset && allowAssets || allowSceneObjects && !isAsset)
						property.objectReferenceValue = newValue;
				}
			}
		}

		public static void ObjectFieldLayout(SerializedProperty property, GUIContent label, Type objectType, bool allowSceneObjects, bool allowAssets)
		{
			EditorGUI.BeginChangeCheck();
			var newValue = EditorGUILayout.ObjectField(label, property.objectReferenceValue, objectType, allowSceneObjects);

			if (EditorGUI.EndChangeCheck())
			{
				if (newValue == null)
					property.objectReferenceValue = newValue;
				else
				{
					bool isAsset = EditorUtility.IsPersistent(newValue);

					if (isAsset && allowAssets || allowSceneObjects && !isAsset)
						property.objectReferenceValue = newValue;
				}
			}
		}

		public static FieldInfo GetFieldInfo(this SerializedProperty property)
		{
			Type type = property.serializedObject.targetObject.GetType();
			FieldInfo field = null;

			string[] path = property.propertyPath.Replace(".Array.data[", "[").Split('.');

			foreach (string element in path)
			{
				if (element.Contains("["))
				{
					string fieldName = element.Substring(0, element.IndexOf("["));
					field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					type = field.FieldType.GetElementType() ?? field.FieldType.GetGenericArguments()[0];
				}
				else
				{
					field = type.GetField(element, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					type = field.FieldType;
				}
			}

			return field;
		}

		public static T[] GetAttributes<T>(this SerializedProperty property) where T : Attribute
		{
			FieldInfo fieldInfo = property.GetFieldInfo();

			if (fieldInfo == null)
				return Array.Empty<T>();

			return (T[])fieldInfo.GetCustomAttributes(typeof(T), true);
		}

		public static object GetTargetObject(this SerializedProperty property)
		{
			if (property == null)
				return null;

			string path = property.propertyPath.Replace(".Array.data[", "[");
			object obj = property.serializedObject.targetObject;

			string[] elements = path.Split('.');

			foreach (var element in elements)
			{
				if (element.Contains("["))
				{
					string elementName = element.Substring(0, element.IndexOf("["));
					int index = int.Parse(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
					obj = GetValue(obj, elementName, index);
				}
				else
				{
					obj = GetValue(obj, element);
				}
			}

			return obj;
		}

		private static object GetValue(object source, string name)
		{
			if (source == null)
				return null;

			var type = source.GetType();

			while (type != null)
			{
				var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

				if (f != null)
					return f.GetValue(source);

				var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

				if (p != null)
					return p.GetValue(source, null);

				type = type.BaseType;
			}

			return null;
		}

		private static object GetValue(object source, string name, int index)
		{
			var enumerable = GetValue(source, name) as IEnumerable;

			if (enumerable == null)
				return null;

			var enumerator = enumerable.GetEnumerator();

			for (int i = 0; i <= index; i++)
				if (!enumerator.MoveNext())
					return null;

			return enumerator.Current;
		}
	}
}
