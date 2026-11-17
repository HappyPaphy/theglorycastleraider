using UnityEngine;

public enum ShopItemKind
{
    Item,               // Any Item prefab (potions, rings, spells). Goes into the inventory like a pickup.
    Weapon,             // A Weapon prefab. Goes into the inventory and auto-equips if a hand slot is free.
    WeaponCarryUpgrade, // +1 copy of each weapon type you can carry
    ReviveTotem,        // Cheat death once
    AttackSpeedUpgrade, // Shorter weapon attack cooldown
    Key,                // Treasure room key (Bronze / Silver / Gold)
    PerkSlot            // Unlocks one more perk card slot
}

public enum TreasureKeyType
{
    Bronze,
    Silver,
    Gold
}

/// <summary>
/// One thing a shop can sell. Create via Assets > Create > Shop > Shop Item.
/// </summary>
[CreateAssetMenu(fileName = "ShopItem", menuName = "Shop/Shop Item")]
public class ShopItemDefinition : ScriptableObject
{
    [Header("Display")]
    public string displayName = "New Item";
    [TextArea(2, 4)] public string description;
    [Tooltip("Optional. Item / Weapon entries fall back to the prefab's icon.")]
    public Sprite icon;

    [Header("Price")]
    [Min(0)] public int price = 50;

    [Header("What it gives")]
    public ShopItemKind kind = ShopItemKind.Item;
    [Tooltip("Item or Weapon prefab (for kind Item / Weapon).")]
    public Item itemPrefab;
    [Tooltip("For kind Key.")]
    public TreasureKeyType keyType = TreasureKeyType.Bronze;
    [Tooltip("For kind AttackSpeedUpgrade. 0.1 = attacks 10% faster.")]
    public float attackSpeedBonus = 0.1f;

    [Header("Limits")]
    [Tooltip("How many one shop has in stock.")]
    [Min(1)] public int stockPerShop = 1;
    [Tooltip("How many times this can be bought in a whole run. 0 = unlimited.")]
    [Min(0)] public int maxPurchasesPerRun = 0;

    public Sprite Icon
    {
        get
        {
            if (icon != null) { return icon; }
            if (itemPrefab != null)
            {
                return itemPrefab.spr_Icon_Frame != null ? itemPrefab.spr_Icon_Frame : itemPrefab.spr_Icon_NoFrame;
            }
            return null;
        }
    }
}
