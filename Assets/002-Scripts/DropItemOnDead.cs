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

    [System.Serializable]
    public class EnergyOrbDrop
    {
        [Tooltip("Rarity of the orb to drop (EnergyOrb_Common / Rare / Unique)")]
        public EnergyOrbDefinition orb;

        [Range(0f, 100f)]
        [Tooltip("Percentage chance this rarity drops (0 to 100). At most one orb drops per box.")]
        public float dropChance;
    }

    [Header("Energy Orb Drops")]
    [Tooltip("The EnergyOrb prefab (Assets/003-Prefabs/EnergyOrb.prefab). Leave empty for no orb drops.")]
    [SerializeField] private EnergyOrbPickup energyOrbPrefab;
    [Tooltip("One roll picks at most one orb. Chances add up, e.g. Common 15 + Rare 7 + Unique 2 = 24% to get any orb.")]
    [SerializeField] private List<EnergyOrbDrop> energyOrbDrops = new List<EnergyOrbDrop>();
    [SerializeField] private Vector3 energyOrbSpawnOffset = new Vector3(0f, 0.75f, 0f);

    [Header("Drop Settings")]
    [Tooltip("Maximum number of items this enemy can drop at once")]
    [SerializeField] private int maxDropsAllowed = 1;
    [SerializeField] private bool isNeedAtLeastOneDrop = false;
    [SerializeField] private List<LootDrop> potentialDrops = new List<LootDrop>();
    private int currentDropCount = 0;

    private List<LootDrop> validDrops = new List<LootDrop>();

    public void DropItem()
    {
        DropEnergyOrb();

        if (InventoryManager.instance == null) return;

        validDrops.Clear();

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
    /// Rolls once against the energy orb table and spawns at most one orb.
    /// </summary>
    private void DropEnergyOrb()
    {
        if (energyOrbPrefab == null || energyOrbDrops == null || energyOrbDrops.Count == 0) return;

        float roll = Random.Range(0f, 100f);
        float cumulative = 0f;

        foreach (EnergyOrbDrop drop in energyOrbDrops)
        {
            if (drop.orb == null || drop.dropChance <= 0f) continue;

            cumulative += drop.dropChance;
            if (roll < cumulative)
            {
                EnergyOrbPickup orb = Instantiate(energyOrbPrefab, transform.position + energyOrbSpawnOffset, Quaternion.identity);
                orb.Initialize(drop.orb);
                return;
            }
        }
    }

    /// <summary>
    /// Checks the InventoryManager to see if the player is at the maximum limit for this specific item.
    /// </summary>
    private bool IsInventoryFullFor(Item itemPrefab)
    {
        // Prefabs without an Item component (e.g. effects) have no inventory limit
        if (itemPrefab == null) return false;

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