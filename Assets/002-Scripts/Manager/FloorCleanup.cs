using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DunGen only deletes what sits inside its room tiles when it builds the next floor. Anything spawned
/// loose in the scene (dropped loot, energy orbs, coins, challenge/shop chests, chest pieces, arrows...)
/// would stay floating where the old floor was. RoguelikeManager calls Clear() before each regeneration.
/// Items the player has collected (they stay in the scene, owned by the inventory) are kept.
/// </summary>
public static class FloorCleanup
{
    public static void Clear()
    {
        Transform player = PlayerController.instance != null ? PlayerController.instance.transform : null;
        Transform pool = ObjectPoolingManager.instance != null ? ObjectPoolingManager.instance.transform : null;
        var toDestroy = new HashSet<GameObject>();

        bool Keep(Component c)
        {
            if (c == null) { return true; }
            Transform t = c.transform;
            if (player != null && t.IsChildOf(player)) { return true; }   // held / equipped things
            if (pool != null && t.IsChildOf(pool)) { return true; }       // idle pooled objects
            if (t.gameObject.scene.name == "DontDestroyOnLoad") { return true; }
            return false;
        }

        // Fracture pieces go back to the pool (destroying them with the old tiles would shrink the pool)
        if (ObjectPoolingManager.instance != null) { ObjectPoolingManager.instance.ReturnAllFragments(); }

        foreach (Item item in Object.FindObjectsByType<Item>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!item.IsCollected && !Keep(item)) { toDestroy.Add(item.gameObject); }
        }

        Collect<EnergyOrbPickup>(toDestroy, Keep);
        Collect<CoinPickup>(toDestroy, Keep);
        Collect<TreasureChest>(toDestroy, Keep);
        Collect<Fracture>(toDestroy, Keep);          // breakable chests / boxes spawned outside the tiles
        Collect<ArrowProjectile>(toDestroy, Keep);
        Collect<SpellProjectile>(toDestroy, Keep);

        // Empty "<name>_Fragments" holders left at the scene root by broken objects
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name.EndsWith("_Fragments")) { toDestroy.Add(root); }
        }

        foreach (GameObject go in toDestroy)
        {
            if (go != null) { Object.Destroy(go); }
        }
    }

    private static void Collect<T>(HashSet<GameObject> into, System.Func<Component, bool> keep) where T : Component
    {
        foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!keep(c)) { into.Add(c.gameObject); }
        }
    }
}
