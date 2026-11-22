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

    // Whether we're listening to the perk selection (picking / upgrading / removing cards)
    private bool subscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (subscribed && PerkSelectionManager.instance != null)
            PerkSelectionManager.instance.OnChanged -= RecalculateActivePerks;
        subscribed = false;

        if (swordTimeActive) { EndSwordTime(); } // never leave the game stuck in slow motion
    }

    private void Start()
    {
        // PerkSelectionManager may not exist yet in OnEnable (same object, script order): subscribe now as well,
        // otherwise picked cards would never reach the perk effects
        if (inventory == null && PerkSelectionManager.instance != null) { inventory = PerkSelectionManager.instance.Inventory; }
        if (inventory == null) { inventory = GetComponentInChildren<PerkCardInventory>(); }
        TrySubscribe();
        RecalculateActivePerks();
    }

    private void TrySubscribe()
    {
        if (subscribed || PerkSelectionManager.instance == null) { return; }
        PerkSelectionManager.instance.OnChanged += RecalculateActivePerks;
        subscribed = true;
    }

    public void RecalculateActivePerks()
    {
        activePerks.Clear();
        if (inventory == null) { return; }

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
        // Last Stand: brief invulnerability after cheating death
        if (Time.time < lastStandInvulnerableUntil) { return 0f; }

        // Glass Jaw: take more damage
        rawDamage *= GlassJawIncomingMultiplier;

        // Curses (e.g. Fragile)
        rawDamage *= PlayerCurses.DamageTakenMultiplier;
        rawDamage *= ChallengeRoom.DamageTakenMultiplier; // challenge modifier
        rawDamage *= PlayerRings.DamageTakenMultiplier;   // rings

        int level = GetPerkLevel(PerkType.IronSkin);
        float reductionPercent = Mathf.Clamp(level * 0.10f, 0f, 0.60f); // Cap at 60%

        float finalDamage = rawDamage * (1.0f - reductionPercent);

        // Last Stand: a lethal hit leaves you at 1 HP instead
        finalDamage = ApplyLastStand(finalDamage);

        // Revive Totem (shop): a hit that is still lethal breaks a totem and you get back up
        if (PlayerUpgrades.Instance.TryReviveFromLethalHit(finalDamage))
        {
            lastStandInvulnerableUntil = Time.time + PlayerUpgrades.Instance.ReviveInvulnerability;
            return 0f;
        }

        return finalDamage;
    }

    // 2. Greedy Raider: Enemy extra gold drop (+10% per level)
    public int CalculateGoldDrop(int baseGold)
    {
        int level = GetPerkLevel(PerkType.GreedyRaider);
        float bonus = (1.0f + (level * 0.10f)) * PlayerRings.CoinMultiplier;
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

        // Headsman's Toll: executions shake everyone nearby
        if (isExecution)
        {
            TriggerHeadsmansToll();
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

        // Riposte Echo: store a shockwave for the next swing
        GainRiposteCharge();

        // Sword Time: Temporary slow time for 1s
        if (HasPerk(PerkType.SwordTime))
        {
            // Stop the previous one by reference (stopping by name doesn't stop coroutines started this way)
            if (swordTimeRoutine != null) { StopCoroutine(swordTimeRoutine); }
            swordTimeRoutine = StartCoroutine(SwordTimeRoutine(0.75f));
        }
    }

    // Big Foot: only a kick done in the air (jump kick) gets the extra push. Kicks on the ground are normal.
    public float GetJumpKickKnockback(float baseKnockback, bool airborne)
    {
        int bigFootLevel = GetPerkLevel(PerkType.BigFoot);
        if (airborne && bigFootLevel > 0)
        {
            return baseKnockback * (1.0f + (1.5f * bigFootLevel)); // Pushes back far
        }
        return baseKnockback;
    }

    // =========================================================
    // MOVE-CHANGER PERKS
    // =========================================================

    [Header("Warden's Boot (kick into wall/furniture)")]
    [SerializeField] private float wardenSlamDamagePerLevel = 15f;
    [SerializeField] private float wardenMinSlamDistance = 0.8f;
    [SerializeField] private float wardenMaxSlamDistance = 3f;

    [Header("Riposte Echo (parry stores a shockwave)")]
    [SerializeField] private float riposteDamageMultiplier = 1.5f;   // x weapon damage, + per level
    [SerializeField] private float riposteRadius = 2.6f;
    [SerializeField] private float riposteKnockback = 7f;
    [SerializeField] private float riposteChargeLifetime = 6f;
    [Tooltip("Perfect parries needed to earn one Riposte Echo charge.")]
    [SerializeField] private int parriesPerRiposteCharge = 4;

    [Header("Splinter Storm (broken furniture explodes)")]
    [SerializeField] private float splinterDamagePerLevel = 12f;
    [SerializeField] private float splinterRadius = 2.5f;
    [SerializeField] private float splinterChainDelay = 0.12f;

    private int riposteCharges;
    private int riposteParryCount;
    private float riposteChargeExpireTime;

    public int RiposteCharges => riposteCharges;
    /// <summary>Parries counted toward the next charge (0 .. parriesPerRiposteCharge - 1).</summary>
    public int RiposteParryProgress => riposteParryCount;
    public int ParriesPerRiposteCharge => parriesPerRiposteCharge;

    // ---------- Warden's Boot ----------
    // Kicked enemies that fly into a wall take slam damage (LV2+: they're stunned too, ready to execute).
    // Kicked into furniture, the furniture shatters (which chains into Splinter Storm).
    public void OnEnemyKicked(EnemyEntity enemy, Vector3 kickerPosition, float knockback)
    {
        int level = GetPerkLevel(PerkType.WardensBoot);
        if (level <= 0 || enemy == null) { return; }

        Vector3 dir = enemy.transform.position - kickerPosition;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) { return; }
        dir.Normalize();

        // Knockback decays quickly, so the travel distance is roughly force / decay (10)
        float travel = Mathf.Clamp(0.5f + knockback * 0.12f, wardenMinSlamDistance, wardenMaxSlamDistance);
        Vector3 origin = enemy.transform.position + Vector3.up * 0.9f;

        RaycastHit[] hits = Physics.SphereCastAll(origin, 0.3f, dir, travel, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<EnemyEntity>() != null) { continue; }
            if (hit.collider.GetComponentInParent<PlayerController>() != null) { continue; }
            if (hit.distance <= 0f) { continue; } // started inside it

            Fracture furniture = hit.collider.GetComponentInParent<Fracture>();
            if (furniture != null)
            {
                StartCoroutine(WardenSlamRoutine(enemy, hit.point, level, furniture, hit.collider));
                return;
            }

            if (hit.collider.attachedRigidbody == null) // static geometry = wall/pillar
            {
                StartCoroutine(WardenSlamRoutine(enemy, hit.point, level, null, null));
                return;
            }
        }
    }

    private IEnumerator WardenSlamRoutine(EnemyEntity enemy, Vector3 point, int level, Fracture furniture, Collider furnitureCollider)
    {
        // Let the knockback carry them into the obstacle first
        yield return new WaitForSeconds(0.12f);
        if (enemy == null) { yield break; }

        float damage = wardenSlamDamagePerLevel * level;
        enemy.TakeSpellHit(false, point, damage, false, 0f, DamageImpactSound.None);

        if (furniture != null)
        {
            furniture.TakeDamage(9999f, furnitureCollider, point, point - enemy.transform.position);
        }
        else if (level >= 2)
        {
            enemy.GotStunned();
        }

        SpawnEffect("Spark", point, 3);
        ShakeIfClose(point, 0.25f, 0.12f);
        SoundManager.instance.KickSound_Human(point);
    }

    // ---------- Riposte Echo ----------
    // Every 4 perfect parries store a charge (up to the perk level). The next swing releases it as an unblockable shockwave.
    private void GainRiposteCharge()
    {
        int level = GetPerkLevel(PerkType.RiposteEcho);
        if (level <= 0) { return; }

        // Drop charges that ran out unused, so they don't block new progress
        if (riposteCharges > 0 && Time.time > riposteChargeExpireTime) { riposteCharges = 0; }

        // Already holding the max: don't bank progress toward an extra charge
        if (riposteCharges >= level) { return; }

        riposteParryCount++;
        if (riposteParryCount < Mathf.Max(1, parriesPerRiposteCharge)) { return; }

        riposteParryCount = 0;
        riposteCharges = Mathf.Min(riposteCharges + 1, level);
        riposteChargeExpireTime = Time.time + riposteChargeLifetime;

        RumbleManager.instance.RumblePulse(0.5f, 1.5f, 0.15f);

        // Charges full: blue screen border tells the player the shockwave is ready
        if (riposteCharges >= level && PlayerController.instance != null && PlayerController.instance.PostProcessEffect != null)
        {
            PlayerController.instance.PostProcessEffect.TriggerRiposteReadyEffect();
        }
    }

    private const string RiposteShockwaveResource = "VFX/RiposteShockwave";
    private GameObject riposteShockwavePrefab;

    // Blue energy shockwave particles (Assets/Resources/VFX/RiposteShockwave.prefab), facing the swing direction
    private void SpawnRiposteShockwave(Vector3 origin, Vector3 forward)
    {
        if (riposteShockwavePrefab == null)
        {
            riposteShockwavePrefab = Resources.Load<GameObject>(RiposteShockwaveResource);
            if (riposteShockwavePrefab == null) { return; }
        }

        Vector3 position = origin + forward * 0.8f + Vector3.down * 0.35f;
        Instantiate(riposteShockwavePrefab, position, Quaternion.LookRotation(forward, Vector3.up));
    }

    /// <summary>Called on every melee swing. Returns true if a stored Riposte Echo was released.</summary>
    public bool TryReleaseRiposte(Vector3 origin, Vector3 forward, float weaponDamage)
    {
        if (riposteCharges <= 0) { return false; }
        if (Time.time > riposteChargeExpireTime) { riposteCharges = 0; return false; }

        riposteCharges--;
        int level = Mathf.Max(1, GetPerkLevel(PerkType.RiposteEcho));

        forward.y = 0f;
        forward.Normalize();
        Vector3 center = origin + forward * (riposteRadius * 0.6f);
        float damage = weaponDamage * (riposteDamageMultiplier + 0.5f * (level - 1));

        bool hitAny = false;
        HashSet<Object> processed = new HashSet<Object>();
        foreach (Collider col in Physics.OverlapSphere(center, riposteRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 to = col.bounds.center - origin;
            to.y = 0f;
            if (Vector3.Dot(to.normalized, forward) < 0.1f) { continue; } // only in front of the player

            EnemyEntity enemy = col.GetComponentInParent<EnemyEntity>();
            if (enemy != null && processed.Add(enemy))
            {
                enemy.TakeSpellHit(false, col.bounds.center, damage, false, riposteKnockback, DamageImpactSound.MetalFlesh);
                SpawnEffect("Spark", col.bounds.center, 2);
                hitAny = true;
                continue;
            }

            Fracture furniture = col.GetComponentInParent<Fracture>();
            if (furniture != null && processed.Add(furniture))
            {
                furniture.TakeDamage(9999f, col, col.bounds.center, col.bounds.center - origin);
            }
        }

        // Blue energy shockwave so the release is readable even when it whiffs
        SpawnRiposteShockwave(origin, forward);

        ShakeIfClose(origin, 0.3f, 0.12f);
        RumbleManager.instance.RumblePulse(1f, 3f, 0.25f);
        if (hitAny) { SoundManager.instance.SwordSound_Flesh(origin); }

        return true;
    }

    // ---------- Splinter Storm ----------
    // Any furniture that breaks bursts into splinters, hurting nearby enemies.
    // Higher levels deal more damage in a wider radius. Splinters never break other furniture.
    public void OnFurnitureBroken(Vector3 position)
    {
        int level = GetPerkLevel(PerkType.SplinterStorm);
        if (level <= 0) { return; }

        StartCoroutine(SplinterRoutine(position, level));
    }

    private IEnumerator SplinterRoutine(Vector3 position, int level)
    {
        yield return new WaitForSeconds(splinterChainDelay);

        float radius = splinterRadius + 0.5f * (level - 1);
        float damage = splinterDamagePerLevel * level;
        HashSet<Object> processed = new HashSet<Object>();

        foreach (Collider col in Physics.OverlapSphere(position, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            EnemyEntity enemy = col.GetComponentInParent<EnemyEntity>();
            if (enemy != null && processed.Add(enemy))
            {
                enemy.TakeSpellHit(false, col.bounds.center, damage, false, 3f, DamageImpactSound.None);
                SpawnEffect("Blood", col.bounds.center, 1);
            }

            // Splinters only hurt enemies: never break other furniture, so one smashed prop can't chain through the room
        }

        SpawnEffect("Spark", position + Vector3.up * 0.5f, 4);
        ShakeIfClose(position, 0.2f, 0.08f);
    }

    // =========================================================
    // MOVE-CHANGER PERKS (set 2)
    // =========================================================

    [Header("Headsman's Toll (execute staggers nearby enemies)")]
    [SerializeField] private float headsmanRadius = 5f;
    [SerializeField] private float headsmanDamagePerLevel = 10f;
    [SerializeField] private float headsmanKnockback = 6f;
    [SerializeField] private float headsmanHealAtLevel2 = 10f;

    [Header("Glass Jaw (more damage dealt AND taken)")]
    [SerializeField] private float glassJawDamagePerLevel = 0.4f;

    [Header("Greased Slide (sliding knocks enemies down)")]
    [SerializeField] private float slideHitRadius = 0.8f;
    [SerializeField] private float slideDamagePerLevel = 5f;
    [SerializeField] private float slideKnockbackBase = 6f;

    [Header("Ember Guard (blocks store embers, kick releases fire)")]
    [SerializeField] private float emberDamagePerEmber = 8f;
    [SerializeField] private float emberRadius = 2.5f;
    [SerializeField] private float emberChargeLifetime = 10f;

    [Header("Last Stand (survive a lethal hit)")]
    [SerializeField] private float lastStandCooldownLV1 = 90f;
    [SerializeField] private float lastStandCooldownLV2 = 45f;
    [SerializeField] private float lastStandInvulnerability = 1.5f;
    [SerializeField] private float lastStandHealAtLevel2 = 20f;

    [Header("Battering Ram (kick doors off their hinges)")]
    [SerializeField] private float ramRadius = 3.5f;
    [SerializeField] private float ramDamagePerLevel = 15f;
    [SerializeField] private float ramKnockback = 12f;
    [Tooltip("LV2: enemies hit stay knocked down (Kicked) this long, or until they take damage. Not a stun: no execution.")]
    [SerializeField] private float ramKnockdownDuration = 4f;

    private readonly HashSet<EnemyEntity> slideHitEnemies = new HashSet<EnemyEntity>();
    private int embers;
    private float emberExpireTime;
    private float lastStandReadyTime;
    private float lastStandInvulnerableUntil;

    public int Embers => embers;
    public bool IsLastStandReady => HasPerk(PerkType.LastStand) && Time.time >= lastStandReadyTime;

    // ---------- Headsman's Toll ----------
    // Executing an enemy staggers and damages everyone around you. LV2+: also heals you.
    private void TriggerHeadsmansToll()
    {
        int level = GetPerkLevel(PerkType.HeadsmansToll);
        if (level <= 0 || PlayerController.instance == null) { return; }

        Vector3 center = PlayerController.instance.transform.position;
        float damage = headsmanDamagePerLevel * level;
        bool hitAny = false;

        HashSet<EnemyEntity> processed = new HashSet<EnemyEntity>();
        foreach (Collider col in Physics.OverlapSphere(center, headsmanRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            EnemyEntity enemy = col.GetComponentInParent<EnemyEntity>();
            if (enemy == null || !processed.Add(enemy)) { continue; }

            enemy.TakeSpellHit(false, col.bounds.center, damage, false, headsmanKnockback, DamageImpactSound.None);
            SpawnEffect("Blood", col.bounds.center, 1);
            hitAny = true;
        }

        if (level >= 2)
        {
            PlayerController.instance.CharacterHealthComponent.Heal(headsmanHealAtLevel2);
            PlayerController.instance.HealthEffect(true);
        }

        if (hitAny) { ShakeIfClose(center, 0.3f, 0.1f); }
    }

    // ---------- Glass Jaw ----------
    // LV1: +40% dealt / +40% taken. LV2: +80% dealt / +60% taken.
    public float GlassJawDamageMultiplier => 1f + glassJawDamagePerLevel * GetPerkLevel(PerkType.GlassJaw);

    public float GlassJawIncomingMultiplier
    {
        get
        {
            int level = GetPerkLevel(PerkType.GlassJaw);
            return level <= 0 ? 1f : 1f + 0.2f + 0.2f * level;
        }
    }

    /// <summary>Total multiplier for the player's melee/kick damage (Blood Lust x Glass Jaw).</summary>
    public float OutgoingDamageMultiplier => CurrentAttackPowerMultiplier_OnEnemyKill * GlassJawDamageMultiplier
        * PlayerUpgrades.BonusDamageMultiplier   // shrine boons
        * PlayerCurses.DamageDealtMultiplier     // curses
        * PlayerRings.DamageDealtMultiplier;     // rings

    // ---------- Greased Slide ----------
    // Sliding into enemies knocks them down (each enemy once per slide).
    public void OnSlideStarted()
    {
        slideHitEnemies.Clear();
    }

    public void OnSliding(Vector3 playerPosition, Vector3 slideDirection)
    {
        int level = GetPerkLevel(PerkType.GreasedSlide);
        if (level <= 0) { return; }

        Vector3 center = playerPosition + slideDirection * 0.6f + Vector3.up * 0.5f;
        foreach (Collider col in Physics.OverlapSphere(center, slideHitRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            EnemyEntity enemy = col.GetComponentInParent<EnemyEntity>();
            if (enemy == null || !slideHitEnemies.Add(enemy)) { continue; }

            enemy.GotKicked(col.bounds.center, slideDamagePerLevel * level, slideKnockbackBase + 3f * level);
            SoundManager.instance.KickSound_Human(col.bounds.center);
            SpawnEffect("Spark", col.bounds.center, 2);
            RumbleManager.instance.RumblePulse(1f, 2f, 0.12f);
            ShakeIfClose(center, 0.15f, 0.06f);
        }
    }

    // ---------- Ember Guard ----------
    // Every blocked hit stores an ember (LV1 max 3, LV2 max 4). Your next kick releases them as a burst of fire.
    public void OnHitBlocked()
    {
        int level = GetPerkLevel(PerkType.EmberGuard);
        if (level <= 0) { return; }

        embers = Mathf.Min(embers + 1, 2 + level);
        emberExpireTime = Time.time + emberChargeLifetime;
        RumbleManager.instance.RumblePulse(0.4f, 1f, 0.1f);
    }

    /// <summary>Called when the player kicks. Returns true if embers were released.</summary>
    public bool TryReleaseEmbers(Vector3 origin, Vector3 forward)
    {
        int level = GetPerkLevel(PerkType.EmberGuard);
        if (level <= 0 || embers <= 0) { return false; }
        if (Time.time > emberExpireTime) { embers = 0; return false; }

        forward.y = 0f;
        forward.Normalize();
        Vector3 center = origin + forward * (emberRadius * 0.6f);
        float damage = embers * (emberDamagePerEmber + 4f * (level - 1));

        HashSet<EnemyEntity> processed = new HashSet<EnemyEntity>();
        foreach (Collider col in Physics.OverlapSphere(center, emberRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 to = col.bounds.center - origin;
            to.y = 0f;
            if (Vector3.Dot(to.normalized, forward) < 0.1f) { continue; }

            EnemyEntity enemy = col.GetComponentInParent<EnemyEntity>();
            if (enemy == null || !processed.Add(enemy)) { continue; }

            enemy.TakeSpellHit(false, col.bounds.center, damage, false, 5f, DamageImpactSound.FireFlesh);
        }

        // A spray of sparks scaled by how many embers were stored
        for (int i = 0; i < embers * 2; i++)
        {
            Vector3 arc = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * forward;
            SpawnEffect("Spark", origin + arc * Random.Range(0.8f, emberRadius) + Vector3.down * 0.3f, 1);
        }

        embers = 0;
        ShakeIfClose(origin, 0.25f, 0.1f);
        RumbleManager.instance.RumblePulse(1f, 3f, 0.2f);
        return true;
    }

    // ---------- Last Stand ----------
    // A hit that would kill you leaves you at 1 HP with a moment of invulnerability. LV2: shorter cooldown + heal.
    private float ApplyLastStand(float incomingDamage)
    {
        int level = GetPerkLevel(PerkType.LastStand);
        if (level <= 0 || Time.time < lastStandReadyTime || PlayerController.instance == null) { return incomingDamage; }

        float hp = PlayerController.instance.CharacterHealthComponent.CurrentHP;
        if (incomingDamage < hp || hp <= 1f) { return incomingDamage; }

        lastStandReadyTime = Time.time + (level >= 2 ? lastStandCooldownLV2 : lastStandCooldownLV1);
        lastStandInvulnerableUntil = Time.time + lastStandInvulnerability;
        StartCoroutine(LastStandRoutine(level));

        return hp - 1f;
    }

    private IEnumerator LastStandRoutine(int level)
    {
        // Wait a frame so the 1 HP hit lands first
        yield return null;

        PlayerController player = PlayerController.instance;
        if (player == null) { yield break; }

        if (level >= 2)
        {
            player.CharacterHealthComponent.Heal(lastStandHealAtLevel2);
            player.HealthEffect(true);
        }

        ShakeIfClose(player.transform.position, 0.4f, 0.15f);
        RumbleManager.instance.RumblePulse(1f, 3f, 0.4f);

        // Short dramatic slow-down, like Sword Time
        if (Time.timeScale > 0.5f)
        {
            Time.timeScale = 0.35f;
            yield return new WaitForSecondsRealtime(0.4f);
            if (Time.timeScale > 0f && Time.timeScale < 0.5f) { Time.timeScale = 1f; }
        }
    }

    // ---------- Battering Ram ----------
    // Kicking a closed door blasts it off its hinges, hurting and knocking back enemies behind it.
    // LV2: they stay knocked down for a few seconds (until hit). Not a stun, so it doesn't allow executions.
    /// <summary>Called when a kick hits something breakable. Returns true if it was handled as a Battering Ram.</summary>
    public bool TryBatteringRam(Fracture target, Collider hitCollider, Vector3 hitPoint, Vector3 kickerPosition)
    {
        int level = GetPerkLevel(PerkType.BatteringRam);
        if (level <= 0 || target == null) { return false; }

        DoorInteractable door = target.GetComponent<DoorInteractable>();
        if (door == null) { return false; }

        Vector3 dir = target.transform.position - kickerPosition;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;

        target.TakeDamage(9999f, hitCollider, hitPoint, dir);

        Vector3 blastCenter = target.transform.position + dir * (ramRadius * 0.5f) + Vector3.up * 0.9f;
        float damage = ramDamagePerLevel * level;

        HashSet<EnemyEntity> processed = new HashSet<EnemyEntity>();
        foreach (Collider col in Physics.OverlapSphere(blastCenter, ramRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            EnemyEntity enemy = col.GetComponentInParent<EnemyEntity>();
            if (enemy == null || !processed.Add(enemy)) { continue; }

            enemy.GotKicked(col.bounds.center, damage, ramKnockback);
            if (level >= 2) { enemy.KnockDown(ramKnockdownDuration); }
            SpawnEffect("Blood", col.bounds.center, 1);
        }

        SpawnEffect("Spark", hitPoint, 5);
        ShakeIfClose(hitPoint, 0.35f, 0.15f);
        RumbleManager.instance.RumblePulse(1f, 3f, 0.3f);
        return true;
    }

    // ---------- Shared feedback ----------
    private static void SpawnEffect(string poolName, Vector3 position, int count)
    {
        if (ObjectPoolingManager.instance == null) { return; }
        for (int i = 0; i < count; i++)
        {
            ObjectPoolingManager.instance.SpawnObject(poolName, position + Random.insideUnitSphere * 0.25f);
        }
    }

    private static void ShakeIfClose(Vector3 position, float duration, float magnitude)
    {
        PlayerController player = PlayerController.instance;
        if (player == null || player.cameraBob == null) { return; }
        if ((player.transform.position - position).sqrMagnitude > 64f) { return; }
        player.cameraBob.TriggerShake(duration, magnitude);
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

    [Header("Sword Time (parry slow-down) effect")]
    [Tooltip("Played when the slow-down starts. Leave empty for no sound.")]
    [SerializeField] private AudioClip swordTimeSound;
    [SerializeField, Range(0f, 1f)] private float swordTimeSoundVolume = 1f;
    [Tooltip("Stop the sound when the slow-down ends (or is cancelled by attacking).")]
    [SerializeField] private bool stopSwordTimeSoundOnEnd = true;
    // The blue camera look is on the camera's CameraPostProcessEffect (Sword Time section)

    private Coroutine swordTimeRoutine;
    private bool swordTimeActive;
    private const float SwordTimeScale = 0.1f;

    private AudioSource swordTimeAudio;

    private IEnumerator SwordTimeRoutine(float duration)
    {
        swordTimeActive = true;
        Time.timeScale = SwordTimeScale; // Slow time
        PlaySwordTimeSound();
        if (CameraPostProcessEffect.Instance != null) { CameraPostProcessEffect.Instance.SetSwordTime(true); } // camera turns blue
        yield return new WaitForSecondsRealtime(duration);
        EndSwordTime();
    }

    private void Update()
    {
        // Pressing attack (either hand) during the parry slow-down ends it right away, so the follow-up
        // swing isn't stuck waiting for the effect to run out
        if (!swordTimeActive) { return; }
        InputSchemeManager input = InputSchemeManager.instance;
        if (input == null) { return; }
        bool attack = (input.input_PerformRightHand != null && input.input_PerformRightHand.WasPressedThisFrame()) ||
                      (input.input_PerformLeftHand != null && input.input_PerformLeftHand.WasPressedThisFrame());
        if (attack) { CancelSwordTime(); }
    }

    private void PlaySwordTimeSound()
    {
        if (swordTimeSound == null) { return; }
        if (swordTimeAudio == null)
        {
            swordTimeAudio = gameObject.AddComponent<AudioSource>();
            swordTimeAudio.playOnAwake = false;
            swordTimeAudio.spatialBlend = 0f; // 2D
            if (SoundManager.instance != null) { swordTimeAudio.outputAudioMixerGroup = SoundManager.instance.SfxGroup; } // SFX volume slider
        }
        swordTimeAudio.Stop();
        swordTimeAudio.clip = swordTimeSound;
        swordTimeAudio.volume = swordTimeSoundVolume;
        swordTimeAudio.Play();
    }

    /// <summary>Ends the Sword Time slow-down now (attack pressed).</summary>
    public void CancelSwordTime()
    {
        if (!swordTimeActive) { return; }
        if (swordTimeRoutine != null) { StopCoroutine(swordTimeRoutine); swordTimeRoutine = null; }
        EndSwordTime();
    }

    private void EndSwordTime()
    {
        swordTimeActive = false;
        swordTimeRoutine = null;
        if (stopSwordTimeSoundOnEnd && swordTimeAudio != null) { swordTimeAudio.Stop(); }
        if (CameraPostProcessEffect.Instance != null) { CameraPostProcessEffect.Instance.SetSwordTime(false); }
        // Only undo our own slow-down: if a menu paused the game (0) meanwhile, leave its time alone
        if (Mathf.Approximately(Time.timeScale, SwordTimeScale)) { Time.timeScale = 1f; }
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
