using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: sets up the Spear.
///  1. HandSpear-OneHanded / HandSpear-TwoHanded override controllers on the sword's controllers, with
///     their own Spear_ clips (copies of the sword clips, attacks slowed to match the spear's pace).
///     Open each Spear_ clip in the Animation window and replace the sprite keys with your spear frames.
///  2. Weapon_Spear prefab: a copy of the Short Sword with spear stats (long narrow thrust, pierces up to
///     3 enemies, weak up close, blocks only while two-handed).
///  3. A Spear item in the shop.
/// Existing assets are never overwritten. Re-run from Tools > Weapons > Set Up Spear.
/// </summary>
public static class SpearSetup
{
    private const string AutoKey = "SpearSetup_v1";

    private const string HandsFolder = "Assets/004-Animations/Hands";
    private const string SwordPrefabPath = "Assets/003-Prefabs/Weapons/Weapon_ShortSword.prefab";
    private const string SpearPrefabPath = "Assets/003-Prefabs/Weapons/Weapon_Spear.prefab";
    private const string ShopItemPath = "Assets/013-Datas/Shop/Items/Weapon_Spear.asset";
    private const string CatalogPath = "Assets/013-Datas/Shop/ShopCatalog (Prison).asset";

    // Attack clips are stretched by the cooldown ratio to the sword (0.3 s):
    // one-handed 0.38 s, two-handed 0.38 x 1.2 = 0.456 s
    private const float OneHandedAttackStretch = 1.25f;
    private const float TwoHandedAttackStretch = 1.5f;

    private static readonly (string sword, string spear, bool attack)[] Clips =
    {
        ("001_HandSword_Idle", "Idle", false),
        ("003_HandSword_Press1", "Press1", true),
        ("004_HandSword_Press2", "Press2", true),
        ("005_HandSword_Press3", "Press3", true),
        ("006_HandSword_Block", "Block", false),
        ("007_HandSword_Execute", "Execute", false),
    };

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

