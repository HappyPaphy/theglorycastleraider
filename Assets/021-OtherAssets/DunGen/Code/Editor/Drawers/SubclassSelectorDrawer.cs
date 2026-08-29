using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace DunGen.Editor.Drawers
{
	[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
	public class SubclassSelectorDrawer : PropertyDrawer
	{
		#region Nested Types

		public class DerivedTypeInfo
		{
			public Type Type { get; }
			public string DisplayName { get; }
			public string Description { get; }
			public string HelpUrl { get; }

			public DerivedTypeInfo(Type type,
				string displayName,
				string description,
				string helpUrl)
			{
				Type = type;
				DisplayName = displayName;
				Description = description;
				HelpUrl = helpUrl;
			}
		}

		#endregion

		private List<DerivedTypeInfo> derivedTypes;
		private bool initialized = false;
		private static GUIContent helpIconContent;

		// Cache for custom drawer types (Type -> DrawerType)
		private static Dictionary<Type, Type> customDrawerTypeCache;
		// Cache for instantiated drawers for this property instance (Type -> DrawerInstance)
		private readonly Dictionary<Type, PropertyDrawer> drawerInstances = new Dictionary<Type, PropertyDrawer>();
		// Cache for ReorderableLists for array properties (PropertyPath -> ReorderableList)
		private readonly Dictionary<string, ReorderableList> lists = new Dictionary<string, ReorderableList>();


		private void Init()
		{
			Type baseType = fieldInfo.FieldType;

			if (baseType.IsArray || (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(List<>)))
				baseType = baseType.GetGenericArguments()[0];

			var types = TypeCache.GetTypesDerivedFrom(baseType)
				.Where(t => !t.IsAbstract)
				.ToList();

			if (!baseType.IsAbstract)
				types.Add(baseType);

			derivedTypes = new List<DerivedTypeInfo>(types.Count);

			if (!baseType.IsAbstract)
			{
				var typeInfo = GetDerivedTypeInfo(baseType, out bool isHidden);

				if (!isHidden)
					derivedTypes.Add(typeInfo);
			}

			foreach (var type in types)
			{
				// Ignore Unity types that we can't instantiate
				if (typeof(MonoBehaviour).IsAssignableFrom(type) ||
					typeof(ScriptableObject).IsAssignableFrom(type))
					continue;

				var typeInfo = GetDerivedTypeInfo(type, out bool isHidden);

				if(isHidden)
					continue;

				derivedTypes.Add(typeInfo);
			}

			initialized = true;
		}

		private static DerivedTypeInfo GetDerivedTypeInfo(Type type, out bool isHidden)
		{
			isHidden = false;

			var displayAttribute = type.GetCustomAttribute<SubclassDisplayAttribute>(false);

			if (displayAttribute != null && displayAttribute.Hidden)
				isHidden = true;

			string displayName = displayAttribute != null && !string.IsNullOrEmpty(displayAttribute.DisplayName) ?
				displayAttribute.DisplayName :
				ObjectNames.NicifyVariableName(type.Name);

			string description = displayAttribute != null && !string.IsNullOrEmpty(displayAttribute.Description) ?
				displayAttribute.Description :
				null;

			string helpUrl = displayAttribute != null && !string.IsNullOrEmpty(displayAttribute.HelpUrl) ?
				displayAttribute.HelpUrl :
				null;

			return new DerivedTypeInfo(type, displayName, description, helpUrl);
		}

		private static Type GetCustomDrawerType(Type type)
		{
			if (customDrawerTypeCache == null)
			{
				customDrawerTypeCache = new Dictionary<Type, Type>();
				var drawerTypes = TypeCache.GetTypesDerivedFrom<PropertyDrawer>();
				foreach (var candidateDrawerType in drawerTypes)
				{
					var attributes = candidateDrawerType.GetCustomAttributes(typeof(CustomPropertyDrawer), true);
					foreach (var attr in attributes)
					{
						if (attr is CustomPropertyDrawer drawerAttr)
						{
							var typeField = typeof(CustomPropertyDrawer).GetField("m_Type", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
							if (typeField != null)
							{
								var targetType = typeField.GetValue(drawerAttr) as Type;
								if (targetType != null && !customDrawerTypeCache.ContainsKey(targetType))
								{
									customDrawerTypeCache[targetType] = candidateDrawerType;
								}
							}
						}
					}
				}
			}

			return customDrawerTypeCache.TryGetValue(type, out var drawerType) ? drawerType : null;
		}

		private PropertyDrawer GetDrawerForType(Type type)
		{
			if (type == null) return null;

			if (!drawerInstances.TryGetValue(type, out var drawer))
			{
				var drawerType = GetCustomDrawerType(type);
				if (drawerType != null)
				{
					drawer = (PropertyDrawer)Activator.CreateInstance(drawerType);
				}
				drawerInstances[type] = drawer;
			}

			if (drawer != null)
			{
				// Use reflection to set m_FieldInfo since fieldInfo is read-only
				var fieldInfoField = typeof(PropertyDrawer).GetField("m_FieldInfo", BindingFlags.Instance | BindingFlags.NonPublic);
				if (fieldInfoField != null)
					fieldInfoField.SetValue(drawer, fieldInfo);
			}

			return drawer;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			if (!initialized)
				Init();

			if (helpIconContent == null)
			{
				helpIconContent = EditorGUIUtility.IconContent("_Help");
				if (helpIconContent != null)
					helpIconContent.tooltip = "Open documentation";
			}

			if (property.isArray && property.propertyType == SerializedPropertyType.ManagedReference)
				DrawList(position, property, label);
			else
				DrawSingle(position, property, label);

			EditorGUI.EndProperty();
		}

		protected void DrawList(Rect position, SerializedProperty property, GUIContent label)
		{
			if (property == null || !property.isArray)
			{
				EditorGUI.LabelField(position, label, new GUIContent("Expected an array/list"));
				return;
			}

			var list = GetOrCreateReorderableList(property, label);

			// ReorderableList handles its own layout rect; we were given a rect, so use DoList.
			list.DoList(position);
		}

		private ReorderableList GetOrCreateReorderableList(SerializedProperty property, GUIContent label)
		{
			if (lists.TryGetValue(property.propertyPath, out var list))
				return list;

			list = new ReorderableList(property.serializedObject, property, true, true, true, true);

			list.drawHeaderCallback = rect =>
			{
				EditorGUI.LabelField(rect, label);
			};

			list.onAddCallback = l =>
			{
				int newIndex = property.arraySize;
				property.InsertArrayElementAtIndex(newIndex);

				var element = property.GetArrayElementAtIndex(newIndex);
				element.managedReferenceValue = null;

				property.serializedObject.ApplyModifiedProperties();
			};

			list.drawElementCallback = (rect, index, isActive, isFocused) =>
			{
				var element = property.GetArrayElementAtIndex(index);

				rect.y += 2f;
				rect.height = EditorGUI.GetPropertyHeight(element, GUIContent.none, true);

				EditorGUI.PropertyField(rect, element, GUIContent.none, true);
			};

			list.elementHeightCallback = index =>
			{
				var element = property.GetArrayElementAtIndex(index);
				return EditorGUI.GetPropertyHeight(element, GUIContent.none, true) + 4f;
			};

			lists[property.propertyPath] = list;
			return list;
		}

		protected List<DerivedTypeInfo> FilterDerivedTypes(List<DerivedTypeInfo> types, SerializedProperty property)
		{
			var filteredList = new List<DerivedTypeInfo>(types);
			var excludeSubclassAttributes = property.GetAttributes<ExcludeSubclassAttribute>();

			foreach(var excludeAttr in excludeSubclassAttributes)
			{
				if(excludeAttr.ExcludeDerivedTypes)
					filteredList.RemoveAll(info => excludeAttr.ExcludedTypes.Any(excludedType => excludedType.IsAssignableFrom(info.Type)));
				else
					filteredList.RemoveAll(info => excludeAttr.ExcludedTypes.Contains(info.Type));
			}

			return filteredList;
		}

		private static bool HasVisibleChildProperties(SerializedProperty property)
		{
			SerializedProperty copy = property.Copy();
			return copy.NextVisible(true) && !SerializedProperty.EqualContents(copy, property.GetEndProperty());
		}

		protected void DrawSingle(Rect position, SerializedProperty property, GUIContent label)
		{
			float lineHeight = EditorGUIUtility.singleLineHeight;
			var subclassSelectorAttribute = (SubclassSelectorAttribute)attribute;
			bool allowNone = subclassSelectorAttribute.AllowNone;

			var filteredDerivedTypes = FilterDerivedTypes(derivedTypes, property);

			// Build options
			string[] options = allowNone
				? new string[filteredDerivedTypes.Count + 1]
				: new string[filteredDerivedTypes.Count];

			if (allowNone)
				options[0] = "None";

			for (int i = 0; i < filteredDerivedTypes.Count; i++)
			{
				int optionIndex = allowNone ? i + 1 : i;
				options[optionIndex] = filteredDerivedTypes[i].DisplayName;
			}

			// Current selection
			int selectedIndex = 0;
			bool hasChildProperties = false;
			Type currentType = null;
			bool hasManagedReference = !string.IsNullOrEmpty(property.managedReferenceFullTypename);

			if (hasManagedReference)
			{
				string[] split = property.managedReferenceFullTypename.Split(' ');
				if (split.Length == 2)
				{
					string currentTypeName = split[1];
					for (int i = 0; i < filteredDerivedTypes.Count; i++)
					{
						if (filteredDerivedTypes[i].Type.FullName == currentTypeName)
						{
							selectedIndex = allowNone ? i + 1 : i;
							currentType = filteredDerivedTypes[i].Type;
							hasChildProperties = HasVisibleChildProperties(property);
							break;
						}
					}
				}
			}

			// If "None" is not allowed, ensure a newly-added null element is instantiated immediately.
			if (!allowNone && !hasManagedReference && filteredDerivedTypes.Count > 0)
			{
				currentType = filteredDerivedTypes[0].Type;
				property.managedReferenceValue = Activator.CreateInstance(currentType);
				property.serializedObject.ApplyModifiedProperties();

				selectedIndex = 0;
				hasChildProperties = HasVisibleChildProperties(property);
			}

			int derivedTypeIndex = allowNone ? selectedIndex - 1 : selectedIndex;
			bool typeSelected = derivedTypeIndex >= 0 && derivedTypeIndex < filteredDerivedTypes.Count;
			string currentHelpUrl = typeSelected ? filteredDerivedTypes[derivedTypeIndex].HelpUrl : null;
			bool showHelpButton = !string.IsNullOrEmpty(currentHelpUrl) && helpIconContent != null;

			const float helpButtonPadding = 2f;
			float helpButtonSize = showHelpButton ? lineHeight : 0f;

			float labelWidth = label.text.Length > 0 ? EditorGUIUtility.labelWidth : 5f;

			Rect labelRect = new Rect(position.x, position.y, labelWidth, lineHeight);
			Rect popupRect = new Rect(labelRect.xMax, position.y,
				position.width - EditorGUIUtility.labelWidth - helpButtonSize - (showHelpButton ? helpButtonPadding : 0f),
				lineHeight);
			Rect helpButtonRect = new Rect(popupRect.xMax + helpButtonPadding, position.y, helpButtonSize, lineHeight);

			// Label or foldout
			if (hasChildProperties)
				property.isExpanded = EditorGUI.Foldout(labelRect, property.isExpanded, label, true);
			else
				EditorGUI.LabelField(labelRect, label);

			int newIndex = EditorGUI.Popup(popupRect, selectedIndex, options);

			if (newIndex != selectedIndex)
			{
				if (allowNone && newIndex == 0)
				{
					property.managedReferenceValue = null;
					property.serializedObject.ApplyModifiedProperties();
					currentType = null;
					hasChildProperties = false;
				}
				else
				{
					int index = allowNone ? newIndex - 1 : newIndex;
					currentType = filteredDerivedTypes[index].Type;
					property.managedReferenceValue = Activator.CreateInstance(currentType);
					property.serializedObject.ApplyModifiedProperties();
					hasChildProperties = HasVisibleChildProperties(property);
				}

				selectedIndex = newIndex;
				derivedTypeIndex = allowNone ? selectedIndex - 1 : selectedIndex;
				typeSelected = derivedTypeIndex >= 0 && derivedTypeIndex < filteredDerivedTypes.Count;
				currentHelpUrl = typeSelected ? filteredDerivedTypes[derivedTypeIndex].HelpUrl : null;
				showHelpButton = !string.IsNullOrEmpty(currentHelpUrl) && helpIconContent != null;
			}

			// Icon help button
			if (showHelpButton)
			{
				GUIStyle iconStyle = GUI.skin.FindStyle("IconButton") ?? GUIStyle.none;
				if (GUI.Button(helpButtonRect, helpIconContent, iconStyle))
				{
					Application.OpenURL(currentHelpUrl);
				}
			}

			// Description
			if (typeSelected)
			{
				var displayInfo = filteredDerivedTypes[derivedTypeIndex];
				if (!string.IsNullOrEmpty(displayInfo.Description))
				{
					Rect helpBoxRect = new Rect(position.x, position.y + lineHeight + EditorGUIUtility.standardVerticalSpacing,
						position.width, EditorGUIUtility.singleLineHeight * 2);

					EditorGUI.HelpBox(helpBoxRect, displayInfo.Description, MessageType.Info);
					lineHeight += helpBoxRect.height + EditorGUIUtility.standardVerticalSpacing;
				}
			}

			// Child properties
			if (hasChildProperties && property.isExpanded)
			{
				EditorGUI.indentLevel++;

				PropertyDrawer customDrawer = GetDrawerForType(currentType);

				if (customDrawer != null)
				{
					Rect childRect = new Rect(position.x, position.y + lineHeight + EditorGUIUtility.standardVerticalSpacing,
						position.width, position.height - lineHeight - EditorGUIUtility.standardVerticalSpacing);

					customDrawer.OnGUI(childRect, property, GUIContent.none);
				}
				else
				{
					Rect childRect = new Rect(position.x, position.y + lineHeight + EditorGUIUtility.standardVerticalSpacing,
						position.width, lineHeight);

					SerializedProperty copy = property.Copy();
					SerializedProperty endProperty = copy.GetEndProperty();

					copy.NextVisible(true);
					while (!SerializedProperty.EqualContents(copy, endProperty))
					{
						childRect.height = EditorGUI.GetPropertyHeight(copy, null, true);

						EditorGUI.BeginChangeCheck();
						EditorGUI.PropertyField(childRect, copy, true);
						if (EditorGUI.EndChangeCheck())
							property.serializedObject.ApplyModifiedProperties();

						childRect.y += childRect.height + EditorGUIUtility.standardVerticalSpacing;
						if (!copy.NextVisible(false))
							break;
					}
				}

				EditorGUI.indentLevel--;
			}
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (!initialized)
				Init();

			if (property.isArray && property.propertyType == SerializedPropertyType.ManagedReference)
				return GetOrCreateReorderableList(property, label).GetHeight();

			float height = EditorGUIUtility.singleLineHeight; // For the type selector
			var filteredDerivedTypes = FilterDerivedTypes(derivedTypes, property);

			int selectedIndex = 0;
			Type currentType = null;

			if (!string.IsNullOrEmpty(property.managedReferenceFullTypename))
			{
				// Determine which type is currently selected
				string[] split = property.managedReferenceFullTypename.Split(' ');
				bool allowNone = ((SubclassSelectorAttribute)attribute).AllowNone;

				if (split.Length == 2)
				{
					string currentTypeName = split[1];

					for (int i = 0; i < filteredDerivedTypes.Count; i++)
					{
						if (filteredDerivedTypes[i].Type.FullName == currentTypeName)
						{
							selectedIndex = allowNone ? i + 1 : i;
							currentType = filteredDerivedTypes[i].Type;
							break;
						}
					}
				}

				if (!(allowNone && selectedIndex == 0))
				{
					var displayInfo = filteredDerivedTypes[allowNone ? selectedIndex - 1 : selectedIndex];

					// Add height for description if we have one
					if (!string.IsNullOrEmpty(displayInfo.Description))
						height += EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
				}

				// Find out if we have any child properties to display
				SerializedProperty copy = property.Copy();
				SerializedProperty endProperty = copy.GetEndProperty();
				bool hasChildProperties = copy.NextVisible(true) && !SerializedProperty.EqualContents(copy, endProperty);

				// Only add height for child properties if we have them and they're expanded
				if (hasChildProperties && property.isExpanded)
				{
					PropertyDrawer customDrawer = GetDrawerForType(currentType);

					if (customDrawer != null)
					{
						height += customDrawer.GetPropertyHeight(property, GUIContent.none) + EditorGUIUtility.standardVerticalSpacing;
					}
					else
					{
						// `copy` is already on the first child because of NextVisible(true) above
						while (!SerializedProperty.EqualContents(copy, endProperty))
						{
							height += EditorGUI.GetPropertyHeight(copy, null, true) + EditorGUIUtility.standardVerticalSpacing;
							if (!copy.NextVisible(false))
								break;
						}
					}
				}
			}

			return height;
		}
	}
}
