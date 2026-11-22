using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Chest inside a treasure room. Press Interact to open: the lid swings up and the tier's rewards pop out
/// (coins straight into the wallet, an Energy Orb, and items as normal pickups on the floor).
/// </summary>
public class TreasureChest : MonoBehaviour
{
    [Header("Tier")]
    [SerializeField] private TreasureKeyType tier = TreasureKeyType.Bronze;

    [Header("Interaction")]
    [SerializeField] private float interactRadius = 1.8f;
    [SerializeField] private LayerMask playerLayer = 1 << 6;
    [SerializeField] private GameObject uiButtonPrompt;

    [Header("Lid (DOTween)")]
    [Tooltip("Pivot of the lid at its back hinge. Rotates around local X.")]
    [SerializeField] private Transform lid;
    [SerializeField] private float lidOpenAngle = -105f;
    [SerializeField] private float lidOpenDuration = 0.35f;

    [Header("Rewards: Coins")]
    [SerializeField] private int coinsMin = 40;
    [SerializeField] private int coinsMax = 70;

    [Header("Rewards: Energy Orb")]
    [SerializeField] private EnergyOrbPickup energyOrbPrefab;
    [SerializeField] private EnergyOrbDefinition energyOrb;
    [Range(0f, 100f)] [SerializeField] private float energyOrbChance = 100f;

    [Header("Rewards: Items")]
    [Tooltip("Item / Weapon prefabs. A few different ones are picked at random.")]
    [SerializeField] private List<GameObject> itemPool = new List<GameObject>();
    [SerializeField] private int itemCount = 1;
    [Tooltip("Where rewards land, in front of the chest (local space).")]
    [SerializeField] private float rewardDistance = 1.0f;

    [Header("Feedback")]
    [SerializeField] private Color coinTextColor = new Color(1f, 0.85f, 0.35f);
    [Tooltip("Where the '+N coins' text appears above the chest, and how far it rises.")]
    [SerializeField] private float coinTextHeight = 0.95f;
    [SerializeField] private float coinTextRise = 0.45f;

    private bool isOpened;

    public TreasureKeyType Tier => tier;

    private void Awake()
    {
        if (uiButtonPrompt != null) { uiButtonPrompt.SetActive(false); }
    }

    private void Update()
    {
        if (isOpened) { return; }

        bool isPlayerNear = IsGameplayActive() &&
                            PhysicsQuery.Any(transform.position + Vector3.up * 0.5f, interactRadius, playerLayer, QueryTriggerInteraction.Collide) &&
                            InteractFacing.IsPlayerFacing(transform.position + Vector3.up * 0.5f);

        if (uiButtonPrompt != null && uiButtonPrompt.activeSelf != isPlayerNear) { uiButtonPrompt.SetActive(isPlayerNear); }

        if (isPlayerNear && InputSchemeManager.instance != null && InputSchemeManager.instance.IsInteractPressed)
        {
            InputSchemeManager.instance.IsInteractPressed = false;
            Open();
        }
    }

    public void Open()
    {
        if (isOpened) { return; }
        isOpened = true;
        if (uiButtonPrompt != null) { uiButtonPrompt.SetActive(false); }

        Sequence seq = DOTween.Sequence().SetLink(gameObject);
        seq.Append(transform.DOPunchScale(new Vector3(0.06f, -0.08f, 0.06f), 0.2f, 6));
        if (lid != null)
        {
            seq.Append(lid.DOLocalRotate(new Vector3(lidOpenAngle, 0f, 0f), lidOpenDuration).SetEase(Ease.OutBack));
        }
        seq.AppendCallback(GiveRewards);

        if (SoundManager.instance != null) { SoundManager.instance.DoorSound_Open(transform.position); }
        if (RumbleManager.instance != null) { RumbleManager.instance.RumblePulse(0.5f, 1f, 0.2f); }
    }

    // =========================================================
    // REWARDS
    // =========================================================

