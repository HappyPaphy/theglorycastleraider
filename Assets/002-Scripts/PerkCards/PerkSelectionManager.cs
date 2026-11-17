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

    /// <summary>Level the player already has of this perk (0 = a new perk). Above 0 the offer upgrades that slot.</summary>
    public int upgradeFromLevel;
    public bool IsUpgrade => upgradeFromLevel > 0;

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

    [Header("All Perk Cards")]
    [Tooltip("Every perk card (all levels). Used to find the next level when a card upgrades a perk you own. " +
             "Filled by Tools > Perks > Fill Perk Card List.")]
    [SerializeField] private List<PerkCardDefinition> allCards = new List<PerkCardDefinition>();

    [Header("Testing")]
    [SerializeField] private EnergyOrbDefinition commonOrb;
    [SerializeField] private EnergyOrbDefinition rareOrb;
    [SerializeField] private EnergyOrbDefinition uniqueOrb;

    private EnergyOrbDefinition currentOrb;

    // The world orb the panel was opened from (null when opened by the test keys)
    private EnergyOrbPickup currentOrbPickup;

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
        if(instance == null)
            instance = this;
    }

    private void Start()
    {
        if(coinWallet == null)
        {
            coinWallet = PlayerController.instance.coinWallet;
        }

        Close();
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

        // Only while open: closing an already closed panel would reset Time.timeScale to 1
        // (e.g. while the pause menu or a tutorial popup is using Cancel)
        if (CurrentState != SelectionState.Closed && InputSchemeManager.instance.IsCancelPressed)
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

    /// <summary>Opens from a world Energy Orb. Reuses the cards already rolled for that orb.</summary>
    public void Open(EnergyOrbPickup orbPickup)
    {
        if (orbPickup == null || orbPickup.Definition == null)
            return;

        Open(orbPickup.Definition, orbPickup);
    }

    public void Open(EnergyOrbDefinition orb)
    {
        Open(orb, null);
    }

    private void Open(EnergyOrbDefinition orb, EnergyOrbPickup orbPickup)
    {
        if (orb == null)
            return;

        if (isPanelActive)
            return;

        currentOrb = orb;
        currentOrbPickup = orbPickup;
        isPanelActive = true;

        selectedOfferIndex = -1;
        selectedReplacementSlot = -1;

        PerkOffer[] savedOffers = orbPickup != null ? orbPickup.GetSavedOffers() : null;

        if (savedOffers != null && savedOffers.Length == offers.Length)
        {
            // Same orb again: show the same set until it's shuffled
            offers = savedOffers;
        }
        else
        {
            GenerateOffers();
        }

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

        // Already own this perk: upgrade that slot instead of taking a new one
        int ownedSlot = inventory.FindSlotWithPerk(selectedOffer.definition.PerkStat);
        if (ownedSlot >= 0)
        {
            int ownedLevel = inventory.GetSlot(ownedSlot).level;
            PerkCardDefinition upgraded = selectedOffer.definition;

            // Offer was rolled before you got the perk elsewhere: still give the next level
            if (selectedOffer.level <= ownedLevel)
            {
                upgraded = GetCard(selectedOffer.definition.PerkStat, ownedLevel + 1);
            }

            if (upgraded != null)
            {
                inventory.Replace(ownedSlot, upgraded, upgraded.Level);
                OnChanged?.Invoke();
            }

            ConsumeCurrentOrb();
            Close();
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

            ConsumeCurrentOrb();
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
        {
            ConsumeCurrentOrb();
            Close();
        }
    }

    /// <summary>The card for this perk at this level, or null if there's no such card.</summary>
    public PerkCardDefinition GetCard(PerkType perkType, int level)
    {
        foreach (PerkCardDefinition card in allCards)
        {
            if (card != null && card.PerkStat == perkType && card.Level == level)
                return card;
        }
        return null;
    }

    /// <summary>Tell listeners (perk stats, inventory UI) the inventory changed, e.g. after a slot unlock.</summary>
    public void NotifyChanged()
    {
        OnChanged?.Invoke();
    }

    private void ConsumeCurrentOrb()
    {
        if (currentOrbPickup != null)
        {
            currentOrbPickup.Consume();
            currentOrbPickup = null;
        }
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

    public void Close()
    {
        Debug.Log("[Perk] Closing Panel");
        isPanelActive = false;

        CurrentState = SelectionState.Closed;

        currentOrb = null;
        currentOrbPickup = null; // cancelled: the orb stays in the world with its saved cards
        selectedOfferIndex = -1;
        selectedReplacementSlot = -1;

        StartCoroutine(ui.Hide());

        if (InputSchemeManager.instance != null)
        {
            InputSchemeManager.instance.OnInputActiveChange.Invoke();
        }
    }

    private void GenerateOffers()
    {
        // Build the candidates: perks you don't own as they are; perks you own become their next level
        // (an UPGRADE offer). Maxed perks, and upgrades above this orb's tier, are left out.
        List<PerkOffer> candidates = new List<PerkOffer>();
        HashSet<PerkCardDefinition> seen = new HashSet<PerkCardDefinition>();

        foreach (PerkCardDefinition card in currentOrb.CardPool)
        {
            if (card == null)
                continue;

            int owned = inventory.GetOwnedLevel(card.PerkStat);
            PerkCardDefinition offerCard = card;

            if (owned > 0)
            {
                offerCard = GetCard(card.PerkStat, owned + 1);
                if (offerCard == null || offerCard.Tier > currentOrb.Tier)
                    continue;
            }

            if (!seen.Add(offerCard))
                continue;

            candidates.Add(new PerkOffer(offerCard, offerCard.Level) { upgradeFromLevel = owned });
        }

        // Nothing left (everything maxed): fall back to the plain pool so the orb still shows cards
        if (candidates.Count == 0)
        {
            foreach (PerkCardDefinition card in currentOrb.CardPool)
            {
                if (card != null && seen.Add(card))
                    candidates.Add(new PerkOffer(card, card.Level));
            }
        }

        if (candidates.Count == 0)
            return;

        int count = Mathf.Min(3, offers.Length);
        List<PerkOffer> remaining = new List<PerkOffer>(candidates);

        for (int i = 0; i < count; i++)
        {
            if (remaining.Count == 0)
            {
                // Fewer candidates than slots: repeat only if this orb allows duplicates
                if (!currentOrb.AllowDuplicateOffers) { offers[i] = null; continue; }
                remaining.AddRange(candidates);
            }

            int index = UnityEngine.Random.Range(0, remaining.Count);
            PerkOffer pick = remaining[index];
            offers[i] = new PerkOffer(pick.definition, pick.level) { upgradeFromLevel = pick.upgradeFromLevel };

            if (!currentOrb.AllowDuplicateOffers)
                remaining.RemoveAt(index);
        }

        // Remember this roll on the world orb so reopening shows the same cards
        if (currentOrbPickup != null)
            currentOrbPickup.SaveOffers(offers);
    }
}