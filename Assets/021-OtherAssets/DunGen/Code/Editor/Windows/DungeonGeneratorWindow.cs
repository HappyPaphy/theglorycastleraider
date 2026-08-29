using DunGen.Editor.Drawers.DunGen.Editor.Drawers;
using DunGen.Generation;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
	public sealed class DungeonGeneratorWindow : EditorWindow
	{
		private class SerializedDungeonGeneratorContainer : ScriptableObject
		{
			[EditorTimeDungeonGenerator]
			public DungeonGenerator Generator;
		}

		private SerializedDungeonGeneratorContainer container;
		private SerializedObject serializedObject;
		private SerializedProperty generatorSettingsProperty;
		private GameObject lastDungeon;
		private bool overwriteExisting = true;

		[MenuItem("Window/DunGen/Generate Dungeon")]
		private static void OpenWindow()
		{
			GetWindow<DungeonGeneratorWindow>(false, "New Dungeon", true);
		}

		private void OnGUI()
		{
			if (serializedObject == null)
				return;

			serializedObject.Update();
			EditorGUILayout.PropertyField(generatorSettingsProperty, GUIContent.none);
			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.Space();

			overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing?", overwriteExisting);

			if (GUILayout.Button("Generate"))
				GenerateDungeon();
		}

		private void OnEnable()
		{
			// Create a container object to hold our generator
			container = CreateInstance<SerializedDungeonGeneratorContainer>();
			container.Generator = new DungeonGenerator
			{
				AllowTilePooling = false
			};

			// Setup serialization
			serializedObject = new SerializedObject(container);
			generatorSettingsProperty = serializedObject
				.FindProperty(nameof(SerializedDungeonGeneratorContainer.Generator))
				.FindPropertyRelative(nameof(DungeonGenerator.Settings));

			container.Generator.OnGenerationStatusChanged += HandleGenerationStatusChanged;
		}

		private void OnDisable()
		{
			if (container != null && container.Generator != null)
				container.Generator.OnGenerationStatusChanged -= HandleGenerationStatusChanged;

			if (container != null)
				DestroyImmediate(container);

			container = null;
			serializedObject = null;
			generatorSettingsProperty = null;
		}

		private void GenerateDungeon()
		{
			if (lastDungeon != null)
			{
				if (overwriteExisting)
					UnityUtil.Destroy(lastDungeon);
				else
					container.Generator.DetachDungeon();
			}

			lastDungeon = new GameObject("Dungeon Layout");
			container.Generator.Root = lastDungeon;

			Undo.RegisterCreatedObjectUndo(lastDungeon, "Create Procedural Dungeon");
			container.Generator.Settings.GenerateAsynchronously = false;

			container.Generator.Generate(new DungeonGenerationRequest(container.Generator.Settings));
		}

		private void HandleGenerationStatusChanged(DungeonGenerator generator, GenerationStatus status)
		{
			if (status == GenerationStatus.Failed)
			{
				UnityUtil.Destroy(lastDungeon);
				lastDungeon = container.Generator.Root = null;
			}
		}
	}
}