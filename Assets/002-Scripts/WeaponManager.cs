using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
//using UnityEngine.Localization;
//using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;

public enum WeaponQuality
{
    S = 3,
    A = 2,
    B = 1,
}

public enum WeaponType
{
    Melee,
    Range
}

public enum MeleeWeaponType
{
    ShortSword = 0,
    Null = 999
}

public enum RangeWeaponType
{
    Ak47 = 1,
    PumpShotgun = 2,
    Uzi = 3,
    M16 = 4,
    Gatling = 5,
    P90 = 6,
    MP5 = 7,
    Scar = 8,
    M16A4 = 9,
    RiotShotgun = 10,
    P1911 = 11,
    DesertEagle = 12,
    Red9 = 13,
    Revolver = 14,
    /*FlameThrower = 15,*/
    M1Garand = 15,
    VSS = 16,
    SawedOffShotgun = 17,
    AutoShotgun = 18,
    Null = 999
};

[System.Flags]
public enum RangeWeaponBulletType
{
    None = 0,
    BulletPistol = 1 << 0,
    BulletSMG = 1 << 1,
    BulletAR = 1 << 2,
    BulletShell = 1 << 3
}

public enum WeaponSkillType
{
    //ResetHeat = 5,

    None = 0,
    FireBullet = 1,
    BulletBouncing = 2,
    StuntBullet = 3,
    ForceBullet = 4,
    FreeBullet = 5
};

public enum TokenColor
{
    None,
    Red,
    Blue,
    Yellow
}

[System.Serializable]
public class TokenModification
{
    public TokenColor tokenColor;
    public int tokenCount;
}

[System.Serializable]
public class TokenStat
{
    public float damageMult = 1.0f;
    public float fireRateMult = 1.0f;
    public float stackGainMult = 1.0f;
    public bool isRainbow = false;
    public float stackGainEffectiveness = 1.0f;
}

[Serializable]
public class WeaponStat
{
    //public LocalizedString localizeString_WeaponName;
    //public LocalizedString localizeString_WeaponDescription;

    public string weaponName;
    public string weaponDescription;
    public WeaponType weaponType;
    public RangeWeaponType rangeWeaponType;
    public MeleeWeaponType meleeWeaponType;
    public WeaponQuality weaponQuality;
    public RangeWeaponBulletType rangeWeaponBulletType;
    public int maxAmmo;
    public int ammoPickupCount;
    //public int bagAmmo;
    public int magazineSize;
    public float bulletPercentage = 0f;
    public float attackCooldownDuration = 0f;
    public float attackComboCooldownDuration = 0f;
    public float bulletForce = 0f;
    public float damage = 0f;
    public float reloadWaitTimeDuration = 0f;
    public float attackPrefabLifeTime = 0f;
    public float maxWeaponSway = 0f;
    public float weaponSwayValue = 0f;
    public float weaponSwayRecoverCooldown = 0f;
    public float weaponFalloffRange = 0f;
    public float weaponCriticalChance = 0f;
    public float weaponCriticalMultiplier = 0f;

}

[Serializable]
public class BulletStat
{
    public float forceValue = 0f;

    public bool isBulletOnFire = false;
    public float fireDamageModifier = 1f;
    public float fireDamage = 0f;
    public float fireDuration = 0f;

    public bool isBulletBouncing = false;
    public int maxBulletBounceCount = 0;
    public int bulletBounceAdditionalDamage = 0;

    public bool isBulletStunt = false;
    public bool isFreeBullet = false;

    public float bulletChainReactionDamage = 0;
    public int maxBulletPiecingCount = 0;

    public float hollowPointModifier = 1f;
    public float pointBlankModifier = 1f;
}

[Serializable]
public class WeaponSkill
{
    public WeaponSkillType weaponSkillType;
    public float currentWeaponSkillValue = 0f;

    public bool isOnWeaponSkill = false;
    public bool isWeaponSkillRunOnce = true;
}

[System.Serializable]
public class WeaponPrefab
{
    public string weaponName;
    public GameObject weaponPrefab;
}

[System.Serializable]
public class WeaponModPrefab
{
    public string weaponModName;
    public GameObject weaponModPrefab;
}

[System.Serializable]
public class WeaponGemFittingUI
{
    [Header("Components")]
    public GameObject parentGameObject;
    //public Weapon currentWeapon;
    public Button button_WeaponCardUI;

    [Header("UI")]
    public Image image_WeaponBG;
    public Image image_WeaponImage;
    public TextMeshProUGUI text_WeaponName;
    public Image image_NotCompatible;

