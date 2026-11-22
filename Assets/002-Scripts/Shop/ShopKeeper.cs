using System;
using System.Collections.Generic;
using UnityEngine;

public enum PurchaseResult
{
    Success,
    NotEnoughCoins,
    SoldOut,
    CannotCarry,
    Failed
}

/// <summary>
/// The shop counter in the Shop Room. Rolls its stock once from a ShopCatalog, and opens the
/// Shop UI when the player presses Interact nearby. Purchases are delivered straight to the player.
/// </summary>
public class ShopKeeper : MonoBehaviour
{
    [Serializable]
    public class StockSlot
    {
        public ShopItemDefinition item;
        public int remaining;
    }

    [Header("Stock")]
    [SerializeField] private ShopCatalog catalog;

    [Header("Interaction")]
    [SerializeField] private float interactRadius = 2.2f;
    [SerializeField] private LayerMask playerLayer = 1 << 6;
    [SerializeField] private GameObject uiButtonPrompt;
    [Tooltip("Where the interact radius is measured from (the counter). Defaults to this transform.")]
    [SerializeField] private Transform interactPoint;

    [Header("Feedback")]
    [SerializeField] private AudioClip buySound;
    [SerializeField] private AudioClip deniedSound;
    [SerializeField] private string shopName = "Wandering Merchant";

    private readonly List<StockSlot> stock = new List<StockSlot>();
    private bool stockRolled;

    public IReadOnlyList<StockSlot> Stock => stock;
    public string ShopName => shopName;

    private Vector3 InteractCenter => (interactPoint != null ? interactPoint.position : transform.position) + Vector3.up * 0.9f;

    private void Awake()
    {
        if (uiButtonPrompt != null) { uiButtonPrompt.SetActive(false); }
    }

    private void Update()
    {
        bool isPlayerNear = !ShopUI.IsOpen && IsGameplayActive() &&
                            PhysicsQuery.Any(InteractCenter, interactRadius, playerLayer, QueryTriggerInteraction.Collide) &&
                            InteractFacing.IsPlayerFacing(transform.position + Vector3.up * 0.9f); // looking at the counter

        if (uiButtonPrompt != null && uiButtonPrompt.activeSelf != isPlayerNear)
        {
            uiButtonPrompt.SetActive(isPlayerNear);
        }

        if (isPlayerNear && InputSchemeManager.instance != null && InputSchemeManager.instance.IsInteractPressed)
        {
            InputSchemeManager.instance.IsInteractPressed = false;
            OpenShop();
        }
    }

    public void OpenShop()
    {
        EnsureStock();
        ShopUI.Open(this);
    }

    private void EnsureStock()
    {
        if (stockRolled) { return; }
        stockRolled = true;

        if (catalog == null)
        {
            Debug.LogWarning($"[ShopKeeper] {name} has no ShopCatalog assigned.", this);
            return;
        }

        List<TreasureKeyType> neededKeys = FindLockedDoorKeys();

        foreach (ShopItemDefinition item in catalog.RollStock(neededKeys))
        {
            int remaining = item.stockPerShop;

            // One key per locked door of that type (e.g. two Gold treasure rooms -> 2 Gold Keys)
            if (item.kind == ShopItemKind.Key)
            {
                remaining = Mathf.Max(remaining, neededKeys.FindAll(k => k == item.keyType).Count);
            }

            stock.Add(new StockSlot { item = item, remaining = remaining });
        }
    }

    /// <summary>
    /// Key type of every locked (treasure) door on this floor, one entry per door. The stock is rolled the first time the
    /// shop opens, after the dungeon is generated, so every treasure door already exists.
    /// Includes inactive objects because DunGen culling switches off rooms the player can't see.
    /// </summary>
    private static List<TreasureKeyType> FindLockedDoorKeys()
    {
        var keys = new List<TreasureKeyType>();
        foreach (DoorInteractable door in FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (door.IsLocked) { keys.Add(door.RequiredKey); }
        }
        return keys;
    }

