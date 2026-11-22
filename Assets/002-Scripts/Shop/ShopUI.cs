using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shop window. Loaded from Resources/UI/ShopUI the first time a shop opens, so no scene setup is needed.
/// Pauses the game like the perk selection. Submit / click buys, Cancel leaves.
/// </summary>
public class ShopUI : MonoBehaviour
{
    private const string ResourcePath = "UI/ShopUI";

    [Header("Panel")]
    [SerializeField] private CanvasGroup panelRoot;
    [SerializeField] private RectTransform window;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text messageText;

    [Header("List")]
    [SerializeField] private Transform listContent;
    [SerializeField] private ShopRowUI rowTemplate;

    [Header("Details")]
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailDescription;
    [SerializeField] private TMP_Text detailPrice;
    [SerializeField] private TMP_Text detailOwned;

    [Header("Colors")]
    [SerializeField] private Color affordableColor = new Color(1f, 0.85f, 0.35f);
    [SerializeField] private Color tooExpensiveColor = new Color(0.85f, 0.3f, 0.25f);
    [SerializeField] private Color soldOutColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color successColor = new Color(0.55f, 0.9f, 0.45f);

    [Header("Timing")]
    [SerializeField] private float fadeDuration = 0.2f;
    [Tooltip("Ignore Submit for a moment after opening, so the Interact press that opened the shop can't buy something.")]
    [SerializeField] private float inputGuard = 0.2f;

    private static ShopUI instance;
    private readonly List<ShopRowUI> rows = new List<ShopRowUI>();
    private ShopKeeper keeper;
    private int selectedIndex;
    private bool isOpen;
    private bool isClosing;
    private Tween messageTween;

    /// <summary>True while the shop window is up (checked by other panels / input).</summary>
    public static bool IsOpen => instance != null && (instance.isOpen || instance.isClosing);

    public static void Open(ShopKeeper shopKeeper)
    {
        if (instance == null)
        {
            ShopUI prefab = Resources.Load<ShopUI>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[ShopUI] Missing prefab at Resources/" + ResourcePath);
                return;
            }
            instance = Instantiate(prefab);
            instance.name = "ShopUI";
        }

