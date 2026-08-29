using DunGen.Culling;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor.Inspectors
{
	[CustomEditor(typeof(CullingCamera))]
	[CanEditMultipleObjects]
	public class CullingCameraInspector : UnityEditor.Editor
	{
		private class Properties
		{
			public SerializedProperty Strategy { get; }
			public SerializedProperty PerCameraCulling { get; }
			public SerializedProperty CullComponents { get; }
			public SerializedProperty DebugDraw { get; }


			public Properties(SerializedObject serializedObject)
			{
				Strategy = serializedObject.FindProperty(nameof(CullingCamera.Strategy));
				PerCameraCulling = serializedObject.FindProperty(nameof(CullingCamera.PerCameraCulling));
				CullComponents = serializedObject.FindProperty(nameof(CullingCamera.ComponentsToCull));
				DebugDraw = serializedObject.FindProperty(nameof(CullingCamera.DebugDraw));
			}
		}

		private class Labels
		{
			public static GUIContent Strategy = new GUIContent("Strategy", "The algorithm used to determine which rooms are visible to this camera");
			public static GUIContent PerCameraCulling = new GUIContent("Per-Camera Culling", "If enabled, culling will be performed individually, so each camera has its own view of the world. This will increase the performance cost and cause any non-culling camera to not render the dungeon");
			public static GUIContent CullComponents = new GUIContent("Cull Components", "Which types of components should be affected by culling");
			public static GUIContent DebugDraw = new GUIContent("Debug Draw", "If enabled, draws some debug information to the screen");
		}

		private Properties props;


		private void OnEnable()
		{
			props = new Properties(serializedObject);
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

			EditorGUILayout.PropertyField(props.Strategy, Labels.Strategy);
			EditorGUILayout.PropertyField(props.PerCameraCulling, Labels.PerCameraCulling);
			EditorGUILayout.PropertyField(props.CullComponents, Labels.CullComponents);

			bool supportsDebugDrawing = targets.Cast<CullingCamera>()
				.Select(c => c.Strategy)
				.Any(s => s.SupportsDebugDrawing);

			EditorGUI.BeginDisabledGroup(!supportsDebugDrawing);
			EditorGUILayout.PropertyField(props.DebugDraw, Labels.DebugDraw);
			EditorGUI.EndDisabledGroup();

			EditorGUI.EndDisabledGroup();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
