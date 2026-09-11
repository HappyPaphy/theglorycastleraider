using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyDirector : MonoBehaviour
{
    [Header("Swarm Control Settings")]
    [Tooltip("Maximum number of enemies allowed to aggressively chase and attack the player at the same time.")]
    public int maxActiveAttackers = 2;

    [SerializeField] private List<EnemyEntity> registeredEnemies = new List<EnemyEntity>();
    public List<EnemyEntity> activeAttackers = new List<EnemyEntity>();

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

    public void RegisterEnemy(EnemyEntity enemy)
    {
        if (!registeredEnemies.Contains(enemy))
            registeredEnemies.Add(enemy);
    }

    public void UnregisterEnemy(EnemyEntity enemy)
    {
        registeredEnemies.Remove(enemy);
        activeAttackers.Remove(enemy);
    }

    /// <summary>
    /// Enemies call this to ask if they are allowed to rush/attack the player.
    /// </summary>
    public bool RequestAttackPermission(EnemyEntity enemy)
    {
        if (activeAttackers.Contains(enemy)) return true;

        if (activeAttackers.Count < maxActiveAttackers)
        {
            activeAttackers.Add(enemy);
            return true;
        }

        return false; // Slots are full! Enemy must wait or circle.
    }

    public void ReleaseAttackPermission(EnemyEntity enemy)
    {
        if (activeAttackers.Contains(enemy))
        {
            activeAttackers.Remove(enemy);
        }
    }
}
