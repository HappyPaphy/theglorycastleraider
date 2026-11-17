using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.UI;

public enum WeaponCategory
{
    None,
    Melee,
    Shield,
    Bow,
    PyromancyFlame,
    SorceryCatalyst
}

public enum MeleeType
{
    None,
    ShortSword,
    Dagger,
    Spear
}

public enum ShieldType
{
    None,
    WoodenShield
}

public enum BowType
{
    None,
    WoodenBow
}
public enum PyromancyFlameType
{
    None,
    DimmedHand
}
public enum SorceryCatalystType
{
    None,
    WoodenStaff
}

public class Weapon : Item
{
    public DamageImpactSound damageImpactSound;
    public WeaponCategory weaponCategory;
    public MeleeType meleeType;
    public ShieldType shieldType;
    public BowType bowType;
    public PyromancyFlameType pyromancyType;
    public SorceryCatalystType sorceryType;

    public float damageKnockbackForce = 8f;
    public float damage = 0f;
    public float attackCooldown = 0.6f;
    public float staminaCost = 10f;
    public float manaCost = 10f;
    public float blockStaminaDamageModifier = 1f;
    [Tooltip("Can this weapon be held with both hands (two-handing)? Off for small weapons like daggers.")]
    public bool canTwoHand = true;
    [Tooltip("The Block button only parries: a tap opens the parry window, holding does not block. For daggers.")]
    public bool parryOnly = false;
    [Tooltip("Parry-only weapons: seconds before the next parry.")]
    [Min(0f)] public float parryCooldown = 0.6f;
    [Tooltip("Can only block / parry while two-handed (e.g. spear). One-handed, the Block button does nothing for it.")]
    public bool blockOnlyWhenTwoHanded = false;

    [Header("Attack Profile (melee)")]
    [Tooltip("Attacks in the combo before it starts over: 2 = Press1, Press2. 3 = Press1, Press2, Press3.")]
    [Range(1, 3)] public int comboLength = 2;
    [Tooltip("Reach compared to the default melee reach (1.5 = 50% further).")]
    [Min(0.1f)] public float attackRangeMultiplier = 1f;
    [Tooltip("Width of the swing/thrust. 0 = the default width. Small = a narrow thrust.")]
    [Min(0f)] public float attackRadiusOverride = 0f;
    [Tooltip("Most enemies one attack can hit (nearest first). 0 = no limit.")]
    [Min(0)] public int maxTargets = 0;
    [Tooltip("Enemies closer than this (metres) take reduced damage, e.g. a spear point is wasted up close. 0 = off.")]
    [Min(0f)] public float closeRangePenaltyDistance = 0f;
    [Range(0f, 1f)] public float closeRangeDamageMultiplier = 1f;

    [Header("Two-Handed")]
    [Tooltip("Damage while two-handing this weapon (1.35 = 35% more).")]
    [Min(0f)] public float twoHandedDamageMultiplier = 1f;
    [Tooltip("Attack cooldown while two-handing (1.2 = 20% slower).")]
    [Min(0.1f)] public float twoHandedCooldownMultiplier = 1f;
    public AudioClip[] audioClips_BlockSound;
    public AudioClip[] audioClips_ParrySound;

    [Header("Visuals")]
    public Sprite spr_Weapon;
    public RuntimeAnimatorController animController_OneHanded;
    public RuntimeAnimatorController animController_TwoHanded;

    private void OnEnable()
    {

    }

    private void OnDisable()
    {

    }

    protected override void Start()
    {
        isCollected = false;
        base.Start();
    }

    protected override void Update()
    {
        base.Update();
    }

    protected override void Collecting()
    {
        // 1. Try to find an empty Right Hand slot first
        lastCollectSucceeded = false;

        if (InventoryManager.instance.TryAddWeapon(this))
        {
            lastCollectSucceeded = true;

            // 2. Hide the object from the world since it is now safely stored
            if (obj_RotateObject != null) obj_RotateObject.SetActive(false);
            if (uiButtonPrompt != null) uiButtonPrompt.SetActive(false);
            isCollected = true;
            Collected();
        }
        else
        {
            Debug.Log("You cannot carry any more of this specific weapon type!");
            // Leave it on the ground if the player already has 2 of them
        }
    }

    private void Collected()
    {
        if (obj_RotateObject != null) obj_RotateObject.SetActive(false);
        if (uiButtonPrompt != null) uiButtonPrompt.SetActive(false);
        isCollected = true;

        bool isAutomaticallyEquipped = false;

        if(EquipmentLoadOut.instance != null && PlayerWeaponManager.instance != null)
        {
            for(int i = 0; i< PlayerWeaponManager.instance.rightHandWeapons.Length; i++)
            {
                if (PlayerWeaponManager.instance.rightHandWeapons[i] == null)
                {
                    isAutomaticallyEquipped = true;
                    EquipmentLoadOut.instance.pendingSlotType = EquipmentSlotType.RightHand;
                    EquipmentLoadOut.instance.pendingSlotIndex = i;
                    EquipmentLoadOut.instance.EquipSelectedItem(this, false);
                    break;
                }

                if (PlayerWeaponManager.instance.leftHandWeapons[i] == null)
                {
                    isAutomaticallyEquipped = true;
                    EquipmentLoadOut.instance.pendingSlotType = EquipmentSlotType.LeftHand;
                    EquipmentLoadOut.instance.pendingSlotIndex = i;
                    EquipmentLoadOut.instance.EquipSelectedItem(this, false);
                    break;
                }
            }
        }
    }
}
