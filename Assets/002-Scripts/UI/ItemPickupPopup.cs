using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Item obtained" notifications in the top-right corner (under the minimap). Shown whenever the player
/// gets something: picked up from the floor or a chest, bought in the shop, keys and upgrades included.
/// Call ItemPickupPopup.Show(...). Creates itself; no scene setup needed.
/// Each line is the prefab Resources/UI/ItemPickupPopupEntry - edit that prefab to change the look.
/// </summary>
public class ItemPickupPopup : MonoBehaviour
{
    public const string PrefabPath = "UI/ItemPickupPopupEntry";

    private class Entry
    {
        public string key;
        public ItemPickupPopupEntry view;
        public float hideAt;
        public bool fading;
    }

    private static ItemPickupPopup instance;
    private static ItemPickupPopupEntry prefab;
    private static bool warnedMissing;

    private RectTransform stack;
    private readonly List<Entry> entries = new List<Entry>(); // newest first

    private static ItemPickupPopupEntry Prefab
    {
        get
        {
            if (prefab == null) { prefab = Resources.Load<ItemPickupPopupEntry>(PrefabPath); }
            if (prefab == null && !warnedMissing)
            {
                warnedMissing = true;
                Debug.LogWarning("[ItemPopup] Missing prefab Resources/" + PrefabPath + " (Tools > UI > Build Item Pickup Popup Prefab).");
            }
            return prefab;
        }
    }

    // =========================================================
    // PUBLIC
    // =========================================================

    /// <summary>The player got an item (pickup, chest, shop...).</summary>
    public static void ShowItem(Item item)
    {
        if (item == null || Prefab == null) { return; }
        string name = string.IsNullOrEmpty(item.equipmentName) ? item.gameObject.name.Replace("(Clone)", "").Trim() : item.equipmentName;

        // The prefab chooses which of the item's icons to show; fall back to the other one if it's missing
        bool useFrame = Prefab.iconSource == ItemPickupPopupEntry.IconSource.Frame;
        Sprite first = useFrame ? item.spr_Icon_Frame : item.spr_Icon_NoFrame;
        Sprite second = useFrame ? item.spr_Icon_NoFrame : item.spr_Icon_Frame;
        Sprite icon = first != null ? first : second;

        string sub = CategoryLabel(item);
        if (item.itemCategory == ItemCategory.Consumable && InventoryManager.instance != null &&
            InventoryManager.instance.consumables.TryGetValue(item.itemType, out int count) && count > 1)
        {
            sub += $"  x{count}";
        }
        Show(name, sub, icon, "item:" + name);
    }

    /// <summary>Generic notification (keys, upgrades...). Same key = updates the existing line.</summary>
    public static void Show(string title, string subtitle, Sprite icon, string key = null)
    {
        if (string.IsNullOrEmpty(title) || Prefab == null) { return; }
        if (Time.timeSinceLevelLoad < 0.5f) { return; } // starting gear handed out while the scene loads
        if (instance == null) { instance = new GameObject("ItemPickupPopup").AddComponent<ItemPickupPopup>(); }
        instance.Add(title, subtitle, icon, key ?? title);
    }

    private static string CategoryLabel(Item item)
    {
        if (item is Weapon) { return "Weapon"; }
        switch (item.itemCategory)
        {
            case ItemCategory.Consumable: return "Consumable";
            case ItemCategory.Ring: return "Ring";
            case ItemCategory.Pyromancy: return "Pyromancy";
            case ItemCategory.Magic: return "Sorcery";
            case ItemCategory.Weapon: return "Weapon";
            default: return "Item";
        }
    }

