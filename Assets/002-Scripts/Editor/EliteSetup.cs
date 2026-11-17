using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates / updates Resources/Elites/EliteSettings.asset:
///  - links the Energy Orb prefab and the orb rarities (only if missing)
///  - creates the 6 starting traits as assets in 013-Datas/Elites/Traits (Brutal, Juggernaut, Swift, Volatile,
///    Vampiric, Warded) and puts them in the trait pool. Existing trait assets are never overwritten.
/// When converting from the old settings (fixed traits), the old values are read from the asset file first.
/// Re-run from Tools > Elites > Build Elite Settings.
/// </summary>
public static class EliteSetup
{
    private const string AutoKey = "EliteSetup_v2";
    private const string SettingsPath = "Assets/Resources/Elites/EliteSettings.asset";
    private const string TraitFolder = "Assets/013-Datas/Elites/Traits";
    private const string OrbPrefabPath = "Assets/003-Prefabs/EnergyOrb.prefab";
    private const string OrbFolder = "Assets/013-Datas/Perks/Energy Orbs";

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

    [MenuItem("Tools/Elites/Build Elite Settings")]
    public static void Build()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
        Directory.CreateDirectory(TraitFolder);

        // Old (fixed-trait) values, read from the file before Unity re-saves it without them
        Dictionary<string, string> old = ReadOldValues();

        EliteSettings settings = AssetDatabase.LoadAssetAtPath<EliteSettings>(SettingsPath);
        bool created = settings == null;
        if (created)
        {
            settings = ScriptableObject.CreateInstance<EliteSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        // Rewards
        if (settings.energyOrbPrefab == null)
        {
            GameObject orb = AssetDatabase.LoadAssetAtPath<GameObject>(OrbPrefabPath);
            settings.energyOrbPrefab = orb != null ? orb.GetComponent<EnergyOrbPickup>() : null;
        }
        if (settings.commonOrb == null) { settings.commonOrb = AssetDatabase.LoadAssetAtPath<EnergyOrbDefinition>($"{OrbFolder}/EnergyOrb_Common.asset"); }
        if (settings.rareOrb == null) { settings.rareOrb = AssetDatabase.LoadAssetAtPath<EnergyOrbDefinition>($"{OrbFolder}/EnergyOrb_Rare.asset"); }
        if (settings.uniqueOrb == null) { settings.uniqueOrb = AssetDatabase.LoadAssetAtPath<EnergyOrbDefinition>($"{OrbFolder}/EnergyOrb_Unique.asset"); }

        // Old "second trait" settings carry over to the new extra-trait settings
        if (old.ContainsKey("secondTraitFromFloor")) { settings.extraTraitsFromFloor = Mathf.Max(1, (int)F(old, "secondTraitFromFloor", 3)); }
        if (old.ContainsKey("secondTraitChance")) { settings.extraTraitChance = F(old, "secondTraitChance", 0.3f); }

        Color C(int index, Color fallback) => OldColor(old, index, fallback);
        string N(int index, string fallback) => old.TryGetValue($"looks.{index}.displayName", out string n) ? n : fallback;

        var traits = new List<EliteTraitDefinition>
        {
            Trait("Brutal", t =>
            {
                t.displayName = N(0, "Brutal"); t.color = C(0, new Color(0.95f, 0.2f, 0.15f));
                t.damageMultiplier = F(old, "brutalDamageMultiplier", 1.6f);
                t.staminaDamageMultiplier = F(old, "brutalDamageMultiplier", 1.6f);
            }),
            Trait("Juggernaut", t =>
            {
                t.displayName = N(1, "Juggernaut"); t.color = C(1, new Color(0.62f, 0.35f, 0.95f));
                t.healthMultiplier = F(old, "juggernautHealthMultiplier", 2.2f);
                t.sizeMultiplier = F(old, "juggernautSizeMultiplier", 1.2f);
            }),
            Trait("Swift", t =>
            {
                t.displayName = N(2, "Swift"); t.color = C(2, new Color(1f, 0.88f, 0.2f));
                t.speedMultiplier = F(old, "swiftSpeedMultiplier", 1.45f);
            }),
            Trait("Volatile", t =>
            {
                t.displayName = N(3, "Volatile"); t.color = C(3, new Color(1f, 0.5f, 0.1f));
                t.explodesOnDeath = true;
                t.explosionFuse = F(old, "volatileFuse", 1.2f);
                t.explosionRadius = F(old, "volatileRadius", 2.8f);
                t.explosionDamage = F(old, "volatileDamage", 25f);
                t.explosionEnemyDamage = F(old, "volatileEnemyDamage", 40f);
            }),
            Trait("Vampiric", t =>
            {
                t.displayName = N(4, "Vampiric"); t.color = C(4, new Color(0.75f, 0.05f, 0.3f));
                t.lifeSteal = F(old, "vampiricHealFraction", 1f);
            }),
            Trait("Warded", t =>
            {
                t.displayName = N(5, "Warded"); t.color = C(5, new Color(0.3f, 0.85f, 1f));
                t.hasWard = true;
                t.wardDamageTaken = F(old, "wardedDamageTaken", 0.35f);
                t.wardBreakDuration = F(old, "wardBreakDuration", 5f);
            }),
        };

        // Add any that aren't in the pool yet (keeps your own traits and order)
        foreach (EliteTraitDefinition trait in traits)
        {
            if (trait != null && !settings.traitPool.Contains(trait)) { settings.traitPool.Add(trait); }
        }
        settings.traitPool.RemoveAll(t => t == null);

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log($"[EliteSetup] {(created ? "Created" : "Updated")} {SettingsPath}: {settings.traitPool.Count} trait(s) in pool, " +
                  $"orbPrefab={settings.energyOrbPrefab != null}, old values found={old.Count}");
    }

