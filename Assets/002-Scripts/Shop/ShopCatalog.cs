using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The pool a shop rolls its stock from. Create via Assets > Create > Shop > Shop Catalog.
/// </summary>
[CreateAssetMenu(fileName = "ShopCatalog", menuName = "Shop/Shop Catalog")]
public class ShopCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public ShopItemDefinition item;
        [Tooltip("Always in stock (e.g. potions).")]
        public bool alwaysStocked;
        [Tooltip("Chance weight when rolling the random part of the stock.")]
        [Min(0f)] public float weight = 1f;
    }

    [Tooltip("Total number of different items a shop shows.")]
    [Min(1)] public int itemsPerShop = 8;
    public List<Entry> entries = new List<Entry>();

    [Tooltip("Always stock the key for every locked treasure door on the current floor.")]
    public bool guaranteeFloorKeys = true;

    /// <summary>
    /// Picks the keys this floor's treasure doors need, then the always-stocked items,
    /// then fills the rest by weight (no duplicates).
    /// </summary>
    public List<ShopItemDefinition> RollStock(IEnumerable<TreasureKeyType> requiredKeys = null)
    {
        var result = new List<ShopItemDefinition>();
        var pool = new List<Entry>();

        // Guaranteed keys first, so they are never pushed out by the item limit
        if (guaranteeFloorKeys && requiredKeys != null)
        {
            foreach (TreasureKeyType key in requiredKeys)
            {
                Entry keyEntry = entries.Find(e => e != null && e.item != null && e.item.kind == ShopItemKind.Key && e.item.keyType == key);
                if (keyEntry == null)
                {
                    Debug.LogWarning($"[ShopCatalog] {name} has no {key} Key item to guarantee.", this);
                    continue;
                }
                if (!result.Contains(keyEntry.item)) { result.Add(keyEntry.item); }
            }
        }

        foreach (Entry e in entries)
        {
            if (e == null || e.item == null || result.Contains(e.item)) { continue; }
            if (PlayerUpgrades.Instance.IsSoldOutForRun(e.item)) { continue; }

            if (e.alwaysStocked) { result.Add(e.item); }
            else if (e.weight > 0f) { pool.Add(e); }
        }

        while (result.Count < itemsPerShop && pool.Count > 0)
        {
            float total = 0f;
            foreach (Entry e in pool) { total += e.weight; }

            float roll = UnityEngine.Random.value * total;
            int picked = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= pool[i].weight;
                if (roll <= 0f) { picked = i; break; }
            }

            result.Add(pool[picked].item);
            pool.RemoveAt(picked);
        }

        return result;
    }
}
