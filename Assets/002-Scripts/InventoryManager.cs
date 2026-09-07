using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;

    [Header("Equipment References (Max 2 or 1)")]
    public List<Weapon> weapons = new List<Weapon>();
    public List<Item> rings = new List<Item>();
    public List<Item> pyromancies = new List<Item>();
    public List<Item> magics = new List<Item>();

    [Header("Consumables (Max 99)")]
    public List<Item> consumablesList = new List<Item>(); // Stores unique item templates for UI display
    public Dictionary<ItemType, int> consumables = new Dictionary<ItemType, int>(); // Tracks 1-99 quantities

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    /// <summary>
    /// Routes standard items (Consumables, Rings, Spells) to their correct inventory lists.
    /// </summary>
    public bool TryAddGeneralItem(Item item)
    {
        switch (item.itemCategory)
        {
            case ItemCategory.Consumable:
                return TryAddConsumable(item);
            case ItemCategory.Ring:
                return TryAddUniqueEquipment(item, rings);
            case ItemCategory.Pyromancy:
                return TryAddUniqueEquipment(item, pyromancies);
            case ItemCategory.Magic:
                return TryAddUniqueEquipment(item, magics);
            default:
                return false;
        }
    }

    /// <summary>
    /// Ensures only 1 of each specific Ring or Spell can be collected.
    /// </summary>
    private bool TryAddUniqueEquipment(Item newItem, List<Item> inventoryList)
    {
        bool alreadyHasItem = inventoryList.Any(i => i.itemType == newItem.itemType);
        if (!alreadyHasItem)
        {
            inventoryList.Add(newItem);
            StoreItemSafely(newItem);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Caps consumable stacking at 99. Destroys the physical drop upon collection.
    /// </summary>
    private bool TryAddConsumable(Item consumable)
    {
        if (!consumables.ContainsKey(consumable.itemType))
        {
            // First time collecting this consumable type: Store its reference and set count to 1
            consumables[consumable.itemType] = 1;
            consumablesList.Add(consumable);
            StoreItemSafely(consumable);
            return true;
        }
        else
        {
            if (consumables[consumable.itemType] < 99)
            {
                // Already have it: increment stack count and destroy the extra world drop
                consumables[consumable.itemType]++;
                Destroy(consumable.gameObject);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Capped at 2 specific weapons per sub-type (e.g. Max 2 Short Swords)
    /// </summary>
    public bool TryAddWeapon(Weapon newWeapon)
    {
        int currentCount = 0;

        switch (newWeapon.weaponCategory)
        {
            case WeaponCategory.Melee:
                currentCount = weapons.Count(w => w.weaponCategory == WeaponCategory.Melee && w.meleeType == newWeapon.meleeType);
                break;
            case WeaponCategory.Shield:
                currentCount = weapons.Count(w => w.weaponCategory == WeaponCategory.Shield && w.shieldType == newWeapon.shieldType);
                break;
            case WeaponCategory.Bow:
                currentCount = weapons.Count(w => w.weaponCategory == WeaponCategory.Bow && w.bowType == newWeapon.bowType);
                break;
                // PyromancyFlame and SorceryCatalyst handled here if they are categorized as Weapons
        }

        if (currentCount < 2)
        {
            weapons.Add(newWeapon);
            StoreItemSafely(newWeapon);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parents the object to the InventoryManager and disables it so it is saved in memory for the Loadout UI.
    /// </summary>
    private void StoreItemSafely(Item item)
    {
        item.transform.SetParent(this.transform);
        item.gameObject.SetActive(false);
    }
}
