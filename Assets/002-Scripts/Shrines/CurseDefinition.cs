using UnityEngine;

/// <summary>
/// A curse the player carries until they go down to the next floor (or cleanse it at a shrine).
/// Create via Assets > Create > Shrines > Curse and add it to the curse pool on ShrineSettings.
/// Several curses stack: multipliers multiply, the elite chance bonuses add up.
/// Leave a value at its default (1 for multipliers, 0 / off for the rest) to not use it.
/// </summary>
[CreateAssetMenu(fileName = "Curse", menuName = "Shrines/Curse")]
public class CurseDefinition : ScriptableObject
{
    [Header("Display")]
    public string displayName = "New Curse";
    [TextArea(1, 3)] public string description;
    public Color color = new Color(0.7f, 0.3f, 0.9f);
    [Tooltip("Higher = picked more often when a shrine curses you.")]
    [Min(0f)] public float weight = 1f;

    [Header("Effects")]
    [Tooltip("Damage the player takes (1.25 = 25% more).")]
    [Min(0f)] public float damageTakenMultiplier = 1f;
    [Tooltip("Stamina lost when blocking (2 = blocks cost double).")]
    [Min(0f)] public float blockStaminaMultiplier = 1f;
    [Tooltip("Coins dropped by enemies (0 = none).")]
    [Min(0f)] public float coinDropMultiplier = 1f;
    [Tooltip("Damage the player deals (0.8 = 20% less).")]
    [Min(0f)] public float damageDealtMultiplier = 1f;
    [Tooltip("Added to the chance that a spawned enemy is an elite (0.25 = +25%).")]
    [Range(0f, 1f)] public float eliteChanceBonus = 0f;
    [Tooltip("No red warning for attacks from behind.")]
    public bool disableRearWarning;
}