        instance.Show(shopKeeper);
    }

    private void Awake()
    {
        if (rowTemplate != null) { rowTemplate.gameObject.SetActive(false); }
        SetVisible(false, instant: true);
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; }
        if (isOpen) { Time.timeScale = 1f; }
    }

    private void Update()
    {
        if (!isOpen || isClosing) { return; }

        InputSchemeManager input = InputSchemeManager.instance;
        if (input == null) { return; }

        if (input.IsCancelPressed)
        {
            input.IsCancelPressed = false;
            Close();
            return;
        }

        // Keep something selected for keyboard / gamepad (clicking empty space clears the selection)
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && rows.Count > 0)
        {
            Select(Mathf.Clamp(selectedIndex, 0, rows.Count - 1));
        }
    }

    // =========================================================
    // OPEN / CLOSE
    // =========================================================

    private void Show(ShopKeeper shopKeeper)
    {
        if (isOpen) { return; }

        keeper = shopKeeper;
        isOpen = true;
        isClosing = false;
        selectedIndex = 0;

        if (titleText != null) { titleText.text = keeper.ShopName; }
        if (messageText != null) { messageText.text = string.Empty; }

        BuildRows();
        RefreshAll();

        Time.timeScale = 0f;
        SetVisible(true, instant: false);

        // Switch the input maps to UI (Cancel / Submit only fire while the UI map is active)
        NotifyInputChange();

        if (window != null)
        {
            window.localScale = Vector3.one * 0.94f;
            window.DOScale(1f, fadeDuration).SetEase(Ease.OutBack).SetUpdate(true);
        }

        StartCoroutine(EnableInputAfterGuard());
    }

    private IEnumerator EnableInputAfterGuard()
    {
        panelRoot.interactable = false;
        yield return new WaitForSecondsRealtime(inputGuard);

        if (!isOpen) { yield break; }
        if (InputSchemeManager.instance != null) { InputSchemeManager.instance.IsSubmitPressed = false; }
        panelRoot.interactable = true;
        if (rows.Count > 0) { Select(0); }
    }

    public void Close()
    {
        if (!isOpen || isClosing) { return; }
        StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        isClosing = true;
        SetVisible(false, instant: false);
        if (EventSystem.current != null) { EventSystem.current.SetSelectedGameObject(null); }

        yield return new WaitForSecondsRealtime(fadeDuration);

        Time.timeScale = 1f;
        isOpen = false;

        // Wait a frame so the same Interact / Cancel press can't reopen the shop or the pause menu
        yield return null;
        if (InputSchemeManager.instance != null)
        {
            InputSchemeManager.instance.IsInteractPressed = false;
            InputSchemeManager.instance.IsCancelPressed = false;
        }
        isClosing = false;

        // Back to the gameplay input map
        NotifyInputChange();
    }

    private static void NotifyInputChange()
    {
        if (InputSchemeManager.instance != null) { InputSchemeManager.instance.OnInputActiveChange?.Invoke(); }
    }

    private void SetVisible(bool visible, bool instant)
    {
        if (panelRoot == null) { return; }

        panelRoot.DOKill();
        panelRoot.blocksRaycasts = visible;
        if (!visible) { panelRoot.interactable = false; }

        if (instant) { panelRoot.alpha = visible ? 1f : 0f; }
        else { panelRoot.DOFade(visible ? 1f : 0f, fadeDuration).SetUpdate(true); }
    }

    // =========================================================
    // LIST
    // =========================================================

    private void BuildRows()
    {
        foreach (ShopRowUI row in rows) { Destroy(row.gameObject); }
        rows.Clear();

        if (rowTemplate == null || keeper == null) { return; }

        for (int i = 0; i < keeper.Stock.Count; i++)
        {
            ShopRowUI row = Instantiate(rowTemplate, listContent);
            row.gameObject.SetActive(true);
            row.name = "Row " + i;
            row.index = i;
            row.Selected += OnRowSelected;
            row.Clicked += OnRowClicked;
            rows.Add(row);
        }

        // Explicit up/down navigation with wrap-around
        for (int i = 0; i < rows.Count; i++)
        {
            Navigation nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = rows[(i - 1 + rows.Count) % rows.Count].button,
                selectOnDown = rows[(i + 1) % rows.Count].button
            };
            rows[i].button.navigation = nav;
        }
    }

    private void RefreshAll()
    {
        int coins = Coins;
        if (coinsText != null) { coinsText.text = coins.ToString(); }

        for (int i = 0; i < rows.Count; i++)
        {
            ShopKeeper.StockSlot slot = keeper.Stock[i];
            ShopRowUI row = rows[i];
            bool available = keeper.IsSlotAvailable(i);

            if (row.icon != null)
            {
                row.icon.sprite = slot.item.Icon;
                row.icon.enabled = row.icon.sprite != null;
                row.icon.color = available ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            if (row.nameText != null)
            {
                row.nameText.text = slot.item.displayName;
                row.nameText.color = available ? Color.white : soldOutColor;
            }
            if (row.priceText != null)
            {
                row.priceText.text = available ? slot.item.Price.ToString() : "-";
                row.priceText.color = !available ? soldOutColor : (coins >= slot.item.Price ? affordableColor : tooExpensiveColor);
            }
            if (row.stockText != null)
            {
                row.stockText.text = available && slot.remaining > 1 ? "x" + slot.remaining : string.Empty;
            }
            if (row.soldOutOverlay != null) { row.soldOutOverlay.SetActive(!available); }
            row.SetHighlighted(i == selectedIndex);
        }

        ShowDetails(selectedIndex);
    }

    private void ShowDetails(int index)
    {
        bool valid = keeper != null && index >= 0 && index < keeper.Stock.Count;
        ShopItemDefinition item = valid ? keeper.Stock[index].item : null;

        if (detailIcon != null)
        {
            detailIcon.sprite = item != null ? item.Icon : null;
            detailIcon.enabled = detailIcon.sprite != null;
        }
        if (detailName != null) { detailName.text = item != null ? item.displayName : string.Empty; }
        if (detailDescription != null) { detailDescription.text = item != null ? item.description : string.Empty; }
        if (detailOwned != null) { detailOwned.text = item != null ? PlayerUpgrades.Instance.GetOwnedText(item) : string.Empty; }

        if (detailPrice != null)
        {
            if (item == null) { detailPrice.text = string.Empty; }
            else if (!keeper.IsSlotAvailable(index)) { detailPrice.text = "Sold out"; detailPrice.color = soldOutColor; }
            else
            {
                detailPrice.text = item.Price + " coins";
                detailPrice.color = Coins >= item.Price ? affordableColor : tooExpensiveColor;
            }
        }
    }

    private void Select(int index)
    {
        if (index < 0 || index >= rows.Count || EventSystem.current == null) { return; }
        EventSystem.current.SetSelectedGameObject(rows[index].button.gameObject);
        OnRowSelected(index);
    }

    private void OnRowSelected(int index)
    {
        selectedIndex = index;
        for (int i = 0; i < rows.Count; i++) { rows[i].SetHighlighted(i == index); }
        ShowDetails(index);
    }

    private void OnRowClicked(int index)
    {
        if (!isOpen || isClosing || !panelRoot.interactable || keeper == null) { return; }

        PurchaseResult result = keeper.TryBuy(index);
        ShopKeeper.StockSlot slot = keeper.Stock[index];

        switch (result)
        {
            case PurchaseResult.Success:
                ShowMessage("Bought " + slot.item.displayName, successColor);
                if (index < rows.Count) { rows[index].transform.DOPunchScale(Vector3.one * 0.06f, 0.2f, 6).SetUpdate(true); }
                if (coinsText != null) { coinsText.transform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 6).SetUpdate(true); }
                break;
            case PurchaseResult.NotEnoughCoins:
                ShowMessage("Not enough coins", tooExpensiveColor);
                Shake(index);
                break;
            case PurchaseResult.SoldOut:
                ShowMessage("Sold out", soldOutColor);
                Shake(index);
                break;
            case PurchaseResult.CannotCarry:
                ShowMessage("You can't carry any more of that", tooExpensiveColor);
                Shake(index);
                break;
            default:
                ShowMessage("Can't buy that right now", tooExpensiveColor);
                break;
        }

        RefreshAll();
    }

    private void Shake(int index)
    {
        if (index < 0 || index >= rows.Count) { return; }
        RectTransform rt = (RectTransform)rows[index].transform;
        rt.DOKill(true);
        rt.DOShakeAnchorPos(0.25f, new Vector2(8f, 0f), 20, 0f).SetUpdate(true);
    }

    private void ShowMessage(string text, Color color)
    {
        if (messageText == null) { return; }

        messageTween?.Kill();
        messageText.text = text;
        messageText.color = color;
        messageText.alpha = 1f;
        messageTween = messageText.DOFade(0f, 0.6f).SetDelay(1.4f).SetUpdate(true);
    }

    private static int Coins => PlayerController.instance != null && PlayerController.instance.coinWallet != null
        ? PlayerController.instance.coinWallet.Coins
        : 0;
}
