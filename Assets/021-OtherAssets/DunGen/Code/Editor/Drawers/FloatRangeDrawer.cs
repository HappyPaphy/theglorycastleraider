using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace DunGen.Editor.Drawers
{
	[CustomPropertyDrawer(typeof(FloatRange))]
	sealed class FloatRangeDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var minProperty = property.FindPropertyRelative("Min");
			var maxProperty = property.FindPropertyRelative("Max");

			EditorGUI.BeginProperty(position, label, property);

			// Get limits from attribute if present
			float minLimit = 0.0f;
			float maxLimit = 1.0f;

			if (fieldInfo != null)
			{
				var limitAtt = fieldInfo.GetCustomAttribute<FloatRangeLimitAttribute>();

				if (limitAtt != null)
				{
					minLimit = limitAtt.MinLimit;
					maxLimit = limitAtt.MaxLimit;
				}
			}

			// Get current values
			float minValue = minProperty.floatValue;
			float maxValue = maxProperty.floatValue;

			const float fieldWidth = 40f;
			const float spacing = 4f;

			Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
			Rect minRect = new Rect(labelRect.xMax, position.y, fieldWidth, position.height);
			Rect sliderRect = new Rect(minRect.xMax + spacing, position.y, position.width - labelRect.width - fieldWidth * 2 - spacing * 2, position.height);
			Rect maxRect = new Rect(sliderRect.xMax + spacing, position.y, fieldWidth, position.height);

			EditorGUI.LabelField(labelRect, label);

			// Min float field
			minValue = EditorGUI.FloatField(minRect, minValue);
			minValue = Mathf.Clamp(minValue, minLimit, maxValue);

			// Max float field
			maxValue = EditorGUI.FloatField(maxRect, maxValue);
			maxValue = Mathf.Clamp(maxValue, minValue, maxLimit);

			// Slider
			EditorGUI.MinMaxSlider(sliderRect, ref minValue, ref maxValue, minLimit, maxLimit);

			minProperty.floatValue = minValue;
			maxProperty.floatValue = maxValue;

			EditorGUI.EndProperty();
		}
	}
}
