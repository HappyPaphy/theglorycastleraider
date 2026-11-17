using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: adds a RingEffect to the ring prefabs with starting values.
///  - Ring of Vitality: +30 max HP.
///  - Ring of Stamina: +30 max stamina, +25% stamina recovery.
/// Rings that already have a RingEffect are left alone. Re-run from Tools > Items > Set Up Ring Effects.
/// </summary>
public static class RingEffectSetup
{
    private const string AutoKey = "RingEffectSetup_v1";
    private const string RingFolder = "Assets/003-Prefabs/Items/Rings";

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

    [MenuItem("Tools/Items/Set Up Ring Effects")]
    public static void Build()
    {
        var log = new StringBuilder();
        int errors = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RingFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Item item = root.GetComponent<Item>();
                if (item == null || item.itemCategory != ItemCategory.Ring) { continue; }
                if (root.GetComponent<RingEffect>() != null) { log.AppendLine("kept " + path); continue; }

                RingEffect effect = root.AddComponent<RingEffect>();
                switch (item.itemType)
                {
                    case ItemType.RingOfVitality:
                        effect.maxHealthBonus = 30f;
                        break;
                    case ItemType.RingOfStamina:
                        effect.maxStaminaBonus = 30f;
                        effect.staminaRegenMultiplier = 1.25f;
                        break;
                    default:
                        log.AppendLine("no starting values for " + item.itemType + " (fill it in on the prefab)");
                        break;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"added RingEffect: {path} -> {effect.Describe().Replace("\n", ", ")}");
            }
            catch (System.Exception e)
            {
                errors++;
                log.AppendLine("!! " + path + ": " + e);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/ring_effects.txt", log.ToString());
        Debug.Log($"[RingEffectSetup] Done with {errors} error(s). Report: Library/PrisonDecor/ring_effects.txt");
    }
}
