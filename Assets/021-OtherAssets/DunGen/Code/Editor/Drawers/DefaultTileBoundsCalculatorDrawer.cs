using DunGen.TileBounds;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor.Drawers
{
	[CustomPropertyDrawer(typeof(DefaultTileBoundsCalculator))]
	public class DefaultTileBoundsCalculatorDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			var layerMask = property.FindPropertyRelative(nameof(DefaultTileBoundsCalculator.LayerMask));
			var includeRenderers = property.FindPropertyRelative(nameof(DefaultTileBoundsCalculator.IncludeRenderers));
			var includeSpriteRenderers = property.FindPropertyRelative(nameof(DefaultTileBoundsCalculator.IncludeSpriteRenderers));
			var includeColliders = property.FindPropertyRelative(nameof(DefaultTileBoundsCalculator.IncludeColliders));
			var includeTriggerColliders = property.FindPropertyRelative(nameof(DefaultTileBoundsCalculator.IncludeTriggerColliders));
			var includeTerrain = property.FindPropertyRelative(nameof(DefaultTileBoundsCalculator.IncludeTerrain));
			var includeInactive = property.FindPropertyRelative(nameof(DefaultTileBoundsCalculator.IncludeInactive));

			Rect currentRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

			// Layer Mask
			EditorGUI.PropertyField(currentRect, layerMask);
			currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

			// Header
			EditorGUI.LabelField(currentRect, "Include", EditorStyles.boldLabel);
			currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

			// Renderers
			EditorGUI.PropertyField(currentRect, includeRenderers, new GUIContent("Renderers"));
			currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

			// Sprite Renderers (Indented)
			EditorGUI.indentLevel++;
			EditorGUI.BeginDisabledGroup(!includeRenderers.boolValue);
			EditorGUI.PropertyField(currentRect, includeSpriteRenderers, new GUIContent("Sprite Renderers"));
			EditorGUI.EndDisabledGroup();
			EditorGUI.indentLevel--;
			currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

			// Colliders
			EditorGUI.PropertyField(currentRect, includeColliders, new GUIContent("Colliders"));
			currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

			// Trigger Colliders (Indented)
			EditorGUI.indentLevel++;
			EditorGUI.BeginDisabledGroup(!includeColliders.boolValue);
			EditorGUI.PropertyField(currentRect, includeTriggerColliders, new GUIContent("Trigger Colliders"));
			EditorGUI.EndDisabledGroup();
			EditorGUI.indentLevel--;
			currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

			// Terrain
			EditorGUI.PropertyField(currentRect, includeTerrain, new GUIContent("Terrain"));
			currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

			// Inactive
			EditorGUI.PropertyField(currentRect, includeInactive, new GUIContent("Inactive"));

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			int lineCount = 8; // LayerMask, Header, Renderers, SpriteRenderers, Colliders, TriggerColliders, Terrain, Inactive
			return (EditorGUIUtility.singleLineHeight * lineCount) + (EditorGUIUtility.standardVerticalSpacing * (lineCount - 1));
		}
	}
}