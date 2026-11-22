using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Resources/Meta/MetaProgressionSettings (Glory amounts, starting potion) if it's missing and selects it.
/// Tools > Meta > Meta Progression Settings.
/// </summary>
public static class MetaProgressionSetup
{
    private const string AutoKey = "MetaProgressionSetup_v1";
    private const string Path = "Assets/Resources/Meta/MetaProgressionSettings.asset";
    private const string PotionPath = "Assets/003-Prefabs/Items/Consumables/HealthPotion_Small.prefab";

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            Open();
        };
    }

    [MenuItem("Tools/Meta/Meta Progression Settings")]
    public static void Open()
    {
        var settings = AssetDatabase.LoadAssetAtPath<MetaProgressionSettings>(Path);
        if (settings == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) { AssetDatabase.CreateFolder("Assets", "Resources"); }
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Meta")) { AssetDatabase.CreateFolder("Assets/Resources", "Meta"); }
            settings = ScriptableObject.CreateInstance<MetaProgressionSettings>();
            GameObject potion = AssetDatabase.LoadAssetAtPath<GameObject>(PotionPath);
            settings.startingPotion = potion != null ? potion.GetComponent<Item>() : null;
            AssetDatabase.CreateAsset(settings, Path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Meta] Settings created: {Path} (starting potion: {(settings.startingPotion != null ? settings.startingPotion.name : "MISSING")})");
        }
        else
        {
            Debug.Log("[Meta] Settings: " + Path);
        }
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }
}
