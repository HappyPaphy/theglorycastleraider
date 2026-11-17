using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global challenge tuning. Lives at Resources/Challenges/ChallengeSettings (built by Tools > Challenges > Build Challenges).
/// </summary>
[CreateAssetMenu(fileName = "ChallengeSettings", menuName = "Challenges/Challenge Settings")]
public class ChallengeSettings : ScriptableObject
{
    public const string ResourcePath = "Challenges/ChallengeSettings";

    [Tooltip("Challenges a Challenge Room can roll. Add your own (Create > Challenges > Challenge).")]
    public List<ChallengeDefinition> challengePool = new List<ChallengeDefinition>();
    [Tooltip("Enemies used by challenges that don't list their own.")]
    public List<ChallengeDefinition.EnemyOption> defaultEnemies = new List<ChallengeDefinition.EnemyOption>();

    [Header("Reward chests")]
    public GameObject bronzeChest;
    public GameObject silverChest;
    public GameObject goldChest;

    [Header("Interaction")]
    public float confirmWindow = 3f;
    public float interactRadius = 2f;

    [Header("Look")]
    public float iconHeight = 1.4f;
    public float labelHeight = 1.68f;

    public GameObject GetChest(TreasureKeyType tier)
    {
        switch (tier)
        {
            case TreasureKeyType.Gold: return goldChest;
            case TreasureKeyType.Silver: return silverChest;
            default: return bronzeChest;
        }
    }

    private static ChallengeSettings cached;
    public static ChallengeSettings Load()
    {
        if (cached == null) { cached = Resources.Load<ChallengeSettings>(ResourcePath); }
        return cached;
    }
}
