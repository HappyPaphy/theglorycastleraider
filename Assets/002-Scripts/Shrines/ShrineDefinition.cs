using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum OrbReward
{
    None,
    Common,
    Rare,
    Unique
}

/// <summary>
/// One kind of shrine: a deal of COSTS and GAINS. Any combination works. Create via
/// Assets > Create > Shrines > Shrine and add it to the shrine pool on ShrineSettings.
/// Leave a value at 0 / None / off to not use it. The offer text above the altar is written from these values.
/// </summary>
[CreateAssetMenu(fileName = "Shrine", menuName = "Shrines/Shrine")]
public class ShrineDefinition : ScriptableObject
{
    [Serializable]
    public class GambleOutcome
    {
        [Tooltip("Coins paid back as a multiple of the cost (0 = lose it all, 3 = triple).")]
        [Min(0f)] public float payoutMultiplier = 1f;
        [Min(0f)] public float weight = 1f;
    }

    [Header("Display")]
    public string displayName = "New Shrine";
    [Tooltip("Optional flavour line under the name.")]
    public string flavour;
    public Color color = Color.white;
    [Tooltip("Floats above the altar.")]
    public Sprite icon;

    [Header("Rolling")]
    [Tooltip("Higher = appears more often in shrine rooms.")]
    [Min(0f)] public float weight = 1f;
    [Tooltip("Can only appear from this floor on (1 = first floor).")]
    [Min(1)] public int minFloor = 1;

    [Header("Costs")]
    [Min(0)] public int coinCost;
    [Tooltip("Fraction of max HP lost for the rest of the run (0.15 = 15%).")]
    [Range(0f, 0.9f)] public float maxHealthCost;
    [Tooltip("Fraction of max stamina lost for the rest of the run.")]
    [Range(0f, 0.9f)] public float maxStaminaCost;
    [Tooltip("Random curses you receive (they last until the next floor).")]
    [Min(0)] public int curseCount;

    [Header("Gains")]
    [Tooltip("An Energy Orb of this rarity appears in front of the altar.")]
    public OrbReward energyOrb = OrbReward.None;
    [Tooltip("Heal this fraction of max HP (1 = full heal).")]
    [Range(0f, 1f)] public float healFraction;
    [Tooltip("Remove all curses.")]
    public bool cleanseCurses;
    [Tooltip("More damage for the rest of the run (0.2 = +20%).")]
    [Min(0f)] public float damageBonus;
    [Tooltip("More max HP for the rest of the run (0.15 = +15%).")]
    [Min(0f)] public float maxHealthBonus;
    [Tooltip("Gamble: the coin cost is paid back as one of these outcomes (picked by weight). Empty = no gamble.")]
    public List<GambleOutcome> gambleOutcomes = new List<GambleOutcome>();

    public bool IsGamble => gambleOutcomes != null && gambleOutcomes.Count > 0;

    /// <summary>Text shown above the altar, built from the values above.</summary>
    public string BuildOfferText()
    {
        var gains = new List<string>();
        if (energyOrb != OrbReward.None) { gains.Add($"{energyOrb} Energy Orb"); }
        if (healFraction >= 0.999f) { gains.Add("Full heal"); }
        else if (healFraction > 0f) { gains.Add($"Heal {Mathf.RoundToInt(healFraction * 100f)}%"); }
        if (cleanseCurses) { gains.Add("Remove all curses"); }
        if (damageBonus > 0f) { gains.Add($"+{Mathf.RoundToInt(damageBonus * 100f)}% damage"); }
        if (maxHealthBonus > 0f) { gains.Add($"+{Mathf.RoundToInt(maxHealthBonus * 100f)}% max HP"); }
        if (IsGamble)
        {
            float best = 0f;
            foreach (GambleOutcome o in gambleOutcomes) { if (o != null) best = Mathf.Max(best, o.payoutMultiplier); }
            gains.Add($"Win up to x{best:0.#} coins");
        }

        var costs = new List<string>();
        if (coinCost > 0) { costs.Add($"{coinCost} coins"); }
        if (maxHealthCost > 0f) { costs.Add($"{Mathf.RoundToInt(maxHealthCost * 100f)}% max HP"); }
        if (maxStaminaCost > 0f) { costs.Add($"{Mathf.RoundToInt(maxStaminaCost * 100f)}% max stamina"); }
        if (curseCount > 0) { costs.Add(curseCount == 1 ? "a curse" : $"{curseCount} curses"); }

        var sb = new StringBuilder();
        if (gains.Count > 0) { sb.Append("<color=#9BE37B>+ ").Append(string.Join(", ", gains)).Append("</color>"); }
        if (costs.Count > 0)
        {
            if (sb.Length > 0) { sb.Append('\n'); }
            sb.Append("<color=#E36B5B>- ").Append(string.Join(", ", costs)).Append("</color>");
        }
        return sb.ToString();
    }
}
