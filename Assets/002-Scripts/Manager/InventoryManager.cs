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
        bool added = TryAddGeneralItemInternal(item);
        if (added) { ItemPickupPopup.ShowItem(item); } // top-right "item obtained" notification
        return added;
    }

    private bool TryAddGeneralItemInternal(Item item)
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
    /// <summary>How many of one weapon kind can be carried: 2, plus the shop's Weapon Carry upgrade.</summary>
    public static int WeaponCarryLimit => 2 + (PlayerUpgrades.Instance != null ? PlayerUpgrades.Instance.BonusWeaponCarry : 0);

    /// <summary>How many weapons of the same kind as this one the player carries.</summary>
    public int CountSameKind(Weapon weapon)
    {
        if (weapon == null) { return 0; }
        switch (weapon.weaponCategory)
        {
            case WeaponCategory.Melee:
                return weapons.Count(w => w.weaponCategory == WeaponCategory.Melee && w.meleeType == weapon.meleeType);
            case WeaponCategory.Shield:
                return weapons.Count(w => w.weaponCategory == WeaponCategory.Shield && w.shieldType == weapon.shieldType);
            case WeaponCategory.Bow:
                return weapons.Count(w => w.weaponCategory == WeaponCategory.Bow && w.bowType == weapon.bowType);
            case WeaponCategory.PyromancyFlame:
                return weapons.Count(w => w.weaponCategory == WeaponCategory.PyromancyFlame && w.pyromancyType == weapon.pyromancyType);
            case WeaponCategory.SorceryCatalyst:
                return weapons.Count(w => w.weaponCategory == WeaponCategory.SorceryCatalyst && w.sorceryType == weapon.sorceryType);
            default:
                return 0;
        }
    }

    /// <summary>True if one more of this weapon fits in the inventory (counts the Weapon Carry upgrade).</summary>
    public bool CanCarryWeapon(Weapon weapon) => weapon != null && CountSameKind(weapon) < WeaponCarryLimit;

    /// <summary>Chests and enemy drops never give a weapon kind the player already has this many of
    /// (fixed; the Weapon Carry upgrade doesn't raise it).</summary>
    public const int LootWeaponLimit = 2;

    /// <summary>True if chests / drops may still give this weapon.</summary>
    public bool CanLootWeapon(Weapon weapon) => weapon != null && CountSameKind(weapon) < LootWeaponLimit;

    public bool TryAddWeapon(Weapon newWeapon)
    {
        // Base carry limit is 2 of each weapon; the shop's Weapon Carry upgrade raises it
        if (CanCarryWeapon(newWeapon))
        {
            weapons.Add(newWeapon);
            StoreItemSafely(newWeapon);
            ItemPickupPopup.ShowItem(newWeapon);
            return true;
        }

        return false;
    }

    public void RemoveConsumableIfEmpty(ItemType itemType)
    {
        if (consumables.ContainsKey(itemType) && consumables[itemType] <= 0)
        {
            // 1. Remove from dictionary
            consumables.Remove(itemType);

            // 2. Remove the visual template from the list
            Item targetTemplate = consumablesList.FirstOrDefault(i => i.itemType == itemType);
            if (targetTemplate != null)
            {
                consumablesList.Remove(targetTemplate);
            }

            // 3. Clear from PlayerWeaponManager equipped items slots
            if (PlayerWeaponManager.instance != null)
            {
                for (int i = 0; i < PlayerWeaponManager.instance.equippedItems.Length; i++)
                {
                    if (PlayerWeaponManager.instance.equippedItems[i] != null &&
                        PlayerWeaponManager.instance.equippedItems[i].itemType == itemType)
                    {
                        PlayerWeaponManager.instance.equippedItems[i] = null;
                    }
                }
            }
        }
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
