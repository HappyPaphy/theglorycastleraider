using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global tuning for elite enemies: how often they appear, how many traits they roll, rewards and the look.
/// The traits themselves are separate EliteTraitDefinition assets listed in <see cref="traitPool"/>.
/// Lives at Resources/Elites/EliteSettings (Tools > Elites > Build Elite Settings).
/// </summary>
[CreateAssetMenu(fileName = "EliteSettings", menuName = "Elites/Elite Settings")]
public class EliteSettings : ScriptableObject
{
    public const string ResourcePath = "Elites/EliteSettings";

    [Header("Spawn chance (per spawned enemy)")]
    [Range(0f, 1f)] public float baseChance = 0.08f;
    [Tooltip("Added per floor after the first.")]
    [Range(0f, 1f)] public float chancePerFloor = 0.06f;
    [Range(0f, 1f)] public float maxChance = 0.35f;

    [Header("Traits")]
    [Tooltip("Every trait an elite can roll. Add your own (Create > Elites > Elite Trait).")]
    public List<EliteTraitDefinition> traitPool = new List<EliteTraitDefinition>();
    [Tooltip("Most traits one elite can have.")]
    [Min(1)] public int maxTraits = 2;
    [Tooltip("Extra traits (beyond the first) can only appear from this floor on.")]
    [Min(1)] public int extraTraitsFromFloor = 3;
    [Tooltip("Chance of each extra trait, rolled one after another (0.3 = 30% for a 2nd, then 30% of that for a 3rd...).")]
    [Range(0f, 1f)] public float extraTraitChance = 0.3f;

    [System.Serializable]
    public class PotionDrop
    {
        [Tooltip("Item prefab to drop (e.g. a potion).")]
        public GameObject itemPrefab;
        [Tooltip("Higher = dropped more often.")]
        [Min(0f)] public float weight = 1f;
    }

    [Header("Rewards: Potions")]
    [Tooltip("Elites drop items from this list (picked by weight).")]
    public List<PotionDrop> potionDrops = new List<PotionDrop>();
    [Min(0)] public int potionCountMin = 1;
    [Min(0)] public int potionCountMax = 2;

    [Header("Rewards: Energy Orb (optional)")]
    [Tooltip("Also drop an Energy Orb. Off by default: perk slots are limited, so elites give potions instead.")]
    public bool dropEnergyOrb = false;
    public EnergyOrbPickup energyOrbPrefab;
    public EnergyOrbDefinition commonOrb;
    public EnergyOrbDefinition rareOrb;
    public EnergyOrbDefinition uniqueOrb;
    [Range(0f, 1f)] public float commonOrbChance = 0.3f;
    [Tooltip("Unique gets whatever chance is left after Common and Rare.")]
    [Range(0f, 1f)] public float rareOrbChance = 0.6f;
    [Tooltip("Total coins as a multiple of the enemy's normal drop: 2.5 = 2.5x (the extra 1.5x drops as a bonus burst). Traits can add more.")]
    public float coinMultiplier = 2.5f;

    [Header("Look")]
    [Tooltip("How much bigger every elite is (traits can add more).")]
    public float sizeMultiplier = 1.12f;
    [Tooltip("Height of the trait names above the enemy's head.")]
    public float labelHeight = 0.25f;
    [Tooltip("Size of the trait names text.")]
    public float labelFontSize = 1.6f;
    [Tooltip("Text between trait names.")]
    public string labelSeparator = " · ";
    [Tooltip("Width of the ground ring, in metres (before the enemy's size).")]
    public float ringDiameter = 1.6f;
    [Tooltip("Degrees per second the ground ring turns.")]
    public float ringSpinSpeed = 40f;
    [Tooltip("How fast the ground ring pulses.")]
    public float ringPulseSpeed = 4f;
    [Tooltip("Ring colour while the elite's ward is broken by a kick.")]
    public Color wardBrokenColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [Tooltip("Ring colour while the elite is about to explode (it flashes between this and white).")]
    public Color explosionWarningColor = new Color(1f, 0.5f, 0.1f, 1f);

    public float ChanceForFloor(int floorIndex)
    {
        return Mathf.Min(maxChance, baseChance + chancePerFloor * Mathf.Max(0, floorIndex));
    }

    private static EliteSettings cached;
    public static EliteSettings Load()
    {
        if (cached == null) { cached = Resources.Load<EliteSettings>(ResourcePath); }
        return cached;
    }
}