    // Creates the trait asset with these values the first time only (never overwrites your edits)
    private static EliteTraitDefinition Trait(string name, System.Action<EliteTraitDefinition> fill)
    {
        string path = $"{TraitFolder}/Trait_{name}.asset";
        EliteTraitDefinition trait = AssetDatabase.LoadAssetAtPath<EliteTraitDefinition>(path);
        if (trait != null) { return trait; }

        trait = ScriptableObject.CreateInstance<EliteTraitDefinition>();
        fill(trait);
        AssetDatabase.CreateAsset(trait, path);
        return trait;
    }

    // ---------- reading the old fixed-trait fields straight from the YAML ----------

    private static Dictionary<string, string> ReadOldValues()
    {
        var values = new Dictionary<string, string>();
        if (!File.Exists(SettingsPath)) { return values; }

        string[] lines = File.ReadAllLines(SettingsPath);
        int lookIndex = -1;
        bool inLooks = false;

        foreach (string line in lines)
        {
            if (Regex.IsMatch(line, @"^  looks:\s*$")) { inLooks = true; continue; }
            if (inLooks && Regex.IsMatch(line, @"^  [A-Za-z]")) { inLooks = false; }

            if (inLooks)
            {
                if (Regex.IsMatch(line, @"^  - trait:")) { lookIndex++; continue; }
                Match m = Regex.Match(line, @"^    (displayName|color): (.*)$");
                if (m.Success && lookIndex >= 0) { values[$"looks.{lookIndex}.{m.Groups[1].Value}"] = m.Groups[2].Value.Trim(); }
                continue;
            }

            Match kv = Regex.Match(line, @"^  ([A-Za-z]+): ([-0-9.eE]+)\s*$");
            if (kv.Success) { values[kv.Groups[1].Value] = kv.Groups[2].Value; }
        }
        return values;
    }

    private static float F(Dictionary<string, string> values, string key, float fallback)
    {
        return values.TryGetValue(key, out string s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;
    }

    private static Color OldColor(Dictionary<string, string> values, int index, Color fallback)
    {
        if (!values.TryGetValue($"looks.{index}.color", out string s)) { return fallback; }
        Match m = Regex.Match(s, @"r: ([-0-9.eE]+), g: ([-0-9.eE]+), b: ([-0-9.eE]+), a: ([-0-9.eE]+)");
        if (!m.Success) { return fallback; }
        float P(int g) => float.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
        return new Color(P(1), P(2), P(3), P(4));
    }
}