    public bool IsSlotAvailable(int index)
    {
        if (index < 0 || index >= stock.Count) { return false; }
        StockSlot slot = stock[index];
        return slot.item != null && slot.remaining > 0 && !PlayerUpgrades.Instance.IsSoldOutForRun(slot.item);
    }

    // =========================================================
    // BUYING
    // =========================================================

    public PurchaseResult TryBuy(int index)
    {
        if (index < 0 || index >= stock.Count) { return PurchaseResult.Failed; }

        StockSlot slot = stock[index];
        if (!IsSlotAvailable(index)) { return PurchaseResult.SoldOut; }

        CoinWallet wallet = PlayerController.instance != null ? PlayerController.instance.coinWallet : null;
        if (wallet == null) { return PurchaseResult.Failed; }

        if (wallet.Coins < slot.item.Price)
        {
            PlayClip(deniedSound);
            return PurchaseResult.NotEnoughCoins;
        }

        // Deliver first, only charge if the player could actually take it
        if (!Deliver(slot.item))
        {
            PlayClip(deniedSound);
            return PurchaseResult.CannotCarry;
        }

        wallet.TrySpend(slot.item.Price);
        slot.remaining--;
        PlayerUpgrades.Instance.RecordPurchase(slot.item);
        PlayClip(buySound);

        // Items/weapons already show a notification from the inventory; keys, upgrades etc. show it here
        if (slot.item.kind != ShopItemKind.Item && slot.item.kind != ShopItemKind.Weapon)
        {
            ItemPickupPopup.Show(slot.item.displayName, "Purchased", slot.item.Icon, "shop:" + slot.item.displayName);
        }
        return PurchaseResult.Success;
    }

    private bool Deliver(ShopItemDefinition item)
    {
        PlayerUpgrades upgrades = PlayerUpgrades.Instance;

        switch (item.kind)
        {
            case ShopItemKind.Item:
            case ShopItemKind.Weapon:
                return GiveItemPrefab(item.itemPrefab);

            case ShopItemKind.WeaponCarryUpgrade:
                upgrades.AddWeaponCarry(1);
                return true;

            case ShopItemKind.ReviveTotem:
                upgrades.AddReviveTotem();
                return true;

            case ShopItemKind.AttackSpeedUpgrade:
                upgrades.AddAttackSpeed(item.attackSpeedBonus);
                return true;

            case ShopItemKind.Key:
                upgrades.AddKey(item.keyType);
                return true;

            case ShopItemKind.PerkSlot:
                PerkSelectionManager perks = PerkSelectionManager.instance;
                if (perks == null || perks.Inventory == null || !perks.Inventory.UnlockNextSlot()) { return false; } // all unlocked
                perks.NotifyChanged();
                return true;
        }

        return false;
    }

    // Spawns the real item and runs its normal pickup logic, so stacking / auto-equip work like a floor pickup
    private bool GiveItemPrefab(Item prefab)
    {
        if (prefab == null || InventoryManager.instance == null) { return false; }

        Vector3 spawnPos = PlayerController.instance != null ? PlayerController.instance.transform.position : transform.position;
        Item instance = Instantiate(prefab, spawnPos, Quaternion.identity);

        if (instance.GiveToPlayer()) { return true; }

        Destroy(instance.gameObject);
        return false;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) { return; }
        Vector3 pos = PlayerController.instance != null ? PlayerController.instance.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, pos);
    }

    private static bool IsGameplayActive()
    {
        if (PauseGame.instance != null && PauseGame.instance.IsPaused) { return false; }
        if (PerkSelectionManager.instance != null && PerkSelectionManager.instance.isPanelActive) { return false; }
        if (TutorialManager.instance != null && TutorialManager.instance.isPanelActive) { return false; }
        if (EquipmentLoadOut.instance != null && EquipmentLoadOut.instance.isPanelActive) { return false; }
        if (PlayerController.instance != null && PlayerController.instance.CharacterHealthComponent.CurrentHP <= 0f) { return false; }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(InteractCenter, interactRadius);
    }
}