    public Image image_WeaponModBG_1;
    public Image image_WeaponModIcon_1;

    public Image image_WeaponModBG_2;
    public Image image_WeaponModIcon_2;

    public Image image_WeaponModBG_3;
    public Image image_WeaponModIcon_3;
}

public class WeaponManager : MonoBehaviour
{
    [SerializeField] private bool isCheatWeaponUnlockedEnable = false;

    public BulletStat bulletStat;
    public List<WeaponStat> weaponStats;
    public List<WeaponPrefab> allWeaponPrefabList;
    public List<WeaponPrefab> unlockedWeaponPrefabList;
    public List<WeaponPrefab> unlockedWeaponPrefabList_Pistol;
    public List<Weapon> weapons;
    public TokenModification redTokens;
    public TokenModification blueTokens;
    public TokenModification yellowTokens;

    public bool isUnlockedWeaponChecked = false;

    public GameObject meleeWeaponUI;
    public Image image_MeleeWeapon;
    public TextMeshProUGUI text_MeleePower;

    [Header("WeaponSkills")]
    public List<WeaponSkill> weaponSkills;

    public bool isBulletOnFire = false;
    public float curFireLifetime = 0f;

    public float rapidFire_CurLifeTime = 0f;
    public float rapidFire_Duration = 0f;
    public float rapidFire_CurFireRate = 1f;
    public float rapidFire_FireRateUp = 1f;

    public float reloadSpeedMultiplier = 1f;

    [Header("WeaponInfoPopUp")]
    public List<Weapon> weaponsInPlayerRange;
    public Weapon weaponOnSwitchSlot;

    private bool isWeaponPopUpShowOnce = false;
   
    [SerializeField] private RectTransform weaponInfoPopUpPanel;
    [SerializeField] private CanvasGroup weaponInfoPopUp_CanvasGroup;
    [SerializeField] private Image weaponInfoPopUp_Image;
    [SerializeField] private TextMeshProUGUI weaponInfoPopUp_NameText;
    [SerializeField] private TextMeshProUGUI weaponInfoPopUp_QualityText;

    [SerializeField] private Color color_WeaponQuality_BulletPistol;
    [SerializeField] private Color color_WeaponQuality_BulletSMG;
    [SerializeField] private Color color_WeaponQuality_BulletShell;
    [SerializeField] private Color color_WeaponQuality_BulletAR;

    [Header("GemSelectingWeapon")]

    public List<WeaponModPrefab> allWeaponModPrefabList;
    public List<WeaponModPrefab> unlockedWeaponModPrefabList;

    private int temporaryShopEquipmentPrice = 0;
    //private ShopKeeperInGame currentShopkeeper = null;
    private GameObject currentGameObject = null;

    [SerializeField] private Button button_CancelWeaponGemSelection;

    [SerializeField] private Button button_FittingWeaponGem_1;
    [SerializeField] private Button button_FittingWeaponGem_2;
    [SerializeField] private Button button_FittingWeaponGem_3;

    public List<WeaponGemFittingUI> weaponGemFittingUIs;
    [SerializeField] private CanvasGroup canvasGroup_GemReplacingUI;
    public bool isGemReplacingActive = false;
    
    [SerializeField] private HorizontalLayoutGroup layoutGroup_WeaponFittingUIs;

    [Header("GemFittingWeapon")]
    [SerializeField] private RectTransform replacingModTransform;
    [SerializeField] private List<RectTransform> modTransforms;
    [SerializeField] private Weapon currentGemFittingWeapon;
    [SerializeField] private CanvasGroup canvasGroup_GemFittingWeaponUI;
    [SerializeField] private CanvasGroup canvasGroup_SameTypeOfGemWarning;

    [SerializeField] private Button button_CancelWeaponGemFitting;
    [SerializeField] private Image image_WeaponImage;
    [SerializeField] private TextMeshProUGUI text_WeaponName;

    [SerializeField] private GameObject weaponModParent_1;
    [SerializeField] private Image image_WeaponModBG_1A;
    [SerializeField] private Image image_WeaponModIcon_1A;
    [SerializeField] private Image image_WeaponModBG_1B;
    [SerializeField] private Image image_WeaponModIcon_1B;
    [SerializeField] private TextMeshProUGUI text_WeaponModDescription_1;

    [SerializeField] private GameObject weaponModParent_2;
    [SerializeField] private Image image_WeaponModBG_2A;
    [SerializeField] private Image image_WeaponModIcon_2A;
    [SerializeField] private Image image_WeaponModBG_2B;
    [SerializeField] private Image image_WeaponModIcon_2B;
    [SerializeField] private TextMeshProUGUI text_WeaponModDescription_2;

