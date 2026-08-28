using System.Collections.Generic;
using UnityEngine;

public class EnemyDirector : MonoBehaviour
{
    [Header("Swarm Control Settings")]
    [Tooltip("Maximum number of enemies allowed to aggressively chase and attack the player at the same time.")]
    public int maxActiveAttackers = 2;

    private List<EnemyThief> registeredEnemies = new List<EnemyThief>();
    [HideInInspector] public List<EnemyThief> activeAttackers = new List<EnemyThief>();

    public static EnemyDirector Instance;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterEnemy(EnemyThief enemy)
    {
        if (!registeredEnemies.Contains(enemy))
            registeredEnemies.Add(enemy);
    }

    public void UnregisterEnemy(EnemyThief enemy)
    {
        registeredEnemies.Remove(enemy);
        activeAttackers.Remove(enemy);
    }

    /// <summary>
    /// Enemies call this to ask if they are allowed to rush/attack the player.
    /// </summary>
    public bool RequestAttackPermission(EnemyThief enemy)
    {
        if (activeAttackers.Contains(enemy)) return true;

        if (activeAttackers.Count < maxActiveAttackers)
        {
            activeAttackers.Add(enemy);
            return true;
        }

        return false; // Slots are full! Enemy must wait or circle.
    }

    public void ReleaseAttackPermission(EnemyThief enemy)
    {
        if (activeAttackers.Contains(enemy))
        {
            activeAttackers.Remove(enemy);
        }
    }
}
