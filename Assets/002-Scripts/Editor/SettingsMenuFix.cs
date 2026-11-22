using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot:
///  - SettingsManager prefab (used by the main menu and the pause menu): the volume sliders ran -40..0 while the
///    code treats them as 0..1 volume, so the mixer got broken values. They now run 0..1.
///  - PauseMenu prefab: its "Button (Setting)" was switched off and not linked; it's on and opens the settings.
/// Tools > UI > Fix Settings Menu.
/// </summary>
public static class SettingsMenuFix
{
    private const string AutoKey = "SettingsMenuFix_v1";
    private const string SettingsPath = "Assets/003-Prefabs/SettingsManager.prefab";
    private const string PausePath = "Assets/003-Prefabs/PauseMenu.prefab";

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            Run();
        };
    }

    [MenuItem("Tools/UI/Fix Settings Menu")]
    public static void Run()
    {
        var log = new StringBuilder();

        GameObject settings = PrefabUtility.LoadPrefabContents(SettingsPath);
        try
        {
            SettingManager manager = settings.GetComponentInChildren<SettingManager>(true);
            var so = new SerializedObject(manager);
            foreach (string field in new[] { "masterSlider", "sfxSlider", "musicSlider" })
            {
                Slider slider = so.FindProperty(field).objectReferenceValue as Slider;
                if (slider == null) { log.AppendLine("!! no " + field); continue; }
                slider.minValue = 0.0001f;
                slider.maxValue = 1f;
                slider.wholeNumbers = false;
                slider.SetValueWithoutNotify(0.8f);
                log.AppendLine($"{field}: 0..1");
            }
            PrefabUtility.SaveAsPrefabAsset(settings, SettingsPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(settings);
        }

        GameObject pause = PrefabUtility.LoadPrefabContents(PausePath);
        try
        {
            PauseGame pauseGame = pause.GetComponentInChildren<PauseGame>(true);
            Button settingButton = null;
            foreach (Button b in pause.GetComponentsInChildren<Button>(true)) { if (b.name == "Button (Setting)") { settingButton = b; } }

            if (pauseGame == null || settingButton == null) { log.AppendLine("!! pause menu or its Setting button not found"); }
            else
            {
                settingButton.gameObject.SetActive(true);
                var so = new SerializedObject(pauseGame);
                so.FindProperty("button_Setting").objectReferenceValue = settingButton;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(pause, PausePath);
                log.AppendLine("PauseMenu: Settings button on and linked");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(pause);
        }

        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/settings_fix.txt", log.ToString());
        Debug.Log("[SettingsFix] Done. Report: Library/PrisonDecor/settings_fix.txt");
    }
}
