using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global shrine tuning: which shrines and curses exist, the orb rewards, and the look.
/// Lives at Resources/Shrines/ShrineSettings (built by Tools > Shrines > Build Shrines).
/// </summary>
[CreateAssetMenu(fileName = "ShrineSettings", menuName = "Shrines/Shrine Settings")]
public class ShrineSettings : ScriptableObject
{
    public const string ResourcePath = "Shrines/ShrineSettings";

    [Header("Pools")]
    [Tooltip("Shrine types a Shrine Room can roll. Add your own (Create > Shrines > Shrine).")]
    public List<ShrineDefinition> shrinePool = new List<ShrineDefinition>();
    [Tooltip("Curses shrines can give. Add your own (Create > Shrines > Curse).")]
    public List<CurseDefinition> cursePool = new List<CurseDefinition>();

    [Header("Energy Orb rewards")]
    public EnergyOrbPickup energyOrbPrefab;
    public EnergyOrbDefinition commonOrb;
    public EnergyOrbDefinition rareOrb;
    public EnergyOrbDefinition uniqueOrb;

    [Header("Interaction")]
    [Tooltip("Seconds the 'press again to accept' confirmation stays armed.")]
    public float confirmWindow = 3f;
    public float interactRadius = 2f;

    [Header("Look")]
    [Tooltip("Height of the floating icon above the altar's base.")]
    public float iconHeight = 1.4f;
    [Tooltip("Height of the shrine text above the altar's base (the text grows upward from here).")]
    public float labelHeight = 1.68f;
    public float labelFontSize = 1.5f;
    public Color spentColor = new Color(0.35f, 0.35f, 0.38f, 1f);

    public EnergyOrbDefinition GetOrb(OrbReward reward)
    {
        switch (reward)
        {
            case OrbReward.Common: return commonOrb;
            case OrbReward.Rare: return rareOrb;
            case OrbReward.Unique: return uniqueOrb;
            default: return null;
        }
    }

    private static ShrineSettings cached;
    public static ShrineSettings Load()
    {
        if (cached == null) { cached = Resources.Load<ShrineSettings>(ResourcePath); }
        return cached;
    }
}
