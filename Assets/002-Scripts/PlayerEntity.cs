using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerEntity : CharacterEntity
{
    public StaminaComponent CharacterStaminaComponent;
    public UltimateComponent CharacterUltimateComponent;
    public PlayerInventory PlayerInventoryComponent;

    [Header("Component")]
    [SerializeField] protected Slider slider_Stamina;
    [SerializeField] protected Slider slider_Ultimate;
    [SerializeField] protected TMPro.TMP_Text text_Health;
    [SerializeField] protected TMPro.TMP_Text text_Stamina;
    [SerializeField] protected TMPro.TMP_Text text_Ultimate;

    public float curMaxHPLevelIndex;
    public float curMaxStaminaLevelIndex;

    private float takeDamage_DifficultyModifier;

    protected override void Awake()
    {
        /*if (GameManager.instance != null)
        {
            CharacterHealthComponent.SetMaxHP(GameManager.instance.playerMaxHealth);
            CharacterStaminaComponent.SetMaxStamina(GameManager.instance.playerMaxStamina);

            CharacterUltimateComponent.SetUltimate(GameManager.instance.playerCurrentUltimate);
            CharacterUltimateComponent.SetMaxUltimate(GameManager.instance.playerMaxUltimate);
        }*/

        base.Awake();
    }

    protected override void Start()
    {
        CharacterStaminaComponent.SetStamina(CharacterStaminaComponent.MaxStamina);
        CharacterHealthComponent.SetHP(CharacterHealthComponent.MaxHP);

        slider_HP.minValue = 0;
        slider_Stamina.minValue = 0;
        slider_Ultimate.minValue = 0;
    }

    protected override void Update()
    {
        slider_Stamina.maxValue = CharacterStaminaComponent.MaxStamina;
        slider_Stamina.value = CharacterStaminaComponent.CurrentStamina;

        slider_Ultimate.maxValue = CharacterUltimateComponent.MaxUltimate;
        slider_Ultimate.value = CharacterUltimateComponent.CurrentUltimate;

        text_Health.text = $"{(int)CharacterHealthComponent.CurrentHP}";
        text_Ultimate.text = $"{(int)CharacterUltimateComponent.CurrentUltimate}";

        if (CharacterStaminaComponent.CurrentStamina < 0)
        {
            text_Stamina.text = $"{0f}";
        }
        else
        {
            text_Stamina.text = $"{(int)CharacterStaminaComponent.CurrentStamina}";
        }
            
        if (CharacterHealthComponent.CurrentHP < 0)
        {
            CharacterHealthComponent.SetHP(0);
        }

        /*if(GameManager.instance != null)
        {
            GameManager.instance.playerMaxHealth = CharacterHealthComponent.MaxHP;
            GameManager.instance.playerMaxStamina = CharacterStaminaComponent.MaxStamina;
            GameManager.instance.playerCurrentUltimate = CharacterUltimateComponent.CurrentUltimate;
        }*/

        base.Update();
    }

    public virtual void SetNewMaxHP(float maxHPMultiplier)
    {
        CharacterHealthComponent.SetMaxHP(CharacterHealthComponent.MaxHP * maxHPMultiplier);
    }

    public virtual void TakeDamage(float damageValue)
    {
        /*float damage = damageValue * GameManager.instance.playerTakeDamage;
        float hp = CharacterHealthComponent.CurrentHP;

        if (hp > 1 && (hp - damage <= 1))
        {
            CharacterHealthComponent.SetHP(1f);
        }
        else
        {
            CharacterHealthComponent.TakeDamage(damage);
        }*/
    }

    private void AdjustDifficultyBalance()
    {
        /*switch (GameManager.instance.gameDifficultyState)
        {
            case GameDifficultyState.Easy:
                takeDamage_DifficultyModifier = 0.25f;
                break;
            case GameDifficultyState.Medium:
                takeDamage_DifficultyModifier = 0.5f;
                break;
            case GameDifficultyState.Hard:
                takeDamage_DifficultyModifier = 1f;
                break;
            case GameDifficultyState.Impossible:
                takeDamage_DifficultyModifier = 2f;
                break;
        }*/
    }

    protected void SetActiveSlider(bool isTrue)
    {
        slider_Ultimate.gameObject.SetActive(isTrue);
    }

    public IEnumerator GainSliderValueFeedback(GameObject slider, bool isPositiveFeedback, Vector3 originalScale)
    {
        Vector3 targetScale = new Vector3 (1f, 1f, 1f);
        float pulseDuration = 0.2f;

        if(isPositiveFeedback)
        {
            targetScale = originalScale * 1.3f;
        }
        else
        {
            targetScale = originalScale * 0.7f;
        }

        float halfDuration = pulseDuration / 2f;

        float timeElapsed = 0f;
        while (timeElapsed < halfDuration)
        {
            float t = timeElapsed / halfDuration;

            slider.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        slider.transform.localScale = targetScale;

        timeElapsed = 0f;

        while (timeElapsed < halfDuration)
        {
            float t = timeElapsed / halfDuration;

            slider.transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            timeElapsed += Time.deltaTime;
            yield return null; 
        }

        slider.transform.localScale = originalScale;
    }
}

[Serializable]
public class StaminaComponent
{
    public float MaxStamina => _maxStamina;
    [SerializeField] private float _maxStamina;

    public float CurrentStamina => _currentStamina;
    [SerializeField] private float _currentStamina;

    public Action OnDamageTaken;

    public void SetStamina(float hpValue)
    {
        _currentStamina = hpValue;
    }

    public void Recover(float recoverValue)
    {
        _currentStamina = Mathf.Clamp(_currentStamina += recoverValue, 0, _maxStamina);
    }

    public void DepleteStamina(float depleteValue/*, int defense = 0*/)
    {
        _currentStamina -= depleteValue;
    }

    public void SetMaxStamina(float value)
    {
        _maxStamina = value;
    }
}

[Serializable]
public class UltimateComponent
{
    public float MaxUltimate => _maxUltimate;
    [SerializeField] private float _maxUltimate;

    public float CurrentUltimate => _currentUltimate;
    [SerializeField] private float _currentUltimate;

    public void SetUltimate(float ultimateValue)
    {
        _currentUltimate = ultimateValue;
    }

    public void GainUltimate(float gainValue)
    {
        _currentUltimate = Mathf.Clamp(_currentUltimate += gainValue, 0, _maxUltimate);
    }

    public void DepleteUltimate(float depleteValue)
    {
        _currentUltimate -= depleteValue;
    }

    public void SetMaxUltimate(float value)
    {
        _maxUltimate = value;
    }
}

[Serializable]
public class PlayerInventory
{
    public int CurDragonFruit => _curDragonFruit;
    [SerializeField] private int _curDragonFruit = 0;

    public int CurCartPart_Player => _curCartPart_Player;
    [SerializeField] private int _curCartPart_Player = 0;

    public int CurCollectBronzeKey => _curCollectBronzeKey;
    [SerializeField] private int _curCollectBronzeKey = 0;

    public int CurCollectSilverKey => _curCollectSilverKey;
    [SerializeField] private int _curCollectSilverKey = 0;

    public int CurCollectGoldenKey => _curCollectGoldenKey;
    [SerializeField] private int _curCollectGoldenKey = 0;

    public int CurCollectGoblinKey => _curCollectGoblinKey;
    [SerializeField] private int _curCollectGoblinKey = 0;

    public void CollectDragonFruit(int dragonFruit)
    {
        _curDragonFruit += dragonFruit;
    }

    public void CollectCartPart(int cartPart)
    {
        _curCartPart_Player += cartPart;
    }

    public void CollectBronzeKey(int keyCount)
    {
        _curCollectBronzeKey += keyCount;
    }

    public void CollectSilverKey(int keyCount)
    {
        _curCollectSilverKey += keyCount;
    }

    public void CollectGoldenKey(int keyCount)
    {
        _curCollectGoldenKey += keyCount;
    }

    public void CollectGoblinKey(int keyCount)
    {
        _curCollectGoblinKey += keyCount;
    }

    public void UseBronzeKey(int keyCount)
    {
        _curCollectBronzeKey -= keyCount;
    }

    public void UseSilverKey(int keyCount)
    {
        _curCollectSilverKey -= keyCount;
    }

    public void UseGoldenKey(int keyCount)
    {
        _curCollectGoldenKey -= keyCount;
    }

    public void UseGoblinKey(int keyCount)
    {
        _curCollectGoblinKey -= keyCount;
    }
}
