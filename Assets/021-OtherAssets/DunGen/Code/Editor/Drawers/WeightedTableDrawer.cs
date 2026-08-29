using DunGen.Weighting;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DunGen.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(WeightedTable<>), true)]
    public class WeightedTableDrawer : PropertyDrawer
    {
        // Cache ReorderableLists to avoid recreating them every frame
        private readonly Dictionary<string, ReorderableList> listCache = new Dictionary<string, ReorderableList>();


        private string GetCacheKey(SerializedProperty property)
        {
            int instanceId;

#if UNITY_6000_4_OR_NEWER
            instanceId = property.serializedObject.targetObject.GetEntityId().GetHashCode();
#else
            instanceId = property.serializedObject.targetObject.GetInstanceID();
#endif

			return instanceId + "-" + property.propertyPath;
        }

        private ReorderableList GetList(SerializedProperty property)
        {
            string key = GetCacheKey(property);
            var listProp = property.FindPropertyRelative("Entries");

            if (listCache.TryGetValue(key, out var list))
            {
                // Update the SerializedProperty reference in case the object was re-serialized
                list.serializedProperty = listProp;
                return list;
            }

            // Initialise new list
            list = new ReorderableList(property.serializedObject, listProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true);

            // Header
            list.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, $"{property.displayName} ({listProp.arraySize})");
            };

            // Element Drawing
            list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                var element = listProp.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(rect, element, GUIContent.none, true);
            };

            // Element Height
            list.elementHeightCallback = (int index) =>
            {
                var element = listProp.GetArrayElementAtIndex(index);
                return EditorGUI.GetPropertyHeight(element, GUIContent.none, true);
            };

            list.onAddCallback = (ReorderableList l) =>
            {
                // Add new element
                int newIndex = l.serializedProperty.arraySize;
                l.serializedProperty.arraySize++;
                l.index = newIndex;
                var newElement = l.serializedProperty.GetArrayElementAtIndex(newIndex);

                // Set default values
                var valueProp = newElement.FindPropertyRelative("Value");
                valueProp.objectReferenceValue = null;
                var mainWeightProp = newElement.FindPropertyRelative("MainPathWeight");
                mainWeightProp.floatValue = 1.0f;
                var branchWeightProp = newElement.FindPropertyRelative("BranchPathWeight");
                branchWeightProp.floatValue = 1.0f;
                var depthCurveProp = newElement.FindPropertyRelative("DepthWeightScale");
                depthCurveProp.animationCurveValue = AnimationCurve.Linear(0, 1, 1, 1);
            };

            listCache[key] = list;
            return list;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var list = GetList(property);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            list.DoList(position);

            EditorGUI.indentLevel = indent;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var list = GetList(property);
            return list.GetHeight();
        }
    }
}