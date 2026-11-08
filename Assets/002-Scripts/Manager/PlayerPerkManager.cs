using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerPerkManager : MonoBehaviour
{
    [SerializeField] private PerkCardInventory inventory;
    private readonly Dictionary<PerkType, int> activePerks = new Dictionary<PerkType, int>();

    public float CurrentAttackPowerMultiplier_OnEnemyKill { get; private set; } = 1.0f;
    public float CurrentMoveSpeedMultiplier_Executioner { get; private set; } = 1.0f;
    private Coroutine vampiricDrainCoroutine;

    public static PlayerPerkManager Instance;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    private void OnEnable()
    {
        if (PerkSelectionManager.instance != null)
            PerkSelectionManager.instance.OnChanged += RecalculateActivePerks;
    }

    private void OnDisable()
    {
        if (PerkSelectionManager.instance != null)
            PerkSelectionManager.instance.OnChanged -= RecalculateActivePerks;
    }

    private void Start()
    {
        RecalculateActivePerks();
    }

    public void RecalculateActivePerks()
    {
        activePerks.Clear();

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            if (!inventory.IsSlotUnlocked(i) || !inventory.IsSlotOccupied(i))
                continue;

            RuntimePerkCard slot = inventory.GetSlot(i);
            if (slot == null || slot.definition == null) continue;

            PerkType type = slot.definition.PerkStat;
            int level = slot.level;

            if (activePerks.ContainsKey(type))
                activePerks[type] += level; // Stacks levels linearly
            else
                activePerks.Add(type, level);
        }

        EvaluateContinuousPerks();
    }

    public int GetPerkLevel(PerkType perkType)
    {
        return activePerks.TryGetValue(perkType, out int level) ? level : 0;
    }

    public bool HasPerk(PerkType perkType)
    {
        return GetPerkLevel(perkType) > 0;
    }

    // --- PASSIVE STAT CALCULATIONS ---

    // 1. Iron Skin: Reduce incoming damage (-10% per level)
    public float CalculateIncomingDamage(float rawDamage)
    {
        int level = GetPerkLevel(PerkType.IronSkin);
        float reductionPercent = Mathf.Clamp(level * 0.10f, 0f, 0.60f); // Cap at 60%

        Debug.LogError($"[PlayerPerkManager] IronSkin Level : {level}");
        Debug.LogError($"[PlayerPerkManager] IronSkin reductionPercent : {reductionPercent}");

        return rawDamage * (1.0f - reductionPercent);
    }

    // 2. Greedy Raider: Enemy extra gold drop (+10% per level)
    public int CalculateGoldDrop(int baseGold)
    {
        int level = GetPerkLevel(PerkType.GreedyRaider);
        float bonus = 1.0f + (level * 0.10f);
        return Mathf.RoundToInt(baseGold * bonus);
    }

    // 3. Shield of Ash: Reduced stamina cost when blocking
    public float CalculateBlockStaminaCost(float baseStaminaCost)
    {
        int level = GetPerkLevel(PerkType.ShieldOfAsh);
        float discount = level * 0.15f; // E.g., 15% discount per stack
        return baseStaminaCost * Mathf.Max(0.1f, 1.0f - discount);
    }

    // 4. Second Wind: Faster stamina recovery when below 30% HP
    public float GetStaminaRegenRate(float currentHP, float maxHP, float baseRegenRate)
    {
        if ((currentHP / maxHP) < 0.30f && HasPerk(PerkType.SecondWind))
        {
            int level = GetPerkLevel(PerkType.SecondWind);
            return baseRegenRate * (1.0f + (0.50f * level)); // +50% regen per level
        }
        return baseRegenRate;
    }

    public void OnEnemyKilled(bool isExecution)
    {
        // Bloodlust: +15% attack power per stack for 6s on kill
        int bloodlustLevel = GetPerkLevel(PerkType.BloodLust);
        float bloodlustPercent = Mathf.Clamp(bloodlustLevel * 0.15f, 0f, 0.60f); // Cap at 60%

        if (bloodlustPercent > 0)
        {
            StopCoroutine(nameof(BloodlustRoutine));
            StartCoroutine(BloodlustRoutine(bloodlustPercent, 6f));
        }

        // Executioner: +20% attack speed per stack for 3s on execute
        int executionerLevel = GetPerkLevel(PerkType.Executioner);
        float executionPercent = Mathf.Clamp(executionerLevel * 0.10f, 0f, 0.50f); // Cap at 50%

        if (isExecution && executionPercent > 0)
        {
            StopCoroutine(nameof(ExecutionerRoutine));
            StartCoroutine(ExecutionerRoutine(executionPercent, 3f));
        }

        // Vampiric Gene: Heal 8 HP per kill per level
        int vampLevel = GetPerkLevel(PerkType.VampiricGene);
        if (vampLevel > 0)
        {
            PlayerController.instance.CharacterHealthComponent.Heal(8 * vampLevel);
        }
    }

    // Triggered on successful parry
    public void OnParrySuccess()
    {
        // Parry Encounter: +20% attack speed for 4s
        int parryLevel = GetPerkLevel(PerkType.ParryEncounter);
        if (parryLevel > 0)
        {
            StopCoroutine(nameof(ParryEncounterRoutine));
            StartCoroutine(ParryEncounterRoutine(0.20f * parryLevel, 4f));
        }

        // Sword Time: Temporary slow time for 1s
        if (HasPerk(PerkType.SwordTime))
        {
            StopCoroutine(nameof(SwordTimeRoutine));
            StartCoroutine(SwordTimeRoutine(0.75f));
        }
    }

    // Triggered on Jump Kick
    public float GetJumpKickKnockback(float baseKnockback)
    {
        int bigFootLevel = GetPerkLevel(PerkType.BigFoot);
        if (bigFootLevel > 0)
        {
            return baseKnockback * (1.0f + (1.5f * bigFootLevel)); // Pushes back far
        }
        return baseKnockback;
    }

    // --- TIMED BUFF ROUTINES ---

    private IEnumerator BloodlustRoutine(float bonusSpeed, float duration)
    {
        CurrentAttackPowerMultiplier_OnEnemyKill = 1.0f + bonusSpeed;
        yield return new WaitForSeconds(duration);
        CurrentAttackPowerMultiplier_OnEnemyKill = 1.0f;
    }

    private IEnumerator ExecutionerRoutine(float bonusSpeed, float duration)
    {
        CurrentMoveSpeedMultiplier_Executioner = 1.0f - bonusSpeed;
        yield return new WaitForSeconds(duration);
        CurrentMoveSpeedMultiplier_Executioner = 1.0f;
    }

    private IEnumerator ParryEncounterRoutine(float bonusSpeed, float duration)
    {
        //CurrentMoveSpeedMultiplier_OnEnemyKill = 1.0f + bonusSpeed;
        yield return new WaitForSeconds(duration);
        //CurrentMoveSpeedMultiplier_OnEnemyKill = 1.0f;
    }

    private IEnumerator SwordTimeRoutine(float duration)
    {
        Time.timeScale = 0.1f; // Slow time
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1.0f;
    }

    // --- CONTINUOUS TICK PERKS ---

    private void EvaluateContinuousPerks()
    {
        if (vampiricDrainCoroutine != null) StopCoroutine(vampiricDrainCoroutine);

        // Vampiric Gene drain loop: Lose 1 HP every 2 seconds during active non-boss combat
        if (HasPerk(PerkType.VampiricGene))
        {
            vampiricDrainCoroutine = StartCoroutine(VampiricDrainLoop());
        }
    }

    private IEnumerator VampiricDrainLoop()
    {
        while (HasPerk(PerkType.VampiricGene))
        {
            yield return new WaitForSeconds(2.0f);
            PlayerController.instance.CharacterHealthComponent.TakeDamage(1f);
            /*if (PlayerCombatState.InCombat && !PlayerCombatState.IsBossFight)
            {
                
            }*/
        }
    }
}