    [MenuItem("Tools/Weapons/Set Up Spear")]
    public static void Build()
    {
        var log = new StringBuilder();
        int errors = 0;

        try
        {
            AnimatorOverrideController oneHanded = BuildController("OneHanded", OneHandedAttackStretch, log, ref errors);
            AnimatorOverrideController twoHanded = BuildController("TwoHanded", TwoHandedAttackStretch, log, ref errors);
            Weapon spear = BuildPrefab(oneHanded, twoHanded, log);
            AddToShop(spear, log);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/spear_setup.txt", log.ToString());
        Debug.Log($"[SpearSetup] Done with {errors} error(s). Report: Library/PrisonDecor/spear_setup.txt");
    }

    private static AnimatorOverrideController BuildController(string grip, float attackStretch, StringBuilder log, ref int errors)
    {
        string swordFolder = $"{HandsFolder}/HandSword-{grip}";
        string spearFolder = $"{HandsFolder}/HandSpear-{grip}";
        string overridePath = $"{spearFolder}/HandSpear-{grip}.overrideController";

        AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
        if (controller == null)
        {
            RuntimeAnimatorController sword = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{swordFolder}/HandSword-{grip}.controller");
            if (sword == null) { throw new System.Exception("sword controller missing: " + swordFolder); }

            Directory.CreateDirectory(spearFolder);
            AssetDatabase.Refresh();
            controller = new AnimatorOverrideController(sword) { name = $"HandSpear-{grip}" };
            AssetDatabase.CreateAsset(controller, overridePath);
            log.AppendLine("created " + overridePath);
        }
        else
        {
            log.AppendLine("kept " + overridePath);
        }

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(controller.overridesCount);
        controller.GetOverrides(overrides);

        foreach (var (swordName, spearSuffix, attack) in Clips)
        {
            string swordPath = $"{swordFolder}/{swordName}.anim";
            string spearName = $"Spear{(grip == "TwoHanded" ? "2H" : "1H")}_{spearSuffix}";
            string spearPath = $"{spearFolder}/{spearName}.anim";

            AnimationClip swordClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(swordPath);
            if (swordClip == null) { log.AppendLine("!! missing " + swordPath); errors++; continue; }

            AnimationClip spearClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(spearPath);
            if (spearClip == null)
            {
                if (!AssetDatabase.CopyAsset(swordPath, spearPath)) { log.AppendLine("!! copy failed " + spearPath); errors++; continue; }
                AssetDatabase.ImportAsset(spearPath, ImportAssetOptions.ForceSynchronousImport);
                spearClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(spearPath);
                if (attack) { Retime(spearClip, attackStretch); }
                log.AppendLine($"created {spearName} ({spearClip.length:0.###} s)");
            }
            else
            {
                log.AppendLine($"kept {spearName}");
            }

            for (int i = 0; i < overrides.Count; i++)
            {
                if (overrides[i].Key == swordClip) { overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(swordClip, spearClip); }
            }
        }

        controller.ApplyOverrides(overrides);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static Weapon BuildPrefab(AnimatorOverrideController oneHanded, AnimatorOverrideController twoHanded, StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SpearPrefabPath) == null)
        {
            if (!AssetDatabase.CopyAsset(SwordPrefabPath, SpearPrefabPath)) { throw new System.Exception("copy failed"); }
            AssetDatabase.ImportAsset(SpearPrefabPath, ImportAssetOptions.ForceSynchronousImport);

            GameObject root = PrefabUtility.LoadPrefabContents(SpearPrefabPath);
            try
            {
                root.name = "Weapon_Spear";
                Weapon weapon = root.GetComponent<Weapon>();
                weapon.equipmentName = "Spear";
                weapon.meleeType = MeleeType.Spear;
                weapon.damage = 16f;                      // sword 18
                weapon.attackCooldown = 0.38f;            // sword 0.3: a committed thrust
                weapon.staminaCost = 8f;                  // sword 7
                weapon.damageKnockbackForce = 9f;         // sword 8: pushes enemies back to the tip
                weapon.blockStaminaDamageModifier = 1.2f; // sword 1: blocking costs a bit more

                weapon.canTwoHand = true;
                weapon.blockOnlyWhenTwoHanded = true;     // one-handed spear can't guard
                weapon.parryOnly = false;

                weapon.attackRangeMultiplier = 1.5f;      // 50% more reach
                weapon.attackRadiusOverride = 0.25f;      // narrow line, not a wide arc
                weapon.maxTargets = 3;                    // pierces up to 3 enemies in a line
                weapon.closeRangePenaltyDistance = 1f;    // hugging enemies...
                weapon.closeRangeDamageMultiplier = 0.6f; // ...only takes 60% damage

                weapon.twoHandedDamageMultiplier = 1.35f; // two-handed: harder thrusts...
                weapon.twoHandedCooldownMultiplier = 1.2f; // ...a bit slower

                weapon.animController_OneHanded = oneHanded;
                weapon.animController_TwoHanded = twoHanded;
                PrefabUtility.SaveAsPrefabAsset(root, SpearPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            log.AppendLine("created " + SpearPrefabPath + " (sword visuals as placeholder)");
        }
        else
        {
            log.AppendLine("spear prefab exists");
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(SpearPrefabPath).GetComponent<Weapon>();
    }

    private static void AddToShop(Weapon spear, StringBuilder log)
    {
        ShopItemDefinition item = AssetDatabase.LoadAssetAtPath<ShopItemDefinition>(ShopItemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ShopItemDefinition>();
            item.displayName = "Spear";
            item.description = "Long piercing thrust that hits up to 3 enemies in a line. Weak up close. Blocks only when held in both hands.";
            item.price = 130;
            item.kind = ShopItemKind.Weapon;
            item.itemPrefab = spear;
            item.stockPerShop = 1;
            AssetDatabase.CreateAsset(item, ShopItemPath);
            log.AppendLine("created " + ShopItemPath);
        }

        ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
        if (catalog != null && !catalog.entries.Any(e => e != null && e.item == item))
        {
            catalog.entries.Add(new ShopCatalog.Entry { item = item, alwaysStocked = false, weight = 1.5f });
            EditorUtility.SetDirty(catalog);
            log.AppendLine("added Spear to the shop catalog");
        }
    }

    private static void Retime(AnimationClip clip, float factor)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            for (int i = 0; i < keys.Length; i++) { keys[i].time *= factor; }
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].time *= factor;
                keys[i].inTangent /= factor;
                keys[i].outTangent /= factor;
            }
            curve.keys = keys;
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        EditorUtility.SetDirty(clip);
    }
}
