using UnityEngine;
using UnityEditor;

namespace DunGen.Editor.Drawers
{
	[CustomPropertyDrawer(typeof(GameObjectFilterAttribute))]
	public class GameObjectFilterDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var filterAttribute = attribute as GameObjectFilterAttribute;

			// Ensure we are working with an Object Reference
			if (property.propertyType != SerializedPropertyType.ObjectReference)
			{
				EditorGUI.LabelField(position, label.text, "Use [GameObjectFilter] with Objects only.");
				return;
			}

			EditorGUI.BeginProperty(position, label, property);
			EditorGUI.BeginChangeCheck();

			Object currentObject = property.objectReferenceValue;
			Object newObject = EditorGUI.ObjectField(position, label, currentObject, typeof(GameObject), filterAttribute.AllowSceneObjects);

			if (EditorGUI.EndChangeCheck())
			{
				if (newObject == null)
					property.objectReferenceValue = null;
				else
				{
					// Check if the assigned object is a Scene Object or an Asset (Prefab)
					bool isPersistent = EditorUtility.IsPersistent(newObject);
					bool isValid;

					if (filterAttribute.AllowSceneObjects && filterAttribute.AllowPrefabAssets)
						isValid = true;
					else if (filterAttribute.AllowSceneObjects)
						isValid = !isPersistent;
					else if (filterAttribute.AllowPrefabAssets)
						isValid = isPersistent;
					else
						isValid = false;

					if (isValid)
						property.objectReferenceValue = newObject;
				}
			}

			EditorGUI.EndProperty();
		}
	}
}