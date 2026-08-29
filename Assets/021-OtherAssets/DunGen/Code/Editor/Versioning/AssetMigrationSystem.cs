using DunGen.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DunGen.Editor.Versioning
{
	[InitializeOnLoad]
	public static class AssetMigrationSystem
	{
		static AssetMigrationSystem()
		{
			EditorApplication.delayCall += CheckDunGenMigrationVersion;
		}

		private static void CheckDunGenMigrationVersion()
		{
			if (!DunGenSettings.Instance.IsMigrationRequired())
				return;

			AssetMigrationWindow.Open(true);
		}

		[MenuItem("Window/DunGen/Run Project Migration", priority = 10)]
		public static void OpenMigrationWindow()
		{
			AssetMigrationWindow.Open(false);
		}

		internal static void RunMigration(bool scanAssets, bool scanOpenScenes, string[] searchFolders = null)
		{
			var updatedScriptableObjectPaths = new List<string>();
			var updatedPrefabPaths = new List<string>();
			var updatedSceneObjectNames = new List<string>();

			try
			{
				AssetDatabase.StartAssetEditing();

				if (scanAssets)
				{
					EditorUtility.DisplayProgressBar("Migrating", "Scanning ScriptableObjects...", 0.0f);
					updatedScriptableObjectPaths = MigrateScriptableObjects(searchFolders);

					EditorUtility.DisplayProgressBar("Migrating", "Scanning Prefabs...", 0.33f);
					updatedPrefabPaths = MigratePrefabs(searchFolders);
				}

				if (scanOpenScenes)
				{
					EditorUtility.DisplayProgressBar("Migrating", "Scanning Scene Objects...", 0.66f);
					updatedSceneObjectNames = MigrateSceneObjects();
				}

				if (DunGenSettings.Instance.IsMigrationRequired())
				{
					DunGenSettings.Instance.UpdateMigrationVersion();
					EditorUtility.SetDirty(DunGenSettings.Instance);
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();

				AssetDatabase.StopAssetEditing();
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}

			string logMessage = "<b>[DunGen]</b> Migration Complete" +
				$"\nUpdated {updatedScriptableObjectPaths.Count} ScriptableObjects, {updatedPrefabPaths.Count} Prefabs, and {updatedSceneObjectNames.Count} Scene Objects";

			if (updatedScriptableObjectPaths.Count > 0)
			{
				logMessage += "\n\n--- ScriptableObjects ---";
				foreach (var path in updatedScriptableObjectPaths)
					logMessage += $"\n\t- {path}";
			}

			if (updatedPrefabPaths.Count > 0)
			{
				logMessage += "\n\n--- Prefabs ---";
				foreach (var path in updatedPrefabPaths)
					logMessage += $"\n\t- {path}";
			}

			if (updatedSceneObjectNames.Count > 0)
			{
				logMessage += "\n\n--- Scene Objects ---";
				foreach (var name in updatedSceneObjectNames)
					logMessage += $"\n\t- {name}";
			}

			Debug.Log(logMessage);
		}

		private static List<string> MigrateScriptableObjects(string[] searchFolders)
		{
			var updatedPaths = new List<string>();

			string[] guids = (searchFolders != null && searchFolders.Length > 0)
				? AssetDatabase.FindAssets("t:VersionedScriptableObject", searchFolders)
				: AssetDatabase.FindAssets("t:VersionedScriptableObject");

			for (int i = 0; i < guids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				var so = AssetDatabase.LoadAssetAtPath<VersionedScriptableObject>(path);

				if (so != null && so.RequiresMigration)
				{
					so.Migrate();
					EditorUtility.SetDirty(so);

					updatedPaths.Add(path);
				}
			}

			return updatedPaths;
		}

		private static List<string> MigratePrefabs(string[] searchFolders)
		{
			var updatedPaths = new List<string>();

			string[] guids = searchFolders is { Length: > 0 }
				? AssetDatabase.FindAssets("t:Prefab", searchFolders)
				: AssetDatabase.FindAssets("t:Prefab");

			var paths = guids
				.Select(AssetDatabase.GUIDToAssetPath)
				.Where(path => path.StartsWith("Assets/"))
				.ToList();

			try
			{
				for (int i = 0; i < paths.Count; i++)
				{
					string path = paths[i];

					EditorUtility.DisplayProgressBar(
						"Migrating Prefabs",
						$"Processing {System.IO.Path.GetFileName(path)}",
						paths.Count == 0 ? 1f : (float)(i + 1) / paths.Count);

					GameObject contentsRoot = null;

					try
					{
						contentsRoot = PrefabUtility.LoadPrefabContents(path);
						bool changed = false;

						foreach (var component in contentsRoot.GetComponentsInChildren<VersionedMonoBehaviour>(true))
						{
							// This component is owned by another prefab asset.
							// That asset will be found and migrated separately.
							if (IsInheritedFromNestedPrefab(contentsRoot, component))
								continue;

							if (!component.RequiresMigration)
								continue;

							component.Migrate();
							changed = true;
						}

						if (!changed)
							continue;

						PrefabUtility.SaveAsPrefabAsset(contentsRoot, path, out bool savedSuccessfully);

						if (!savedSuccessfully)
							throw new InvalidOperationException($"Unity failed to save prefab '{path}'.");

						updatedPaths.Add(path);
					}
					catch (Exception exception)
					{
						Debug.LogException(new Exception($"Failed to migrate prefab at '{path}'.", exception));
					}
					finally
					{
						if (contentsRoot != null)
							PrefabUtility.UnloadPrefabContents(contentsRoot);
					}
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			return updatedPaths;
		}

		private static bool IsInheritedFromNestedPrefab(GameObject contentsRoot, Component component)
		{
			GameObject nearestInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(component.gameObject);

			// Regular objects belonging directly to this prefab
			if (nearestInstanceRoot == null)
				return false;

			// For a Prefab Variant, the loaded contents root can itself be an
			// instance root. It belongs to the asset currently being edited.
			if (nearestInstanceRoot == contentsRoot)
				return false;

			// A component explicitly added as an override belongs to the current
			// prefab, even though it is attached to a nested prefab instance.
			if (PrefabUtility.IsAddedComponentOverride(component))
				return false;

			// Likewise, components on GameObjects added beneath a nested instance
			// belong to the current prefab.
			for (Transform current = component.transform;
				 current != null && current != nearestInstanceRoot.transform;
				 current = current.parent)
			{
				if (PrefabUtility.IsAddedGameObjectOverride(current.gameObject))
					return false;
			}

			// Otherwise this component is inherited from a nested prefab asset.
			return true;
		}

		private static List<string> MigrateSceneObjects()
		{
			var updatedObjectNames = new List<string>();

			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				var scene = SceneManager.GetSceneAt(i);

				foreach (var rootGo in scene.GetRootGameObjects())
				{
					var versionedComps = rootGo.GetComponentsInChildren<VersionedMonoBehaviour>(true);

					foreach (var comp in versionedComps)
					{
						if (comp.RequiresMigration)
						{
							comp.Migrate();
							EditorUtility.SetDirty(comp);
							updatedObjectNames.Add(comp.gameObject.name);
						}
					}
				}
			}

			return updatedObjectNames;
		}
	}
}