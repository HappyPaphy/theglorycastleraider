using System;
using System.Collections.Generic;
using UnityEngine;

public enum PerkType
{
    BloodLust,
    Executioner,
    IronSkin,
    SecondWind,
    GreedyRaider,
    ParryEncounter,
    ShieldOfAsh,
    SwordTime,
    VampiricGene,
    BigFoot,
    // "Move changer" perks: they change what a kick / parry / broken furniture does, not just a number.
    // Always add new values at the end so existing card assets keep their serialized value.
    WardensBoot,
    RiposteEcho,
    SplinterStorm,
    HeadsmansToll,
    GlassJaw,
    GreasedSlide,
    EmberGuard,
    LastStand,
    BatteringRam
}

[CreateAssetMenu(
    fileName = "PerkCard_",
    menuName = "Perk/Perk Card"
)]
public class PerkCardDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private PerkType perkStat;
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private int level;
    [SerializeField] private OrbTier tier;

    public PerkType PerkStat => perkStat;
    public string ID => id;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int Level => level;
    public OrbTier Tier => tier;
}