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

public class Weapon : MonoBehaviour
{
    public WeaponCategory weaponCategory;
    public MeleeType meleeType;
    public ShieldType shieldType;
    public BowType bowType;

    public float damage = 0f;
    public float attackCooldown = 0.6f;
    public float staminaCost = 10f;

    [Header("Visuals")]
    public Sprite weaponIcon;

    private void OnEnable()
    {

    }

    private void OnDisable()
    {

    }

    void Start()
    {
        
    }

    void Update()
    {

    }
}
