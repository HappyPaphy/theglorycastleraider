using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.UI;

public enum MeleeState
{
    Melee1,
    Melee2
}
public class Weapon : MonoBehaviour
{
    [Header("ItemProperty")]
    //[SerializeField] private EquippableItemSO weapon;
    //[SerializeField] private InventorySO inventoryHelper;
    //[SerializeField] private List<ItemParameter> parametersToModify, itemCurrentState;

    [Header("Components")]
    public SpriteRenderer weaponSprRndr;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private CircleCollider2D col;
    [SerializeField] private BoxCollider2D hitBoxWeaponThrow;

    //public List<WeaponModification> modSlots = new List<WeaponModification>(3);

    public LocalizedString localizeString_WeaponName;
    public LocalizedString localizeString_WeaponDescription;
    //public WeaponCompatibility weaponCompatibility;
    public WeaponType weaponType;
    public MeleeWeaponType meleeWeaponType;
    public RangeWeaponType rangeWeaponType;
    public WeaponQuality weaponQuality;
    public WeaponSkillType weaponSkillType;
    public RangeWeaponBulletType bulletType;

    [Header("Materials")]
    [SerializeField] private Material defaultMat;
    [SerializeField] private Material shinyMat;

    [Header("WeaponStat_Gun")]
    public int maxAmmo;
    public int currentAmmo;
    public int currentAmmoInMagazine;
    public int magazineSize;
    //public int currentAmmo;
    public float bulletPercentage;
    public TokenStat overallTokenStat;
    public List<TokenColor> tokenColors;

    [Header("WeaponStat_Melee")]
    [SerializeField] private float nextMelee = 0.0f;
    [SerializeField] private float nextMeleeCombo = 0.0f;
    [SerializeField] public float attackComboCooldownDuration = 0.0f;
    [SerializeField] private MeleeState meleeState;

    [Header("WeaponStat_Other")]
    [SerializeField] public float attackCooldownDuration = 0f;
    [HideInInspector] public float bulletForce = 0f;
    [HideInInspector] public float meleeDamage = 0f;
    [HideInInspector] public float rangeDamage = 0f;
    [HideInInspector] public float reloadWaitTimeDuration = 0f;
    [HideInInspector] public float attackPrefabLifeTime = 0f;
    [HideInInspector] public float maxWeaponSway = 0f;
    [HideInInspector] public float weaponSwayValue = 0f;
    [HideInInspector] public float weaponSwayRecoverCooldown = 0f;
    [HideInInspector] public float falloffRange = 0f;
    [HideInInspector] public float criticalChance = 0f;
    [HideInInspector] public float criticalMultiplier = 0f;
    [HideInInspector] public float recoilValue = 0f;
    [HideInInspector] public int ammoPickupCount = 0;
    [HideInInspector] public float finalFireRate = 1f;
    [HideInInspector] public float finalFirePower = 1f;
    public float staminaCost = 18f;

    [Header("Other")]
    [SerializeField] private GameObject rangeUIGunInfoPrefab;
    [SerializeField] private GameObject meleeUIGunInfoPrefab;
    [SerializeField] private GameObject weaponPanelGroup;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private GameObject muzzleFlashPrefab;

    public Transform firePoint;

    private float nextAttack = 0f;
    [HideInInspector] public bool isReloading = false;
    [HideInInspector] public bool isReloadingBulletFinished = false;
    private int bulletReloadCount = 1;
    private float nextReload = 0f;
    [SerializeField] private float currentWeaponSway = 0f;
    private float nextWeaponSway = 0f;
    private bool isPlayerInRange = false;

    [HideInInspector] public bool isWeaponActive = false;
    public bool isWeaponBeingPickUp = false;

    public bool isWeaponBeingPickUpAtStart = false;
    public bool isIntializeWeaponStatAtStart = true;
    public bool isThisMeleeWeapon = false;
    [SerializeField] private GameObject spriteKeyboard_E;
    [SerializeField] private GameObject text_CollectItem;

    [SerializeField] private Slider slider_Overheat;
    [SerializeField] private GameObject gameObject_Overheat;
    [SerializeField] private float currentValue_Overheat;
    [SerializeField] private float maxValue_Overheat;
    [SerializeField] private bool isOverHeat = false;

    /*[SerializeField] private float showWeaponTransformX;
    [SerializeField] private float hideWeaponTransformX;*/

    private bool isThrowed = false;
    private bool isSubscribed = false;

    [SerializeField] private bool isCanBeThrowed = false;
    [SerializeField] private GameObject tipFirePoint;
    [SerializeField] private BulletStat bulletStat;

    public int weaponLeafIndex = 0;

    private void OnEnable()
    {
        SubscribeInputs();
    }

    private void OnDisable()
    {
        UnsubscribeInputs();
    }

    private void SubscribeInputs()
    {
        if (PlayerController.instance == null || isSubscribed) return;

        if (weaponType == WeaponType.Melee)
        {
            
            isSubscribed = true;
        }
        else if (weaponType == WeaponType.Range)
        {
            isSubscribed = true;
        }
    }

    private void UnsubscribeInputs()
    {
        if (PlayerController.instance == null || !isSubscribed) return;

        if (weaponType == WeaponType.Melee)
        {
            isSubscribed = false;
        }
        else if (weaponType == WeaponType.Range)
        {
            isSubscribed = false;
        }
    }

