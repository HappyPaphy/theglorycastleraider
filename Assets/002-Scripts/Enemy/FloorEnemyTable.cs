using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which enemies spawn on which floors, overriding the room's own spawner list (so a biome can have its own
/// enemies without editing every room). Floor index: 0 = Floor 1-1, 3 = 2-1, 6 = 3-1 (Castle).
/// Lives at Resources/Enemies/FloorEnemyTables (Tools > Enemies > Set Up Castle Enemies).
/// </summary>
[CreateAssetMenu(fileName = "FloorEnemyTables", menuName = "Enemies/Floor Enemy Tables")]
public class FloorEnemyTable : ScriptableObject
{
    public const string ResourcePath = "Enemies/FloorEnemyTables";

    [System.Serializable]
    public class Table
    {
        public string name = "Castle";
        [Tooltip("First floor index this table is used on (6 = Floor 3-1).")]
        public int fromFloor = 6;
        [Tooltip("Last floor index this table is used on (8 = Floor 3-3).")]
        public int toFloor = 8;
        [Tooltip("Rolled in order, like a spawner: each enemy gets its spawn chance, the first success spawns.")]
        public List<EnemySpawnData> enemies = new List<EnemySpawnData>();
    }

    public List<Table> tables = new List<Table>();

    private static FloorEnemyTable cached;
    private static bool loaded;

    /// <summary>The enemy list for this floor, or null to use the room's own spawner list.</summary>
    public static List<EnemySpawnData> ForFloor(int floorIndex)
    {
        if (!loaded) { cached = Resources.Load<FloorEnemyTable>(ResourcePath); loaded = true; }
        if (cached == null) { return null; }
        foreach (Table t in cached.tables)
        {
            if (t != null && floorIndex >= t.fromFloor && floorIndex <= t.toFloor && t.enemies.Count > 0) { return t.enemies; }
        }
        return null;
    }

    /// <summary>
    /// Every enemy the normal spawners can produce on this floor: the floor's table if it has one, otherwise
    /// all the room spawner lists of the current dungeon. Level requirements are applied the same way.
    /// Used so challenge rooms only bring in this floor's enemies.
    /// </summary>
    public static List<EnemySpawnData> Roster(int floorIndex)
    {
        var result = new List<EnemySpawnData>();
        var seen = new HashSet<GameObject>();

        void AddFrom(List<EnemySpawnData> list)
        {
            if (list == null) { return; }
            foreach (EnemySpawnData d in list)
            {
                if (d == null || d.enemyPrefab == null || floorIndex < d.levelIndexRequirement) { continue; }
                if (seen.Add(d.enemyPrefab)) { result.Add(d); }
            }
        }

        List<EnemySpawnData> table = ForFloor(floorIndex);
        if (table != null) { AddFrom(table); return result; }

        foreach (EnemySpawner spawner in Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (spawner.gameObject.scene.IsValid()) { AddFrom(spawner.SpawnData); }
        }
        return result;
    }
}
