using DunGen.Weighting;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor.Drawers
{
	public abstract class WeightedEntryDrawer : PropertyDrawer
	{
		private sealed class Label
		{
			public static readonly GUIContent WeightsHeader = new GUIContent("Weights", "Determined how likely this element is to be chosen relative to other elements in the table. E.g. a weight of 2 makes this element twice as like to be picked than another element with a weight of 1");
			public static readonly GUIContent MainPathWeight = new GUIContent("Main Path Weight", "Base weight to use when the tile is on the main path");
			public static readonly GUIContent BranchPathWeight = new GUIContent("Branch Path Weight", "Base weight to use when the tile is on a branch path");
			public static readonly GUIContent DepthMode = new GUIContent("Depth Mode", "Which depth is used to modify the base weight.\nNone: No depth scaling applied\nAuto: Path depth is used if the tile is on the main path, and branch depth is used if the tile is on a branch path\nMain Path Depth: Uses the normalized (0-1) depth along the main path, even if the tile is on a branch\nBranch Depth: Uses the normalized (0-1) depth along a branch, or 0 if the tile is not on a branch");
			public static readonly GUIContent DepthScale = new GUIContent("Depth Scale", "A curve used to multiply the base weight by a value based on the normalized depth of the tile. The x-axis represents the normalized (0-1) depth of the tile");

		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			// Properties
			var valProp = property.FindPropertyRelative("Value");
			var mainProp = property.FindPropertyRelative("MainPathWeight");
			var branchProp = property.FindPropertyRelative("BranchPathWeight");
			var curveProp = property.FindPropertyRelative("DepthWeightScale");
			var curveModeProp = property.FindPropertyRelative("DepthScalingMode");

			float singleLine = EditorGUIUtility.singleLineHeight;
			float spacing = EditorGUIUtility.standardVerticalSpacing;

			// Line rects
			Rect valueLine = new Rect(position.x, position.y, position.width, singleLine);
			Rect foldoutLine = new Rect(position.x, valueLine.yMax + spacing, position.width, singleLine);

			// Draw Value (always visible)
			DrawValueProperty(property, valProp, valueLine);

			// Foldout toggle
			property.isExpanded = EditorGUI.Foldout(
				new Rect(foldoutLine.x, foldoutLine.y, 70f, singleLine),
				property.isExpanded,
				Label.WeightsHeader,
				true);

			if (property.isExpanded)
			{
				Rect mainPathWeightLine = new Rect(position.x, foldoutLine.yMax + spacing, position.width, singleLine);
				Rect branchPathWeightLine = new Rect(position.x, mainPathWeightLine.yMax + spacing, position.width, singleLine);
				Rect curveModeLine = new Rect(position.x, branchPathWeightLine.yMax + spacing, position.width, singleLine);
				Rect curveLine = new Rect(position.x, curveModeLine.yMax + spacing, position.width, singleLine);

				// Weights
				EditorGUI.PropertyField(mainPathWeightLine, mainProp, Label.MainPathWeight);
				EditorGUI.PropertyField(branchPathWeightLine, branchProp, Label.BranchPathWeight);

				// Depth mode
				EditorGUI.PropertyField(curveModeLine, curveModeProp, Label.DepthMode);

				// Depth curve
				using (new EditorGUI.DisabledGroupScope(curveModeProp.enumValueIndex == (int)DepthScalingMode.None))
				{
					EditorGUI.PropertyField(curveLine, curveProp, Label.DepthScale);
				}
			}

			EditorGUI.EndProperty();
		}

		protected virtual void DrawValueProperty(SerializedProperty property, SerializedProperty valueProperty, Rect valueRect)
		{
			EditorGUI.PropertyField(valueRect, valueProperty, GUIContent.none);
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float singleLine = EditorGUIUtility.singleLineHeight;
			float spacing = EditorGUIUtility.standardVerticalSpacing;

			// Base: value + foldout header
			int lineCount = 2;

			// Expanded: + weights + curve
			if (property.isExpanded)
				lineCount += 4;

			return (singleLine * lineCount) + (spacing * (lineCount - 1));
		}
	}

	[CustomPropertyDrawer(typeof(WeightedEntry<GameObject>))]
	public class GameObjectWeightedEntryDrawer : WeightedEntryDrawer
	{
		protected static bool TryGetReferenceOptions(SerializedProperty property, out bool allowSceneObjects, out bool allowPrefabAssets)
		{
			allowSceneObjects = true;
			allowPrefabAssets = true;

			// Attempt to get attribute from the root field (the field that holds `WeightedTable<GameObject>`)
			var target = property.serializedObject.targetObject;
			if (target == null)
				return false;

			string path = property.propertyPath; // e.g. "myTable.entries.Array.data[0]"
			int dotIndex = path.IndexOf('.');
			string rootFieldName = dotIndex >= 0 ? path.Substring(0, dotIndex) : path;

			FieldInfo rootField = target.GetType().GetField(rootFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (rootField == null)
				return false;

			var attr = rootField.GetCustomAttribute<GameObjectWeightFilterAttribute>(inherit: true);
			if (attr == null)
				return false;

			allowSceneObjects = attr.AllowSceneObjects;
			allowPrefabAssets = attr.AllowPrefabAssets;
			return true;
		}

		protected override void DrawValueProperty(SerializedProperty property, SerializedProperty valueProp, Rect valueRect)
		{
			bool hasOptions = TryGetReferenceOptions(property, out bool allowSceneObjects, out bool allowPrefabAssets);

			var currentObj = valueProp.objectReferenceValue;
			// If scene objects are not allowed, Unity's ObjectField can enforce this via the flag
			// If prefab assets are not allowed, validate after selection
			var newObj = EditorGUI.ObjectField(valueRect, GUIContent.none, currentObj, typeof(GameObject), allowSceneObjects);

			if (newObj != currentObj)
			{
				GameObject go = newObj as GameObject;

				if (hasOptions && go != null)
				{
					bool isAsset = EditorUtility.IsPersistent(go);
					bool isSceneObject = !isAsset;

					if (!allowPrefabAssets && isAsset)
						newObj = null;
					else if (!allowSceneObjects && isSceneObject)
						newObj = null;
				}

				valueProp.objectReferenceValue = newObj as GameObject;
			}
		}
	}
}