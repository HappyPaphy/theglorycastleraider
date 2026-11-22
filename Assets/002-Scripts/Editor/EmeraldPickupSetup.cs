using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: creates Resources/Pickups/EmeraldPickup from the coin pickup (same burst / magnet / "+N"),
/// set to the Emerald currency, green text, no expiry, and an emerald-green tint on the coin sprite
/// (swap in your own emerald sprite on the prefab any time). Never overwrites an existing prefab.
/// Tools > Meta > Build Emerald Pickup.
/// </summary>
public static class EmeraldPickupSetup
{
    private const string AutoKey = "EmeraldPickupSetup_v1";
    private const string CoinPath = "Assets/Resources/Pickups/CoinPickup.prefab";
    private const string EmeraldPath = "Assets/Resources/Pickups/EmeraldPickup.prefab";

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            Build();
        };
    }

    [MenuItem("Tools/Meta/Build Emerald Pickup")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EmeraldPath) != null)
        {
            Debug.Log("[Emerald] Prefab already exists, left as is: " + EmeraldPath);
            return;
        }
        if (!AssetDatabase.CopyAsset(CoinPath, EmeraldPath))
        {
            Debug.LogWarning("[Emerald] Couldn't copy " + CoinPath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(EmeraldPath);
        try
        {
            root.name = "EmeraldPickup";
            CoinPickup pickup = root.GetComponent<CoinPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("currency").enumValueIndex = (int)CoinPickup.Currency.Emerald;
            so.FindProperty("textColor").colorValue = new Color(0.35f, 1f, 0.55f, 1f);
            so.FindProperty("lifetime").floatValue = 0f; // emeralds don't expire (cleaned up on the next floor)
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.color = new Color(0.25f, 1f, 0.5f, 1f); // emerald tint until a real emerald sprite is set
            }

            PrefabUtility.SaveAsPrefabAsset(root, EmeraldPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(EmeraldPath);
        Debug.Log("[Emerald] Prefab built: " + EmeraldPath);
    }
}
