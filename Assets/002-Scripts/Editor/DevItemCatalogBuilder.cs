using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Keeps Resources/Dev/DevItemCatalog (the F6 cheat panel's item list) in step with every prefab that has an
/// Item / Weapon component. Runs after each compile and before each build; only writes when the list changed.
/// Tools > Dev > Rebuild Item Catalog.
/// </summary>
public class DevItemCatalogBuilder : IPreprocessBuildWithReport
{
    private const string Path = "Assets/Resources/Dev/DevItemCatalog.asset";

    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => Rebuild(false);

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            Rebuild(false);
        };
    }

    [MenuItem("Tools/Dev/Rebuild Item Catalog")]
    private static void RebuildFromMenu() => Rebuild(true);

    public static void Rebuild(bool select)
    {
        var found = new List<Item>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/003-Prefabs" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            Item item = go != null ? go.GetComponent<Item>() : null; // root only: real pickups, not things holding an item
            if (item != null && !string.IsNullOrEmpty(item.equipmentName)) { found.Add(item); }
        }
        found = found.OrderBy(i => i is Weapon ? 0 : 1).ThenBy(i => (int)i.itemCategory).ThenBy(i => i.equipmentName).ToList();

        var perks = AssetDatabase.FindAssets("t:PerkCardDefinition")
            .Select(g => AssetDatabase.LoadAssetAtPath<PerkCardDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null)
            .OrderBy(p => p.DisplayName).ThenBy(p => p.Level)
            .ToList();

        var catalog = AssetDatabase.LoadAssetAtPath<DevItemCatalog>(Path);
        if (catalog == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) { AssetDatabase.CreateFolder("Assets", "Resources"); }
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Dev")) { AssetDatabase.CreateFolder("Assets/Resources", "Dev"); }
            catalog = ScriptableObject.CreateInstance<DevItemCatalog>();
            catalog.items = found;
            catalog.perks = perks;
            AssetDatabase.CreateAsset(catalog, Path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DevItems] Catalog created with {found.Count} items, {perks.Count} perks: {Path}");
        }
        else if (!catalog.items.SequenceEqual(found) || catalog.perks == null || !catalog.perks.SequenceEqual(perks))
        {
            catalog.items = found;
            catalog.perks = perks;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DevItems] Catalog updated: {found.Count} items, {perks.Count} perks");
        }

        if (select) { Selection.activeObject = catalog; EditorGUIUtility.PingObject(catalog); }
    }
}
