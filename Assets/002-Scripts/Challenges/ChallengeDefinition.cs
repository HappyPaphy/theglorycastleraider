using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// One kind of challenge: waves of enemies, optional modifiers and the reward.
/// Create via Assets > Create > Challenges > Challenge and add it to the pool on ChallengeSettings.
/// The text on the challenge altar is written from these values.
/// </summary>
[CreateAssetMenu(fileName = "Challenge", menuName = "Challenges/Challenge")]
public class ChallengeDefinition : ScriptableObject
{
    [Serializable]
    public class Wave
    {
        [Min(1)] public int enemyCount = 3;
        [Tooltip("Chance for each enemy in this wave to be an elite.")]
        [Range(0f, 1f)] public float eliteChance = 0f;
        [Tooltip("This many enemies of the wave are always elites.")]
        [Min(0)] public int guaranteedElites = 0;
    }

    [Serializable]
    public class EnemyOption
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Tooltip("Only spawns from this floor on (1 = first floor).")]
        [Min(1)] public int minFloor = 1;
    }

    [Header("Display")]
    public string displayName = "New Challenge";
    [Tooltip("Optional flavour line.")]
    public string flavour;
    public Color color = new Color(0.95f, 0.75f, 0.3f);
    public Sprite icon;

    [Header("Rolling")]
    [Min(0f)] public float weight = 1f;
    [Min(1)] public int minFloor = 1;

    [Header("Waves")]
    public List<Wave> waves = new List<Wave>();
    [Tooltip("Seconds between clearing a wave and the next one arriving.")]
    [Min(0f)] public float delayBetweenWaves = 2.5f;
    [Tooltip("Seconds between each enemy appearing within a wave.")]
    [Min(0f)] public float spawnInterval = 0.35f;
    [Tooltip("Enemies this challenge uses. Empty = the default pool on ChallengeSettings.")]
    public List<EnemyOption> enemies = new List<EnemyOption>();

    [Header("Modifiers (while the challenge runs)")]
    [Tooltip("Every enemy is an elite.")]
    public bool allElites;
    [Tooltip("Damage the player takes (1.3 = 30% more).")]
    [Min(0f)] public float damageTakenMultiplier = 1f;
    [Tooltip("Finish within this many seconds for the time bonus (0 = no timer).")]
    [Min(0f)] public float timeLimit = 0f;
    [Tooltip("Coins for beating the time limit.")]
    [Min(0)] public int timeBonusCoins = 0;

    [Header("Reward")]
    [Tooltip("Treasure chest that appears when the challenge is cleared.")]
    public TreasureKeyType rewardChest = TreasureKeyType.Silver;
    [Tooltip("Extra coins on clearing.")]
    [Min(0)] public int bonusCoins = 0;

    public int TotalEnemies
    {
        get { int n = 0; foreach (Wave w in waves) { if (w != null) n += w.enemyCount; } return n; }
    }

    /// <summary>Text shown on the altar, built from the values above.</summary>
    public string BuildDescription()
    {
        var sb = new StringBuilder();
        sb.Append($"{waves.Count} wave{(waves.Count == 1 ? "" : "s")}, {TotalEnemies} enemies");

        var mods = new List<string>();
        if (allElites) { mods.Add("All elites"); }
        if (damageTakenMultiplier > 1.001f) { mods.Add($"Take +{Mathf.RoundToInt((damageTakenMultiplier - 1f) * 100f)}% damage"); }
        if (timeLimit > 0f) { mods.Add($"Beat {Mathf.RoundToInt(timeLimit)}s for +{timeBonusCoins} coins"); }
        if (mods.Count > 0) { sb.Append("\n<color=#E36B5B>").Append(string.Join(", ", mods)).Append("</color>"); }

        sb.Append($"\n<color=#9BE37B>Reward: {rewardChest} chest");
        if (bonusCoins > 0) { sb.Append($" + {bonusCoins} coins"); }
        sb.Append("</color>");
        return sb.ToString();
    }
}
