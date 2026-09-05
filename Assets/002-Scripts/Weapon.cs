using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.UI;

public enum WeaponCategory
{
    Melee,
    Shield,
    Bow,
    PyromancyFlame,
    SorceryCatalyst
}

public enum MeleeType
{
    ShortSword
}

public enum ShieldType
{
    WoodenShield
}

public enum BowType
{
    WoodenBow
}

public class Weapon : Item
{
    public WeaponCategory weaponCategory;
    public MeleeType meleeType;
    public ShieldType shieldType;
    public BowType bowType;


    public float damage = 0f;
    public float attackCooldown = 0.6f;
    public float staminaCost = 10f;

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
        if(PlayerWeaponManager.instance.rightHandWeapon == null)
        {
            PlayerWeaponManager.instance.rightHandWeapon = this;
            PlayerWeaponManager.instance.EquipWeapon(this, false);
            Collected();
        }
        else if(PlayerWeaponManager.instance.leftHandWeapon == null)
        {
            PlayerWeaponManager.instance.leftHandWeapon = this;
            PlayerWeaponManager.instance.EquipWeapon(this, true);
            Collected();
        }
        else
        {
            //Open WeaponSwapUI
        }
    }

    private void Collected()
    {
        obj_RotateObject.SetActive(false);
        uiButtonPrompt.SetActive(false);
        isCollected = true;
    }
}
