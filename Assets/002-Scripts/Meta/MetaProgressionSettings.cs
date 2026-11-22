using UnityEngine;

/// <summary>
/// Tuning for meta progression: Emerald drops from enemies, other Emerald rewards, and the starting items.
/// Lives at Resources/Meta/MetaProgressionSettings (created by Tools > Meta > Meta Progression Settings).
/// </summary>
[CreateAssetMenu(fileName = "MetaProgressionSettings", menuName = "Meta/Meta Progression Settings")]
public class MetaProgressionSettings : ScriptableObject
{
    public const string ResourcePath = "Meta/MetaProgressionSettings";

    [Header("Emerald drops (from killed enemies)")]
    [Tooltip("Chance (0..1) that a normal enemy drops emeralds.")]
    [Range(0f, 1f)] public float normalDropChance = 0.35f;
    public int normalDropAmount = 1;
    [Tooltip("Elites and the Brute always drop between these.")]
    public int toughDropMin = 2;
    public int toughDropMax = 3;
    [Tooltip("Bosses (Big Knight, Castle Lord) always drop between these.")]
    public int bossDropMin = 8;
    public int bossDropMax = 12;

    [Header("Other Emerald rewards (0 = none)")]
    public int perChallengeRoom = 10;
    public int victoryBonus = 50;

    [Header("Starting items (Camp boosts)")]
    [Tooltip("Given per level of Packed Satchel.")]
    public Item startingPotion;
    public int coinsPerPurseLevel = 50;
    public float hpPerVitalityLevel = 10f;

    private static MetaProgressionSettings cached;
    public static MetaProgressionSettings Load()
    {
        if (cached == null) { cached = Resources.Load<MetaProgressionSettings>(ResourcePath); }
        if (cached == null) { cached = CreateInstance<MetaProgressionSettings>(); } // defaults if missing
        return cached;
    }
}
