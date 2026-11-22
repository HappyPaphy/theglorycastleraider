using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: writes a description into every item / weapon prefab whose description is still empty
/// (shown in the equipment screen). Never overwrites a description that is already written.
/// Numbers match the game code (PlayerWeaponManager potions, spell prefabs, weapon stats).
/// Tools > Items > Fill Missing Item Descriptions.
/// </summary>
public static class ItemDescriptionFill
{
    private const string AutoKey = "ItemDescriptionFill_v1";

    private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        // Consumables (PlayerWeaponManager.UseConsumable)
        { "Assets/003-Prefabs/Items/Consumables/HealthPotion_Small.prefab",
          "A small vial of crimson draught, bitter but welcome.\nRestores 60 HP." },
        { "Assets/003-Prefabs/Items/Consumables/HealthPotion_Medium.prefab",
          "A sturdy flask of crimson draught that knits wounds shut.\nRestores 80 HP." },
        { "Assets/003-Prefabs/Items/Consumables/HealthPotion_Big.prefab",
          "A heavy flask brewed for the castle's knights.\nRestores 135 HP." },
        { "Assets/003-Prefabs/Items/Consumables/ManaPotion_Small.prefab",
          "A small vial of shimmering blue liquid.\nRestores 40 mana." },
        { "Assets/003-Prefabs/Items/Consumables/ManaPotion_Medium.prefab",
          "A flask of shimmering blue liquid that clears the mind.\nRestores 80 mana." },
        { "Assets/003-Prefabs/Items/Consumables/ManaPotion_Big.prefab",
          "A large flask of concentrated arcane essence.\nRestores 135 mana." },

        // Rings (their stat bonuses are listed under this automatically)
        { "Assets/003-Prefabs/Items/Rings/Ring_Stamina.prefab",
          "A plain iron band that steadies your breath, letting you fight longer before tiring." },
        { "Assets/003-Prefabs/Items/Rings/Ring_Vitality.prefab",
          "A ring set with a warm red stone that strengthens the body of whoever wears it." },

        // Pyromancies (need a pyromancy flame in hand)
        { "Assets/003-Prefabs/Items/Spells/Spell_SmallFireball.prefab",
          "Pyromancy. Hurl a ball of fire that bursts on impact for 25 damage.\nCosts 16 mana. Cast with a pyromancy flame in hand." },
        { "Assets/003-Prefabs/Items/Spells/Spell_SmallCombustion.prefab",
          "Pyromancy. Hold to release a short stream of flame that keeps burning everything in front of you.\nDrains mana while casting. Cast with a pyromancy flame in hand." },

        // Weapons
        { "Assets/003-Prefabs/Weapons/Pyromancy_DimmedHand.prefab",
          "A faint ember cupped in the palm. Hold it in your hand to cast pyromancies." },
        { "Assets/003-Prefabs/Weapons/Weapon_Dagger.prefab",
          "Fast, light strikes that cost little stamina (11 damage, 2-hit combo).\nWeak guard. Pairs well with a second dagger." },
        { "Assets/003-Prefabs/Weapons/Weapon_ShortSword.prefab",
          "A quick, reliable blade with balanced reach and power (18 damage, 2-hit combo)." },
        { "Assets/003-Prefabs/Weapons/Weapon_Spear.prefab",
          "Long piercing thrusts that hit up to 3 enemies in a line (16 damage, 3-hit combo).\nWeak up close. Blocks only when held in both hands." },
        { "Assets/003-Prefabs/Weapons/Weapon_WoodenShield.prefab",
          "Raise it to block incoming blows. Time it just right to parry and leave the attacker open for a counter." },
    };

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

    [MenuItem("Tools/Items/Fill Missing Item Descriptions")]
    public static void Run()
    {
        var log = new StringBuilder();
        int filled = 0;

        foreach (var pair in Descriptions)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(pair.Key) == null) { log.AppendLine("!! missing " + pair.Key); continue; }

            GameObject root = PrefabUtility.LoadPrefabContents(pair.Key);
            try
            {
                Item item = root.GetComponent<Item>();
                if (item == null) { log.AppendLine("!! no Item on " + pair.Key); continue; }
                if (!string.IsNullOrWhiteSpace(item.description)) { log.AppendLine("kept   " + item.equipmentName); continue; }

                item.description = pair.Value;
                PrefabUtility.SaveAsPrefabAsset(root, pair.Key);
                filled++;
                log.AppendLine("filled " + item.equipmentName);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Anything else with an Item and no description (new items added later)
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/003-Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Descriptions.ContainsKey(path)) { continue; }
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Item item = go != null ? go.GetComponent<Item>() : null;
            if (item != null && string.IsNullOrWhiteSpace(item.description) && item.GetComponent<RingEffect>() == null)
            {
                log.AppendLine("?? still empty: " + path);
            }
        }

        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/item_descriptions.txt", log.ToString());
        Debug.Log($"[ItemDescriptions] Done, filled {filled}. Report: Library/PrisonDecor/item_descriptions.txt");
    }
}