    // =========================================================
    // UI
    // =========================================================

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25; // above the minimap
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        CanvasGroup cg = gameObject.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        stack = new GameObject("Stack", typeof(RectTransform)).GetComponent<RectTransform>();
        stack.SetParent(transform, false);
        stack.anchorMin = stack.anchorMax = new Vector2(1f, 1f);
        stack.pivot = new Vector2(1f, 1f);
        PlaceStack();
    }

    // Under the minimap, right edges lined up
    private void PlaceStack()
    {
        MinimapSettings map = MinimapSettings.Load();
        stack.sizeDelta = new Vector2(Width, 10f);
        stack.anchoredPosition = new Vector2(-map.margin.x, -(map.margin.y + map.size + Prefab.gapBelowMinimap));
    }

    private static float Width => ((RectTransform)Prefab.transform).sizeDelta.x;
    private static float Height => ((RectTransform)Prefab.transform).sizeDelta.y;

    private void OnDestroy()
    {
        if (instance == this) { instance = null; }
        foreach (Entry e in entries) { if (e.view != null) { e.view.rect.DOKill(); e.view.group.DOKill(); } }
    }

    private void Add(string title, string subtitle, Sprite icon, string key)
    {
        ItemPickupPopupEntry p = Prefab;
        PlaceStack();

        // Same thing again (e.g. another potion): refresh that line instead of adding a new one
        Entry existing = entries.Find(e => e.key == key && !e.fading && e.view != null);
        if (existing != null)
        {
            existing.view.SetSubtitle(subtitle);
            existing.hideAt = Time.unscaledTime + p.holdTime;
            existing.view.rect.DOKill(true);
            existing.view.rect.localScale = Vector3.one;
            existing.view.rect.DOPunchScale(Vector3.one * 0.06f, 0.25f, 6, 0.5f).SetUpdate(true);
            entries.Remove(existing);
            entries.Insert(0, existing);
            Layout(true);
            return;
        }

        ItemPickupPopupEntry view = Instantiate(p, stack, false);
        view.name = "Entry";
        view.rect.anchorMin = view.rect.anchorMax = new Vector2(1f, 1f);
        view.rect.pivot = new Vector2(1f, 1f);
        view.Fill(title, subtitle, icon);

        var entry = new Entry { key = key, view = view, hideAt = Time.unscaledTime + p.holdTime };
        entries.Insert(0, entry);

        // Too many: drop the oldest
        while (entries.Count > Mathf.Max(1, p.maxEntries))
        {
            Entry old = entries[entries.Count - 1];
            entries.RemoveAt(entries.Count - 1);
            Kill(old);
        }

        // Slide in from the right
        view.group.alpha = 0f;
        view.rect.anchoredPosition = new Vector2(Width * 0.6f, 0f);
        Layout(true, entry);
        view.group.DOFade(1f, p.slideTime).SetUpdate(true);
    }

    private void Update()
    {
        ItemPickupPopupEntry p = Prefab;
        if (p == null) { return; }

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry e = entries[i];
            if (e.view == null) { entries.RemoveAt(i); continue; }
            if (e.fading || Time.unscaledTime < e.hideAt) { continue; }
            e.fading = true;
            e.view.rect.DOAnchorPosX(Width * 0.4f, p.fadeTime).SetEase(Ease.InQuad).SetUpdate(true);
            e.view.group.DOFade(0f, p.fadeTime).SetUpdate(true).OnComplete(() =>
            {
                entries.Remove(e);
                Kill(e);
                Layout(true);
            });
        }
    }

    // Newest at the top, the rest move down
    private void Layout(bool animate, Entry sliding = null)
    {
        ItemPickupPopupEntry p = Prefab;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e.fading || e.view == null) { continue; }
            RectTransform r = e.view.rect;
            Vector2 target = new Vector2(0f, -i * (Height + p.spacing));
            if (e == sliding)
            {
                r.anchoredPosition = new Vector2(r.anchoredPosition.x, target.y);
                r.DOAnchorPos(target, p.slideTime).SetEase(Ease.OutCubic).SetUpdate(true);
            }
            else if (animate)
            {
                r.DOAnchorPosY(target.y, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
            }
            else
            {
                r.anchoredPosition = target;
            }
        }
    }

    private static void Kill(Entry e)
    {
        if (e.view == null) { return; }
        e.view.rect.DOKill();
        e.view.group.DOKill();
        Destroy(e.view.gameObject);
    }
}