    private void OnRangeAttackPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is first pressed down
    }

    private void OnRangeAttackCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is released
    }

    private void OnMeleeAttackPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is first pressed down
    }

    private void OnMeleeAttackCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is released
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        WeaponManager.instance.AddToWeaponManager(this);

        if (isIntializeWeaponStatAtStart)
        {
            WeaponManager.instance.IntializeWeaponStat(this);
        }

        /*if(weaponType == WeaponType.Range)
        modSlots[0].CreateRandomModification();*/
        
        if (spriteKeyboard_E != null)
        {
            spriteKeyboard_E.SetActive(false);
        }
        if (spriteKeyboard_E != null)
        {
            text_CollectItem.SetActive(false);
        }

        /*if(rangeWeaponType == RangeWeaponType.Gatling)
        {
            gameObject_Overheat.SetActive(false);
            slider_Overheat.maxValue = maxValue_Overheat;
        }*/

        currentAmmoInMagazine = magazineSize;
        currentAmmo = maxAmmo;
    }

    void Update()
    {
        //CalculateFinalFireRate();

        if (weaponType == WeaponType.Range)
        {
            //CalculateFinalFirePower();
        }

        recoilValue = weaponSwayValue * 1 / finalFireRate;

        if (!isSubscribed)
        {
            SubscribeInputs();
        }

        /*if (isWeaponBeingPickUpAtStart && !PlayerController.instance.isCutSceneOn)
        {
            isWeaponBeingPickUpAtStart = false;
            //PlayerController_New.instance.AddWeapon(this);

            if (weaponType == WeaponType.Melee)
            {
                PlayerController_New.instance.AddMeleeWeapon(this);
            }
            *//*else if (weaponType == WeaponType.Range)
            {
                WeaponShortcutUI.instance.CheckWeaponLeafUI(this);
            }*//*

            PickUpWeapon();
            isWeaponBeingPickUp = true;
        }

        if (GameManagerSpecialMode.instance != null && rangeWeaponType == RangeWeaponType.Gatling)
        {
            if(GameManager.instance.powerUpIndex_FireRate > 0)
            maxValue_Overheat = 100f + (GameManager.instance.powerUpIndex_FireRate * 20f);
        }*/

        
        

        if((isWeaponActive || !isWeaponBeingPickUp) && !isThisMeleeWeapon)
        {
            if (weaponType == WeaponType.Range)
            {
                /*if (PlayerController_New.instance.isAttackingMelee || PlayerController_New.instance.isExecuting)
                {
                    weaponSprRndr.enabled = false;
                }
                else
                {
                    weaponSprRndr.enabled = true;
                }*/
            }

            if (rangeWeaponType == RangeWeaponType.Gatling && (isWeaponActive) && !isThisMeleeWeapon)
            {
                gameObject_Overheat.SetActive(true);
            }
        }
        else if(!isWeaponBeingPickUp && isThisMeleeWeapon)
        {
            weaponSprRndr.enabled = true;
        }
        else
        {
            weaponSprRndr.enabled = false;

            if (rangeWeaponType == RangeWeaponType.Gatling)
            {
                gameObject_Overheat.SetActive(false);
            }
        }

        /*if (Time.time >= nextMelee && weaponType == WeaponType.Melee)
        {
            PlayerController_New.instance.isAttackingMelee = false;
        }

        if(weaponType == WeaponType.Melee && isWeaponBeingPickUp && PlayerController_New.instance.CharacterHealthComponent.CurrentHP > 0)
        {
            transform.position = PlayerController_New.instance.weaponPos.position;
            HandleMeleeWeaponType();
        }*/

        /*if (weaponType == WeaponType.Range && isWeaponActive && isWeaponBeingPickUp && PlayerController_New.instance.CharacterHealthComponent.CurrentHP > 0 && !PlayerController_New.instance.isCutSceneOn)
        {
            CheckReloadWeapon();
            HandleRangeWeaponType();
            //HandleWeaponReload();
        }*/

        /*if (isWeaponBeingPickUp && PlayerController_New.instance.CharacterHealthComponent.CurrentHP > 0 && !PlayerController_New.instance.isCutSceneOn)
        {
            HandleWeaponReload();
        }*/

        if (isWeaponBeingPickUp)
        {
            if (WeaponManager.instance.weaponsInPlayerRange.Contains(this))
            {
                WeaponManager.instance.weaponsInPlayerRange.Remove(this);
            }

            weaponSprRndr.sortingLayerName = "Character";
            weaponSprRndr.sortingOrder = 2;
            weaponSprRndr.material = defaultMat;
            col.enabled = false;
        }
        else if(!isWeaponBeingPickUp && !isThrowed)
        {
            weaponSprRndr.sortingLayerName = "Item";
            weaponSprRndr.sortingOrder = 0;
            weaponSprRndr.material = shinyMat;
            col.enabled = true;
        }

        /*if(Input.GetKeyDown(KeyCode.G) && isWeaponActive)
        {
            gameObject.transform.rotation = Quaternion.Euler(0, 0, 0);
            isWeaponBeingPickUp = false;
            isWeaponActive = false;
            PlayerController_New.instance.RemoveWeapon(this);
            RemoveWeaponUI();
        }*/

        if(isThrowed)
        {
            weaponSprRndr.transform.Rotate(0, 0, -20f);
        }

        /*if ((Input.GetKeyDown(KeyCode.LeftControl) && weaponType == WeaponType.Range) ||
            (bagAmmo <= 0 && currentAmmo <= 0 && !isReloading && PlayerController_New.instance.isRangeAttackHeld))
        {
            if(GameManagerSpecialMode.instance == null) { return; }

            if(!isCanBeThrowed) { return; }
            if(!isWeaponActive) { return; }

            isThrowed = true;
            hitBoxWeaponThrow.enabled = true;

            gameObject.transform.position = firePoint.transform.position;
            gameObject.transform.rotation = firePoint.transform.rotation * Quaternion.Euler(0, 0, 0);

            SoundManager.instance.ThrowWeaponSound(transform.position);

            Rigidbody2D rb = gameObject.GetComponent<Rigidbody2D>();
            rb.AddForce(gameObject.transform.up * 20f, ForceMode2D.Impulse);

            gameObject.transform.rotation = Quaternion.Euler(0, 0, 0);
            isWeaponBeingPickUp = false;
            isWeaponActive = false;
            PlayerController_New.instance.RemoveWeapon(this);
            RemoveWeaponUI();

            Invoke("ResetHitBoxCollider", 1.3f);
        }*/

        if(isPlayerInRange && !isThrowed)
        {
            if(PlayerController.instance.IsInteractPressed)
            {
                PlayerController.instance.IsInteractPressed = false;

                hitBoxWeaponThrow.enabled = false;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;

                PickUpWeapon();
            }
        }

        bulletStat = WeaponManager.instance.bulletStat;

        /*if (rangeWeaponType == RangeWeaponType.Gatling)
        {
            if(currentValue_Overheat > 0)
            {
                currentValue_Overheat -= Time.deltaTime * 5;
            }
            else if(currentValue_Overheat < 0)
            {
                currentValue_Overheat = 0;
            }
            
            *//*slider_Overheat.value = currentValue_Overheat;
            gameObject_Overheat.transform.position = PlayerController_New.instance.transform.position + new Vector3(0f, -1f, 0f);
            gameObject_Overheat.transform.rotation = PlayerController_New.instance.transform.rotation;*//*
        }*/
    }

    /*private void CalculateFinalFireRate()
    {
        if (weaponType == WeaponType.Range)
        {
            finalFireRate = attackCooldownDuration * EquipmentManager.instance.totalMultiplier_WeaponAttackSpeed * WeaponManager.instance.rapidFire_CurFireRate * WeaponManager.instance.executeHoodEffect_AttackSpeedMultiplier;

            if (UpgradeCharacterCardManager.instance != null)
            {
                finalFireRate *= UpgradeCharacterCardManager.instance.playerFireRate;
            }

            if (IsModPowerValid(ModifierType.FastFinger))
            {
                switch (GetModQuality(ModifierType.FastFinger))
                {
                    case ModificationQuality.B: finalFireRate *= 0.75f; break;
                    case ModificationQuality.A: finalFireRate *= 0.65f; break;
                    case ModificationQuality.S: finalFireRate *= 0.55f; break;
                }
            }

            if (IsModPowerValid(ModifierType.HardPunch))
            {
                finalFireRate *= 1.15f;
            }
        }
        else if(weaponType == WeaponType.Melee)
        {
            finalFireRate = attackCooldownDuration * EquipmentManager.instance.totalMultiplier_MeleeAttackSpeed * WeaponManager.instance.executeHoodEffect_AttackSpeedMultiplier*//* * PlayerController_New.instance.parryEffect_FireRateUp*//*;

            if(UpgradeCharacterCardManager.instance != null)
            {
                finalFireRate *= UpgradeCharacterCardManager.instance.playerMeleeAttackSpeed;
            }
        }
    }

    private void CalculateFinalFirePower()
    {
        finalFirePower = rangeDamage * EquipmentManager.instance.totalMultiplier_WeaponAttackPower; ;

        if (UpgradeCharacterCardManager.instance != null)
        {
            finalFirePower *= UpgradeCharacterCardManager.instance.playerFirePower;

            if (UpgradeCharacterCardManager.instance.isLastStandActive)
            {
                finalFirePower *= UpgradeCharacterCardManager.instance.lastStandDamage;
            }
        }

        if (IsModPowerValid(ModifierType.FastFinger))
        {
            finalFirePower *= 0.85f;
        }

        if (IsModPowerValid(ModifierType.HardPunch))
        {
            switch (GetModQuality(ModifierType.HardPunch))
            {
                case ModificationQuality.B: finalFirePower *= 1.25f; break;
                case ModificationQuality.A: finalFirePower *= 1.35f; break;
                case ModificationQuality.S: finalFirePower *= 1.45f; break;
            }
        }
    }*/

    private bool IsPlayerPreforming()
    {
        if (PlayerController.instance.isExecuting)
        {
            return true;
        }

        return false;
    }
    private void ResetHitBoxCollider()
    {
        Destroy(gameObject);
        col.enabled = false;
        hitBoxWeaponThrow.enabled = false;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    public void PickUpWeapon()
    {
        //SoundManager.instance.ChangeWeaponSound((int)weaponType);
        //PlayerController_New.instance.AddWeapon(this);
        InstantiateWeaponUI();

        if (weaponType == WeaponType.Melee)
        {
            isWeaponBeingPickUp = true;
            //PlayerController_New.instance.meleeWeapon = this;
        }
    }

    private void HandleMeleeWeaponType()
    {
        if(IsPlayerPreforming()) { return; }

        switch (meleeWeaponType)
        {
            case MeleeWeaponType.ShortSword:
                MeleeWeapon();
                break;
        }
    }

    private void DepleteAmmo(int value)
    {
        if(bulletType != RangeWeaponBulletType.BulletPistol)
        {
            currentAmmo -= value;
        }

        currentAmmoInMagazine -= value;

        if(currentAmmoInMagazine <= 0 && currentAmmo > 0)
        {
            ReloadMagazine();
        }

        //currentAmmo -= value;
        //StartCoroutine(UIRangeWeaponInfo.instance.rangeWeaponUI_Equipped.GameObjectFeedback(UIRangeWeaponInfo.instance.rangeWeaponUI_Equipped.gameObject, false, new Vector3(1.5f, 1.5f, 1.5f)));
    }

    private void HandleRangeWeaponType()
    {
        if (Time.time > nextWeaponSway)
        {
            if (currentWeaponSway > 0)
            {
                currentWeaponSway -= Time.deltaTime * 20f;
            }
            else
            {
                currentWeaponSway = 0;
            }
        }

        /*if (PlayerController.instance.isAttackingMelee)
        {
            return;
        }*/

        if (IsPlayerPreforming()) { return; }

        switch (rangeWeaponType)
        {
            case RangeWeaponType.Ak47: ShootAutomaticWeapon(); break;
            //case RangeWeaponType.PumpShotgun: ShootSingleShotgun(); break;
            case RangeWeaponType.Uzi: ShootAutomaticWeapon(); break;
            case RangeWeaponType.M16: ShootBurstWeapon(); break;
            case RangeWeaponType.Gatling: ShootMinigun(); break;
            case RangeWeaponType.P90: ShootAutomaticWeapon(); break;
            case RangeWeaponType.MP5: ShootAutomaticWeapon(); break;
            case RangeWeaponType.Scar: ShootAutomaticWeapon(); break;
            case RangeWeaponType.M16A4: ShootAutomaticWeapon(); break;
            //case RangeWeaponType.RiotShotgun: ShootSingleShotgun(); break;
            case RangeWeaponType.P1911: ShootSemiAutoWeapon(); break;
            case RangeWeaponType.DesertEagle: ShootSemiAutoWeapon(); break;
            case RangeWeaponType.Red9: ShootSemiAutoWeapon(); break;
            case RangeWeaponType.Revolver: ShootSemiAutoWeapon(); break;
            case RangeWeaponType.M1Garand: ShootSemiAutoWeapon(); break;
            case RangeWeaponType.VSS: ShootAutomaticWeapon(); break;
            //case RangeWeaponType.SawedOffShotgun: ShootSingleShotgun(); break;
            //case RangeWeaponType.AutoShotgun: ShootAutoShotgun(); break;

                /*case WeaponType.MeleeShovel: MeleeWeapon(); break;
                case WeaponType.MeleeFireAxe: MeleeWeapon(); break;
                case WeaponType.MeleeLightSaber: MeleeWeapon(); break;*/
        }
    }

    public void RefillHalfAmmo()
    {
        currentAmmo += maxAmmo / 2;

        if(currentAmmo >= maxAmmo)
        {
            currentAmmo = maxAmmo;
        }
    }

    public void FireWeapon()
    {
        // ... your firing logic ...

        /*List<WeaponModificationSlot> slots = WeaponShortcutUI.instance.GetSlotsForWeapon(WeaponWheelUI.instance.currentWeaponIndex);
        foreach (var slot in slots)
        {
            slot.OnShotFired();
        }*/
    }

    /*private void HandleWeaponReload()
    {
        switch (rangeWeaponType)
        {
            case RangeWeaponType.Ak47: ReloadMagazine(); break;
            case RangeWeaponType.PumpShotgun: ReloadBullet(); break;
            case RangeWeaponType.Uzi: ReloadMagazine(); break;
            case RangeWeaponType.M16: ReloadMagazine(); break;
            case RangeWeaponType.Gatling: ReloadMagazine(); break;
            case RangeWeaponType.P90: ReloadMagazine(); break;
            case RangeWeaponType.MP5: ReloadMagazine(); break;
            case RangeWeaponType.Scar: ReloadMagazine(); break;
            case RangeWeaponType.M16A4: ReloadMagazine(); break;
            case RangeWeaponType.RiotShotgun: ReloadBullet(); break;
            case RangeWeaponType.P1911: ReloadMagazine(); break;
            case RangeWeaponType.DesertEagle: ReloadMagazine(); break;
            case RangeWeaponType.Red9: ReloadMagazine(); break;
            case RangeWeaponType.Revolver: ReloadBullet(); break;
            case RangeWeaponType.M1Garand: ReloadMagazine(); break;
            case RangeWeaponType.VSS: ReloadMagazine(); break;
            case RangeWeaponType.SawedOffShotgun: ReloadSawedOffShotgun(); break;
            case RangeWeaponType.AutoShotgun: ReloadMagazine(); break;
        }
    }*/


    private void ShootAutomaticWeapon()
    {
        /*if (PlayerController.instance.isRangeAttackHeld && !isReloading && Time.time > nextAttack && !EventSystem.current.IsPointerOverGameObject())
        {
            nextAttack = Time.time + finalFireRate;

            if (currentAmmo > 0 && currentAmmoInMagazine > 0)
            {
                if (!WeaponManager.instance.bulletStat.isFreeBullet)
                {
                    DepleteAmmo(1);
                }

                //SoundManager.instance.GunFireSound(transform.position, (int)rangeWeaponType);
                BulletShotFired();
                GetAndAssignBulletAndMuzzleFlash();
            }
            else
            {
                if (currentAmmo > 0)
                {
                    ReloadMagazine();
                }
                else
                {
                    //SoundManager.instance.GunBulletRunOutSound(transform.position);
                }
            }
        }*/
    }

    private void AssignBulletProperty(GameObject bulletGameObject)
    {
        //BulletStat bulletStat = WeaponManager.instance.bulletStat;
        //BulletStat bulletStat = CheckWeaponMod();

        //Bullet bullet = bulletGameObject.GetComponent<Bullet>();

        /*bullet.bounceCount = 0;
        bullet.isBulletBouncing = bulletStat.isBulletBouncing;
        bullet.additionalBounceDamage = bulletStat.bulletBounceAdditionalDamage;
        bullet.maxBounceCount = bulletStat.maxBulletBounceCount;
        bullet.speed = bulletForce;
        bullet.isBulletStunt = bulletStat.isBulletStunt;

        int randomIndex = Random.Range(1, 100);

        if(randomIndex <= criticalChance + EquipmentManager.instance.totalMultiplier_WeaponCritChance)
        {
            finalFirePower *= criticalMultiplier * EquipmentManager.instance.totalMultiplier_WeaponCritDamage;
            bullet.bulletStatusIndex = 1;
        }

        bullet.damage = finalFirePower;
        bullet.spawnPosition = firePoint.position;
        bullet.falloffRange = falloffRange;
        bullet.fireDamage = bulletStat.fireDamage;
        bullet.fireDamageModifier = bulletStat.fireDamageModifier;
        bullet.fireDuration = bulletStat.fireDuration;
        bullet.isBulletOnFire = WeaponManager.instance.isBulletOnFire;
        bullet.bulletType = bulletType;

        bullet.hollowPointModifier = bulletStat.hollowPointModifier;
        bullet.pointBlankModifier = bulletStat.pointBlankModifier;

        bullet.piecingCount = 0;
        bullet.maxPiecingCount = bulletStat.maxBulletPiecingCount;

        bullet.chainReactionDamage = bulletStat.bulletChainReactionDamage;

        bullet.forceValue = bulletStat.forceValue;*/
    }


    private void ShootMinigun()
    {
        /*if (PlayerController.instance.isRangeAttackHeld && !isReloading && Time.time > nextAttack && *//*currentValue_Overheat < maxValue_Overheat && !isOverHeat &&*//* !EventSystem.current.IsPointerOverGameObject())
        {
            //currentValue_Overheat += 2;
            nextAttack = Time.time + finalFireRate;

            if (currentAmmo > 0 && currentAmmoInMagazine > 0)
            {
                //SoundManager.instance.GunFireSound(transform.position, (int)rangeWeaponType);
                StartCoroutine(BurstShotCoroutine());
            }
            else
            {
                if(currentAmmo > 0)
                {
                    ReloadMagazine();
                }
                else
                {
                    //SoundManager.instance.GunBulletRunOutSound(transform.position);
                }
            }

        }*/
        /*else if(currentValue_Overheat >= maxValue_Overheat)
        {
            isOverHeat = true;
        }
        else if(isOverHeat && currentValue_Overheat <= 0)
        {
            isOverHeat = false;
        }*/
    }

    private void ShootSemiAutoWeapon()
    {
        /*if (PlayerController.instance.isRangeAttackPressed && !isReloading && Time.time > nextAttack && !EventSystem.current.IsPointerOverGameObject())
        {
            PlayerController.instance.isRangeAttackPressed = false;
            nextAttack = Time.time + finalFireRate;

            if (currentAmmo > 0 && currentAmmoInMagazine > 0)
            {
                if (!WeaponManager.instance.bulletStat.isFreeBullet)
                {
                    DepleteAmmo(1);
                }

                if (currentAmmoInMagazine <= 0 && rangeWeaponType == RangeWeaponType.M1Garand)
                {
                    //SoundManager.instance.M1GarandPingSound(transform.position);
                }

                *//*if (IsModPowerValid(ModifierType.HollowPoint))
                {
                    PlayerController.instance.PlayerKnockbackEffect();
                }*//*

                //SoundManager.instance.GunFireSound(transform.position, (int)rangeWeaponType);
                BulletShotFired();
                GetAndAssignBulletAndMuzzleFlash();
            }
            else
            {
                if (currentAmmo > 0)
                {
                    ReloadMagazine();
                }
                else
                {
                    //SoundManager.instance.GunBulletRunOutSound(transform.position);
                }
            }
        }*/
    }

    

   

    private void ShootBurstWeapon()
    {
        /*if (PlayerController.instance.isRangeAttackHeld && !isReloading && Time.time > nextAttack && !EventSystem.current.IsPointerOverGameObject())
        {
            nextAttack = Time.time + finalFireRate;

            if (currentAmmo > 0 && currentAmmoInMagazine > 0)
            {
                //SoundManager.instance.GunFireSound(transform.position, (int)rangeWeaponType);
                StartCoroutine(BurstShotCoroutine());
            }
            else
            {
                if (currentAmmo > 0)
                {
                    ReloadMagazine();
                }
                else
                {
                    //SoundManager.instance.GunBulletRunOutSound(transform.position);
                }
            }
        }*/
    }

    private IEnumerator BurstShotCoroutine()
    {
        for (int i = 0; i < 3; i++)
        {
            if (currentAmmo > 0 && currentAmmoInMagazine > 0)
            {
                if (!WeaponManager.instance.bulletStat.isFreeBullet)
                {
                    DepleteAmmo(1);
                }

                BulletShotFired();
                GetAndAssignBulletAndMuzzleFlash();
                yield return new WaitForSeconds(0.05f);
            }
            else
            {
                break;
            }
        }
    }

    private void BulletShotFired()
    {
        //RumbleManager.instance.RumblePulse(1f, 1f, 0.1f);
        //PlayerController_New.instance.virtualCamBasicMultiChannelPerlin.m_FrequencyGain = 0.3f;
        //PlayerController_New.instance.virtualCamBasicMultiChannelPerlin.m_AmplitudeGain = 0.8f;
        Invoke("ResetShake", 0.15f);

        if (maxWeaponSway > currentWeaponSway)
            currentWeaponSway += weaponSwayValue;

        nextWeaponSway = Time.time + weaponSwayRecoverCooldown;
    }

    private void GetAndAssignBulletAndMuzzleFlash(bool isShotgun = false)
    {
        /*GameObject muzzleFlash = ObjectPool.instance.GetPooledMuzzleFlash();
        muzzleFlash.SetActive(true);
        muzzleFlash.GetComponent<DestroyGameObject>().PlayerAnimation();
        muzzleFlash.transform.position = firePoint.position;
        muzzleFlash.transform.rotation = firePoint.rotation * Quaternion.Euler(0, 0, 90);

        GameObject muzzleLight = ObjectPool.instance.GetPooledMuzzleLight();
        muzzleLight.SetActive(true);
        muzzleLight.transform.position = firePoint.position;
        muzzleLight.transform.rotation = firePoint.rotation;*/

        if (isShotgun)
        {
            ShotgunFire();
        }
        else
        {
            SingleBulletFire();
        }
    }

    private GameObject GetBulletType()
    {
        switch (bulletType)
        {
            /*case RangeWeaponBulletType.BulletPistol:
                return ObjectPool.instance.GetPooledBullet_Pistol();

            case RangeWeaponBulletType.BulletSMG:
                return ObjectPool.instance.GetPooledBullet_SMG();

            case RangeWeaponBulletType.BulletAR:
                return ObjectPool.instance.GetPooledBullet_AR();*/

            default:
                return null;
        }
    }

    private void SingleBulletFire()
    {
        GameObject bulletGameObject = GetBulletType();

        bulletGameObject.SetActive(true);
        bulletGameObject.transform.position = firePoint.transform.position;
        bulletGameObject.transform.rotation = firePoint.transform.rotation * Quaternion.Euler(0, 0, UnityEngine.Random.Range(-currentWeaponSway, currentWeaponSway));
        
        Rigidbody2D rb = bulletGameObject.GetComponent<Rigidbody2D>();
        rb.AddForce(bulletGameObject.transform.up * bulletForce, ForceMode2D.Impulse);
        AssignBulletProperty(bulletGameObject);
    }

    private void ShotgunFire()
    {
        /*for (int i = 1; i <= 3; i++)
        {
            float currentAngle = i * 3;

            GameObject bullet = ObjectPool.instance.GetPooledBullet_Pellet();
            bullet.SetActive(true);

            bullet.transform.position = firePoint.transform.position;
            bullet.transform.rotation = firePoint.transform.rotation * Quaternion.Euler(0, 0, currentAngle);

            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            rb.AddForce(bullet.transform.up * bulletForce, ForceMode2D.Impulse);
            AssignBulletProperty(bullet);
        }
        for (int i = -1; i >= -3; i--)
        {
            float currentAngle = i * 3;

            GameObject bullet = ObjectPool.instance.GetPooledBullet_Pellet();
            bullet.SetActive(true);

            bullet.transform.position = firePoint.transform.position;
            bullet.transform.rotation = firePoint.transform.rotation * Quaternion.Euler(0, 0, currentAngle);

            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            rb.AddForce(bullet.transform.up * bulletForce, ForceMode2D.Impulse);
            AssignBulletProperty(bullet);
        }*/
    }

    

    private void MeleeWeapon()
    {
        /*if (PlayerController_New.instance.isMeleeAttackPressed && Time.time > nextMelee)
        {
            if(PlayerController_New.instance.CheckIfStuntedEnemyNearbyForKick()) { return; }
            if(!PlayerController_New.instance.IsStaminaEnoughForMelee()) { return; }

            WeaponManager.instance.MeleeFeedback();

            PlayerController_New.instance.isMeleeAttackPressed = false;
            PlayerController_New.instance.isAttackingMelee = true;
            nextMelee = Time.time + finalFireRate;
            nextMeleeCombo = Time.time + finalFireRate;

            RumbleManager.instance.RumblePulse(1f, 1f, 0.1f);
            SoundManager.instance.SwordSoundWind();
            GameObject swingMelee = Instantiate(attackPrefab, firePoint.position, firePoint.rotation);

            float finalMeleePower = meleeDamage * EquipmentManager.instance.totalMultiplier_MeleeAttackPower;

            if(UpgradeCharacterCardManager.instance != null )
            {
                finalMeleePower *= UpgradeCharacterCardManager.instance.playerMeleeAttackPower;

                if(UpgradeCharacterCardManager.instance.isLastStandActive)
                {
                    finalMeleePower *= UpgradeCharacterCardManager.instance.lastStandDamage;
                }
            }

            swingMelee.GetComponent<SwingEffect>().damage = finalMeleePower;
            swingMelee.GetComponent<SwingEffect>().gameObjectLifeTime = attackPrefabLifeTime;
            BoxCollider2D box2D = swingMelee.GetComponent<BoxCollider2D>();
            //Debug.LogError($"swingPrefab : {swingMelee.name}, damage : {damage}, lifeTime : {attackPrefabLifeTime}");

            //StartCoroutine(uiGunInfo.GameObjectFeedback(uiGunInfo.gameObject, false));

            if (Time.time < nextMeleeCombo)
            {
                if (meleeState == MeleeState.Melee1)
                {
                    meleeState = MeleeState.Melee2;
                    PlayerController_New.instance.currentState = PlayerState.Melee2;
                }
                else if (meleeState == MeleeState.Melee2)
                {
                    meleeState = MeleeState.Melee1;
                    PlayerController_New.instance.currentState = PlayerState.Melee1;
                }

                *//*if (PlayerController_New.instance.currentState == PlayerState.Melee1)
                {
                    PlayerController_New.instance.currentState = PlayerState.Melee2;
                }
                else if (PlayerController_New.instance.currentState == PlayerState.Melee2)
                {
                    PlayerController_New.instance.currentState = PlayerState.Melee3;
                }
                else
                {
                    PlayerController_New.instance.currentState = PlayerState.Melee1;
                }*//*
            }
            
            if (Time.time >= nextMeleeCombo)
            {
                PlayerController_New.instance.isAttackingMelee = false;
            }
        }*/
    }

    private void ReloadMagazine()
    {
       /* StartCoroutine(ReloadQTE.instance.StartReload(this));
        PlayerController_New.instance.isReloading = true;
        isReloading = true;
        nextReload = Time.time + reloadWaitTimeDuration * WeaponManager.instance.reloadSpeedMultiplier;
        SoundManager.instance.ReloadGunSound(transform.position, (int)rangeWeaponType, true, WeaponManager.instance.reloadSpeedMultiplier);*/
    }

    public void QTEReloadSuccess()
    {
        /*if (EquipmentManager.instance.CheckIfequipmentTypeExist(EquipmentType.TriggerFingerNecklace) > 0)
        {
            WeaponManager.instance.rapidFire_FireRateUp = 0.5f;
            WeaponManager.instance.rapidFire_Duration = 3f;
            WeaponManager.instance.ActivateRapidFireSkill();
        }
        else if(EquipmentManager.instance.CheckIfequipmentTypeExist(EquipmentType.ShockWavePendant) > 0)
        {

        }

        SoundManager.instance.LevelUpSound();
        ReloadSuccess();*/
    }

    public void ReloadSuccess()
    {
        if (currentAmmo >= magazineSize)
        {
            currentAmmoInMagazine = magazineSize;
        }
        else
        {
            currentAmmoInMagazine = currentAmmo;
        }

        //SoundManager.instance.ReloadGunSound(transform.position, (int)rangeWeaponType, false, WeaponManager.instance.reloadSpeedMultiplier);
        //StartCoroutine(UIRangeWeaponInfo.instance.rangeWeaponUI_Equipped.GameObjectFeedback(UIRangeWeaponInfo.instance.rangeWeaponUI_Equipped.gameObject, true, new Vector3(1.5f, 1.5f, 1.5f)));
        isReloading = false;
    }

    private void CheckReloadWeapon()
    {
        /*if (PlayerController.instance.isReloading)
        {
            if (isReloading && Time.time >= nextReload)
            {
                ReloadSuccess();
            }
        }
        else
        {
            isReloading = false;
        }*/
        
    }

    /*private void ReloadMagazine_Normal()
    {
        if (PlayerController_New.instance.IsReloadPressed && bagAmmo > 0 && currentAmmo < maxAmmo
                && !isReloading && Time.time > nextAttack)
        {
            PlayerController_New.instance.IsReloadPressed = false;
            ammoReloadCount = 0;
            PerformReload();

            while (bagAmmo > 0)
            {
                if (bagAmmo <= 0 || ammoReloadCount >= maxAmmo - currentAmmo)
                {
                    break;
                }

                ammoReloadCount++;
            }
        }
        else if (PlayerController_New.instance.isRangeAttackHeld && bagAmmo > 0 && currentAmmo <= 0
                && !isReloading)
        {
            ammoReloadCount = 0;
            PerformReload();

            while (bagAmmo > 0)
            {
                if (bagAmmo <= 0 || ammoReloadCount >= maxAmmo - currentAmmo)
                {
                    break;
                }

                ammoReloadCount++;
            }
        }
    }*/


    /*private void ReloadBullet_Normal()
    {
        if ((PlayerController_New.instance.isRangeAttackPressed || PlayerController_New.instance.isRangeAttackHeld) && !isReloadingBulletFinished)
        {
            isReloadingBulletFinished = true;
        }

        if (PlayerController_New.instance.IsReloadPressed && bagAmmo > 0 && currentAmmo < maxAmmo
                && !isReloading && isReloadingBulletFinished && Time.time > nextAttack)
        {
            isReloadingBulletFinished = false;
            int reloadBullets = 0;

            while (!isReloadingBulletFinished)
            {
                if (isReloading && Time.time >= nextReload)
                {
                    ammoReloadCount = 0;

                    if (bagAmmo >= bulletReloadCount)
                    {
                        reloadBullets = bulletReloadCount;
                    }
                    else
                    {
                        reloadBullets = bulletReloadCount - bagAmmo;
                    }

                    ammoReloadCount = reloadBullets;

                    PerformReload();
                    StartCoroutine(UIGunInfo.instance.GameObjectFeedback(UIGunInfo.instance.gameObject, true));
                }

                bagAmmo -= ammoReloadCount;
                currentAmmo += ammoReloadCount;

                if (currentAmmo >= maxAmmo)
                {
                    isReloadingBulletFinished = true;
                    isReloading = false;
                }
            }
        }
    }*/

    /*private void ReloadSawedOffShotgun_Normal()
    {
        if (PlayerController_New.instance.IsReloadPressed && bagAmmo > 0 && currentAmmo < maxAmmo
                && !isReloading && Time.time > nextAttack)
        {
            PlayerController_New.instance.IsReloadPressed = false;
            ammoReloadCount = 0;
            PerformReload();

            while (bagAmmo > 0)
            {
                if (bagAmmo <= 0 || ammoReloadCount >= maxAmmo - currentAmmo)
                {
                    break;
                }

                ammoReloadCount++;
            }
        }
        else if (PlayerController_New.instance.isRangeAttackHeld && bagAmmo > 0 && currentAmmo <= 0
                && !isReloading)
        {
            ammoReloadCount = 0;
            PerformReload();

            while (bagAmmo > 0)
            {
                if (bagAmmo <= 0 || ammoReloadCount >= maxAmmo - currentAmmo)
                {
                    break;
                }

                ammoReloadCount++;
            }
        }
    }*/

    /*private void PerformReload()
    {
        isReloading = true;
        nextReload = Time.time + reloadWaitTimeDuration * WeaponManager.instance.reloadSpeedMultiplier;
        SoundManager.instance.ReloadGunSound(transform.position, (int)rangeWeaponType, WeaponManager.instance.reloadSpeedMultiplier);
    }*/

    /*private void ReloadBullet()
    {
        if ((PlayerController_New.instance.isRangeAttackPressed || PlayerController_New.instance.isRangeAttackHeld))
        {
            if (isReloading)
            {
                StopAllCoroutines(); // Stops the reload
                isReloading = false;
            }
        }

        // 2. Start Reload
        if (PlayerController_New.instance.IsReloadPressed && !isReloading && bagAmmo > 0 && currentAmmo < maxAmmo)
        {
            StartCoroutine(ReloadBulletRoutine());
        }
    }

    private IEnumerator ReloadBulletRoutine()
    {
        isReloading = true;

        // While we still need ammo and have ammo in the bag
        while (currentAmmo < maxAmmo && bagAmmo > 0)
        {
            // Wait for the reload delay (e.g., 0.5 seconds)
            yield return new WaitForSeconds(reloadWaitTimeDuration);

            int amountToReload = Mathf.Min(maxAmmo - currentAmmo, bulletReloadCount, bagAmmo);

            bagAmmo -= amountToReload;
            currentAmmo += amountToReload;

            PerformReload();
            StartCoroutine(UIGunInfo.instance.GameObjectFeedback(UIGunInfo.instance.gameObject, true));

            // Update UI or Play Sound here
        }

        isReloading = false;
        isReloadingBulletFinished = true;
    }

    int ammoReloadCount;
    private void ReloadSawedOffShotgun()
    {
        ReloadSawedOffShotgun_Normal();

        if (isReloading && Time.time >= nextReload)
        {
            currentAmmo += ammoReloadCount;
            bagAmmo -= ammoReloadCount;
            StartCoroutine(UIGunInfo.instance.GameObjectFeedback(UIGunInfo.instance.gameObject, true));
            isReloading = false;
        }
    }*/

    private void InstantiateWeaponUI()
    {
        /*switch(weaponType)
        {
            case WeaponType.Melee:

                weaponPanelGroup = GameObject.Find("MeleeWeaponPanel");
                uiGunInfo = Instantiate(meleeUIGunInfoPrefab, weaponPanelGroup.transform);

                item = uiGunInfo.GetComponent<UIGunInfo>();
                item.weaponType = weaponType;
                item.meleeWeaponType = meleeWeaponType;
                item.weapon = this;
                break;

            case WeaponType.Range:
                weaponPanelGroup = GameObject.Find("RangeWeaponPanelGroup");
                uiGunInfo = Instantiate(rangeUIGunInfoPrefab, weaponPanelGroup.transform);

                item = uiGunInfo.GetComponent<UIGunInfo>();
                item.weaponType = weaponType;
                item.rangeWeaponType = rangeWeaponType;
                item.weapon = this;
                break;
        }*/
        /*switch (weaponType)
        {
            case WeaponType.Melee:
                UIMeleeWeaponInfoPanel.instance.AddWeapon(item);
                break;

            case WeaponType.Range:
                UIRangeWeaponInfo.instance.AddWeapon(item);
                break;
        }*/
        /* item.gameObject.SetActive(false);
        item.gameObject.SetActive(true);*/

        //isWeaponBeingPickUp = true;

        if (weaponType == WeaponType.Range)
        {
           //WeaponShortcutUI.instance.CheckWeaponLeafUI(this);
        } 
    }

    private void RemoveWeaponUI()
    {
        switch (weaponType)
        {
            case WeaponType.Melee:
                //UIMeleeWeaponInfoPanel.instance.RemoveWeapon(uiGunInfo);
                break;

            case WeaponType.Range:
                //UIRangeWeaponInfo.instance.RemoveWeapon(uiGunInfo);
                break;
        }
    }

    public void AddTokenModToWeapon(TokenModification token, TokenColor color)
    {
        TokenColor newTokenColor = new TokenColor();
        newTokenColor = color;

        if (tokenColors.Count >= 3)
        {
            // Replacing Token
        }
        else
        {
            tokenColors.Add(newTokenColor);
            //WeaponManager.instance.UpdateTokenStats(overallTokenStat, tokenColors);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            isPlayerInRange = true;

            if (!isWeaponBeingPickUp)
            {
                WeaponManager.instance.weaponsInPlayerRange.Add(this);
            }

            /*if (text_CollectItem != null)
            {
                text_CollectItem.SetActive(true);
            }*/

            if (spriteKeyboard_E != null)
            {
                spriteKeyboard_E.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            isPlayerInRange = false;

            if (!isWeaponBeingPickUp)
            {
                WeaponManager.instance.weaponsInPlayerRange.Remove(this);
            }

            /*if (text_CollectItem != null)
            {
                text_CollectItem.SetActive(false);
            }*/

            if (spriteKeyboard_E != null)
            {
                spriteKeyboard_E.SetActive(false);
            }
        }
    }
}
