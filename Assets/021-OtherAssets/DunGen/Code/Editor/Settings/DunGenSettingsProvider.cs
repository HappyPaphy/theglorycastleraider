using System.Collections.Generic;
using UnityEditor;

namespace DunGen.Editor.Settings
{
	public static class DunGenSettingsProvider
	{
		[SettingsProvider]
		public static SettingsProvider CreateDunGenSettingsProvider()
		{
			var provider = new SettingsProvider("Project/DunGen", SettingsScope.Project)
			{
				guiHandler = (searchContext) =>
				{
					var settings = DunGenSettings.Instance;

					if (settings == null)
					{
						EditorGUILayout.HelpBox("Failed to load DunGenSettings asset", MessageType.Error);
						return;
					}

					var serializedSettings = new SerializedObject(settings);

					// Save current label width and set a wider one
					float originalLabelWidth = EditorGUIUtility.labelWidth;
					EditorGUIUtility.labelWidth = 200;

					EditorGUI.BeginChangeCheck();

					// Draw the default inspector
					var editor = UnityEditor.Editor.CreateEditor(settings);
					editor.OnInspectorGUI();

					// Apply changes if any were made
					if (EditorGUI.EndChangeCheck())
					{
						serializedSettings.ApplyModifiedProperties();
						//AssetDatabase.SaveAssets();
					}

					// Restore original label width
					EditorGUIUtility.labelWidth = originalLabelWidth;
				},

				// Populate the search keywords
				keywords = new HashSet<string>(new[]
				{
					"DunGen",
					"dungeon",
					"broadphase",
					"collision",
					"tile",
					"cache",
					"pool",
					"pooling",
					"tag",
					"tags"
				})
			};

			return provider;
		}
	}
}