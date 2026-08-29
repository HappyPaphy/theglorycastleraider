using DunGen.Generation.Steps;
using DunGen.PostProcessing;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DunGen.Editor.Drawers
{
	[CustomPropertyDrawer(typeof(PostProcessingStep), true)]
	public class PostProcessingStepDrawer : PropertyDrawer
	{
		private readonly Dictionary<string, ReorderableList> lists = new Dictionary<string, ReorderableList>();


		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			var stepsProperty = property.FindPropertyRelative(nameof(PostProcessingStep.Steps));
			var list = GetOrCreateList(stepsProperty);

			list.DoList(position);

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var stepsProperty = property.FindPropertyRelative(nameof(PostProcessingStep.Steps));
			return GetOrCreateList(stepsProperty).GetHeight();
		}

		protected virtual ReorderableList GetOrCreateList(SerializedProperty stepsProperty)
		{
			if (lists.TryGetValue(stepsProperty.propertyPath, out var existing))
				return existing;

			var list = new ReorderableList(
				stepsProperty.serializedObject,
				stepsProperty,
				draggable: true,
				displayHeader: true,
				displayAddButton: true,
				displayRemoveButton: true);

			list.drawHeaderCallback = rect =>
			{
				EditorGUI.LabelField(rect, new GUIContent($"Steps ({list.count})"));
			};

			list.onAddCallback = l =>
			{
				int newIndex = stepsProperty.arraySize;
				stepsProperty.InsertArrayElementAtIndex(newIndex);

				var element = stepsProperty.GetArrayElementAtIndex(newIndex);

				var enabledProperty = element.FindPropertyRelative(nameof(PostProcessingStep.PostProcessStepEntry.Enabled));
				var stepProperty = element.FindPropertyRelative(nameof(PostProcessingStep.PostProcessStepEntry.Step));

				// Ensure the new entry is not duplicated from the previous element
				enabledProperty.boolValue = true;
				stepProperty.managedReferenceValue = null;

				stepsProperty.serializedObject.ApplyModifiedProperties();
			};

			list.onRemoveCallback = l =>
			{
				ReorderableList.defaultBehaviours.DoRemoveButton(l);
				stepsProperty.serializedObject.ApplyModifiedProperties();
			};

			list.drawElementCallback = (rect, index, isActive, isFocused) =>
			{
				var element = stepsProperty.GetArrayElementAtIndex(index);

				var enabledProperty = element.FindPropertyRelative(nameof(PostProcessingStep.PostProcessStepEntry.Enabled));
				var stepProperty = element.FindPropertyRelative(nameof(PostProcessingStep.PostProcessStepEntry.Step));

				rect.y += 2f;

				var step = stepProperty.GetTargetObject() as IPostProcessStep;
				string stepName = step != null ? step.DisplayName : "None";

				const float padding = 6f;
				const float enabledToggleWidth = 18f;

				var headerRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
				var toggleRect = new Rect(headerRect.x, headerRect.y, enabledToggleWidth, headerRect.height);
				var labelRect = new Rect(toggleRect.xMax + padding, headerRect.y, headerRect.width - enabledToggleWidth - padding, headerRect.height);

				enabledProperty.boolValue = EditorGUI.Toggle(toggleRect, enabledProperty.boolValue);
				EditorGUI.LabelField(labelRect, stepName);

				var stepRect = new Rect(rect.x, headerRect.yMax + 2f, rect.width, rect.height - headerRect.height - 2f);

				using (new EditorGUI.DisabledScope(!enabledProperty.boolValue))
				{
					stepRect.height = EditorGUI.GetPropertyHeight(stepProperty, GUIContent.none, true);
					EditorGUI.PropertyField(stepRect, stepProperty, GUIContent.none, true);
				}
			};

			list.elementHeightCallback = index =>
			{
				var element = stepsProperty.GetArrayElementAtIndex(index);
				var stepProperty = element.FindPropertyRelative(nameof(PostProcessingStep.PostProcessStepEntry.Step));

				// 2 lines:
				// - header (toggle + stepName)
				// - property field
				const float topPadding = 2f;
				const float betweenLines = 2f;

				float headerHeight = EditorGUIUtility.singleLineHeight;
				float stepHeight = EditorGUI.GetPropertyHeight(stepProperty, GUIContent.none, true);

				return topPadding + headerHeight + betweenLines + stepHeight + 2f;
			};

			lists[stepsProperty.propertyPath] = list;
			return list;
		}
	}
}