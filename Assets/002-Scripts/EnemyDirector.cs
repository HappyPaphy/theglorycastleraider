using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyDirector : MonoBehaviour
{
    [Header("Swarm Control Settings")]
    [Tooltip("Maximum number of enemies allowed to aggressively chase and attack the player at the same time.")]
    public int maxActiveAttackers = 2;

    [SerializeField] private List<EnemyThief> registeredEnemies = new List<EnemyThief>();
    public List<EnemyThief> activeAttackers = new List<EnemyThief>();

    public static EnemyDirector instance;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        //ResetDirector();
    }

    public void ResetDirector()
    {
        registeredEnemies.RemoveAll(item => item = null);
        activeAttackers.RemoveAll(item => item = null);

        activeAttackers.Clear();
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
