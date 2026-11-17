using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: sets up the Dagger.
///  1. HandDagger-OneHanded.overrideController: an Animator Override Controller on the sword's one-handed
///     controller. Same states (Idle, Press1-3, Block, Execute...), so drop your dagger clips into its slots.
///     Empty slots fall back to the sword clips (placeholder).
///  2. Weapon_Dagger prefab: a copy of the Short Sword with dagger stats and the override controller.
///  3. A Dagger item in the shop.
/// Existing assets are never overwritten. Re-run from Tools > Weapons > Set Up Dagger.
/// </summary>
public static class DaggerSetup
{
    private const string AutoKey = "DaggerSetup_v1";

    private const string SwordControllerPath = "Assets/004-Animations/Hands/HandSword-OneHanded/HandSword-OneHanded.controller";
    private const string DaggerAnimFolder = "Assets/004-Animations/Hands/HandDagger-OneHanded";
    private const string OverridePath = DaggerAnimFolder + "/HandDagger-OneHanded.overrideController";
    private const string SwordPrefabPath = "Assets/003-Prefabs/Weapons/Weapon_ShortSword.prefab";
    private const string DaggerPrefabPath = "Assets/003-Prefabs/Weapons/Weapon_Dagger.prefab";
    private const string ShopItemPath = "Assets/013-Datas/Shop/Items/Weapon_Dagger.asset";
    private const string CatalogPath = "Assets/013-Datas/Shop/ShopCatalog (Prison).asset";

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

    [MenuItem("Tools/Weapons/Set Up Dagger")]
    public static void Build()
    {
        var log = new StringBuilder();
        int errors = 0;

        try
        {
            AnimatorOverrideController controller = BuildOverrideController(log);
            Weapon dagger = BuildPrefab(controller, log);
            AddToShop(dagger, log);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/dagger_setup.txt", log.ToString());
        Debug.Log($"[DaggerSetup] Done with {errors} error(s). Report: Library/PrisonDecor/dagger_setup.txt");
    }

    private static AnimatorOverrideController BuildOverrideController(StringBuilder log)
    {
        AnimatorOverrideController existing = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
        if (existing != null) { log.AppendLine("override controller exists"); return existing; }

        RuntimeAnimatorController sword = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SwordControllerPath);
        if (sword == null) { throw new System.Exception("sword controller missing: " + SwordControllerPath); }

        Directory.CreateDirectory(DaggerAnimFolder);
        var controller = new AnimatorOverrideController(sword) { name = "HandDagger-OneHanded" };
        AssetDatabase.CreateAsset(controller, OverridePath);

        string slots = string.Join(", ", sword.animationClips.Select(c => c.name).Distinct());
        log.AppendLine("created " + OverridePath);
        log.AppendLine("slots to fill: " + slots);
        return controller;
    }

    private static Weapon BuildPrefab(AnimatorOverrideController controller, StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DaggerPrefabPath) == null)
        {
            if (!AssetDatabase.CopyAsset(SwordPrefabPath, DaggerPrefabPath)) { throw new System.Exception("copy failed"); }
            AssetDatabase.ImportAsset(DaggerPrefabPath, ImportAssetOptions.ForceSynchronousImport);

            GameObject root = PrefabUtility.LoadPrefabContents(DaggerPrefabPath);
            try
            {
                root.name = "Weapon_Dagger";
                Weapon weapon = root.GetComponent<Weapon>();
                weapon.equipmentName = "Dagger";
                weapon.meleeType = MeleeType.Dagger;
                weapon.damage = 11f;                     // sword 18: weaker per hit...
                weapon.attackCooldown = 0.2f;            // sword 0.3: ...but much faster
                weapon.staminaCost = 4f;                 // sword 7: cheap to keep swinging
                weapon.damageKnockbackForce = 4f;        // sword 8: light hits
                weapon.blockStaminaDamageModifier = 1.5f; // sword 1: a small blade makes a poor guard
                weapon.animController_OneHanded = controller;
                weapon.animController_TwoHanded = controller; // never used: daggers can't be two-handed
                weapon.canTwoHand = false;
                weapon.parryOnly = true;
                weapon.parryCooldown = 0.35f;               // Block button = tap to parry, no held block
                PrefabUtility.SaveAsPrefabAsset(root, DaggerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            log.AppendLine("created " + DaggerPrefabPath + " (sword visuals as placeholder)");
        }
        else
        {
            log.AppendLine("dagger prefab exists");
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(DaggerPrefabPath).GetComponent<Weapon>();
    }

    private static void AddToShop(Weapon dagger, StringBuilder log)
    {
        ShopItemDefinition item = AssetDatabase.LoadAssetAtPath<ShopItemDefinition>(ShopItemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ShopItemDefinition>();
            item.displayName = "Dagger";
            item.description = "Fast, light strikes. Weak guard. Pairs well with a second dagger.";
            item.price = 90;
            item.kind = ShopItemKind.Weapon;
            item.itemPrefab = dagger;
            item.stockPerShop = 1;
            AssetDatabase.CreateAsset(item, ShopItemPath);
            log.AppendLine("created " + ShopItemPath);
        }

        ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
        if (catalog != null && !catalog.entries.Any(e => e != null && e.item == item))
        {
            catalog.entries.Add(new ShopCatalog.Entry { item = item, alwaysStocked = false, weight = 1.5f });
            EditorUtility.SetDirty(catalog);
            log.AppendLine("added Dagger to the shop catalog");
        }
    }
}
