using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
	public static class TileAssetProcessor
	{
		private static bool isProcessing;
		private static readonly List<string> tilesToProcess = new List<string>();
		private static string tileScriptPath;

		[InitializeOnLoadMethod]
		private static void ResetStatics()
		{
			isProcessing = false;
			tilesToProcess.Clear();
			tileScriptPath = null;
		}

		private static void FindTileScriptPath()
		{
			// Find all MonoScript assets in the project
			string[] guids = AssetDatabase.FindAssets("t:MonoScript");

			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

				if (monoScript != null)
				{
					// Check if this MonoScript represents a DunGen Tile
					if (monoScript.GetClass() == typeof(Tile))
					{
						tileScriptPath = path;
						break;
					}
				}
			}
		}

		public class TileSaveDetector : UnityEditor.AssetModificationProcessor
		{
			static string[] OnWillSaveAssets(string[] paths)
			{
				if (!DunGenSettings.Instance.RecalculateTileBoundsOnSave)
					return paths;

				if (isProcessing)
					return paths;

				// Cache path to the Tile script for quick lookup later
				if (tileScriptPath == null)
					FindTileScriptPath();

				foreach (var path in paths)
				{
					// We're only interested in prefabs
					if (!path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
						continue;

					// Check dependencies as a quick way to see if the prefab has a Tile component
					var dependencies = AssetDatabase.GetDependencies(path, true);

					if (!dependencies.Contains(tileScriptPath))
						continue;

					// Load the prefab
					var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

					if (prefab == null)
						continue;

					// If it has a Tile component at the root, add it to the list for later processing
					if (prefab.TryGetComponent<Tile>(out var _))
						tilesToProcess.Add(path);
				}

				if (tilesToProcess.Count > 0)
					isProcessing = true;

				return paths;
			}
		}

		public class TileSaveProcessor : AssetPostprocessor
		{
			static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
			{
				// Nothing to process
				if (!isProcessing || tilesToProcess.Count == 0)
					return;

				try
				{
					// Combine imported and moved assets into one array
					string[] allAssets = new string[importedAssets.Length + movedAssets.Length];
					importedAssets.CopyTo(allAssets, 0);
					movedAssets.CopyTo(allAssets, importedAssets.Length);

					foreach (var path in tilesToProcess)
					{
						var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

						if (prefab == null)
							continue;

						if (prefab.TryGetComponent<Tile>(out var tile))
						{
							bool boundsChanged = tile.RecalculateBounds();

							// If the prefab is open in a prefab stage, we need to copy the new bounds to it as well
							if (boundsChanged)
							{
#if UNITY_2021_2_OR_NEWER
								var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
#else
								var prefabStage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
#endif

								if (prefabStage != null && prefabStage.assetPath == path)
								{
									if (prefabStage.prefabContentsRoot.TryGetComponent<Tile>(out var prefabTile))
										prefabTile.CopyBoundsFrom(tile);
								}
							}
						}

						EditorUtility.SetDirty(prefab);
					}

					AssetDatabase.SaveAssets();
				}
				finally
				{
					tilesToProcess.Clear();
					isProcessing = false;
				}
			}
		}
	}
}