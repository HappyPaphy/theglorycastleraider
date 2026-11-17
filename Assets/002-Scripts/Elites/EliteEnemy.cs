using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Turns a normal enemy into an elite with one or more traits (EliteTraitDefinition assets).
/// All of its traits' effects are combined:
///   - multipliers (damage, stamina damage, HP, size, speed, damage taken) multiply together
///   - life steal and bonus coins add up
///   - ward: on if any trait has one; the lowest damage-taken and the longest kick break win
///   - explosion: on if any trait explodes; the shortest fuse and the biggest radius / damages win
/// Visuals: a ground ring in the first trait's colour and the trait names (each in its colour) above the head.
/// Rewards: a guaranteed Energy Orb and extra coins.
/// Added at spawn time by <see cref="TryMakeElite"/> (called from EnemySpawner).
/// </summary>
public class EliteEnemy : MonoBehaviour
{
    /// <summary>All of an elite's traits merged into one set of numbers.</summary>
    public struct CombinedEffects
    {
        public float damage, staminaDamage, health, size, speed, damageTaken;
        public bool hasWard; public float wardDamageTaken, wardBreakDuration;
        public float lifeSteal;
        public bool explodes; public float explosionFuse, explosionRadius, explosionDamage, explosionEnemyDamage;
        public float bonusCoins;

        public static CombinedEffects From(IEnumerable<EliteTraitDefinition> traits)
        {
            var e = new CombinedEffects
            {
                damage = 1f, staminaDamage = 1f, health = 1f, size = 1f, speed = 1f, damageTaken = 1f,
                wardDamageTaken = 1f, explosionFuse = float.MaxValue,
            };

            foreach (EliteTraitDefinition t in traits)
            {
                if (t == null) { continue; }

                e.damage *= t.damageMultiplier;
                e.staminaDamage *= t.staminaDamageMultiplier;
                e.health *= t.healthMultiplier;
                e.size *= t.sizeMultiplier;
                e.speed *= t.speedMultiplier;
                e.damageTaken *= t.damageTakenMultiplier;

                if (t.hasWard)
                {
                    e.hasWard = true;
                    e.wardDamageTaken = Mathf.Min(e.wardDamageTaken, t.wardDamageTaken);
                    e.wardBreakDuration = Mathf.Max(e.wardBreakDuration, t.wardBreakDuration);
                }

                e.lifeSteal += t.lifeSteal;

                if (t.explodesOnDeath)
                {
                    e.explodes = true;
                    e.explosionFuse = Mathf.Min(e.explosionFuse, t.explosionFuse);
                    e.explosionRadius = Mathf.Max(e.explosionRadius, t.explosionRadius);
                    e.explosionDamage = Mathf.Max(e.explosionDamage, t.explosionDamage);
                    e.explosionEnemyDamage = Mathf.Max(e.explosionEnemyDamage, t.explosionEnemyDamage);
                }

                e.bonusCoins += t.bonusCoinMultiplier;
            }

            if (!e.explodes) { e.explosionFuse = 0f; }
            return e;
        }
    }

    private readonly List<EliteTraitDefinition> traits = new List<EliteTraitDefinition>();
    private CombinedEffects effects;
    private EnemyEntity enemy;
    private EliteSettings settings;

    private float wardBrokenUntil;
    private bool hasDied;

    private Transform ring;
    private SpriteRenderer ringRenderer;
    private TextMeshPro label;
    private Color ringColor;
    private float ringBaseScale = 1f;

    private static Sprite ringSprite;
    private const float RingSpriteDiameter = 1.6f;

    public IReadOnlyList<EliteTraitDefinition> Traits => traits;
    public CombinedEffects Effects => effects;

    /// <summary>Movement speed multiplier.</summary>
    public float SpeedMultiplier => effects.speed;

    /// <summary>Damage taken multiplier (always-on reduction, times the ward while it's up).</summary>
    public float IncomingDamageMultiplier => effects.damageTaken * (IsWardUp ? effects.wardDamageTaken : 1f);

    public bool IsWardUp => effects.hasWard && Time.time >= wardBrokenUntil;

    // =========================================================
    // SPAWN
    // =========================================================

    /// <summary>Rolls the floor's elite chance and, on success, makes this enemy an elite.</summary>
    public static EliteEnemy TryMakeElite(GameObject enemyObject, int floorIndex)
    {
        EliteSettings settings = EliteSettings.Load();
        if (enemyObject == null || settings == null) { return null; }

        float chance = settings.ChanceForFloor(floorIndex) + PlayerCurses.EliteChanceBonus; // curses (e.g. Hunted)
        if (Random.value >= chance) { return null; }
        return MakeElite(enemyObject, floorIndex);
    }

