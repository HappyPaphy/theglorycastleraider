using DunGen.Weighting;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(WeightedTable<GameObject>), true)]
    public class GameObjectWeightedTableDrawer : WeightedTableDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            base.OnGUI(position, property, label);

            HandleDragObject(position, property);
        }

        protected virtual void HandleDragObject(Rect position, SerializedProperty property)
        {
            var dragTargetRect = position;
            var evt = Event.current;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                var gameObjects = DragAndDrop.objectReferences
                    .Where(x => x is GameObject)
                    .Cast<GameObject>();

                var listProperty = property.FindPropertyRelative("Entries");
                var validDraggingObjects = GetValueGameObjects(gameObjects, property).ToArray();

                if (dragTargetRect.Contains(evt.mousePosition) && validDraggingObjects.Length > 0)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (var dragObject in validDraggingObjects)
                        {
                            int newIndex = listProperty.arraySize;
                            listProperty.InsertArrayElementAtIndex(newIndex);

                            var newElement = listProperty.GetArrayElementAtIndex(newIndex);
                            newElement.FindPropertyRelative("Value").objectReferenceValue = dragObject;
                            newElement.FindPropertyRelative("DepthWeightScale").animationCurveValue = AnimationCurve.Linear(0, 1, 1, 1);
                            newElement.FindPropertyRelative("MainPathWeight").floatValue = 1.0f;
                            newElement.FindPropertyRelative("BranchPathWeight").floatValue = 1.0f;
                        }

                        property.serializedObject.ApplyModifiedProperties();
                    }
                }
            }
        }

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

        protected List<GameObject> GetValueGameObjects(IEnumerable<GameObject> gameObjects, SerializedProperty property)
        {
            var validGameObjects = new List<GameObject>();

            if (!TryGetReferenceOptions(property, out bool allowSceneObjects, out bool allowPrefabAssets))
            {
                validGameObjects.AddRange(gameObjects);
                return validGameObjects;
            }

            foreach (var go in gameObjects)
            {
                if (go == null)
                    continue;

                bool isAsset = EditorUtility.IsPersistent(go);
                bool isSceneObject = !isAsset;

                if (allowPrefabAssets && isAsset)
                    validGameObjects.Add(go);
                else if (allowSceneObjects && isSceneObject)
                    validGameObjects.Add(go);
            }

            return validGameObjects;
        }
    }
}