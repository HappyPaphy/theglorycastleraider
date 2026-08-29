using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DunGen.Editor
{
    [CustomEditor(typeof(TileSet))]
    public sealed class TileSetInspector : UnityEditor.Editor
    {
        #region Labels

        private static class Label
        {
            public static readonly GUIContent Tiles = new GUIContent("Tiles", "Tiles and associated weights belonging to this set");
        }

        #endregion

        private SerializedProperty tilesProp;
        private SerializedProperty lockPrefabsProp;
        private ReorderableList lockPrefabsList;


        private void OnEnable()
        {
            tilesProp = serializedObject.FindProperty(nameof(TileSet.Tiles));
            lockPrefabsProp = serializedObject.FindProperty(nameof(TileSet.LockPrefabs));

            lockPrefabsList = new ReorderableList(serializedObject, lockPrefabsProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true);

            // Header
            lockPrefabsList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, $"{lockPrefabsProp.displayName} ({lockPrefabsProp.arraySize})");
            };

            // Element Drawing
            lockPrefabsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                EditorGUI.indentLevel++;
                var element = lockPrefabsProp.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(rect, element, GUIContent.none, true);
                EditorGUI.indentLevel--;
            };

            // Element Height
            lockPrefabsList.elementHeightCallback = (int index) =>
            {
                var element = lockPrefabsProp.GetArrayElementAtIndex(index);
                return EditorGUI.GetPropertyHeight(element, GUIContent.none, true);
            };

            lockPrefabsList.onAddCallback = (ReorderableList l) =>
            {
                // Add new element
                int newIndex = l.serializedProperty.arraySize;
                l.serializedProperty.arraySize++;
                l.index = newIndex;
                var newElement = l.serializedProperty.GetArrayElementAtIndex(newIndex);

                // Set default values
                var socket = newElement.FindPropertyRelative(nameof(LockedDoorwayAssociation.Socket));
                socket.objectReferenceValue = null;

                var prefabs = newElement.FindPropertyRelative(nameof(LockedDoorwayAssociation.Prefabs));
                prefabs.arraySize = 0;
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(tilesProp, Label.Tiles);
            EditorGUILayout.Space();
            lockPrefabsList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
