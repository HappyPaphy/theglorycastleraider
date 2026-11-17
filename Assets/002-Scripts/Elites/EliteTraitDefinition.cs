using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One elite trait = any mix of effects. Create via Assets > Create > Elites > Elite Trait, then add it to the
/// trait pool on EliteSettings. When an elite has several traits, their effects combine (see EliteEnemy):
///   multipliers multiply together, life steal and bonus coins add up, the strongest ward and the biggest
///   explosion win. Leave a value at its default (1 for multipliers, 0 / off for the rest) to not use it.
/// </summary>
[CreateAssetMenu(fileName = "EliteTrait", menuName = "Elites/Elite Trait")]
public class EliteTraitDefinition : ScriptableObject
{
    [Header("Display")]
    [Tooltip("Shown above the elite's head.")]
    public string displayName = "New Trait";
    [Tooltip("Colour of the name, and of the ground ring when this is the elite's first trait.")]
    public Color color = Color.white;

    [Header("Rolling")]
    [Tooltip("Higher = picked more often.")]
    [Min(0f)] public float weight = 1f;
    [Tooltip("Can only appear from this floor on (1 = first floor).")]
    [Min(1)] public int minFloor = 1;
    [Tooltip("Never rolled together with these traits.")]
    public List<EliteTraitDefinition> incompatibleWith = new List<EliteTraitDefinition>();

    [Header("Stats (multipliers, 1 = unchanged)")]
    [Tooltip("Damage dealt to the player's HP.")]
    [Min(0f)] public float damageMultiplier = 1f;
    [Tooltip("Stamina damage dealt when the player blocks (guard break).")]
    [Min(0f)] public float staminaDamageMultiplier = 1f;
    [Min(0.01f)] public float healthMultiplier = 1f;
    [Min(0.01f)] public float sizeMultiplier = 1f;
    [Tooltip("Overall speed: movement, animation, and attacks (shorter wind-up and cooldown). 1.5 = 50% faster at everything.")]
    [Min(0.01f)] public float speedMultiplier = 1f;
    [Tooltip("Damage the elite takes, always (0.5 = takes half damage).")]
    [Min(0f)] public float damageTakenMultiplier = 1f;

    [Header("Ward (damage reduction a kick can break)")]
    public bool hasWard;
    [Tooltip("Damage taken while the ward is up (0.35 = 35%).")]
    [Range(0f, 1f)] public float wardDamageTaken = 0.35f;
    [Tooltip("Seconds a kick keeps the ward broken.")]
    [Min(0f)] public float wardBreakDuration = 5f;

    [Header("Life Steal")]
    [Tooltip("Fraction of the damage dealt to the player that the elite heals (1 = 100%).")]
    [Min(0f)] public float lifeSteal = 0f;

    [Header("Explode On Death")]
    public bool explodesOnDeath;
    [Tooltip("Seconds between death and the blast (the ring flashes faster and faster).")]
    [Min(0f)] public float explosionFuse = 1.2f;
    [Min(0f)] public float explosionRadius = 2.8f;
    [Tooltip("Damage to the player (can't be blocked).")]
    [Min(0f)] public float explosionDamage = 25f;
    [Tooltip("Damage to other enemies caught in the blast.")]
    [Min(0f)] public float explosionEnemyDamage = 40f;

    [Header("Rewards")]
    [Tooltip("Extra coins as a multiple of the normal drop, on top of the global elite bonus (0.5 = +50%).")]
    [Min(0f)] public float bonusCoinMultiplier = 0f;
}
