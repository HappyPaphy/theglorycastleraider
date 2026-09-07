using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    ShortSword
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
        if (InventoryManager.instance.TryAddWeapon(this))
        {
            // 2. Hide the object from the world since it is now safely stored
            if (obj_RotateObject != null) obj_RotateObject.SetActive(false);
            uiButtonPrompt.SetActive(false);
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
        obj_RotateObject.SetActive(false);
        uiButtonPrompt.SetActive(false);
        isCollected = true;
    }
}