    /// <summary>Makes this enemy an elite (no chance roll). Pass traits to choose them, or none to roll them.</summary>
    public static EliteEnemy MakeElite(GameObject enemyObject, int floorIndex, params EliteTraitDefinition[] forcedTraits)
    {
        EliteSettings settings = EliteSettings.Load();
        EnemyEntity entity = enemyObject != null ? enemyObject.GetComponentInChildren<EnemyEntity>() : null;
        if (entity == null || settings == null) { return null; }

        List<EliteTraitDefinition> picked = forcedTraits != null && forcedTraits.Length > 0
            ? new List<EliteTraitDefinition>(forcedTraits)
            : RollTraits(settings, floorIndex);
        if (picked.Count == 0) { return null; } // nothing available on this floor

        EliteEnemy elite = entity.gameObject.AddComponent<EliteEnemy>();
        elite.Initialize(entity, settings, picked);
        return elite;
    }

    // Weighted picks from the pool, respecting each trait's earliest floor and incompatibilities
    private static List<EliteTraitDefinition> RollTraits(EliteSettings settings, int floorIndex)
    {
        int floor = floorIndex + 1;
        var candidates = new List<EliteTraitDefinition>();
        foreach (EliteTraitDefinition t in settings.traitPool)
        {
            if (t != null && t.weight > 0f && floor >= t.minFloor && !candidates.Contains(t)) { candidates.Add(t); }
        }

        var picked = new List<EliteTraitDefinition>();
        EliteTraitDefinition first = PickWeighted(candidates, picked);
        if (first == null) { return picked; }
        picked.Add(first);

        while (picked.Count < settings.maxTraits && floor >= settings.extraTraitsFromFloor && Random.value < settings.extraTraitChance)
        {
            EliteTraitDefinition next = PickWeighted(candidates, picked);
            if (next == null) { break; }
            picked.Add(next);
        }
        return picked;
    }

    private static EliteTraitDefinition PickWeighted(List<EliteTraitDefinition> candidates, List<EliteTraitDefinition> picked)
    {
        float total = 0f;
        var allowed = new List<EliteTraitDefinition>();
        foreach (EliteTraitDefinition t in candidates)
        {
            if (picked.Contains(t) || !IsCompatible(t, picked)) { continue; }
            allowed.Add(t);
            total += t.weight;
        }
        if (allowed.Count == 0 || total <= 0f) { return null; }

        float roll = Random.value * total;
        foreach (EliteTraitDefinition t in allowed)
        {
            roll -= t.weight;
            if (roll <= 0f) { return t; }
        }
        return allowed[allowed.Count - 1];
    }

    // Incompatible if either trait lists the other
    private static bool IsCompatible(EliteTraitDefinition trait, List<EliteTraitDefinition> picked)
    {
        foreach (EliteTraitDefinition other in picked)
        {
            if (trait.incompatibleWith.Contains(other) || other.incompatibleWith.Contains(trait)) { return false; }
        }
        return true;
    }

    private void Initialize(EnemyEntity entity, EliteSettings eliteSettings, List<EliteTraitDefinition> pickedTraits)
    {
        enemy = entity;
        settings = eliteSettings;
        traits.AddRange(pickedTraits);
        effects = CombinedEffects.From(traits);
        enemy.Elite = this;

        // Damage
        enemy.attackDamage *= effects.damage;
        enemy.staminaDamage *= effects.staminaDamage;

        // Health
        if (!Mathf.Approximately(effects.health, 1f))
        {
            float maxHP = enemy.CharacterHealthComponent.MaxHP * effects.health;
            enemy.CharacterHealthComponent.SetMaxHP(maxHP);
            enemy.CharacterHealthComponent.SetHP(maxHP);
        }

        // Speed: move speed, animation speed and attack timing (wind-up / cooldown use SpeedMultiplier in
        // the enemy scripts). Values set in Awake, before the elite existed, are scaled once here.
        if (!Mathf.Approximately(effects.speed, 1f))
        {
            UnityEngine.AI.NavMeshAgent agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) { agent.speed *= effects.speed; }

            foreach (Animator animator in enemy.GetComponentsInChildren<Animator>(true))
            {
                animator.speed *= effects.speed;
            }
        }

        // Size
        enemy.transform.localScale *= settings.sizeMultiplier * effects.size;

