using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: makes KnightsQuest SDF the font of everything added for the shop, treasure rooms, shrines and challenges.
///  - creates Resources/UI/GameFontSettings (used by text made at runtime: elite labels, curses, banners, pop-ups)
///  - switches every TextMeshPro text in those prefabs to it
/// Re-run (e.g. after picking another font in GameFontSettings): Tools > UI > Apply Game Font To Prefabs.
/// </summary>
public static class GameFontSetup
{
    private const string AutoKey = "GameFontSetup_v1";
    private const string FontPath = "Assets/020-Others/Font/KnightQuest/KnightsQuest SDF.asset";
    private const string SettingsPath = "Assets/Resources/UI/GameFontSettings.asset";

    private static readonly string[] Prefabs =
    {
        "Assets/Resources/UI/ShopUI.prefab",
        "Assets/003-Prefabs/Treasure/TreasureDoor_Bronze.prefab",
        "Assets/003-Prefabs/Treasure/TreasureDoor_Silver.prefab",
        "Assets/003-Prefabs/Treasure/TreasureDoor_Gold.prefab",
        "Assets/003-Prefabs/Shrines/ShrineAltar.prefab",
        "Assets/003-Prefabs/Challenges/ChallengeAltar.prefab",
    };

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            Apply();
        };
    }

    [MenuItem("Tools/UI/Apply Game Font To Prefabs")]
    public static void Apply()
    {
        var log = new StringBuilder();

        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
        GameFontSettings settings = AssetDatabase.LoadAssetAtPath<GameFontSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<GameFontSettings>();
            settings.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            AssetDatabase.CreateAsset(settings, SettingsPath);
            log.AppendLine("created " + SettingsPath);
        }

        TMP_FontAsset font = settings.font;
        if (font == null)
        {
            Debug.LogError("[GameFont] No font set in " + SettingsPath);
            return;
        }

        int total = 0;
        foreach (string path in Prefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.AppendLine("missing " + path); continue; }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = 0;
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.font = font;
                    text.fontSharedMaterial = font.material;
                    count++;
                }
                if (count > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); }
                log.AppendLine($"{Path.GetFileName(path)}: {count} text(s)");
                total += count;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/game_font.txt", log.ToString());
        Debug.Log($"[GameFont] Done: {total} prefab text(s) set to {font.name}. Report: Library/PrisonDecor/game_font.txt");
    }
}
