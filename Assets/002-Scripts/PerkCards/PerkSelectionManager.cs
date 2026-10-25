using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum SelectionState
{
    Closed,
    Choosing,
    Replacing
}

[Serializable]
public class PerkOffer
{
    public PerkCardDefinition definition;
    public int level;

    public PerkOffer(
        PerkCardDefinition definition,
        int level)
    {
        this.definition = definition;
        this.level = level;
    }
}

public class PerkSelectionManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PerkCardInventory inventory;
    [SerializeField] private PerkSelectionUI ui;
    [SerializeField] private CoinWallet coinWallet;

    [Header("Testing")]
    [SerializeField] private EnergyOrbDefinition commonOrb;
    [SerializeField] private EnergyOrbDefinition rareOrb;
    [SerializeField] private EnergyOrbDefinition uniqueOrb;

    private EnergyOrbDefinition currentOrb;

    private PerkOffer[] offers = new PerkOffer[3];

    public int selectedOfferIndex = -1;
    public int selectedReplacementSlot = -1;

    public PerkCardInventory Inventory => inventory;

    [HideInInspector] public bool isPanelActive = false;

    public SelectionState CurrentState = SelectionState.Closed;
    
    public event Action OnChanged;

    public static PerkSelectionManager instance;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        if(coinWallet == null)
        {
            coinWallet = PlayerController.instance.coinWallet;
        }
    }

    private void Update()
    {
        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            Open(commonOrb);
        }

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            Open(rareOrb);
        }

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            Open(uniqueOrb);
        }

        if (InputSchemeManager.instance.IsCancelPressed)
        {
            InputSchemeManager.instance.IsCancelPressed = false;
            Cancel();
        }
    }

    public PerkOffer GetOffer(int index)
    {
        if (index < 0 || index >= offers.Length)
            return null;

        return offers[index];
    }

    public void Open(EnergyOrbDefinition orb)
    {
        if (orb == null)
            return;

        currentOrb = orb;
        isPanelActive = true;

        selectedOfferIndex = -1;
        selectedReplacementSlot = -1;

        GenerateOffers();

        CurrentState = SelectionState.Choosing;

        ui.ShowChoosing(
            currentOrb,
            offers,
            selectedOfferIndex,
            inventory,
            coinWallet.Coins);

        InputSchemeManager.instance.OnInputActiveChange.Invoke();
    }

    public void SelectOffer(int index)
    {
        if (CurrentState != SelectionState.Choosing)
            return;

        if (index < 0 || index >= offers.Length)
            return;

        if (offers[index] == null)
            return;

        selectedOfferIndex = index;

        ui.Refresh(
            currentOrb,
            offers,
            selectedOfferIndex,
            selectedReplacementSlot,
            inventory,
            coinWallet.Coins,
            false);
    }

    public void Shuffle()
    {
        if (CurrentState != SelectionState.Choosing)
            return;

        if (!coinWallet.TrySpend(
                currentOrb.ShuffleCost))
        {
            ui.ShowNotEnoughCoins();
            return;
        }

        selectedOfferIndex = -1;

        GenerateOffers();

        ui.Refresh(
            currentOrb,
            offers,
            selectedOfferIndex,
            selectedReplacementSlot,
            inventory,
            coinWallet.Coins,
            false);
    }

    public void Confirm(int offerIndex)
    {
        Debug.Log($"[Perk] currentSelected : {offerIndex}");

        if (CurrentState == SelectionState.Choosing)
        {
            selectedOfferIndex = offerIndex;
            ConfirmCardSelection();
            return;
        }

        if (CurrentState == SelectionState.Replacing)
        {
            selectedReplacementSlot = offerIndex;
            ConfirmReplacement();
        }
    }

    private void ConfirmCardSelection()
    {
        //Debug.Log("[Perk] ConfirmCardSelection");
        if (selectedOfferIndex < 0)
        {
            //Debug.Log($"[Perk] selectedOfferIndex < 0");
            return;
        }

        PerkOffer selectedOffer =
            offers[selectedOfferIndex];

        if (selectedOffer == null)
        {
            //Debug.Log($"[Perk] selectedOffer == null");
            return;
        }

        // There is an empty unlocked slot.
        if (inventory.HasFreeUnlockedSlot())
        {
            Debug.Log("[Perk] TryAdd");
            inventory.TryAdd(
                selectedOffer.definition,
                selectedOffer.level,
                out _);

            OnChanged.Invoke();

            Close();
            return;
        }

        // Inventory is full.
        EnterReplacementMode();
    }

    private void EnterReplacementMode()
    {
        Debug.Log("[Perk] EnterReplacementMode");
        CurrentState = SelectionState.Replacing;
        selectedReplacementSlot = -1;

        ui.Refresh(
            currentOrb,
            offers,
            selectedOfferIndex,
            selectedReplacementSlot,
            inventory,
            coinWallet.Coins,
            true);
    }

    public void SelectReplacementSlot(int slotIndex)
    {
        if (CurrentState != SelectionState.Replacing)
            return;

        if (!inventory.IsSlotUnlocked(slotIndex))
            return;

        if (!inventory.IsSlotOccupied(slotIndex))
            return;

        selectedReplacementSlot = slotIndex;

        ui.Refresh(
            currentOrb,
            offers,
            selectedOfferIndex,
            selectedReplacementSlot,
            inventory,
            coinWallet.Coins,
            true);
    }

    private void ConfirmReplacement()
    {
        if (selectedOfferIndex < 0)
            return;

        if (selectedReplacementSlot < 0)
            return;

        PerkOffer selectedOffer =
            offers[selectedOfferIndex];

        if (selectedOffer == null)
            return;

        bool success = inventory.Replace(
            selectedReplacementSlot,
            selectedOffer.definition,
            selectedOffer.level);

        OnChanged.Invoke();

        if (success)
            Close();
    }

    public void Cancel()
    {
        if (CurrentState == SelectionState.Replacing)
        {
            CurrentState = SelectionState.Choosing;
            selectedReplacementSlot = -1;

            ui.Refresh(
                currentOrb,
                offers,
                selectedOfferIndex,
                selectedReplacementSlot,
                inventory,
                coinWallet.Coins,
                false);

            return;
        }

        Close();
    }

    private void Close()
    {
        Debug.Log("[Perk] Closing Panel");
        isPanelActive = false;

        CurrentState = SelectionState.Closed;

        currentOrb = null;
        selectedOfferIndex = -1;
        selectedReplacementSlot = -1;

        StartCoroutine(ui.Hide());

        InputSchemeManager.instance.OnInputActiveChange.Invoke();
    }

    private void GenerateOffers()
    {
        List<PerkCardDefinition> pool =
            new List<PerkCardDefinition>();

        foreach (PerkCardDefinition card
                 in currentOrb.CardPool)
        {
            if (card != null)
                pool.Add(card);
        }

        if (pool.Count == 0)
            return;

        List<PerkCardDefinition> picked =
            new List<PerkCardDefinition>();

        int count =
            Mathf.Min(3, offers.Length);

        for (int i = 0; i < count; i++)
        {
            List<PerkCardDefinition> candidates =
                new List<PerkCardDefinition>();

            foreach (PerkCardDefinition card in pool)
            {
                if (!currentOrb.AllowDuplicateOffers &&
                    picked.Contains(card))
                {
                    continue;
                }

                candidates.Add(card);
            }

            // If there are not enough unique cards,
            // fall back to the entire pool.
            if (candidates.Count == 0)
                candidates.AddRange(pool);

            PerkCardDefinition selected =
                candidates[
                    UnityEngine.Random.Range(
                        0,
                        candidates.Count)
                ];

            int level =
                selected.Level;

            offers[i] =
                new PerkOffer(selected, level);

            picked.Add(selected);
        }
    }
}