    [SerializeField] private GameObject weaponModParent_3;
    [SerializeField] private Image image_WeaponModBG_3A;
    [SerializeField] private Image image_WeaponModIcon_3A;
    [SerializeField] private Image image_WeaponModBG_3B;
    [SerializeField] private Image image_WeaponModIcon_3B;
    [SerializeField] private TextMeshProUGUI text_WeaponModDescription_3;

    [SerializeField] private Image image_WeaponModBG_ReplacingA;
    [SerializeField] private Image image_WeaponModIcon_ReplacingA;
    [SerializeField] private Image image_WeaponModBG_ReplacingB;
    [SerializeField] private Image image_WeaponModIcon_ReplacingB;
    [SerializeField] private TextMeshProUGUI text_WeaponModDescription_Replacing;


    [SerializeField] private Sprite spr_WeaponModBG_Null;
    [SerializeField] private Sprite spr_WeaponModIcon_Null;

    public Sprite AmmoSprite_Pistol;
    public Sprite AmmoSprite_SMG;
    public Sprite AmmoSprite_AR;
    public Sprite AmmoSprite_Shell;

    [Header("Others")]
    public float executeHoodEffect_AttackSpeedMultiplier = 1f;

    public static WeaponManager instance;

    private void Awake()
    {
        instance = this;
        CheckUnlockedWeapon();

        SetUpButtons();
        SetUpUIs();
    }

    private void Update()
    {
        weapons.RemoveAll(item => item == null);

        UpdateWeaponInfoPopUp();

        UpdateRapidFireState();

        UpdateMeleeUI();
    }

    private void UpdateMeleeUI()
    {
        /*if(PlayerController.instance.meleeWeapon != null && !meleeWeaponUI.activeInHierarchy)
        {
            meleeWeaponUI.SetActive(true);
        }
        else if(PlayerController.instance.meleeWeapon == null && meleeWeaponUI.activeInHierarchy)
        {
            meleeWeaponUI.SetActive(false);
        }*/
    }

    private void UpdateWeaponInfoPopUp()
    {
        if(weaponsInPlayerRange.Count <= 0)
        {
            if(isWeaponPopUpShowOnce)
            {
                isWeaponPopUpShowOnce = false;
                weaponInfoPopUp_CanvasGroup.DOFade(0f, 0.5f);
            }
            return;
        }

        if (!isWeaponPopUpShowOnce)
        {
            isWeaponPopUpShowOnce = true;
            weaponInfoPopUp_CanvasGroup.DOFade(1f, 0.5f);
        }

        Weapon closestWeapon = null;
        float closestDistanceSqr = Mathf.Infinity;
        Vector3 playerPosition = transform.position;

        foreach (Weapon weapon in weaponsInPlayerRange)
        {
            // Calculate the vector between player and weapon
            Vector3 directionToWeapon = weapon.transform.position - playerPosition;

            // Get the squared length of that vector
            float dSqrToWeapon = directionToWeapon.sqrMagnitude;

            if (dSqrToWeapon < closestDistanceSqr)
            {
                if(weapon.isWeaponBeingPickUp) { continue; }

                closestDistanceSqr = dSqrToWeapon;
                closestWeapon = weapon;
            }
        }

        // Now you have the closest weapon!
        if (closestWeapon != null)
        {
            weaponOnSwitchSlot = closestWeapon;
            ShowWeaponPopUp(closestWeapon);
        }
    }

