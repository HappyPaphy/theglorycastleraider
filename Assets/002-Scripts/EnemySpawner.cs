using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemySpawnData
{
    public GameObject enemyPrefab;
    [Range(0f, 100f)] public float spawnChance;
    public int levelIndexRequirement = 0;
}

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] List<EnemySpawnData> enemySpawnData;
    [SerializeField] private bool isGuaranteedSpawn = false;

    private List<EnemySpawnData> validEnemies = new List<EnemySpawnData>();
    private bool isSpawned = false;

    void Start()
    {
        SpawnEnemy();
    }

    void SpawnEnemy()
    {
        if (RoguelikeManager.instance == null || enemySpawnData == null || enemySpawnData.Count == 0) return;

        int currentDungeonIndex = RoguelikeManager.instance.dungeonIndex;

        // 1. Filter valid enemies based on the level index requirement
        
        foreach (EnemySpawnData data in enemySpawnData)
        {
            if (data.enemyPrefab != null && currentDungeonIndex >= data.levelIndexRequirement)
            {
                validEnemies.Add(data);
            }
        }

        if (validEnemies.Count == 0) return;

        // 2. Roll for spawn chances
        RandomEnemy();
    }

    private void RandomEnemy()
    {
        foreach (EnemySpawnData data in validEnemies)
        {
            float roll = Random.Range(0f, 100f);
            if (roll <= data.spawnChance)
            {
                GameObject enemy = Instantiate(data.enemyPrefab, transform.position, transform.rotation);

                if (RoguelikeManager.instance != null)
                {
                    RoguelikeManager.instance.obj_Enemies.Add(enemy);
                }

                isSpawned = true;
                break; // Spawn only one enemy per spawner node
            }
        }

        // 3. Fallback: If nothing spawned and guaranteed spawn is checked, pick a random valid enemy
        if (!isSpawned && isGuaranteedSpawn)
        {
            RandomEnemy();
        }
    }
}
