using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PerkSelectionUI : MonoBehaviour
{
    [System.Serializable]
    public class OfferSlotUI
    {
        public Image fillImage;
        public Button button;
        public Image icon;
        public TMP_Text levelText;
    }

    [System.Serializable]
    public class InventorySlotUI
    {
        public Image fillImage;
        public Button button;
        public Image icon;
        public TMP_Text levelText;
    }

    [Header("========== ROOT ==========")]
    [SerializeField] private CanvasGroup panelRoot;

    [Header("========== HEADER ==========")]
    [SerializeField] private TMP_Text titleText;

    [Header("========== CARD OFFERS ==========")]
    [SerializeField] private OfferSlotUI[] offerSlotsUI = new OfferSlotUI[3];
    [SerializeField] private Sprite spr_LockedIcon;
    private PerkOffer[] currentOffers;
    private PerkOffer currentSelectedOffer;
    private OfferSlotUI currentSelectedOfferSlotUI;
    private int currentSelectedOfferIndex = -1;

    [Header("========== INVENTORY ==========")]
    [SerializeField]
    private InventorySlotUI[] inventorySlotsUI =
        new InventorySlotUI[9];

    private InventorySlotUI currentSelectedInventorySlotUI;
    private int currentSelectedInventorySlotIndex = -1;

    [Header("========== DESCRIPTION ==========")]
    [SerializeField] private TMP_Text perkNameText;
    [SerializeField] private TMP_Text perkLevelText;
    [SerializeField] private TMP_Text perkDescriptionText;

    [Header("========== COIN ==========")]
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private TMP_Text shuffleCostText;

    [Header("========== MODE ==========")]
    [SerializeField] private TMP_Text replacingCardText;

    [Header("========== TIER COLORS ==========")]
    [SerializeField]
    private Color commonColor = new Color(
        0.20f, 0.60f, 0.20f);

    [SerializeField]
    private Color rareColor = new Color(
        0.20f, 0.80f, 0.90f);

    [SerializeField]
    private Color uniqueColor = new Color(
        0.85f, 0.75f, 0.15f);

    [SerializeField] private float holdDuration = 1.0f;
    private float currentHoldTime;
    [HideInInspector] public int CurrentAddingPerkSlotIndex;

    [Header("========== MANAGER ==========")]
    [SerializeField] private PerkSelectionManager selectionManager;


    private void Awake()
    {
        SetupOfferButtons();
        SetupInventoryButtons();

        StartCoroutine(Hide());

        selectionManager.OnChanged += RefreshInventory;
    }

    private void Update()
    {
        HandlePerkSelectionInput();
    }

    private void HandlePerkSelectionInput()
    {
        HandleCurrentSelectedOffer();
        HandleCurrentSelectedInventorySlot();
    }

    private void HandleCurrentSelectedOffer()
    {
        if(currentSelectedOffer == null || currentSelectedOfferSlotUI == null) { return; }

        if (selectionManager.CurrentState != SelectionState.Choosing)
        {
            currentHoldTime = 0f;
            currentSelectedOfferSlotUI.fillImage.fillAmount = 0f;
            return; 
        }

        UpdateFillImage(currentSelectedOfferSlotUI.fillImage, currentSelectedOfferIndex);
    }

    private void HandleCurrentSelectedInventorySlot()
    {
        if(currentSelectedInventorySlotUI == null) { return; }

        if (selectionManager.CurrentState != SelectionState.Replacing)
        {
            currentHoldTime = 0f;
            currentSelectedInventorySlotUI.fillImage.fillAmount = 0f;
            return; 
        }

        UpdateFillImage(currentSelectedInventorySlotUI.fillImage, currentSelectedInventorySlotIndex);
    }

    private void UpdateFillImage(Image fillImage, int currentIndex)
    {
        if (fillImage == null) return;

        if (InputSchemeManager.instance.IsSubmitHeld)
        {
            currentHoldTime += Time.unscaledDeltaTime;

            fillImage.fillAmount = Mathf.Clamp01(
                currentHoldTime / holdDuration
            );

            if (currentHoldTime >= holdDuration)
            {
                currentHoldTime = 0f;
                fillImage.fillAmount = 0f;
                selectionManager.Confirm(currentIndex);
            }
        }
        else
        {
            currentHoldTime = 0f;
            fillImage.fillAmount = 0f;
        }
    }

    private void ResetHoldProgress()
    {
        currentHoldTime = 0f;

        if (offerSlotsUI != null)
        {
            foreach (OfferSlotUI slot in offerSlotsUI)
            {
                if (slot?.fillImage != null)
                    slot.fillImage.fillAmount = 0f;
            }
        }

        if (inventorySlotsUI != null)
        {
            foreach (InventorySlotUI slot in inventorySlotsUI)
            {
                if (slot?.fillImage != null)
                    slot.fillImage.fillAmount = 0f;
            }
        }
    }

    // =========================================================
    // SETUP
    // =========================================================

    private void SetupOfferButtons()
    {
        if (offerSlotsUI == null)
            return;

        for (int i = 0; i < offerSlotsUI.Length; i++)
        {
            int index = i;

            if (offerSlotsUI[i] == null ||
                offerSlotsUI[i].button == null)
                continue;

            Button button = offerSlotsUI[i].button;

            button.onClick.RemoveAllListeners();

            button.onClick.AddListener(
                () => OnOfferClicked(index));

            UIFeedback uiFeedback =
                button.GetComponent<UIFeedback>();

            uiFeedback.onSelected.RemoveAllListeners();
            uiFeedback.onSelected.AddListener(
                () => OnOfferSelected(index));

            // Refresh the perk UI when this button is selected.
            EventTrigger trigger =
                button.GetComponent<EventTrigger>();

            if (trigger == null)
            {
                trigger = button.gameObject
                    .AddComponent<EventTrigger>();
            }

            // Use the component's existing ISelectHandler instead.
            // See the next section for the cleaner implementation.
        }
    }

    private void OnOfferSelected(int index)
    {
        ResetHoldProgress();
        UpdateDescription(currentOffers, index);

        currentSelectedOfferIndex = index;
        currentSelectedOffer = currentOffers[index];
        currentSelectedOfferSlotUI = offerSlotsUI[index];
        currentSelectedInventorySlotUI = null;
    }

    private void SetupInventoryButtons()
    {
        if (inventorySlotsUI == null)
            return;

        for (int i = 0; i < inventorySlotsUI.Length; i++)
        {
            int index = i;

            if (inventorySlotsUI[i] == null ||
                inventorySlotsUI[i].button == null)
                continue;

            Button button = inventorySlotsUI[i].button;

            button.onClick.RemoveAllListeners();

            button.onClick.AddListener(
                () => OnInventorySlotClicked(index));

            UIFeedback uiFeedback =
                button.GetComponent<UIFeedback>();

            if (uiFeedback == null)
            {
                uiFeedback = button.gameObject
                    .AddComponent<UIFeedback>();
            }

            uiFeedback.onSelected.RemoveAllListeners();

            uiFeedback.onSelected.AddListener(
                () => OnInventorySlotsUISelected(index));
        }
    }

    private void OnInventorySlotsUISelected(int index)
    {
        ResetHoldProgress();

        if (selectionManager == null)
            return;

        currentSelectedInventorySlotIndex = index;
        currentSelectedOffer = null;
        currentSelectedOfferSlotUI = null;
        currentSelectedInventorySlotUI = inventorySlotsUI[index];

        Debug.Log($"[InventorySlot] OnInventorySlotsUISelected : Index = {index}");

        PerkCardInventory inventory =
            selectionManager.Inventory;

        if (inventory == null ||
            !inventory.IsSlotUnlocked(index) ||
            !inventory.IsSlotOccupied(index))
        {
            ClearDescription();
            return;
        }

        RuntimePerkCard runtimeCard = inventory.GetSlot(index);

        if (runtimeCard == null ||
            runtimeCard.definition == null)
        {
            ClearDescription();
            return;
        }

        PerkCardDefinition definition = runtimeCard.definition;

        if (perkNameText != null)
            perkNameText.text = $"{definition.DisplayName} [LV.{definition.Level}]";

        if (perkLevelText != null)
            perkLevelText.text = $"[LV. {runtimeCard.level}]";

        if (perkDescriptionText != null)
            perkDescriptionText.text = definition.Description;

        
    }

    // =========================================================
    // OPEN / CLOSE
    // =========================================================

    public void ShowChoosing(
        EnergyOrbDefinition orb,
        PerkOffer[] offers,
        int selectedOfferIndex,
        PerkCardInventory inventory,
        int coins)
    {
        Time.timeScale = 0f;

        if (panelRoot != null)
        {
            panelRoot.DOFade(1f, 0.25f).SetUpdate(true);
        }

        Refresh(
            orb,
            offers,
            selectedOfferIndex,
            -1,
            inventory,
            coins,
            false);
    }

    public IEnumerator Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.DOFade(0f, 0.25f).SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(0.25f);
        Time.timeScale = 1f;
    }

    // =========================================================
    // REFRESH EVERYTHING
    // =========================================================

    public void Refresh(
        EnergyOrbDefinition orb,
        PerkOffer[] offers,
        int selectedOfferIndex,
        int selectedReplacementSlot,
        PerkCardInventory inventory,
        int coins,
        bool isReplacingCard)
    {
        currentOffers = offers;

        UpdateHeader(orb);
        UpdateOffers(
            offers,
            selectedOfferIndex);

        UpdateInventory(
            inventory,
            selectedReplacementSlot,
            isReplacingCard);

        UpdateDescription(
            offers,
            selectedOfferIndex);

        UpdateCoins(
            orb,
            coins);

        UpdateMode(
            isReplacingCard);
    }

    private void RefreshInventory()
    {
        Debug.Log("[Perk] RefreshInventory");

        UpdateInventory(
            selectionManager.Inventory,
            CurrentAddingPerkSlotIndex,
            false); // Can be true nor false via just want to update inventory
    }

    // =========================================================
    // HEADER
    // =========================================================

    private void UpdateHeader(
        EnergyOrbDefinition orb)
    {
        if (orb == null)
            return;

        string tierName =
            orb.Tier.ToString().ToUpper();

        if (titleText != null)
        {
            titleText.text =
                $"CHOOSE PERK CARD [{tierName}]";

            titleText.color =
                GetTierColor(orb.Tier);
        }

        // If you have another Image representing
        // the outer panel border, you can apply the
        // tier color here as well.
    }

    // =========================================================
    // OFFER CARDS
    // =========================================================

    private void UpdateOffers(
        PerkOffer[] offers,
        int selectedOfferIndex)
    {
        if (offerSlotsUI == null)
            return;

        for (int i = 0; i < offerSlotsUI.Length; i++)
        {
            OfferSlotUI slot = offerSlotsUI[i];

            if (slot == null)
                continue;

            bool hasOffer =
                offers != null &&
                i < offers.Length &&
                offers[i] != null &&
                offers[i].definition != null;

            if (!hasOffer)
            {
                ClearOffer(slot);
                continue;
            }

            PerkOffer offer =
                offers[i];

            PerkCardDefinition card =
                offer.definition;

            // Icon
            if (slot.icon != null)
            {
                slot.icon.sprite = card.Icon;
                slot.icon.enabled = card.Icon != null;
                slot.icon.SetNativeSize();
            }

            // Level
            if (slot.levelText != null)
            {
                slot.levelText.text =
                    $"LV. {offer.level}";
            }

            // Button
            if (slot.button != null)
                slot.button.interactable = true;

            EventSystem.current.SetSelectedGameObject(offerSlotsUI[0].button.gameObject);
        }
    }

    private void ClearOffer(
        OfferSlotUI slot)
    {
        if (slot.icon != null)
        {
            slot.icon.sprite = null;
            slot.icon.enabled = false;
            slot.icon.SetNativeSize();
        }

        if (slot.levelText != null)
            slot.levelText.text = "";

        if (slot.button != null)
            slot.button.interactable = false;
    }

    // =========================================================
    // INVENTORY
    // =========================================================

    private void UpdateInventory(
        PerkCardInventory inventory,
        int selectedReplacementSlot,
        bool replacementMode)
    {
        if (inventory == null ||
            inventorySlotsUI == null)
            return;

        int count =
            Mathf.Min(
                inventorySlotsUI.Length,
                inventory.SlotCount);

        for (int i = 0; i < count; i++)
        {
            InventorySlotUI slot =
                inventorySlotsUI[i];

            if (slot == null)
                continue;

            bool unlocked =
                inventory.IsSlotUnlocked(i);

            bool occupied =
                inventory.IsSlotOccupied(i);

            RuntimePerkCard runtimeCard =
                inventory.GetSlot(i);

            // -----------------------------------------
            // LOCKED SLOT
            // -----------------------------------------

            if (!unlocked)
            {
                if (slot.icon != null)
                {
                    slot.icon.sprite = spr_LockedIcon;
                    slot.icon.enabled = true;
                    slot.icon.SetNativeSize();
                }

                if (slot.levelText != null)
                    slot.levelText.text = "";

                if (slot.button != null)
                    slot.button.interactable = false;

                continue;
            }

            // -----------------------------------------
            // UNLOCKED
            // -----------------------------------------

            if (slot.icon == spr_LockedIcon)
                slot.icon = null;

            // -----------------------------------------
            // EMPTY
            // -----------------------------------------

            if (!occupied || runtimeCard.definition == null)
            {
                if (slot.icon != null)
                {
                    slot.icon.sprite = null;
                    slot.icon.enabled = false;
                    slot.icon.SetNativeSize();
                }

                if (slot.levelText != null)
                    slot.levelText.text = "";

                if (slot.button != null)
                {
                    // Empty slots cannot be selected
                    // during replacement.
                    slot.button.interactable =
                        !replacementMode;
                }

                continue;
            }

            // -----------------------------------------
            // OCCUPIED
            // -----------------------------------------

            if (slot.icon != null && runtimeCard.definition != null)
            {
                slot.icon.sprite =
                    runtimeCard.definition.Icon;

                slot.icon.enabled =
                    runtimeCard.definition.Icon != null;

                slot.icon.SetNativeSize();
            }

            if (slot.levelText != null)
            {
                slot.levelText.text =
                    $"{runtimeCard.level}";
            }

            if (slot.button != null)
                slot.button.interactable = true;
        }

        // Clear extra UI slots if your array is smaller/larger.
        for (int i = count; i < inventorySlotsUI.Length; i++)
        {
            InventorySlotUI slot = inventorySlotsUI[i];

            if (slot == null)
                continue;

            if (slot.icon != null)
            {
                slot.icon.sprite = spr_LockedIcon;
                slot.icon.enabled = false;
                slot.icon.SetNativeSize();
            }

            if (slot.button != null)
                slot.button.interactable = false;
        }
    }

    // =========================================================
    // DESCRIPTION
    // =========================================================

    private void UpdateDescription(
        PerkOffer[] offers,
        int selectedOfferIndex)
    {
        if (selectedOfferIndex < 0 ||
            offers == null ||
            selectedOfferIndex >= offers.Length ||
            offers[selectedOfferIndex] == null ||
            offers[selectedOfferIndex].definition == null)
        {
            ClearDescription();
            return;
        }

        PerkOffer offer =
            offers[selectedOfferIndex];

        PerkCardDefinition definition =
            offer.definition;

        string cardDescription =
            definition.Description;

        if (perkNameText != null)
            perkNameText.text =
                $"{definition.DisplayName} [LV.{definition.Level}]";

        if (perkLevelText != null)
            perkLevelText.text =
                $"[LV. {offer.level}]";

        if (perkDescriptionText != null)
        {
            perkDescriptionText.text =
                cardDescription;
        }
    }

    private void ClearDescription()
    {
        if (perkNameText != null)
            perkNameText.text = "SELECT A PERK CARD";

        if (perkLevelText != null)
            perkLevelText.text = "";

        if (perkDescriptionText != null)
            perkDescriptionText.text = "";
    }

    // =========================================================
    // COINS
    // =========================================================

    private void UpdateCoins(
        EnergyOrbDefinition orb,
        int coins)
    {
        if (coinText != null)
            coinText.text = coins.ToString();

        if (shuffleCostText != null)
        {
            int cost =
                orb != null
                    ? orb.ShuffleCost
                    : 0;

            shuffleCostText.text =
                $"SHUFFLE ({cost} COIN)";
        }
    }

    // =========================================================
    // MODE
    // =========================================================

    private void UpdateMode(bool isReplacingCard)
    {
        replacingCardText.gameObject.SetActive(isReplacingCard);
        titleText.gameObject.SetActive(!isReplacingCard);

        foreach (OfferSlotUI ui in offerSlotsUI)
        {
            ui.button.interactable = !isReplacingCard;
        }

        if(isReplacingCard)
        {
            EventSystem.current.SetSelectedGameObject(inventorySlotsUI[0].button.gameObject);
        }
        else
        {
            EventSystem.current.SetSelectedGameObject(offerSlotsUI[0].button.gameObject);
        }
    }

    // =========================================================
    // INPUT CALLBACKS
    // =========================================================

    private void OnOfferClicked(int index)
    {
        if (selectionManager == null)
            return;

        selectionManager.SelectOffer(index);
    }

    private void OnInventorySlotClicked(int index)
    {
        if (selectionManager == null)
            return;

        selectionManager.SelectReplacementSlot(index);
    }

    // =========================================================
    // NOTIFICATIONS
    // =========================================================

    public void ShowNotEnoughCoins()
    {
        // Replace this with your own popup/toast system.
        // For now this simply logs the message.

        Debug.Log("Not enough coins to shuffle.");
    }

    // =========================================================
    // TIER COLOR
    // =========================================================

    private Color GetTierColor(
        OrbTier tier)
    {
        switch (tier)
        {
            case OrbTier.Common:
                return commonColor;

            case OrbTier.Rare:
                return rareColor;

            case OrbTier.Unique:
                return uniqueColor;

            default:
                return Color.white;
        }
    }

    private void OnDestroy()
    {
        selectionManager.OnChanged -= RefreshInventory;
    }
}