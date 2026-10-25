using System.Collections.Generic;
using UnityEngine;

public enum OrbTier
{
    Common,
    Rare,
    Unique
}

[CreateAssetMenu(
    fileName = "EnergyOrb_",
    menuName = "Perk/Energy Orb"
)]
public class EnergyOrbDefinition : ScriptableObject
{
    [Header("Orb")]
    [SerializeField] private OrbTier tier;

    [Header("Shuffle")]
    [SerializeField] private int shuffleCost = 20;

    [Header("Card Offers")]
    [SerializeField] private int offerCount = 3;

    [Header("Card Level")]
    [SerializeField] private int minCardLevel = 1;
    [SerializeField] private int maxCardLevel = 3;

    [Header("Available Cards")]
    [SerializeField]
    private List<PerkCardDefinition> cardPool =
        new List<PerkCardDefinition>();

    [Header("Rules")]
    [SerializeField] private bool allowDuplicateOffers = false;

    public OrbTier Tier => tier;
    public int ShuffleCost => shuffleCost;
    public int OfferCount => offerCount;

    public int MinCardLevel => minCardLevel;
    public int MaxCardLevel => maxCardLevel;

    public IReadOnlyList<PerkCardDefinition> CardPool => cardPool;

    public bool AllowDuplicateOffers => allowDuplicateOffers;
}