using DunGen.Analysis;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor.Drawers
{
	[CustomPropertyDrawer(typeof(AnalysisGenerationSettings))]
	public class AnalysisGenerationSettingsDrawer : PropertyDrawer
	{
		private static class Labels
		{
			public static readonly GUIContent DungeonFlow = new GUIContent("Dungeon Flow", "The dungeon flow to analyse");
			public static readonly GUIContent PipelineOverride = new GUIContent("Pipeline Override", "An optional pipeline override to use when generating the dungeon for analysis");
			public static readonly GUIContent Iterations = new GUIContent("Iterations", "The number of generation iterations to perform during the analysis");
			public static readonly GUIContent MaxFailedAttempts = new GUIContent("Max Failed Attempts", "The maximum number of failed generation attempts before aborting an iteration");
			public static readonly GUIContent RunOnStart = new GUIContent("Run On Start", "Whether to automatically start the analysis when entering Play mode");
			public static readonly GUIContent MaximumAnalysisTime = new GUIContent("Maximum Analysis Time", "The maximum amount of time (in seconds) to spend performing the analysis (0 = no limit)");
			public static readonly GUIContent SeedGenerationMode = new GUIContent("Seed Mode", "The method used to generate seeds for each iteration\n\nRandom: A random seed is used each iteration\n\nIncremental: The random seed will increase by 1 with each iteration\n\nFixed: The same seed will be used for every iteration");
			public static readonly GUIContent Seed_Random = new GUIContent("Seed", "Starting random seed to use when generation the seeds for each dungeon iteration");
			public static readonly GUIContent Seed_Incremental = new GUIContent("Starting Seed", "The initial seed to use. This will be incremented with each iteration");
			public static readonly GUIContent Seed_Fixed = new GUIContent("Seed", "The exact seed to use for every iteration");
			public static readonly GUIContent ClearDungeonOnCompletion = new GUIContent("Clear Dungeon On Completion", "Whether to clear the generated dungeon when the analysis is complete");
			public static readonly GUIContent AllowTilePooling = new GUIContent("Allow Tile Pooling", "Whether to allow tile pooling when generating the dungeon for analysis. This is only possible if tile pooling is enabled in the project settings");
			public static readonly GUIContent LogMessagesToConsole = new GUIContent("Log Messages To Console", "Whether to log analysis messages to the console");
			public static readonly GUIContent AnalysisModules = new GUIContent("Analysis Modules", "The analysis modules to use during the generation analysis. Custom modules can be created by creating a new class that implements `IGenerationAnalysisModule`");
		}

		private sealed class Properties
		{
			public SerializedProperty DungeonFlow;
			public SerializedProperty PipelineOverride;
			public SerializedProperty Iterations;
			public SerializedProperty MaxFailedAttempts;
			public SerializedProperty RunOnStart;
			public SerializedProperty MaximumAnalysisTime;
			public SerializedProperty SeedGenerationMode;
			public SerializedProperty Seed;
			public SerializedProperty ClearDungeonOnCompletion;
			public SerializedProperty AllowTilePooling;
			public SerializedProperty LogMessagesToConsole;
			public SerializedProperty AnalysisModules;
		}


		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			var props = GetProperties(property);

			float y = position.y;

			// Header
			y = DrawProperty(position, y, props.DungeonFlow, Labels.DungeonFlow);
			y = DrawProperty(position, y, props.PipelineOverride, Labels.PipelineOverride);
			y = DrawProperty(position, y, props.Iterations, Labels.Iterations);
			y = DrawProperty(position, y, props.MaxFailedAttempts, Labels.MaxFailedAttempts);
			y = DrawProperty(position, y, props.RunOnStart, Labels.RunOnStart);
			y = DrawProperty(position, y, props.MaximumAnalysisTime, Labels.MaximumAnalysisTime);
			y = DrawProperty(position, y, props.SeedGenerationMode, Labels.SeedGenerationMode);

			// Change the label for the Seed property based on the selected SeedGenerationMode
			GUIContent seedLabel = props.SeedGenerationMode.enumValueIndex switch
			{
				(int)AnalysisGenerationSettings.SeedMode.Random => Labels.Seed_Random,
				(int)AnalysisGenerationSettings.SeedMode.Incremental => Labels.Seed_Incremental,
				(int)AnalysisGenerationSettings.SeedMode.Fixed => Labels.Seed_Fixed,
				_ => Labels.Seed_Random,
			};

			y = DrawProperty(position, y, props.Seed, seedLabel);
			y = DrawProperty(position, y, props.ClearDungeonOnCompletion, Labels.ClearDungeonOnCompletion);

			bool tilePoolingEnabled = DunGenSettings.Instance.EnableTilePooling;

			using (new EditorGUI.DisabledScope(!tilePoolingEnabled))
			{
				y = DrawProperty(position, y, props.AllowTilePooling, Labels.AllowTilePooling);
			}

			y = DrawProperty(position, y, props.LogMessagesToConsole, Labels.LogMessagesToConsole);
			y = DrawProperty(position, y, props.AnalysisModules, Labels.AnalysisModules);

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var props = GetProperties(property);

			float height = 0f;

			height += GetPropertyHeightWithSpacing(props.DungeonFlow);
			height += GetPropertyHeightWithSpacing(props.PipelineOverride);
			height += GetPropertyHeightWithSpacing(props.Iterations);
			height += GetPropertyHeightWithSpacing(props.MaxFailedAttempts);
			height += GetPropertyHeightWithSpacing(props.RunOnStart);
			height += GetPropertyHeightWithSpacing(props.MaximumAnalysisTime);
			height += GetPropertyHeightWithSpacing(props.SeedGenerationMode);
			height += GetPropertyHeightWithSpacing(props.Seed);

			height += GetPropertyHeightWithSpacing(props.ClearDungeonOnCompletion);
			height += GetPropertyHeightWithSpacing(props.AllowTilePooling);
			height += GetPropertyHeightWithSpacing(props.LogMessagesToConsole);

			height += GetPropertyHeightWithSpacing(props.AnalysisModules);
			height += EditorGUIUtility.standardVerticalSpacing;

			return height;
		}

		private static Properties GetProperties(SerializedProperty property)
		{
			return new Properties
			{
				DungeonFlow = property.FindPropertyRelative(nameof(AnalysisGenerationSettings.DungeonFlow)),
				PipelineOverride = property.FindPropertyRelative(nameof(AnalysisGenerationSettings.PipelineOverride)),
				Iterations = property.FindPropertyRelative(nameof(AnalysisGenerationSettings.Iterations)),
				MaxFailedAttempts = property.FindPropertyRelative(nameof(AnalysisGenerationSettings.MaxFailedAttempts)),
				RunOnStart = property.FindPropertyRelative(nameof(AnalysisGenerationSettings.RunOnStart)),
				MaximumAnalysisTime = property.FindPropertyRelative(nameof(AnalysisGenerationSettings.MaximumAnalysisTime)),
				SeedGenerationMode = property.FindPropertyRelative(nameof(AnalysisGenerationSettings.SeedGenerationMode)),
				Seed = property.FindPropertyRelative(nameof(AnalysisGenerationSettings.Seed)),
				ClearDungeonOnCompletion = property.FindPropertyRelative(nameof(AnalysisGenerationSettings.ClearDungeonOnCompletion)),
				AllowTilePooling = property.FindPropertyRelative(nameof(AnalysisGenerationSettings.AllowTilePooling)),
				LogMessagesToConsole = property.FindPropertyRelative(nameof(AnalysisGenerationSettings.LogMessagesToConsole)),
				AnalysisModules = property.FindPropertyRelative(nameof(AnalysisGenerationSettings.AnalysisModules)),
			};
		}

		private static float DrawProperty(Rect fullRect, float y, SerializedProperty property, GUIContent label)
		{
			float h = EditorGUI.GetPropertyHeight(property, true);
			var r = new Rect(fullRect.x, y, fullRect.width, h);
			EditorGUI.PropertyField(r, property, label, true);

			return y + h + EditorGUIUtility.standardVerticalSpacing;
		}

		private static float GetPropertyHeightWithSpacing(SerializedProperty property)
		{
			return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.standardVerticalSpacing;
		}
	}
}