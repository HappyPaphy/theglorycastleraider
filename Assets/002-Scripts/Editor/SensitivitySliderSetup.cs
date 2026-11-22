using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot: adds "Mouse Sensitivity" and "Controller Sensitivity" rows to the settings menu
/// (SettingsManager prefab, Sound tab, under Music) by copying the Music Volume row, and links them to
/// SettingManager. Tools > UI > Add Look Sensitivity Sliders. Report: Library/PrisonDecor/sensitivity_sliders.txt
/// </summary>
public static class SensitivitySliderSetup
{
    private const string AutoKey = "SensitivitySliderSetup_v1";
    private const string PrefabPath = "Assets/003-Prefabs/SettingsManager.prefab";

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

    [MenuItem("Tools/UI/Add Look Sensitivity Sliders")]
    public static void Run()
    {
        var log = new StringBuilder();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            SettingManager manager = root.GetComponentInChildren<SettingManager>(true);
            RectTransform master = Find(root.transform, "Master Volume") as RectTransform;
            RectTransform sfx = Find(root.transform, "SFX Volume") as RectTransform;
            RectTransform music = Find(root.transform, "Music Volume") as RectTransform;
            if (manager == null || master == null || sfx == null || music == null)
            {
                log.AppendLine("!! settings manager or volume rows not found - nothing changed");
                return;
            }
            if (Find(root.transform, "Mouse Sensitivity") != null)
            {
                log.AppendLine("rows already exist - nothing changed");
                return;
            }

            RectTransform body = (RectTransform)music.parent;
            float step = sfx.anchoredPosition.y - master.anchoredPosition.y; // distance between rows (negative = downwards)
            log.AppendLine($"panel '{body.name}', row step {step:F1}");

            RectTransform mouseRow = CopyRow(music, "Mouse Sensitivity", log);
            RectTransform padRow = CopyRow(music, "Controller Sensitivity", log);
            mouseRow.anchoredPosition = music.anchoredPosition + new Vector2(0f, step);
            padRow.anchoredPosition = music.anchoredPosition + new Vector2(0f, step * 2f);

            // Do all five rows fit inside the panel? If not, spread them evenly over the space there is.
            RectTransform[] rows = { master, sfx, music, mouseRow, padRow };
            float bodyBottom = body.rect.yMin;
            float lowest = Bottom(padRow, body);
            if (lowest < bodyBottom)
            {
                float top = master.anchoredPosition.y;
                float rowHeight = Top(padRow, body) - lowest;
                float newStep = -(Top(master, body) - bodyBottom - rowHeight) / (rows.Length - 1);
                for (int i = 0; i < rows.Length; i++) { rows[i].anchoredPosition = new Vector2(rows[i].anchoredPosition.x, top + newStep * i); }
                log.AppendLine($"rows didn't fit: re-spaced all five rows with step {newStep:F1}");
            }
            else
            {
                log.AppendLine("rows fit under Music");
            }

            var so = new SerializedObject(manager);
            so.FindProperty("mouseSensitivitySlider").objectReferenceValue = mouseRow.GetComponentInChildren<Slider>(true);
            so.FindProperty("controllerSensitivitySlider").objectReferenceValue = padRow.GetComponentInChildren<Slider>(true);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            log.AppendLine("saved " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);

            // Settings menus that are NOT this prefab won't get the rows
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/010-Scenes" }))
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                string text = File.ReadAllText(scenePath);
                // cheap check: the SettingManager script referenced directly in the scene = an unpacked copy
                string managerGuid = AssetDatabase.AssetPathToGUID("Assets/002-Scripts/Manager/SettingManager.cs");
                if (text.Contains("guid: " + managerGuid)) { log.AppendLine("note: " + scenePath + " has its own (unpacked) SettingManager - add the rows there by hand or re-link it to the prefab"); }
            }

            Directory.CreateDirectory("Library/PrisonDecor");
            File.WriteAllText("Library/PrisonDecor/sensitivity_sliders.txt", log.ToString());
            Debug.Log("[Sensitivity] Done. Report: Library/PrisonDecor/sensitivity_sliders.txt");
        }
    }

    private static RectTransform CopyRow(RectTransform music, string name, StringBuilder log)
    {
        GameObject copy = Object.Instantiate(music.gameObject, music.parent, false);
        copy.name = name;
        var rt = (RectTransform)copy.transform;

        // Label
        foreach (Text t in copy.GetComponentsInChildren<Text>(true))
        {
            if (t.GetComponentInParent<Slider>(true) != null) { continue; } // text inside the slider (if any)
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name.Contains("Localize")) { Object.DestroyImmediate(c); } // would turn it back into "Music"
            }
            t.text = name;
            t.gameObject.name = $"Text (Legacy) ({name})";
        }

        // Music icon doesn't belong here
        foreach (Image img in copy.GetComponentsInChildren<Image>(true))
        {
            if (img.GetComponentInParent<Slider>(true) == null && img.transform != rt) { img.gameObject.SetActive(false); }
        }

        // Slider: no listeners copied from the music slider
        Slider slider = copy.GetComponentInChildren<Slider>(true);
        if (slider != null)
        {
            slider.gameObject.name = $"Slider ({name})";
            var so = new SerializedObject(slider);
            SerializedProperty calls = so.FindProperty("m_OnValueChanged.m_PersistentCalls.m_Calls");
            if (calls != null && calls.arraySize > 0) { log.AppendLine($"  {name}: cleared {calls.arraySize} copied slider listener(s)"); calls.ClearArray(); }
            so.ApplyModifiedPropertiesWithoutUndo();
            slider.minValue = LookSensitivity.Min;
            slider.maxValue = LookSensitivity.Max;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(LookSensitivity.Default);
        }
        log.AppendLine($"  added row '{name}'");
        return rt;
    }

    private static float Bottom(RectTransform r, RectTransform space)
    {
        var c = new Vector3[4];
        r.GetWorldCorners(c);
        float min = float.MaxValue;
        foreach (Vector3 v in c) { min = Mathf.Min(min, space.InverseTransformPoint(v).y); }
        foreach (RectTransform child in r.GetComponentsInChildren<RectTransform>(true))
        {
            child.GetWorldCorners(c);
            foreach (Vector3 v in c) { min = Mathf.Min(min, space.InverseTransformPoint(v).y); }
        }
        return min;
    }

    private static float Top(RectTransform r, RectTransform space)
    {
        var c = new Vector3[4];
        float max = float.MinValue;
        foreach (RectTransform child in r.GetComponentsInChildren<RectTransform>(true))
        {
            child.GetWorldCorners(c);
            foreach (Vector3 v in c) { max = Mathf.Max(max, space.InverseTransformPoint(v).y); }
        }
        return max;
    }

    private static Transform Find(Transform t, string name)
    {
        if (t.name.Trim() == name) { return t; }
        foreach (Transform c in t) { Transform f = Find(c, name); if (f != null) { return f; } }
        return null;
    }
}
