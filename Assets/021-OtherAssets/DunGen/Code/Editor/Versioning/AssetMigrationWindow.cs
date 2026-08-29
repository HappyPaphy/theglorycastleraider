using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor.Versioning
{
    public class AssetMigrationWindow : EditorWindow
    {
        private bool isNewVersionPrompt = false;
        private bool scanAssets = true;
        private bool scanOpenScenes = true;
        private bool scanEntireProject = true;
        private readonly List<string> folders = new List<string>();
        private Vector2 scroll;

        public static void Open(bool newVersionPrompt)
        {
            var window = GetWindow<AssetMigrationWindow>(true, "DunGen Migration", true);
            window.isNewVersionPrompt = newVersionPrompt;

            float minHeight = newVersionPrompt ? 430 : 400;
            window.minSize = new Vector2(500, minHeight);

            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("DunGen Project Migration", EditorStyles.boldLabel);

            if (isNewVersionPrompt)
            {
                EditorGUILayout.HelpBox(
                    "DunGen has been updated and may require a one-time migration to update existing data.\n\n" +
                    "Choose to scan the entire project (default) or specify folders to limit the scan. " +
                    "This can help avoid opening too many assets in large projects.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This utility will scan your project for DunGen assets that may need updating.\n\n" +
                    "Choose to scan the entire project (default) or specify folders to limit the scan. " +
                    "This can help avoid opening too many assets in large projects.",
                    MessageType.Info);
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scanning Options", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                scanAssets = EditorGUILayout.ToggleLeft("Scan Assets", scanAssets);
                scanOpenScenes = EditorGUILayout.ToggleLeft("Scan Open Scenes", scanOpenScenes);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginDisabledGroup(!scanAssets);
                scanEntireProject = EditorGUILayout.ToggleLeft("Scan Entire Project (Assets/)", scanEntireProject);

                EditorGUI.BeginDisabledGroup(scanEntireProject);
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Folders to Scan", EditorStyles.boldLabel);

                using (var scrollView = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.Height(140)))
                {
                    scroll = scrollView.scrollPosition;

                    for (int i = 0; i < folders.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        folders[i] = EditorGUILayout.TextField(folders[i]);

                        if (GUILayout.Button("...", GUILayout.Width(30)))
                        {
                            string selected = EditorUtility.OpenFolderPanel("Select Folder Under Assets", Application.dataPath, string.Empty);

                            if (!string.IsNullOrEmpty(selected))
                            {
                                string rel = MakeRelativeToAssets(selected);

                                if (!string.IsNullOrEmpty(rel))
                                    folders[i] = rel;
                                else
                                    EditorUtility.DisplayDialog("Invalid Folder", "Please select a folder under the project's Assets directory.", "OK");
                            }
                        }
                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        {
                            folders.RemoveAt(i);
                            GUIUtility.ExitGUI();
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Add Folder"))
                    folders.Add("Assets/");

                if (GUILayout.Button("Add Selected"))
                {
                    foreach (var obj in Selection.objects)
                    {
                        string path = AssetDatabase.GetAssetPath(obj);

                        if (!string.IsNullOrEmpty(path))
                        {
                            string folderPath = System.IO.Directory.Exists(path) ? path : System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                            if (!string.IsNullOrEmpty(folderPath) && folderPath.StartsWith("Assets") && !folders.Contains(folderPath))
                                folders.Add(folderPath);
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(!scanAssets && !scanOpenScenes);

            if (GUILayout.Button("Migrate", GUILayout.Width(120), GUILayout.Height(28)))
            {
                if (scanAssets && !scanEntireProject && folders.Count == 0)
                    EditorUtility.DisplayDialog("No Folders Selected", "Add at least one folder or enable 'Scan Entire Project'.", "OK");
                else
                {
                    string[] searchFolders = null;

                    if (!scanEntireProject)
                    {
                        // Normalize folder paths and ensure they exist
                        var normalized = new List<string>();
                        foreach (var f in folders)
                        {
                            if (!string.IsNullOrEmpty(f))
                            {
                                string clean = f.Replace("\\", "/");

                                if (!clean.StartsWith("Assets"))
                                    clean = "Assets/" + clean.TrimStart('/');

                                if (AssetDatabase.IsValidFolder(clean))
                                    normalized.Add(clean);
                                else
                                    Debug.LogWarning($"[DunGen] Skipping invalid folder: {clean}");
                            }
                        }

                        searchFolders = normalized.Count > 0 ? normalized.ToArray() : null;
                    }

                    AssetMigrationSystem.RunMigration(scanAssets, scanOpenScenes, searchFolders);
                }
            }

            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (isNewVersionPrompt)
            {
                EditorGUILayout.Space();
                DrawFooterActions();
            }
        }

        private void DrawFooterActions()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Don't Ask Me Again", GUILayout.Width(180)))
            {
                if (DunGenSettings.Instance.IsMigrationRequired())
                {
                    DunGenSettings.Instance.UpdateMigrationVersion();
                    EditorUtility.SetDirty(DunGenSettings.Instance);
                    AssetDatabase.SaveAssets();
                }

                Close();
            }

            if (GUILayout.Button("Remind Me Later", GUILayout.Width(160)))
                Close();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static string MakeRelativeToAssets(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return null;

            absolutePath = absolutePath.Replace("\\", "/");
            string assetsPath = Application.dataPath.Replace("\\", "/");

            if (!absolutePath.StartsWith(assetsPath))
                return null;

            string rel = "Assets" + absolutePath.Substring(assetsPath.Length);
            return rel;
        }
    }
}