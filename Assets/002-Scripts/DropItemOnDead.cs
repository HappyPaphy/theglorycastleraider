using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DropItemOnDead : MonoBehaviour
{
    [System.Serializable]
    public class LootDrop
    {
        [Tooltip("The Item prefab to drop. (Must have the Item or Weapon component attached)")]
        public GameObject itemPrefab;

        [Range(0f, 100f)]
        [Tooltip("Percentage chance this specific item will drop (0 to 100)")]
        public float dropChance;
    }

    

    [Header("Drop Settings")]
    [Tooltip("Maximum number of items this enemy can drop at once")]
    [SerializeField] private int maxDropsAllowed = 1;
    [SerializeField] private bool isNeedAtLeastOneDrop = false;
    [SerializeField] private List<LootDrop> potentialDrops = new List<LootDrop>();
    private int currentDropCount = 0;

    private List<LootDrop> validDrops = new List<LootDrop>();

    public void DropItem()
    {
        if (InventoryManager.instance == null) return;

        foreach (LootDrop loot in potentialDrops)
        {
            if (loot.itemPrefab != null && !IsInventoryFullFor(loot.itemPrefab.GetComponent<Item>()))
            {
                validDrops.Add(loot);
            }
        }

        if (validDrops.Count == 0) return;

        RandomItem();
    }

    private void RandomItem()
    {
        foreach (LootDrop loot in validDrops)
        {
            if (currentDropCount >= maxDropsAllowed) break;

            float roll = Random.Range(0f, 100f);
            if (roll <= loot.dropChance)
            {
                Instantiate(loot.itemPrefab, transform.position, transform.rotation);
                currentDropCount++;
            }
        }

        if (currentDropCount == 0 && isNeedAtLeastOneDrop)
        {
            // Pick a completely random item from the remaining valid options
            RandomItem();
        }
    }

    /// <summary>
    /// Checks the InventoryManager to see if the player is at the maximum limit for this specific item.
    /// </summary>
    private bool IsInventoryFullFor(Item itemPrefab)
    {
        switch (itemPrefab.itemCategory)
        {
            case ItemCategory.Consumable:
                // Capped at 99
                if (InventoryManager.instance.consumables.TryGetValue(itemPrefab.itemType, out int count))
                {
                    return count >= 99;
                }
                return false;

            case ItemCategory.Ring:
                // Capped at 1
                return InventoryManager.instance.rings.Any(i => i.itemType == itemPrefab.itemType);

            case ItemCategory.Pyromancy:
                // Capped at 1
                return InventoryManager.instance.pyromancies.Any(i => i.itemType == itemPrefab.itemType);

            case ItemCategory.Magic:
                // Capped at 1
                return InventoryManager.instance.magics.Any(i => i.itemType == itemPrefab.itemType);

            case ItemCategory.Weapon:
                // Capped at 2 per weapon sub-type
                if (itemPrefab is Weapon weaponPrefab)
                {
                    int currentCount = 0;
                    switch (weaponPrefab.weaponCategory)
                    {
                        case WeaponCategory.Melee:
                            currentCount = InventoryManager.instance.weapons.Count(w => w.weaponCategory == WeaponCategory.Melee && w.meleeType == weaponPrefab.meleeType);
                            break;
                        case WeaponCategory.Shield:
                            currentCount = InventoryManager.instance.weapons.Count(w => w.weaponCategory == WeaponCategory.Shield && w.shieldType == weaponPrefab.shieldType);
                            break;
                        case WeaponCategory.Bow:
                            currentCount = InventoryManager.instance.weapons.Count(w => w.weaponCategory == WeaponCategory.Bow && w.bowType == weaponPrefab.bowType);
                            break;
                        case WeaponCategory.PyromancyFlame:
                            currentCount = InventoryManager.instance.weapons.Count(w => w.weaponCategory == WeaponCategory.PyromancyFlame && w.pyromancyType == weaponPrefab.pyromancyType);
                            break;
                        case WeaponCategory.SorceryCatalyst:
                            currentCount = InventoryManager.instance.weapons.Count(w => w.weaponCategory == WeaponCategory.SorceryCatalyst && w.sorceryType == weaponPrefab.sorceryType);
                            break;
                    }
                    return currentCount >= 2;
                }
                return false;

            default:
                return false;
        }
    }
}