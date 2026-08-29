using DunGen.Generation;
using DunGen.Generation.Steps;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DunGen.Editor.Inspectors
{
	[CustomEditor(typeof(GenerationPipeline))]
	public class GenerationPipelineInspector : UnityEditor.Editor
	{
		#region Helpers

		private static class Labels
		{
			public static readonly GUIContent ServicesHeader = new GUIContent("Services");
			public static readonly GUIContent ServicesDescription = new GUIContent("A collection of services used for dungeon generation. You can provide custom behaviour by subclassing the relevant class and assigning it here.");
			public static readonly GUIContent Services = new GUIContent("Service Classes", "A collection of services used for dungeon generation");
			public static readonly GUIContent StepsHeader = new GUIContent("Generation Steps");
			public static readonly GUIContent StepsDescription = new GUIContent("The steps that make up the dungeon generation pipeline. You can override the behaviour by subclassing the relevant step class and assigning it here. Custom serialized fields will be accessible using the foldout.");
			public static readonly GUIContent ExtensionStepsHeader = new GUIContent("Extension Steps");
			public static readonly GUIContent ExtensionStepsDescription = new GUIContent("Additional custom steps to run at various stages of the generation pipeline.");
			public static readonly GUIContent TileInjectionStep = new GUIContent("Tile Injection", "Injects tiles into the dungeon layout before generation begins. Subclass from `DunGen.Generation.Steps.TileInjectionStep` to override behaviour");
			public static readonly GUIContent PreProcessingStep = new GUIContent("Pre-Processing", "Prepares the dungeon layout before generation begins. Subclass from `DunGen.Generation.Steps.PreProcessingStep` to override behaviour");
			public static readonly GUIContent MainPathStep = new GUIContent("Main Path", "Generates the main path of the dungeon. Subclass from `DunGen.Generation.Steps.MainPathStep` to override behaviour");
			public static readonly GUIContent BranchingStep = new GUIContent("Branching", "Generates branches off the main path. Subclass from `DunGen.Generation.Steps.BranchingStep` to override behaviour");
			public static readonly GUIContent ValidateRequiredTilesStep = new GUIContent("Validate Required Tiles", "Validates that all required injected tiles are present in the layout. Subclass from `DunGen.Generation.Steps.ValidateRequiredTilesStep` to override behaviour");
			public static readonly GUIContent BranchPruningStep = new GUIContent("Branch Pruning", "Trims tiles meet certain criteria off the ends of branches. Subclass from `DunGen.Generation.Steps.BranchPruningStep` to override behaviour");
			public static readonly GUIContent FinaliseLayoutStep = new GUIContent("Finalise Layout", "Finalises the dungeon layout before instantiation. Subclass from `DunGen.Generation.Steps.FinaliseLayoutStep` to override behaviour");
			public static readonly GUIContent InstantiateTilesStep = new GUIContent("Instantiate Tiles", "Instantiates the tile prefabs as actual GameObjects in the dungeon layout. Subclass from `DunGen.Generation.Steps.InstantiateTilesStep` to override behaviour");
			public static readonly GUIContent ProcessPropsStep = new GUIContent("Process Props", "Selects and activates which props should be spawned within instantiated tiles. Subclass from `DunGen.Generation.Steps.ProcessPropsStep` to override behaviour");
			public static readonly GUIContent LockAndKeyPlacementStep = new GUIContent("Lock & Key Placement", "Places locks and keys within the dungeon layout. Subclass from `DunGen.Generation.Steps.LockAndKeyPlacementStep` to override behaviour");
			public static readonly GUIContent PostProcessingStep = new GUIContent("Post-Processing", "Performs any final processing on the dungeon after all tiles and props have been instantiated. Subclass from `DunGen.Generation.Steps.PostProcessingStep` to override behaviour");
			public static readonly GUIContent ExtensionSteps = new GUIContent("Extension Steps", "Additional custom steps to run at various stages of the generation pipeline");

			public static readonly GUIContent Anchor = new GUIContent("Anchor", "The point in the generation pipeline where this step should be executed");
			public static readonly GUIContent Order = new GUIContent("Order", "The order in which this step should be executed relative to other steps at the same anchor point. Lower numbers are processed first");
			public static readonly GUIContent Step = new GUIContent("Step", "The custom generation step to execute");
		}

		private sealed class Properties
		{
			public SerializedProperty Services;
			public SerializedProperty TileInjectionStep;
			public SerializedProperty PreProcessingStep;
			public SerializedProperty MainPathStep;
			public SerializedProperty BranchingStep;
			public SerializedProperty ValidateRequiredTilesStep;
			public SerializedProperty BranchPruningStep;
			public SerializedProperty FinaliseLayoutStep;
			public SerializedProperty InstantiateTilesStep;
			public SerializedProperty ProcessPropsStep;
			public SerializedProperty LockAndKeyPlacementStep;
			public SerializedProperty PostProcessingStep;
			public SerializedProperty ExtensionSteps;


			public Properties(SerializedObject obj)
			{
				Services = obj.FindProperty(nameof(GenerationPipeline.Services));
				TileInjectionStep = obj.FindProperty(nameof(GenerationPipeline.TileInjectionStep));
				PreProcessingStep = obj.FindProperty(nameof(GenerationPipeline.PreProcessingStep));
				MainPathStep = obj.FindProperty(nameof(GenerationPipeline.MainPathStep));
				BranchingStep = obj.FindProperty(nameof(GenerationPipeline.BranchingStep));
				ValidateRequiredTilesStep = obj.FindProperty(nameof(GenerationPipeline.ValidateRequiredTilesStep));
				BranchPruningStep = obj.FindProperty(nameof(GenerationPipeline.BranchPruningStep));
				FinaliseLayoutStep = obj.FindProperty(nameof(GenerationPipeline.FinaliseLayoutStep));
				InstantiateTilesStep = obj.FindProperty(nameof(GenerationPipeline.InstantiateTilesStep));
				ProcessPropsStep = obj.FindProperty(nameof(GenerationPipeline.ProcessPropsStep));
				LockAndKeyPlacementStep = obj.FindProperty(nameof(GenerationPipeline.LockAndKeyPlacementStep));
				PostProcessingStep = obj.FindProperty(nameof(GenerationPipeline.PostProcessingStep));
				ExtensionSteps = obj.FindProperty(nameof(GenerationPipeline.ExtensionSteps));
			}
		}

		#endregion

		private static readonly Color extensionStepColour = Color.cyan;

		private Properties props;
		private ReorderableList extensionStepsList;


		protected virtual void OnEnable()
		{
			props = new Properties(serializedObject);

			extensionStepsList = new ReorderableList(serializedObject,
				props.ExtensionSteps,
				draggable: true,
				displayHeader: true,
				displayAddButton: true,
				displayRemoveButton: true);

			extensionStepsList.drawHeaderCallback = rect =>
			{
				EditorGUI.LabelField(rect, new GUIContent($"Steps ({extensionStepsList.count})"));
			};

			extensionStepsList.onAddCallback = l =>
			{
				int newIndex = props.ExtensionSteps.arraySize;
				props.ExtensionSteps.InsertArrayElementAtIndex(newIndex);

				var element = props.ExtensionSteps.GetArrayElementAtIndex(newIndex);

				var enabledProperty = element.FindPropertyRelative(nameof(ExtensionStepEntry.Enabled));
				var anchorProperty = element.FindPropertyRelative(nameof(ExtensionStepEntry.Anchor));
				var orderProperty = element.FindPropertyRelative(nameof(ExtensionStepEntry.Order));
				var stepProperty = element.FindPropertyRelative(nameof(ExtensionStepEntry.Step));

				// Ensure the new entry is not duplicated from the previous element
				enabledProperty.boolValue = true;
				anchorProperty.enumValueIndex = 0;
				orderProperty.intValue = 0;
				stepProperty.managedReferenceValue = null;

				props.ExtensionSteps.serializedObject.ApplyModifiedProperties();
			};

			extensionStepsList.drawElementCallback = (rect, index, isActive, isFocused) =>
			{
				var element = props.ExtensionSteps.GetArrayElementAtIndex(index);

				var enabledProperty = element.FindPropertyRelative(nameof(ExtensionStepEntry.Enabled));
				var anchorProperty = element.FindPropertyRelative(nameof(ExtensionStepEntry.Anchor));
				var orderProperty = element.FindPropertyRelative(nameof(ExtensionStepEntry.Order));
				var stepProperty = element.FindPropertyRelative(nameof(ExtensionStepEntry.Step));

				float spacing = EditorGUIUtility.standardVerticalSpacing;
				float lineHeight = EditorGUIUtility.singleLineHeight;

				// Use the Anchor property's foldout state for the element foldout
				bool expanded = anchorProperty.isExpanded;
				var step = stepProperty.GetTargetObject() as CustomGenerationStep;
				string stepTypeName = step != null ? step.DisplayName : "None";

				rect.y += 1f;

				// Left padding so it doesn't clip with the foldout arrow
				rect.x += 10f;
				rect.width -= 10f;

				// Line 1: foldout + enabled toggle + type label
				var headerRect = new Rect(rect.x, rect.y, rect.width, lineHeight);

				float foldoutWidth = 14f;
				float toggleWidth = 18f;
				float headerPad = 2f;

				var foldoutRect = new Rect(headerRect.x, headerRect.y, foldoutWidth, lineHeight);
				var enabledRect = new Rect(foldoutRect.xMax + headerPad, headerRect.y, toggleWidth, lineHeight);
				var nameRect = new Rect(enabledRect.xMax + headerPad, headerRect.y, headerRect.width - (foldoutWidth + toggleWidth + (headerPad * 2f)), lineHeight);

				expanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, toggleOnLabelClick: false);
				enabledProperty.boolValue = EditorGUI.Toggle(enabledRect, enabledProperty.boolValue);

				using (new EditorGUI.DisabledScope(!enabledProperty.boolValue))
					EditorGUI.LabelField(nameRect, stepTypeName, EditorStyles.boldLabel);

				anchorProperty.isExpanded = expanded;

				if (!expanded)
					return;

				// Line 2: Anchor
				var anchorRect = new Rect(rect.x, headerRect.yMax + spacing, rect.width, lineHeight);

				// Line 3: Order
				var orderRect = new Rect(rect.x, anchorRect.yMax + spacing, rect.width, lineHeight);

				// Line 4+: Step
				float stepHeight = EditorGUI.GetPropertyHeight(stepProperty, includeChildren: true);
				var stepRect = new Rect(rect.x, orderRect.yMax + spacing, rect.width, stepHeight);

				using (new EditorGUI.DisabledScope(!enabledProperty.boolValue))
				{
					EditorGUI.PropertyField(anchorRect, anchorProperty, Labels.Anchor);
					EditorGUI.PropertyField(orderRect, orderProperty, Labels.Order);
					EditorGUI.PropertyField(stepRect, stepProperty, Labels.Step, includeChildren: true);
				}
			};

			extensionStepsList.elementHeightCallback = index =>
			{
				var element = props.ExtensionSteps.GetArrayElementAtIndex(index);

				var anchorProperty = element.FindPropertyRelative(nameof(ExtensionStepEntry.Anchor));
				var stepProperty = element.FindPropertyRelative(nameof(ExtensionStepEntry.Step));

				float spacing = EditorGUIUtility.standardVerticalSpacing;
				float lineHeight = EditorGUIUtility.singleLineHeight;

				bool expanded = anchorProperty.isExpanded;
				if (!expanded)
					return lineHeight + 2f;

				float stepHeight = EditorGUI.GetPropertyHeight(stepProperty, includeChildren: true);

				// header + (Anchor + Order) + Step, with spacing between blocks, plus a tiny top pad
				return (lineHeight * 3f) + (spacing * 3f) + stepHeight + 2f;
			};
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			// Services Foldout
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				// Header
				EditorGUILayout.LabelField(Labels.ServicesHeader, EditorStyles.boldLabel);
				EditorGUILayout.LabelField(Labels.ServicesDescription, EditorStyles.wordWrappedMiniLabel);

				EditorGUILayout.Space();

				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(props.Services, Labels.Services);
				EditorGUI.indentLevel--;

				EditorGUILayout.Space();
			}

			EditorGUILayout.Space();

			// Core Generation Steps
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				// Header
				EditorGUILayout.LabelField(Labels.StepsHeader, EditorStyles.boldLabel);
				EditorGUILayout.LabelField(Labels.StepsDescription, EditorStyles.wordWrappedMiniLabel);

				EditorGUILayout.Space();
				EditorGUI.indentLevel++;

				// Steps
				DrawExtensionSteps(PipelineAnchor.BeforeAll);
				EditorGUILayout.PropertyField(props.TileInjectionStep, Labels.TileInjectionStep);
				DrawExtensionSteps(PipelineAnchor.AfterTileInjection);
				EditorGUILayout.PropertyField(props.PreProcessingStep, Labels.PreProcessingStep);
				DrawExtensionSteps(PipelineAnchor.AfterPreProcessing);
				EditorGUILayout.PropertyField(props.MainPathStep, Labels.MainPathStep);
				DrawExtensionSteps(PipelineAnchor.AfterMainPath);
				EditorGUILayout.PropertyField(props.BranchingStep, Labels.BranchingStep);
				DrawExtensionSteps(PipelineAnchor.AfterBranching);
				EditorGUILayout.PropertyField(props.BranchPruningStep, Labels.BranchPruningStep);
				DrawExtensionSteps(PipelineAnchor.AfterBranchPruning);
				EditorGUILayout.PropertyField(props.ValidateRequiredTilesStep, Labels.ValidateRequiredTilesStep);
				DrawExtensionSteps(PipelineAnchor.AfterValidateRequiredTiles);
				EditorGUILayout.PropertyField(props.FinaliseLayoutStep, Labels.FinaliseLayoutStep);
				DrawExtensionSteps(PipelineAnchor.AfterFinaliseLayout);
				EditorGUILayout.PropertyField(props.InstantiateTilesStep, Labels.InstantiateTilesStep);
				DrawExtensionSteps(PipelineAnchor.AfterInstantiateTiles);
				EditorGUILayout.PropertyField(props.ProcessPropsStep, Labels.ProcessPropsStep);
				DrawExtensionSteps(PipelineAnchor.AfterPropProcessing);
				EditorGUILayout.PropertyField(props.LockAndKeyPlacementStep, Labels.LockAndKeyPlacementStep);
				DrawExtensionSteps(PipelineAnchor.AfterLockAndKeyPlacement);
				EditorGUILayout.PropertyField(props.PostProcessingStep, Labels.PostProcessingStep);
				DrawExtensionSteps(PipelineAnchor.AfterPostProcessing);

				EditorGUI.indentLevel--;
				EditorGUILayout.Space();
			}

			EditorGUILayout.Space();

			// Extension Steps
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				// Header
				EditorGUILayout.LabelField(Labels.ExtensionStepsHeader, EditorStyles.boldLabel);
				EditorGUILayout.LabelField(Labels.ExtensionStepsDescription, EditorStyles.wordWrappedMiniLabel);

				EditorGUILayout.Space();
				EditorGUI.indentLevel++;

				extensionStepsList.DoLayoutList();

				EditorGUI.indentLevel--;
			}

			serializedObject.ApplyModifiedProperties();
		}

		protected virtual void DrawExtensionSteps(PipelineAnchor anchor)
		{
			var pipeline = (GenerationPipeline)target;

			var extensionSteps = pipeline.ExtensionSteps
				.Where(x => x.Anchor == anchor)
				.OrderBy(x => x.Order);

			var previousColour = GUI.color;
			GUI.color = extensionStepColour;

			foreach (var stepEntry in extensionSteps)
			{
				if (!stepEntry.Enabled)
					continue;

				string label = stepEntry.Step != null ? stepEntry.Step.DisplayName : "None";
				label += " (Extension Step)";
				EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
			}

			GUI.color = previousColour;
		}
	}
}