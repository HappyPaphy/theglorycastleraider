using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class CharacterEntity : MonoBehaviour
{
    public HealthComponent CharacterHealthComponent;

    [Header("Component")]
    [SerializeField] protected Slider slider_HP;

    protected virtual void Awake()
    {

    }

    protected virtual void Start()
    {
        if (slider_HP != null)
        {
            slider_HP.minValue = 0;
        }

        CharacterHealthComponent.SetHP(CharacterHealthComponent.MaxHP);
    }

    protected virtual void Update()
    {
        if (slider_HP != null)
        {
            if (slider_HP.maxValue != CharacterHealthComponent.MaxHP)
            {
                slider_HP.maxValue = CharacterHealthComponent.MaxHP;
            }

            if (slider_HP.value != CharacterHealthComponent.CurrentHP)
            {
                slider_HP.value = CharacterHealthComponent.CurrentHP;
            }
        }

        UpdateEntity();
    }

    protected virtual void UpdateEntity()
    {
        if (CharacterHealthComponent.CurrentHP <= 0)
        {
            Die();
        }
    }

    public virtual void Die()
    {

    }
}

[Serializable]
public class HealthComponent
{
    public float MaxHP => _maxHP;
    [SerializeField] private float _maxHP;

    public float CurrentHP => _currentHP;
    [SerializeField] private float _currentHP;

    public float ExecutePercentage => _executePercentage;
    [SerializeField] private float _executePercentage;

    public Action OnDamageTaken;

    public void SetHP(float hpValue)
    {
        _currentHP = hpValue;
    }

    public void Heal(float healValue)
    {
        _currentHP = Mathf.Clamp(_currentHP += healValue, 0, _maxHP);
    }

    public void TakeDamage(float damageValue)
    {

        _currentHP -= damageValue;
    }

    public void SetMaxHP(float value)
    {
        _maxHP = value;
    }
}
