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
    BigFoot
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

    public string ID => id;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int Level => level;
}