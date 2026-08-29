using DunGen.Collision;
using DunGen.Generation;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor.Drawers
{
	namespace DunGen.Editor.Drawers
	{
		[AttributeUsage(AttributeTargets.Field)]
		public sealed class EditorTimeDungeonGeneratorAttribute : Attribute { }

		[CustomPropertyDrawer(typeof(DungeonGeneratorSettings))]
		public sealed class DungeonGeneratorSettingsDrawer : PropertyDrawer
		{
			private static class Labels
			{
				public static readonly GUIContent DungeonFlow = new GUIContent("Dungeon Flow", "The Dungeon Flow asset which defines the layout and structure of the dungeon to be generated");
				public static readonly GUIContent PipelineOverride = new GUIContent("Pipeline Override", "If set, this generation pipeline will be used instead of the one defined in the DungeonFlow");
				public static readonly GUIContent RandomizeSeed = new GUIContent("Randomize Seed", "If checked, a new random seed will be created every time a dungeon is generated. If unchecked, a specific seed will be used each time");
				public static readonly GUIContent Seed = new GUIContent("Seed", "The seed used to generate a dungeon layout. Generating a dungeon multiple times with the same seed will produce the exact same results each time");
				public static readonly GUIContent MaxFailedAttempts = new GUIContent("Max Failed Attempts", "The maximum number of times DunGen is allowed to fail at generating a dungeon layout before giving up. This only applies in-editor; in a packaged build, DunGen will keep trying indefinitely");
				public static readonly GUIContent LengthMultiplier = new GUIContent("Length Multiplier", "Used to alter the length of the dungeon without modifying the Dungeon Flow asset. 1 = normal-length, 2 = double-length, 0.5 = half-length, etc.");
				public static readonly GUIContent UpDirection = new GUIContent("Up Direction", "The up direction of the dungeon. This won't actually rotate your dungeon, but it must match the expected up-vector for your dungeon layout - usually +Y for 3D and side-on 2D, -Z for top-down 2D");
				public static readonly GUIContent TriggerPlacement = new GUIContent("Trigger Placement", "Places trigger colliders around Tiles which can be used in conjunction with the DungenCharacter component to receive events when changing rooms");
				public static readonly GUIContent TriggerLayer = new GUIContent("Trigger Layer", "The layer to place the tile root objects on if \"Place Tile Triggers\" is checked");
				public static readonly GUIContent GenerateAsynchronously = new GUIContent("Generate Asynchronously", "If checked, DunGen will generate the layout without blocking Unity's main thread, allowing for things like animated loading screens to be shown");
				public static readonly GUIContent MaxFrameTime = new GUIContent("Max Frame Time", "How many milliseconds the dungeon generation is allowed to take per-frame");
				public static readonly GUIContent PauseBetweenRooms = new GUIContent("Pause Between Rooms", "If greater than zero, the dungeon generation will pause for the set time (in seconds) after placing a room; useful for visualising the generation process");
				public static readonly GUIContent OverlapThreshold = new GUIContent("Overlap Threshold", "Maximum distance two connected tiles are allowed to overlap without being discarded. If doorways aren't exactly on the tile's axis-aligned bounding box, two tiles can overlap slightly when connected. This property can help to fix this issue");
				public static readonly GUIContent MultiDungeonCollisionMode = new GUIContent("Multi-Dungeon Collision", "Which other dungeons should be checked when testing if a tile is colliding?");
				public static readonly GUIContent DisallowOverhangs = new GUIContent("Disallow Overhangs", "If checked, two tiles cannot overlap along the Up-Vector (a room cannot spawn above another room)");
				public static readonly GUIContent Padding = new GUIContent("Padding", "A minimum buffer distance between two unconnected tiles");
				public static readonly GUIContent RestrictToBounds = new GUIContent("Restrict to Bounds?", "If checked, tiles will only be placed within the specified bounds below. May increase generation times");
				public static readonly GUIContent PlacementBounds = new GUIContent("Placement Bounds", "Tiles are not allowed to be placed outside of these bounds");
				public static readonly GUIContent RepeatMode = new GUIContent("Repeat Mode");

				public static readonly GUIContent[] UpDirectionDisplayOptions = new GUIContent[]
				{
					new GUIContent("+X"),
					new GUIContent("-X"),
					new GUIContent("+Y"),
					new GUIContent("-Y"),
					new GUIContent("+Z"),
					new GUIContent("-Z"),
				};
			}

			public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
			{
				// This drawer uses IMGUI layout (EditorGUILayout)
				return 0;
			}

			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
			{
				// Determine if this is an editor-time or runtime dungeon generator
				// by inspecting the `DungeonGenerator` field that owns this settings instance.
				var splitPropertyPath = property.propertyPath.Split('.');
				var generatorPath = string.Join(".", splitPropertyPath, 0, splitPropertyPath.Length - 1);

				var dungeonGeneratorField = property.serializedObject.targetObject.GetType().GetField(generatorPath, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

				var editorTimeAttribute = dungeonGeneratorField.GetCustomAttribute<EditorTimeDungeonGeneratorAttribute>();
				bool isRuntimeDungeon = editorTimeAttribute == null;

				// We intentionally ignore `position` and use GUILayout so the existing UI structure stays intact.
				EditorGUI.BeginProperty(position, label, property);

				var dungeonFlowProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.DungeonFlow));
				var pipelineOverrideProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.PipelineOverride));
				var shouldRandomizeSeedProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.ShouldRandomizeSeed));
				var seedProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.Seed));
				var maxAttemptCountProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.MaxAttemptCount));
				var lengthMultiplierProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.LengthMultiplier));
				var upDirectionProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.UpDirection));
				var debugRenderSettingsProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.DebugRenderSettings));
				var triggerPlacementProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.TriggerPlacement));
				var tileTriggerLayerProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.TileTriggerLayer));
				var generateAsyncProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.GenerateAsynchronously));
				var maxAsyncFrameMsProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.MaxAsyncFrameMilliseconds));
				var pauseBetweenRoomsProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.PauseBetweenRooms));
				var restrictToBoundsProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.RestrictDungeonToBounds));
				var placementBoundsProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.TilePlacementBounds));
				var overrideRepeatModeProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.OverrideRepeatMode));
				var repeatModeProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.RepeatMode));
				var overrideAllowTileRotationProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.OverrideAllowTileRotation));
				var allowTileRotationProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.AllowTileRotation));
				var collisionSettingsProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.CollisionSettings));

				EditorGUILayout.PropertyField(dungeonFlowProp, Labels.DungeonFlow);
				EditorGUILayout.PropertyField(shouldRandomizeSeedProp, Labels.RandomizeSeed);

				if (!shouldRandomizeSeedProp.boolValue)
					EditorGUILayout.PropertyField(seedProp, Labels.Seed);

				EditorGUILayout.PropertyField(lengthMultiplierProp, Labels.LengthMultiplier);

				upDirectionProp.enumValueIndex = EditorGUILayout.Popup(Labels.UpDirection, upDirectionProp.enumValueIndex, Labels.UpDirectionDisplayOptions);

				if (lengthMultiplierProp.floatValue < 0f)
					lengthMultiplierProp.floatValue = 0f;

				if (isRuntimeDungeon)
				{
					// Asynchronous Generation
					EditorGUILayout.Space();
					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					{
						EditorGUI.indentLevel++;
						generateAsyncProp.isExpanded = EditorGUILayout.Foldout(generateAsyncProp.isExpanded, "Asynchronous Generation", true);

						if (generateAsyncProp.isExpanded)
						{
							EditorGUILayout.PropertyField(generateAsyncProp, Labels.GenerateAsynchronously);

							var unitsLabelSize = EditorStyles.label.CalcSize(new GUIContent("milliseconds"));

							EditorGUI.BeginDisabledGroup(!generateAsyncProp.boolValue);

							EditorGUILayout.BeginHorizontal();
							maxAsyncFrameMsProp.floatValue = EditorGUILayout.Slider(Labels.MaxFrameTime, maxAsyncFrameMsProp.floatValue, 0f, 1000f);
							EditorGUILayout.LabelField("milliseconds", GUILayout.Width(unitsLabelSize.x));
							EditorGUILayout.EndHorizontal();

							EditorGUILayout.BeginHorizontal();
							pauseBetweenRoomsProp.floatValue = EditorGUILayout.Slider(Labels.PauseBetweenRooms, pauseBetweenRoomsProp.floatValue, 0f, 5f);
							EditorGUILayout.LabelField("seconds", GUILayout.Width(unitsLabelSize.x));
							EditorGUILayout.EndHorizontal();

							EditorGUI.EndDisabledGroup();
						}

						EditorGUI.indentLevel--;
					}
					EditorGUILayout.EndVertical();
				}

				// Collision
				if (collisionSettingsProp != null)
				{
					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					{
						EditorGUI.indentLevel++;
						collisionSettingsProp.isExpanded = EditorGUILayout.Foldout(collisionSettingsProp.isExpanded, "Collision", true);

						if (collisionSettingsProp.isExpanded)
						{
							EditorGUILayout.PropertyField(triggerPlacementProp, Labels.TriggerPlacement);

							EditorGUI.BeginDisabledGroup(triggerPlacementProp.enumValueIndex == 0);
							{
								tileTriggerLayerProp.intValue = EditorGUILayout.LayerField(Labels.TriggerLayer, tileTriggerLayerProp.intValue);
							}
							EditorGUI.EndDisabledGroup();

							EditorGUILayout.Space();

							EditorGUILayout.PropertyField(collisionSettingsProp.FindPropertyRelative(nameof(DungeonCollisionSettings.OverlapThreshold)), Labels.OverlapThreshold);
							EditorGUILayout.PropertyField(collisionSettingsProp.FindPropertyRelative(nameof(DungeonCollisionSettings.MultiDungeonCollisionMode)), Labels.MultiDungeonCollisionMode);
							EditorGUILayout.PropertyField(collisionSettingsProp.FindPropertyRelative(nameof(DungeonCollisionSettings.DisallowOverhangs)), Labels.DisallowOverhangs);

							var paddingProp = collisionSettingsProp.FindPropertyRelative("Padding");
							EditorGUI.BeginChangeCheck();

							float padding = EditorGUILayout.DelayedFloatField(Labels.Padding, paddingProp.floatValue);

							if (EditorGUI.EndChangeCheck())
								paddingProp.floatValue = Mathf.Max(0f, padding);
						}

						EditorGUI.indentLevel--;
					}
					EditorGUILayout.EndVertical();
				}

				// Constraints
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				{
					EditorGUI.indentLevel++;
					restrictToBoundsProp.isExpanded = EditorGUILayout.Foldout(restrictToBoundsProp.isExpanded, "Constraints", true);

					if (restrictToBoundsProp.isExpanded)
					{
						EditorGUILayout.HelpBox("Constraints can make dungeon generation more likely to fail. Stricter constraints increase the chance of failure.", MessageType.Info);
						EditorGUILayout.Space();

						EditorGUILayout.PropertyField(restrictToBoundsProp, Labels.RestrictToBounds);

						EditorGUI.BeginDisabledGroup(!restrictToBoundsProp.boolValue);
						EditorGUILayout.PropertyField(placementBoundsProp, Labels.PlacementBounds);
						EditorGUI.EndDisabledGroup();
					}

					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndVertical();

				// Global Overrides
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				{
					EditorGUI.indentLevel++;
					overrideRepeatModeProp.isExpanded = EditorGUILayout.Foldout(overrideRepeatModeProp.isExpanded, "Global Overrides", true);

					if (overrideRepeatModeProp.isExpanded)
					{
						EditorGUILayout.BeginHorizontal();
						{
							EditorGUILayout.PropertyField(overrideRepeatModeProp, GUIContent.none, GUILayout.Width(10));
							EditorGUI.BeginDisabledGroup(!overrideRepeatModeProp.boolValue);
							EditorGUILayout.PropertyField(repeatModeProp, Labels.RepeatMode);
							EditorGUI.EndDisabledGroup();
						}
						EditorGUILayout.EndHorizontal();

						DrawOverride("Allow Tile Rotation", overrideAllowTileRotationProp, allowTileRotationProp);
					}

					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndVertical();

				// Debug
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				{
					EditorGUI.indentLevel++;
					debugRenderSettingsProp.isExpanded = EditorGUILayout.Foldout(debugRenderSettingsProp.isExpanded, "Debug", true);

					if (debugRenderSettingsProp.isExpanded)
					{
						if(isRuntimeDungeon)
							DrawDebugRenderSettings(debugRenderSettingsProp);

						EditorGUILayout.PropertyField(maxAttemptCountProp, Labels.MaxFailedAttempts);
					}

					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndVertical();

				// Advanced
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				{
					EditorGUI.indentLevel++;
					pipelineOverrideProp.isExpanded = EditorGUILayout.Foldout(pipelineOverrideProp.isExpanded, "Advanced", true);

					if (pipelineOverrideProp.isExpanded)
					{
						EditorGUILayout.PropertyField(pipelineOverrideProp, Labels.PipelineOverride);
					}

					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndVertical();

				EditorGUI.EndProperty();
			}

			private static void DrawOverride(string label, SerializedProperty overrideProp, SerializedProperty valueProp)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PropertyField(overrideProp, GUIContent.none, GUILayout.Width(10));
				EditorGUI.BeginDisabledGroup(!overrideProp.boolValue);
				EditorGUILayout.PropertyField(valueProp, new GUIContent(label));
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.EndHorizontal();
			}

			private static void DrawDebugRenderSettings(SerializedProperty debugRenderSettingsProp)
			{
				var enabledProp = debugRenderSettingsProp.FindPropertyRelative(nameof(DebugRenderSettings.Enabled));
				var showCollisionProp = debugRenderSettingsProp.FindPropertyRelative(nameof(DebugRenderSettings.ShowCollision));
				var showPathColoursProp = debugRenderSettingsProp.FindPropertyRelative(nameof(DebugRenderSettings.ShowPathColours));

				EditorGUILayout.PropertyField(enabledProp, new GUIContent("Enable Debug Rendering"));

				if (enabledProp.boolValue)
				{
					EditorGUI.indentLevel++;

					EditorGUILayout.PropertyField(showCollisionProp, new GUIContent("Collision", "Displays a visual representation of the collision broad phase (if one is enabled)"));
					EditorGUILayout.PropertyField(showPathColoursProp, new GUIContent("Path Colours", "Draws boxes around tiles in the dungeon, colour-coded based on the type of path the tile belongs to and the depth along the branch. Main Path: Red -> Green, Branch Path: Blue -> Purple"));

					EditorGUI.indentLevel--;
				}
			}
		}
	}
}