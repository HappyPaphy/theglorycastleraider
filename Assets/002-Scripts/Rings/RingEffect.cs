using UnityEngine;

/// <summary>
/// What a ring does while it is in one of the player's ring slots. Put it on a ring prefab (next to its Item).
/// Every value can be mixed: 1 = no change for multipliers, 0 = no change for bonuses.
/// Several equipped rings stack (bonuses add, multipliers multiply).
/// </summary>
public class RingEffect : MonoBehaviour
{
    [Tooltip("Shown in the item description.")]
    [TextArea] public string effectDescription;

    [Header("Health & Stamina")]
    [Tooltip("Extra max HP while worn (the player's base is 100).")]
    public float maxHealthBonus = 0f;
    [Tooltip("Extra max stamina while worn (the player's base is 100).")]
    public float maxStaminaBonus = 0f;
    [Tooltip("Stamina recovery speed (1.25 = 25% faster).")]
    [Min(0f)] public float staminaRegenMultiplier = 1f;
    [Tooltip("Stamina lost when blocking a hit (0.8 = 20% less).")]
    [Min(0f)] public float blockStaminaMultiplier = 1f;

    [Header("Combat")]
    [Tooltip("Damage you deal (1.15 = 15% more).")]
    [Min(0f)] public float damageDealtMultiplier = 1f;
    [Tooltip("Damage you take (0.9 = 10% less).")]
    [Min(0f)] public float damageTakenMultiplier = 1f;
    [Tooltip("Attack speed (1.1 = 10% faster attacks).")]
    [Min(0.1f)] public float attackSpeedMultiplier = 1f;

    [Header("Movement & Loot")]
    [Tooltip("Walk / sprint speed (1.1 = 10% faster).")]
    [Min(0.1f)] public float moveSpeedMultiplier = 1f;
    [Tooltip("Coins dropped by enemies (1.25 = 25% more).")]
    [Min(0f)] public float coinMultiplier = 1f;

    /// <summary>The description text, or a list of the effects if none was written.</summary>
    public string Describe()
    {
        if (!string.IsNullOrWhiteSpace(effectDescription)) { return effectDescription; }

        var sb = new System.Text.StringBuilder();
        if (maxHealthBonus != 0f) { sb.AppendLine($"{maxHealthBonus:+0;-0} Max HP"); }
        if (maxStaminaBonus != 0f) { sb.AppendLine($"{maxStaminaBonus:+0;-0} Max Stamina"); }
        Percent(sb, staminaRegenMultiplier, "Stamina recovery");
        Percent(sb, blockStaminaMultiplier, "Block stamina cost");
        Percent(sb, damageDealtMultiplier, "Damage dealt");
        Percent(sb, damageTakenMultiplier, "Damage taken");
        Percent(sb, attackSpeedMultiplier, "Attack speed");
        Percent(sb, moveSpeedMultiplier, "Move speed");
        Percent(sb, coinMultiplier, "Coins from enemies");
        return sb.ToString().TrimEnd();
    }

    private static void Percent(System.Text.StringBuilder sb, float multiplier, string label)
    {
        if (Mathf.Approximately(multiplier, 1f)) { return; }
        int percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
        sb.AppendLine($"{percent:+0;-0}% {label}");
    }
}