        BuildVisuals();
    }

    // =========================================================
    // HOOKS (called by the player / enemy code)
    // =========================================================

    /// <summary>The elite just damaged the player.</summary>
    public void OnDealtDamage(float damage)
    {
        if (effects.lifeSteal <= 0f || damage <= 0f || hasDied) { return; }

        enemy.CharacterHealthComponent.Heal(damage * effects.lifeSteal);
        if (ring != null) { ring.DOPunchScale(Vector3.one * 0.35f * ringBaseScale, 0.3f, 6).SetLink(gameObject); }
    }

    /// <summary>The player kicked the elite.</summary>
    public void OnKicked()
    {
        if (!effects.hasWard) { return; }

        bool wasUp = IsWardUp;
        wardBrokenUntil = Time.time + effects.wardBreakDuration;
        if (wasUp && ring != null) { ring.DOPunchScale(Vector3.one * 0.6f * ringBaseScale, 0.35f, 8).SetLink(gameObject); }
    }

    // =========================================================
    // UPDATE / DEATH
    // =========================================================

    private void Update()
    {
        if (enemy == null) { return; }

        if (!hasDied && enemy.CharacterHealthComponent.CurrentHP <= 0f)
        {
            hasDied = true;
            OnDeath();
        }

        AnimateVisuals();
    }

    private void OnDeath()
    {
        DropRewards(enemy.transform.position);

        if (effects.explodes) { StartCoroutine(Explosion()); }
        else { FadeOutVisuals(); }
    }

    private void DropRewards(Vector3 pos)
    {
        // Potions, spread around the body
        int count = Random.Range(Mathf.Min(settings.potionCountMin, settings.potionCountMax),
                                 Mathf.Max(settings.potionCountMin, settings.potionCountMax) + 1);
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = RollPotion();
            if (prefab == null) { break; }

            float angle = (i / (float)Mathf.Max(1, count)) * 360f + Random.Range(-30f, 30f);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (count > 1 ? 0.6f : 0f);
            GameObject item = Instantiate(prefab, pos + offset + Vector3.up * 0.1f, Quaternion.identity);
            Vector3 scale = item.transform.localScale;
            item.transform.localScale = Vector3.zero;
            item.transform.DOScale(scale, 0.3f).SetEase(Ease.OutBack).SetLink(item);
        }

        // Optional Energy Orb
        EnergyOrbDefinition orb = settings.dropEnergyOrb ? RollOrb() : null;
        if (settings.energyOrbPrefab != null && orb != null)
        {
            EnergyOrbPickup pickup = Instantiate(settings.energyOrbPrefab, pos + Vector3.up * 0.75f, Quaternion.identity);
            pickup.Initialize(orb);
        }

        // Bonus coins on top of the normal drop: global elite bonus + every trait's bonus
        float bonusMultiplier = Mathf.Max(0f, settings.coinMultiplier - 1f) + effects.bonusCoins;
        int bonus = Mathf.RoundToInt(enemy.RollCoinDrop() * bonusMultiplier);
        if (bonus > 0) { CoinPickup.Drop(pos + Vector3.up * 0.8f, bonus, 6); }
    }

    private GameObject RollPotion()
    {
        float total = 0f;
        foreach (EliteSettings.PotionDrop drop in settings.potionDrops)
        {
            if (drop != null && drop.itemPrefab != null && drop.weight > 0f) { total += drop.weight; }
        }
        if (total <= 0f) { return null; }

        float roll = Random.value * total;
        foreach (EliteSettings.PotionDrop drop in settings.potionDrops)
        {
            if (drop == null || drop.itemPrefab == null || drop.weight <= 0f) { continue; }
            roll -= drop.weight;
            if (roll <= 0f) { return drop.itemPrefab; }
        }
        return null;
    }

    private EnergyOrbDefinition RollOrb()
    {
        float roll = Random.value;
        if (roll < settings.commonOrbChance && settings.commonOrb != null) { return settings.commonOrb; }
        if (roll < settings.commonOrbChance + settings.rareOrbChance && settings.rareOrb != null) { return settings.rareOrb; }
        if (settings.uniqueOrb != null) { return settings.uniqueOrb; }
        return settings.rareOrb != null ? settings.rareOrb : settings.commonOrb;
    }

    // Fuse with a faster and faster flash, then a blast that hurts the player and other enemies
    private IEnumerator Explosion()
    {
        Color warn = settings.explosionWarningColor;
        float fuse = Mathf.Max(0.01f, effects.explosionFuse);
        float t = 0f;
        while (t < fuse)
        {
            t += Time.deltaTime;
            float pulse = Mathf.PingPong(t * (4f + t * 10f), 1f);
            if (ringRenderer != null)
            {
                ringRenderer.color = Color.Lerp(warn, Color.white, pulse);
                float blastScale = effects.explosionRadius * 2f / RingSpriteDiameter;
                ring.localScale = Vector3.one * Mathf.Lerp(ringBaseScale, blastScale * 0.9f, t / fuse);
            }
            yield return null;
        }

        Vector3 center = enemy.transform.position + Vector3.up * 0.8f;
        float radius = effects.explosionRadius;

        // Player
        PlayerController player = PlayerController.instance;
        if (player != null && effects.explosionDamage > 0f &&
            Vector3.Distance(player.transform.position + Vector3.up * 0.9f, center) <= radius)
        {
            player.TakeUnblockableHit(effects.explosionDamage, center);
        }

        // Other enemies
        if (effects.explosionEnemyDamage > 0f)
        {
            var hit = new HashSet<EnemyEntity>();
            foreach (Collider c in Physics.OverlapSphere(center, radius, 1 << 7, QueryTriggerInteraction.Ignore))
            {
                EnemyEntity other = c.GetComponentInParent<EnemyEntity>();
                if (other != null && other != enemy && hit.Add(other) && other.CharacterHealthComponent.CurrentHP > 0f)
                {
                    other.TakeDamage(effects.explosionEnemyDamage);
                }
            }
        }

        if (player != null && player.cameraBob != null && Vector3.Distance(player.transform.position, center) < radius * 3f)
        {
            player.cameraBob.TriggerShake(0.35f, 0.18f);
        }
        if (SoundManager.instance != null) { SoundManager.instance.FireSound_Impact(center); }

        if (ring != null)
        {
            ringRenderer.color = new Color(1f, 0.85f, 0.4f, 1f);
            ring.DOScale(radius * 2f / RingSpriteDiameter, 0.25f).SetLink(gameObject);
            ringRenderer.DOFade(0f, 0.3f).SetLink(gameObject);
        }
        if (label != null) { label.gameObject.SetActive(false); }
    }

    // =========================================================
    // VISUALS
    // =========================================================

    private void BuildVisuals()
    {
        ringColor = traits[0].color;

        // Ground ring
        GameObject ringGo = new GameObject("Elite Ring");
        ring = ringGo.transform;
        ring.SetParent(enemy.transform, false);
        ring.localPosition = Vector3.up * 0.05f / Mathf.Max(0.01f, enemy.transform.localScale.y);
        ring.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ringBaseScale = settings.ringDiameter / RingSpriteDiameter;
        ring.localScale = Vector3.one * ringBaseScale;
        ringRenderer = ringGo.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = GetRingSprite();
        ringRenderer.color = ringColor;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Trait names over the head, each in its own colour
        GameObject labelGo = new GameObject("Elite Label");
        labelGo.transform.SetParent(enemy.transform, false);
        label = labelGo.AddComponent<TextMeshPro>();
        GameFontSettings.Apply(label);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = settings.labelFontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(4f, 0.4f);
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 200);

        var parts = new List<string>();
        foreach (EliteTraitDefinition trait in traits)
        {
            parts.Add($"<color=#{ColorUtility.ToHtmlStringRGBA(trait.color)}>{trait.displayName}</color>");
        }
        label.text = string.Join(settings.labelSeparator, parts);
    }

    private void AnimateVisuals()
    {
        if (label != null && label.gameObject.activeSelf)
        {
            Transform head = enemy.eyesTransform != null ? enemy.eyesTransform : enemy.transform;
            label.transform.position = head.position + Vector3.up * settings.labelHeight;
            Camera cam = Camera.main;
            if (cam != null) { label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position); }
        }

        if (ringRenderer != null && !hasDied)
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * settings.ringPulseSpeed);
            Color c = (effects.hasWard && !IsWardUp) ? settings.wardBrokenColor : ringColor;
            c.a *= pulse;
            ringRenderer.color = c;
            ring.Rotate(0f, 0f, settings.ringSpinSpeed * Time.deltaTime, Space.Self);
        }
    }

    private void FadeOutVisuals()
    {
        if (ringRenderer != null) { ringRenderer.DOFade(0f, 0.6f).SetLink(gameObject); }
        if (label != null) { label.DOFade(0f, 0.4f).SetLink(gameObject); }
    }

    // Soft ring with a few gaps, drawn once in code (1.6 m wide at scale 1)
    private static Sprite GetRingSprite()
    {
        if (ringSprite != null) { return ringSprite; }

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center, dy = y - center;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / center;
                float band = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.1f);
                float glow = Mathf.Clamp01(1f - r) * 0.18f;
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f;
                float gaps = Mathf.Repeat(angle, 45f) < 6f ? 0.25f : 1f;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Max(band * gaps, glow));
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / RingSpriteDiameter);
        return ringSprite;
    }
}