    private void GiveRewards()
    {
        // Coins
        int coins = Random.Range(coinsMin, coinsMax + 1);
        if (coins > 0 && PlayerController.instance != null && PlayerController.instance.coinWallet != null)
        {
            PlayerController.instance.coinWallet.AddCoins(coins);
            SpawnFloatingText($"+{coins} coins", transform.position + Vector3.up * coinTextHeight, coinTextColor, coinTextRise);
        }

        // Spots fanned out in front of the chest
        List<Vector3> spots = new List<Vector3>();
        int total = itemCount + 1;
        for (int i = 0; i < total; i++)
        {
            float t = total == 1 ? 0.5f : i / (float)(total - 1);
            Vector3 local = new Vector3(Mathf.Lerp(-0.8f, 0.8f, t), 0f, -rewardDistance + Mathf.Abs(t - 0.5f) * 0.3f);
            spots.Add(transform.TransformPoint(local));
        }

        int spot = 0;

        // Energy Orb (middle spot)
        if (energyOrbPrefab != null && energyOrb != null && Random.Range(0f, 100f) < energyOrbChance)
        {
            Vector3 pos = transform.TransformPoint(new Vector3(0f, 0.9f, -rewardDistance * 0.6f));
            EnergyOrbPickup orb = Instantiate(energyOrbPrefab, pos, Quaternion.identity);
            orb.Initialize(energyOrb);
            PopIn(orb.transform);
        }

        // Items
        foreach (GameObject prefab in PickItems())
        {
            Vector3 pos = spots[Mathf.Min(spot++, spots.Count - 1)] + Vector3.up * 0.1f;
            GameObject item = Instantiate(prefab, pos, Quaternion.identity);
            PopIn(item.transform);
        }
    }

    private IEnumerable<GameObject> PickItems()
    {
        List<GameObject> pool = itemPool.Where(p => p != null && CanPlayerTake(p.GetComponent<Item>())).ToList();
        for (int i = 0; i < itemCount && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);
            yield return pool[index];
            pool.RemoveAt(index);
        }
    }

    // Skip unique things the player already owns (rings / spells), so the chest never gives a dud
    private static bool CanPlayerTake(Item item)
    {
        if (item == null || InventoryManager.instance == null) { return true; }

        // Weapons: never a third of the same kind (always max 2, even with the Weapon Carry upgrade)
        if (item is Weapon weapon) { return InventoryManager.instance.CanLootWeapon(weapon); }

        switch (item.itemCategory)
        {
            case ItemCategory.Ring: return !InventoryManager.instance.rings.Any(i => i.itemType == item.itemType);
            case ItemCategory.Pyromancy: return !InventoryManager.instance.pyromancies.Any(i => i.itemType == item.itemType);
            case ItemCategory.Magic: return !InventoryManager.instance.magics.Any(i => i.itemType == item.itemType);
            default: return true;
        }
    }

    private static void PopIn(Transform t)
    {
        Vector3 scale = t.localScale;
        t.localScale = Vector3.zero;
        t.DOScale(scale, 0.35f).SetEase(Ease.OutBack).SetLink(t.gameObject);
    }

    private static void SpawnFloatingText(string text, Vector3 position, Color color, float rise)
    {
        GameObject go = new GameObject("Floating Text");
        go.transform.position = position;
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        GameFontSettings.Apply(tmp);
        tmp.text = text;
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
        SharpWorldText.Attach(tmp).hideBehindWalls = false; // short pop-up over the chest

        Camera cam = Camera.main;
        if (cam != null) { go.transform.rotation = Quaternion.LookRotation(position - cam.transform.position); }

        go.transform.DOMoveY(position.y + rise, 1.2f).SetEase(Ease.OutCubic).SetLink(go);
        tmp.DOFade(0f, 0.5f).SetDelay(0.8f).SetLink(go).OnComplete(() => Destroy(go));
    }

    private static bool IsGameplayActive()
    {
        if (PauseGame.instance != null && PauseGame.instance.IsPaused) { return false; }
        if (PerkSelectionManager.instance != null && PerkSelectionManager.instance.isPanelActive) { return false; }
        if (TutorialManager.instance != null && TutorialManager.instance.isPanelActive) { return false; }
        if (ShopUI.IsOpen) { return false; }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, interactRadius);
    }
}
