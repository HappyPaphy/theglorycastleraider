using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Run-long things the player bought: revive totems, keys, attack speed and weapon carry upgrades.
/// Lives in the scene like the CoinWallet, so it resets when the run restarts.
/// Created on first use, no scene setup needed.
/// </summary>
public class PlayerUpgrades : MonoBehaviour
{
    private static PlayerUpgrades instance;
    public static PlayerUpgrades Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<PlayerUpgrades>();
                if (instance == null) { instance = new GameObject("PlayerUpgrades").AddComponent<PlayerUpgrades>(); }
            }
            return instance;
        }
    }

    [Header("Revive Totem")]
    [Tooltip("Fraction of max HP you get back up with.")]
    [SerializeField] private float reviveHealthFraction = 0.5f;
    [SerializeField] private float reviveInvulnerability = 2f;

    [Header("Limits")]
    [SerializeField] private float maxAttackSpeedMultiplier = 1.6f;

    private int reviveTotems;
    private readonly int[] keys = new int[3];
    private float attackSpeedMultiplier = 1f;
    private int bonusWeaponCarry;
    private float damageMultiplier = 1f;
    private readonly Dictionary<ShopItemDefinition, int> purchases = new Dictionary<ShopItemDefinition, int>();

    public int ReviveTotems => reviveTotems;
    public float AttackSpeedMultiplier => attackSpeedMultiplier;
    public int BonusWeaponCarry => bonusWeaponCarry;
    public float ReviveInvulnerability => reviveInvulnerability;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
    }

    // ---------- Weapons ----------

    /// <summary>Weapon attack cooldown after the attack speed upgrade.</summary>
    public static float GetAttackCooldown(Weapon weapon)
    {
        if (weapon == null) { return 0f; }
        return weapon.attackCooldown / (instance != null ? instance.attackSpeedMultiplier : 1f) / PlayerRings.AttackSpeedMultiplier;
    }

    public void AddAttackSpeed(float bonus)
    {
        attackSpeedMultiplier = Mathf.Min(maxAttackSpeedMultiplier, attackSpeedMultiplier + bonus);
    }

    public void AddWeaponCarry(int amount = 1) => bonusWeaponCarry += amount;

    // ---------- Damage (shrine boons) ----------

    /// <summary>Permanent damage bonus for the run (0.2 = +20%). Stacks multiplicatively.</summary>
    public void AddDamageBonus(float bonus) => damageMultiplier *= 1f + Mathf.Max(0f, bonus);

    /// <summary>Damage multiplier from run upgrades (1 if none, safe to read without creating the object).</summary>
    public static float BonusDamageMultiplier => instance != null ? instance.damageMultiplier : 1f;

    // ---------- Revive ----------

    public void AddReviveTotem() => reviveTotems++;

    /// <summary>
    /// Called with the final damage of a hit. If it would kill and a totem is held, the totem breaks
    /// and the player gets back up. Returns true when the hit was cancelled.
    /// </summary>
    public bool TryReviveFromLethalHit(float incomingDamage)
    {
        PlayerController player = PlayerController.instance;
        if (reviveTotems <= 0 || player == null) { return false; }

        float hp = player.CharacterHealthComponent.CurrentHP;
        if (hp <= 0f || incomingDamage < hp) { return false; }

        reviveTotems--;
        float maxHP = player.CharacterHealthComponent.MaxHP;
        player.CharacterHealthComponent.SetHP(Mathf.Max(1f, maxHP * reviveHealthFraction));

        if (player.cameraBob != null) { player.cameraBob.TriggerShake(0.35f, 0.15f); }
        if (RumbleManager.instance != null) { RumbleManager.instance.RumblePulse(1f, 3f, 0.3f); }
        Debug.Log($"[PlayerUpgrades] Revive Totem used. {reviveTotems} left.");
        return true;
    }

    // ---------- Keys ----------

    public int GetKeyCount(TreasureKeyType type) => keys[(int)type];
    public void AddKey(TreasureKeyType type) => keys[(int)type]++;

    /// <summary>For treasure doors / chests: spends one key of that type if the player has it.</summary>
    public bool TryUseKey(TreasureKeyType type)
    {
        if (keys[(int)type] <= 0) { return false; }
        keys[(int)type]--;
        return true;
    }

    // ---------- Shop bookkeeping ----------

    public int GetPurchaseCount(ShopItemDefinition item)
    {
        return item != null && purchases.TryGetValue(item, out int count) ? count : 0;
    }

    public void RecordPurchase(ShopItemDefinition item)
    {
        if (item == null) { return; }
        purchases[item] = GetPurchaseCount(item) + 1;
    }

    public bool IsSoldOutForRun(ShopItemDefinition item)
    {
        return item != null && item.maxPurchasesPerRun > 0 && GetPurchaseCount(item) >= item.maxPurchasesPerRun;
    }

    /// <summary>Short "owned" line for the shop details panel.</summary>
    public string GetOwnedText(ShopItemDefinition item)
    {
        if (item == null) { return string.Empty; }

        switch (item.kind)
        {
            case ShopItemKind.ReviveTotem: return $"Owned: {reviveTotems}";
            case ShopItemKind.Key: return $"Owned: {GetKeyCount(item.keyType)}";
            case ShopItemKind.AttackSpeedUpgrade: return $"Attack speed: {Mathf.RoundToInt((attackSpeedMultiplier - 1f) * 100f)}% faster";
            case ShopItemKind.WeaponCarryUpgrade: return $"Carry limit: {2 + bonusWeaponCarry} of each weapon";
            case ShopItemKind.PerkSlot:
                PerkSelectionManager perks = PerkSelectionManager.instance;
                return perks != null && perks.Inventory != null
                    ? $"Perk slots: {perks.Inventory.UnlockedSlotCount} / {PerkCardInventory.MaxSlots}"
                    : string.Empty;
            case ShopItemKind.Item:
                if (item.itemPrefab != null && InventoryManager.instance != null &&
                    InventoryManager.instance.consumables.TryGetValue(item.itemPrefab.itemType, out int count))
                {
                    return $"Owned: {count}";
                }
                return string.Empty;
            default:
                return string.Empty;
        }
    }
}
