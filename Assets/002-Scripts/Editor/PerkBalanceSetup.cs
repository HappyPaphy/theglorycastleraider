using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot for the perk balance pass:
///  1. Fills PerkSelectionManager.allCards (on PerkCardManager.prefab) with every perk card, so owned perks
///     can be offered as their next level.        Re-run: Tools > Perks > Fill Perk Card List
///  2. Adds a "Perk Slot" item to the shop catalog.
///  3. Fills the elite potion drops (only if the list is empty) and turns the elite orb drop off.
/// </summary>
public static class PerkBalanceSetup
{
    private const string AutoKey = "PerkBalanceSetup_v1";
    private const string CardFolder = "Assets/013-Datas/Perks/Perk Cards";
    private const string PerkManagerPrefab = "Assets/003-Prefabs/PerkCardManager.prefab";
    private const string ShopItemPath = "Assets/013-Datas/Shop/Items/Upgrade_PerkSlot.asset";
    private const string CatalogPath = "Assets/013-Datas/Shop/ShopCatalog (Prison).asset";
    private const string EliteSettingsPath = "Assets/Resources/Elites/EliteSettings.asset";
    private const string Potions = "Assets/003-Prefabs/Items/Consumables";

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            RunAll();
        };
    }

    private static void RunAll()
    {
        var log = new StringBuilder();
        int errors = 0;

        void Step(string name, System.Action<StringBuilder> action)
        {
            try { action(log); }
            catch (System.Exception e) { errors++; log.AppendLine("!! " + name + ": " + e); }
        }

        Step("cards", FillCardList);
        Step("shop", AddPerkSlotToShop);
        Step("elites", SetElitePotions);

        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/perk_balance.txt", log.ToString());
        Debug.Log($"[PerkBalance] Done with {errors} error(s). Report: Library/PrisonDecor/perk_balance.txt");
    }

    [MenuItem("Tools/Perks/Fill Perk Card List")]
    private static void FillCardListMenu()
    {
        var log = new StringBuilder();
        FillCardList(log);
        Debug.Log("[PerkBalance] " + log);
    }

    private static void FillCardList(StringBuilder log)
    {
        List<PerkCardDefinition> cards = AssetDatabase.FindAssets("t:PerkCardDefinition", new[] { CardFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<PerkCardDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null)
            .OrderBy(c => c.PerkStat.ToString()).ThenBy(c => c.Level)
            .ToList();

        GameObject root = PrefabUtility.LoadPrefabContents(PerkManagerPrefab);
        try
        {
            PerkSelectionManager manager = root.GetComponentInChildren<PerkSelectionManager>(true);
            if (manager == null) { throw new System.Exception("no PerkSelectionManager in " + PerkManagerPrefab); }

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty list = so.FindProperty("allCards");
            list.arraySize = cards.Count;
            for (int i = 0; i < cards.Count; i++) { list.GetArrayElementAtIndex(i).objectReferenceValue = cards[i]; }
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PerkManagerPrefab);
            log.AppendLine($"perk card list: {cards.Count} cards");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AddPerkSlotToShop(StringBuilder log)
    {
        ShopItemDefinition item = AssetDatabase.LoadAssetAtPath<ShopItemDefinition>(ShopItemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ShopItemDefinition>();
            item.displayName = "Perk Slot";
            item.description = "Unlocks one more perk card slot for the rest of the run.";
            item.price = 220;
            item.kind = ShopItemKind.PerkSlot;
            item.stockPerShop = 1;
            item.maxPurchasesPerRun = 3;
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/013-Datas/Shop/Icons/Icon_WeaponCarry.png");
            AssetDatabase.CreateAsset(item, ShopItemPath);
            log.AppendLine("created " + ShopItemPath);
        }

        ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
        if (catalog == null) { log.AppendLine("no shop catalog"); return; }
        if (!catalog.entries.Any(e => e != null && e.item == item))
        {
            catalog.entries.Add(new ShopCatalog.Entry { item = item, alwaysStocked = false, weight = 1f });
            EditorUtility.SetDirty(catalog);
            log.AppendLine("added Perk Slot to the shop catalog");
        }
    }

    private static void SetElitePotions(StringBuilder log)
    {
        EliteSettings settings = AssetDatabase.LoadAssetAtPath<EliteSettings>(EliteSettingsPath);
        if (settings == null) { log.AppendLine("no elite settings"); return; }

        settings.dropEnergyOrb = false;

        if (settings.potionDrops == null || settings.potionDrops.Count == 0)
        {
            settings.potionDrops = new List<EliteSettings.PotionDrop>();
            void Add(string file, float weight)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Potions}/{file}.prefab");
                if (prefab != null) { settings.potionDrops.Add(new EliteSettings.PotionDrop { itemPrefab = prefab, weight = weight }); }
                else { log.AppendLine("missing potion " + file); }
            }
            Add("HealthPotion_Small", 4f);
            Add("HealthPotion_Medium", 3f);
            Add("HealthPotion_Big", 1.5f);
            Add("ManaPotion_Small", 3f);
            Add("ManaPotion_Medium", 2f);
            Add("ManaPotion_Big", 1f);
            settings.potionCountMin = 1;
            settings.potionCountMax = 2;
        }

        EditorUtility.SetDirty(settings);
        log.AppendLine($"elite potions: {settings.potionDrops.Count} entries, orb drop off");
    }
}