    private void ShowWeaponPopUp(Weapon weapon)
    {
        weaponInfoPopUpPanel.transform.position = weapon.transform.position + new Vector3(-0.3f, 1.45f, 0f);
        weaponInfoPopUp_QualityText.text = GetQualityText(weapon);

        var handleWeaponName = weapon.localizeString_WeaponName.GetLocalizedStringAsync();
        handleWeaponName.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                weaponInfoPopUp_NameText.text = op.Result;
            }
        };

        switch (weapon.bulletType)
        {
            case RangeWeaponBulletType.BulletPistol:
                weaponInfoPopUp_Image.sprite = AmmoSprite_Pistol;
                break;

            case RangeWeaponBulletType.BulletSMG:
                weaponInfoPopUp_Image.sprite = AmmoSprite_SMG;
                break;

            case RangeWeaponBulletType.BulletAR:
                weaponInfoPopUp_Image.sprite = AmmoSprite_AR;
                break;

            case RangeWeaponBulletType.BulletShell:
                weaponInfoPopUp_Image.sprite = AmmoSprite_Shell;
                break;
        }
    }

    private string GetQualityText(Weapon weapon)
    {
        switch (weapon.weaponQuality)
        {
            case WeaponQuality.B:
                return "I";

            case WeaponQuality.A:
                return "II";

            case WeaponQuality.S:
                return "III";

            default:
                return "ERROR";
        }
    }


    private IEnumerator CanvasGroupOff(CanvasGroup canvasGroup, float delayDuration)
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        yield return new WaitForSecondsRealtime(delayDuration);

        canvasGroup.DOKill();
        canvasGroup.DOFade(0f, 0.3f).SetUpdate(true);
    }

    
    private Color GetQualityColor(RangeWeaponBulletType bulletType)
    {
        switch(bulletType)
        {
            case RangeWeaponBulletType.BulletPistol:
                return color_WeaponQuality_BulletPistol;

            case RangeWeaponBulletType.BulletSMG:
                return color_WeaponQuality_BulletSMG;

            case RangeWeaponBulletType.BulletShell:
                return color_WeaponQuality_BulletShell;

            case RangeWeaponBulletType.BulletAR:
                return color_WeaponQuality_BulletAR;

            default:
                return color_WeaponQuality_BulletPistol;
        }
    }

    
    public void CreateAndEquipWeapon(string name)
    {
        WeaponPrefab weaponPrefab = allWeaponPrefabList.FirstOrDefault(w => w.weaponName == name);

        if (weaponPrefab != null && weaponPrefab.weaponPrefab != null)
        {
            GameObject weaponGameObject = Instantiate(weaponPrefab.weaponPrefab, transform.position, transform.rotation);
            Weapon weapon = weaponGameObject.GetComponent<Weapon>();
            weapon.isWeaponBeingPickUpAtStart = true;
        }
    }

    private void CheckUnlockedWeapon()
    {
        unlockedWeaponPrefabList.Clear();

        if(isCheatWeaponUnlockedEnable)
        {
            foreach (WeaponPrefab weaponPrefab in allWeaponPrefabList)
            {
                unlockedWeaponPrefabList.Add(weaponPrefab);

                if(weaponPrefab.weaponPrefab.GetComponent<Weapon>().bulletType == RangeWeaponBulletType.BulletPistol)
                {
                    unlockedWeaponPrefabList_Pistol.Add(weaponPrefab);
                }
            }
        }
        else
        {
            foreach (WeaponPrefab weaponPrefab in allWeaponPrefabList)
            {
                /*if (SaveSystem.LoadItemUnlocks().IsUnlocked(weaponPrefab.weaponName))
                {
                    unlockedWeaponPrefabList.Add(weaponPrefab);

                    if (weaponPrefab.weaponPrefab.GetComponent<Weapon>().bulletType == RangeWeaponBulletType.BulletPistol)
                    {
                        unlockedWeaponPrefabList_Pistol.Add(weaponPrefab);
                    }
                }*/
            }
        }

        isUnlockedWeaponChecked = true;
    }

    public void ActivateRapidFireSkill()
    {
        rapidFire_CurLifeTime = rapidFire_Duration;
        //PlayerUpgradeManager.instance.TemporarySkillStatusUITurnOn("skillID_RapidFire_Unlock", rapidFire_Duration);
    }

    private void UpdateRapidFireState()
    {
        if(rapidFire_CurLifeTime > 0)
        {
            rapidFire_CurFireRate = rapidFire_FireRateUp;
            rapidFire_CurLifeTime -= Time.deltaTime;
        }
        else
        {
            rapidFire_CurFireRate = 1f;
            rapidFire_CurLifeTime = 0f;
        }
    }

    public void AddToWeaponManager(Weapon weapon)
    {
        weapons.Add(weapon);
    }

    /*public void ChangeWeaponStat_MagazineCapacity(int levelIndex)
    {
        foreach (WeaponStat weaponStat in weaponStats)
        {
            switch (weaponStat.rangeWeaponType)
            {
                case RangeWeaponType.Ak47: weaponStat.maxAmmo += 3; break;
                case RangeWeaponType.PumpShotgun: weaponStat.maxAmmo += 1; break;
                case RangeWeaponType.Uzi: weaponStat.maxAmmo += 6; break;
                case RangeWeaponType.Gatling: weaponStat.maxAmmo += 5; break;
                case RangeWeaponType.M16: weaponStat.maxAmmo += 3; break;
                case RangeWeaponType.P90: weaponStat.maxAmmo += 6; break;
                case RangeWeaponType.MP5: weaponStat.maxAmmo += 4; break;
                case RangeWeaponType.Scar: weaponStat.maxAmmo += 3; break;
                case RangeWeaponType.M16A4: weaponStat.maxAmmo += 3; break;
                case RangeWeaponType.RiotShotgun: weaponStat.maxAmmo += 1; break;
                case RangeWeaponType.P1911: weaponStat.maxAmmo += 4; break;
                case RangeWeaponType.DesertEagle: weaponStat.maxAmmo += 2; break;
                case RangeWeaponType.Red9: weaponStat.maxAmmo += 4; break;
                case RangeWeaponType.Revolver: weaponStat.maxAmmo += 2; break;
                //case WeaponType.FlameThrower: weaponStat.maxAmmo += 20; break;
                case RangeWeaponType.M1Garand: weaponStat.maxAmmo += 2; break;
                case RangeWeaponType.VSS: weaponStat.maxAmmo += 4; break;
                case RangeWeaponType.SawedOffShotgun: weaponStat.maxAmmo += 1; break;
                case RangeWeaponType.AutoShotgun: weaponStat.maxAmmo += 3; break;
            }

            UpdateEquippedWeaponStat();
        }
    }*/

    public void IntializeWeaponStat(Weapon weapon)
    {
        foreach(WeaponStat weaponStat in weaponStats)
        {
            if(weapon.rangeWeaponType == weaponStat.rangeWeaponType)
            {
                //weapon.localizeString_WeaponName = weaponStat.localizeString_WeaponName;
                //weapon.localizeString_WeaponDescription = weaponStat.localizeString_WeaponDescription;
                weapon.maxAmmo = weaponStat.maxAmmo;
                //weapon.currentAmmo = weaponStat.maxAmmo;
                //weapon.bagAmmo = weaponStat.bagAmmo;
                weapon.weaponQuality = weaponStat.weaponQuality;
                weapon.ammoPickupCount = weaponStat.ammoPickupCount;
                weapon.magazineSize = weaponStat.magazineSize;
                weapon.bulletPercentage = weaponStat.bulletPercentage;
                //weapon.attackCooldownDuration = weaponStat.attackCooldownDuration * GameManager.instance.playerAttackSpeedMultiplier;
                weapon.attackComboCooldownDuration = weaponStat.attackComboCooldownDuration;
                weapon.bulletForce = weaponStat.bulletForce;
                //weapon.meleeDamage = weaponStat.damage * GameManager.instance.playerAttackPowerMultiplier;
                //weapon.rangeDamage = weaponStat.damage * GameManager.instance.playerAttackPowerMultiplier;
                weapon.reloadWaitTimeDuration = weaponStat.reloadWaitTimeDuration;
                weapon.attackPrefabLifeTime = weaponStat.attackPrefabLifeTime;
                weapon.maxWeaponSway = weaponStat.maxWeaponSway;
                weapon.weaponSwayValue = weaponStat.weaponSwayValue;
                weapon.weaponSwayRecoverCooldown = weaponStat.weaponSwayRecoverCooldown;
                weapon.bulletType = weaponStat.rangeWeaponBulletType;
                weapon.falloffRange = weaponStat.weaponFalloffRange;
                weapon.criticalChance = weaponStat.weaponCriticalChance;
                weapon.criticalMultiplier = weaponStat.weaponCriticalMultiplier;
            }
        }
    }

    /*public void ChangeWeaponStat_AttackPower()
    {
        foreach (WeaponStat weaponStat in weaponStats)
        {
            if (GameManager.instance != null)
            {
                weaponStat.damage *= GameManager.instance.playerAttackPowerMultiplier;
            }
        }

        UpdateEquippedWeaponStat();
    }*/

    /*public void ChangeWeaponStat_AttackSpeed()
    {
        foreach (WeaponStat weaponStat in weaponStats)
        {
            if (GameManager.instance != null)
            {
                weaponStat.attackCooldownDuration *= GameManager.instance.playerAttackSpeedMultiplier;
            }
        }

        UpdateEquippedWeaponStat();
    }*/

    public void UpdateEquippedWeaponStat()
    {
        foreach (Weapon weapon in weapons)
        {
            foreach (WeaponStat weaponStat in weaponStats)
            {
                if (weapon.rangeWeaponType == weaponStat.rangeWeaponType)
                {
                    //weapon.maxAmmo = weaponStat.maxAmmo;

                    /*if(GameManagerSpecialMode.instance != null)
                    {
                        weapon.attackCooldownDuration = weaponStat.attackCooldownDuration * GameManager.instance.playerAttackSpeedMultiplier;
                        weapon.meleeDamage = weaponStat.damage * GameManager.instance.playerAttackPowerMultiplier;
                        weapon.rangeDamage = weaponStat.damage * GameManager.instance.playerAttackPowerMultiplier;
                    }
                    else
                    {
                        weapon.attackCooldownDuration = weaponStat.attackCooldownDuration;
                        weapon.meleeDamage = weaponStat.damage;
                        weapon.rangeDamage = weaponStat.damage;
                    }*/
                   
                    weapon.attackComboCooldownDuration = weaponStat.attackComboCooldownDuration;
                    weapon.bulletForce = weaponStat.bulletForce;
                    weapon.reloadWaitTimeDuration = weaponStat.reloadWaitTimeDuration;
                    weapon.attackPrefabLifeTime = weaponStat.attackPrefabLifeTime;
                    weapon.maxWeaponSway = weaponStat.maxWeaponSway;
                    weapon.weaponSwayValue = weaponStat.weaponSwayValue;
                    weapon.weaponSwayRecoverCooldown = weaponStat.weaponSwayRecoverCooldown;

                    weapon.falloffRange = weaponStat.weaponFalloffRange;

                    weapon.criticalChance = weaponStat.weaponCriticalChance;
                    weapon.criticalMultiplier = weaponStat.weaponCriticalMultiplier;
                }
            }
        }
    }

    public void CollectAmmoBox()
    {
        /*foreach (Weapon weapon in PlayerController_New.instance.rangeWeapons)
        {
            weapon.RefillHalfAmmo();
        }*/

        /*foreach (Weapon weapon in PlayerController_New.instance.rangeWeapons)
        {
            foreach (WeaponStat weaponStat in weaponStats)
            {
                if (weapon.rangeWeaponType == weaponStat.rangeWeaponType)
                {
                    if(weapon.rangeWeaponType == RangeWeaponType.Gatling)
                    {
                        weapon.currentAmmo += 80;
                    }
                    else if(weapon.rangeWeaponType == RangeWeaponType.PumpShotgun || weapon.rangeWeaponType == RangeWeaponType.RiotShotgun
                        || weapon.rangeWeaponType == RangeWeaponType.Revolver)
                    {
                        weapon.currentMagazine += (int)(weapon.maxAmmo * 1.2f);
                    }
                    else if(weapon.rangeWeaponType == RangeWeaponType.P1911 || weapon.rangeWeaponType == RangeWeaponType.DesertEagle || weapon.rangeWeaponType == RangeWeaponType.Red9)
                    {
                        weapon.currentMagazine += 2;
                    }
                    else if (weapon.rangeWeaponType == RangeWeaponType.SawedOffShotgun)
                    {
                        weapon.currentMagazine += (int)(weapon.maxAmmo * 4f);
                    }
                    else
                    {
                        weapon.currentMagazine++;
                    }
                }
            }
        }*/
    }

    public async Task<string> UpdateWeaponSkillName(WeaponSkillType weaponSkillType)
    {
        /*var localizedMap = new Dictionary<WeaponSkillType, LocalizedString>
    {
        { WeaponSkillType.FireBullet, localizedString_FireBullet },
        { WeaponSkillType.BulletBouncing, localizedString_BulletBouncing },
        { WeaponSkillType.StuntBullet, localizedString_StuntBullet },
        { WeaponSkillType.ForceBullet, localizedString_ForceBullet },
        { WeaponSkillType.FreeBullet, localizedString_FreeBullet },
    };*/

        /*if (localizedMap.TryGetValue(weaponSkillType, out var localizedString))
        {
            var handle = localizedString.GetLocalizedStringAsync();
            await handle.Task;

            return handle.Status == AsyncOperationStatus.Succeeded
                ? handle.Result
                : "[Missing text]";
        }*/

        return "[Unknown Skill]";
    }

    public float GetUltimateConsumeAmountWeaponSkill(WeaponSkillType weaponSkillType)
    {
        float weaponSkillConsumeAmount = 0;

        switch (weaponSkillType)
        {
            case WeaponSkillType.FireBullet:
                weaponSkillConsumeAmount = 40;
                break;

            case WeaponSkillType.BulletBouncing:
                weaponSkillConsumeAmount = 20;
                break;

            case WeaponSkillType.StuntBullet:
                weaponSkillConsumeAmount = 40;
                break;

            case WeaponSkillType.ForceBullet:
                weaponSkillConsumeAmount = 40;
                break;

            /*case WeaponSkillType.ResetHeat:
                weaponSkillConsumeAmount = 60;
                break;*/

            case WeaponSkillType.FreeBullet:
                weaponSkillConsumeAmount = 50;
                break;
        }

        return weaponSkillConsumeAmount;
    }

    public float GetValueAmountWeaponSkill(WeaponSkillType weaponSkillType)
    {
        float weaponSkillValueAmount = 0;

        switch (weaponSkillType)
        {
            case WeaponSkillType.FireBullet:
                weaponSkillValueAmount = 10;
                break;

            case WeaponSkillType.BulletBouncing:
                weaponSkillValueAmount = 20;
                break;

            case WeaponSkillType.StuntBullet:
                weaponSkillValueAmount = 10;
                break;

            case WeaponSkillType.ForceBullet:
                weaponSkillValueAmount = 10;
                break;

            /*case WeaponSkillType.ResetHeat:
                weaponSkillConsumeAmount = 60;
                break;*/

            case WeaponSkillType.FreeBullet:
                weaponSkillValueAmount = 10;
                break;
        }

        return weaponSkillValueAmount;
    }

    /*private void WeaponSkillsCountdown(WeaponSkill w)
    {
        if (w.isOnWeaponSkill)
        {
            if(!w.isWeaponSkillRunOnce)
            {
                w.isWeaponSkillRunOnce = true;
                w.currentWeaponSkillValue = GetValueAmountWeaponSkill(w.weaponSkillType);
            }

            if (w.currentWeaponSkillValue > 0)
            {
                WeaponSkillAttribute(w, true);
                w.currentWeaponSkillValue -= Time.deltaTime;
            }
            else
            {
                WeaponSkillAttribute(w, false);
                w.isOnWeaponSkill = false;
            }
        }
    }*/

    public void CancelWeaponSkill(WeaponSkill w)
    {
        WeaponSkillAttribute(w, false);
        w.isOnWeaponSkill = false;
    }

    private void WeaponSkillAttribute(WeaponSkill w, bool isSkillOn)
    {
        switch (w.weaponSkillType)
        {
            case WeaponSkillType.FireBullet:

                if (isSkillOn)
                {
                    bulletStat.fireDamage = weaponStats[0].damage / 5;
                    //Instantiate(tipFirePoint, firePoint.transform);
                }
                else
                {
                    bulletStat.fireDamage = 0f;
                }

                break;

            case WeaponSkillType.BulletBouncing:

                bulletStat.isBulletBouncing = isSkillOn;

                break;

            case WeaponSkillType.StuntBullet:

                bulletStat.isBulletStunt = isSkillOn;

                break;

            case WeaponSkillType.ForceBullet:

                if (isSkillOn)
                {
                    bulletStat.forceValue = 50;
                }
                else
                {
                    bulletStat.forceValue = 0;
                }

                break;

            /*case WeaponSkillType.ResetHeat:

                if (isSkillOn)
                {
                    bulletStat.isOverHeat = false;
                    bulletStat.currentValue_Overheat = 0f;
                }

                break;*/

            case WeaponSkillType.FreeBullet:

                bulletStat.isFreeBullet = isSkillOn;

                break;
        }
    }

    public float UseWeaponSkill(WeaponSkillType weaponSkillType, float currentUltimateValue)
    {
        foreach(WeaponSkill w in weaponSkills)
        {
            if(w.weaponSkillType == weaponSkillType)
            {
                //w.currentWeaponSkillValue = GetValueAmountWeaponSkill(weaponSkillType);
                float weaponSkillConsume = GetUltimateConsumeAmountWeaponSkill(weaponSkillType);
                w.weaponSkillType = weaponSkillType;

                if (currentUltimateValue >= weaponSkillConsume && !w.isOnWeaponSkill)
                {
                    w.isOnWeaponSkill = true;
                    w.isWeaponSkillRunOnce = false;
                    currentUltimateValue -= weaponSkillConsume;
                }

                return currentUltimateValue;
            }
        }

        return currentUltimateValue;
    }

    public void RemoveWeaponSkill(WeaponSkillType weaponSkillType)
    {
        /*foreach (WeaponSkill weaponSkill in weaponSkills)
        {
            if(weaponSkill.weaponSkillType == weaponSkillType)
            {
                weaponSkills.Remove(weaponSkill);
            }
        }*/

        for (int i = weaponSkills.Count - 1; i >= 0; i--)
        {
            if (weaponSkills[i].weaponSkillType == weaponSkillType)
            {
                weaponSkills.RemoveAt(i);
            }
        }
    }

    public void AddWeaponSkillType(WeaponSkillType weaponSkillType)
    {
        WeaponSkill weaponSkill = new WeaponSkill();
        weaponSkill.weaponSkillType = weaponSkillType;
        weaponSkills.Add(weaponSkill);
    }

    public float GetCurrentSkillSliderValue(WeaponSkillType weaponSkillType)
    {
        float currentValue = 0f;

        foreach (WeaponSkill weaponSkill in weaponSkills)
        {
            if (weaponSkill.weaponSkillType == weaponSkillType)
            {
                currentValue = weaponSkill.currentWeaponSkillValue;
            }
        }

        return currentValue;
    }

    public void AmmoCollected()
    {
        /*foreach (Weapon weapon in PlayerController_New.instance.rangeWeapons)
        {
            if(weapon.isWeaponActive) { continue; }

            weapon.currentAmmo += weapon.ammoPickupCount;

            if(weapon.currentAmmo > weapon.maxAmmo)
            {
                weapon.currentAmmo = weapon.maxAmmo;
            }
        }

        foreach(UIGunInfo ui in WeaponShortcutUI.instance.gunUIs_NotEquipped)
        {
            StartCoroutine(ui.GameObjectFeedback(ui.gameObject, true, new Vector3(0.6f, 0.6f, 0.6f)));
        }*/
    }

    public void UpdateMeleePowerUI(int index)
    {
        text_MeleePower.text = $"{index}";
        MeleeFeedback();
    }

    public void UpdateMeleePowerUI(string str)
    {
        if(str == "Max")
        {
            /*if (!SteamManager.instance.IsThisAchievementUnlocked("UPGRADE_SWORDMAN"))
            {
                SteamManager.instance.UnlockAchievement("UPGRADE_SWORDMAN");
            }*/
        }

        text_MeleePower.text = $"{str}";
        MeleeFeedback();
    }

    public void MeleeFeedback()
    {
        StartCoroutine(GameObjectFeedback(meleeWeaponUI, true, new Vector3(1f, 1f, 1f)));
    }

    public IEnumerator GameObjectFeedback(GameObject obj, bool isPositive, Vector3 originalScale)
    {
        Vector3 targetScale = new Vector3(1f, 1f, 1f);
        float pulseDuration = 0.05f;

        if (isPositive)
        {
            targetScale = originalScale * 1.5f;
        }
        else
        {
            targetScale = originalScale * 0.85f;
        }

        float halfDuration = pulseDuration / 2f;

        float timeElapsed = 0f;
        while (timeElapsed < halfDuration)
        {
            float t = timeElapsed / halfDuration;

            obj.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        obj.transform.localScale = targetScale;

        timeElapsed = 0f;

        while (timeElapsed < halfDuration)
        {
            float t = timeElapsed / halfDuration;

            obj.transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        obj.transform.localScale = originalScale;
    }

    private void SetUpUIs()
    {
        canvasGroup_GemReplacingUI.alpha = 0f;
        canvasGroup_GemReplacingUI.interactable = false;
        canvasGroup_GemReplacingUI.blocksRaycasts = false;

        canvasGroup_GemFittingWeaponUI.alpha = 0f;
        canvasGroup_GemFittingWeaponUI.interactable = false;
        canvasGroup_GemFittingWeaponUI.blocksRaycasts = false;

        canvasGroup_SameTypeOfGemWarning.alpha = 0f;
    }

    private void SetUpButtons()
    {
        button_CancelWeaponGemSelection.onClick.AddListener(() =>
        {
            isGemReplacingActive = false;
            Time.timeScale = 1f;

            //RefreshWeaponGemFittingUIs(replacingWeaponMod);

            canvasGroup_GemReplacingUI.DOKill();
            canvasGroup_GemReplacingUI.DOFade(0f, 0.5f).SetUpdate(true);
            canvasGroup_GemReplacingUI.interactable = false;
            canvasGroup_GemReplacingUI.blocksRaycasts = false;

            temporaryShopEquipmentPrice = 0;
        });

        button_CancelWeaponGemFitting.onClick.AddListener(() =>
        {
            canvasGroup_GemFittingWeaponUI.DOKill();
            canvasGroup_GemFittingWeaponUI.DOFade(0f, 0.5f).SetUpdate(true);
            canvasGroup_GemFittingWeaponUI.interactable = false;
            canvasGroup_GemFittingWeaponUI.blocksRaycasts = false;

            canvasGroup_GemReplacingUI.DOKill();
            canvasGroup_GemReplacingUI.DOFade(1f, 0.5f).SetUpdate(true);
            canvasGroup_GemReplacingUI.interactable = true;
            canvasGroup_GemReplacingUI.blocksRaycasts = true;

            canvasGroup_SameTypeOfGemWarning.DOKill();
            canvasGroup_SameTypeOfGemWarning.DOFade(0f, 0.3f).SetUpdate(true);
        });
    }
}
