using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor.Windows
{
	public class DunGenFolderCleaningWindow : EditorWindow
	{
		#region Statics

		[InitializeOnLoadMethod]
		private static void OnLoad()
		{
			if (!DunGenSettings.Instance.CheckForUnusedFiles)
				return;

			EditorApplication.delayCall += () =>
			{
				CleanDunGenDirectory();
			};
		}

		public static void CleanDunGenDirectory()
		{
			string relativeDunGenRootDirectory = GetRelativeDunGenRootDirectory();
			string absoluteDunGenRootDirectory = RelativeToAbsolutePath(relativeDunGenRootDirectory);

			if (relativeDunGenRootDirectory == null || !Directory.Exists(absoluteDunGenRootDirectory))
				return;

			var filesToRemove = GetFilesToRemove(relativeDunGenRootDirectory);

			if(filesToRemove == null || filesToRemove.Count == 0)
				return;

			var window = GetWindow<DunGenFolderCleaningWindow>("DunGen Cleaner");
			window.minSize = new Vector2(500, 280);

			window.relativeDunGenRootDirectory = relativeDunGenRootDirectory;
			window.absoluteDunGenRootDirectory = absoluteDunGenRootDirectory;
			window.filesToRemove = filesToRemove;
			window.Show();
		}

		private static HashSet<string> GetFilesToRemove(string dungenRootDirectory)
		{
			string filesToRemovePath = Path.Combine(dungenRootDirectory, "removed-files.txt");

			if (!File.Exists(filesToRemovePath))
				return new HashSet<string>();

			try
			{
				var filesToRemove = new HashSet<string>();
				
				foreach(var filePath in File.ReadAllLines(filesToRemovePath))
				{
					string absolutePath = Path.Combine(dungenRootDirectory, filePath);

					if(File.Exists(absolutePath))
						filesToRemove.Add(filePath);
				}

				return filesToRemove;
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"An error occurred while reading the removed-files.txt: {ex.Message}");
				return new HashSet<string>();
			}
		}

		private static string RelativeToAbsolutePath(string relativePath)
		{
			string absolutePath = Path.Combine(
				Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length),
				relativePath);

			// Normalize directory separators to the current platform's separator
			absolutePath = absolutePath.Replace('\\', Path.DirectorySeparatorChar);
			absolutePath = absolutePath.Replace('/', Path.DirectorySeparatorChar);

			return absolutePath;
		}

		private static string GetRelativeDunGenRootDirectory()
		{
			var type = typeof(Tile);

			// Search all MonoScript assets in the project
			foreach (var guid in AssetDatabase.FindAssets("t:MonoScript"))
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

				if (monoScript != null && monoScript.GetClass() == type)
				{
					// path will be something like "Assets/DunGen/Code/Tile.cs"
					// We want to get "Assets/DunGen"

					// "Assets/DunGen/Code"
					var directory = Path.GetDirectoryName(path);

					if (directory != null)
					{
						// "Assets/DunGen"
						directory = directory.Substring(0, directory.Length - 5);

						// For safety, assume we didn't find the directory if it doesn't end with "DunGen"
						if (!directory.EndsWith("DunGen"))
							return null;

						return directory;
					}
				}
			}

			return null;
		}

		#endregion

		private string relativeDunGenRootDirectory;
		private string absoluteDunGenRootDirectory;
		private HashSet<string> filesToRemove;
		private Vector2 scrollPosition;


		private void OnGUI()
		{
			EditorGUILayout.LabelField("The following files are no longer used by DunGen and can be deleted. If you've made any changes to any of these files, you may want to consider backing them up first. This utility can be run again at any time from the DunGen project settings.", EditorStyles.wordWrappedLabel);

			EditorGUILayout.Space(10);

			EditorGUILayout.LabelField("Files to Remove:", EditorStyles.boldLabel);

			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
			EditorGUILayout.BeginVertical(GUI.skin.box);
			foreach (var file in filesToRemove)
			{
				string displayPath = Path.Combine(relativeDunGenRootDirectory, file).Replace('\\', '/');
				EditorGUILayout.LabelField(displayPath);
			}
			EditorGUILayout.EndVertical();
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space(10);

			EditorGUI.BeginChangeCheck();
			var settings = DunGenSettings.Instance;
			bool dontAskAgain = EditorGUILayout.Toggle("Don't ask me again", !settings.CheckForUnusedFiles);
			if(EditorGUI.EndChangeCheck())
			{
				settings.CheckForUnusedFiles = !dontAskAgain;
				EditorUtility.SetDirty(settings);
			}

			EditorGUILayout.Space(10);

			if (GUILayout.Button("Delete Files"))
			{
				if (EditorUtility.DisplayDialog("Confirm Deletion", "Are you sure you want to delete the selected files? This action cannot be undone.", "Yes", "No"))
				{
					if (DeleteFiles())
						EditorUtility.DisplayDialog("DunGen Folder Cleaned", "The unused files have been deleted.", "OK");
					else
						EditorUtility.DisplayDialog("Error", "An error occurred while trying to delete the files. Some or all of the files were not deleted. Please check the console for more details.", "OK");

					AssetDatabase.Refresh();
					Close();
				}
			}

			if (GUILayout.Button("Cancel"))
				Close();
		}

		private bool DeleteFiles()
		{
			bool success = true;

			foreach (var file in filesToRemove)
			{
				var filePath = Path.GetFullPath(Path.Combine(absoluteDunGenRootDirectory, file));

				// Never delete files outside of the DunGen root directory
				if (!filePath.StartsWith(absoluteDunGenRootDirectory))
					continue;

				try
				{
					if (File.Exists(filePath))
						File.Delete(filePath);

					var metaFilePath = filePath + ".meta";

					if (File.Exists(metaFilePath))
						File.Delete(metaFilePath);
				}
				catch(System.Exception ex)
				{
					Debug.LogError($"Failed to delete '{filePath}': {ex.Message}");
					success = false;
				}
			}

			return success;
		}
	}
}
