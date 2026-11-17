using UnityEngine;

/// <summary>
/// Adds up the RingEffects of the rings in the player's ring slots and applies them.
/// Max HP / stamina bonuses are applied to the player directly (and taken back when the ring comes off);
/// the multipliers are read by the combat, movement, stamina and coin code through the static properties.
/// Lives on the player, created automatically.
/// </summary>
public class PlayerRings : MonoBehaviour
{
    private static PlayerRings instance;

    private readonly Item[] lastRings = new Item[8];
    private float appliedHealthBonus;
    private float appliedStaminaBonus;

    private float staminaRegen = 1f, blockStamina = 1f, damageDealt = 1f, damageTaken = 1f, attackSpeed = 1f, moveSpeed = 1f, coins = 1f;

    public static float StaminaRegenMultiplier => instance != null ? instance.staminaRegen : 1f;
    public static float BlockStaminaMultiplier => instance != null ? instance.blockStamina : 1f;
    public static float DamageDealtMultiplier => instance != null ? instance.damageDealt : 1f;
    public static float DamageTakenMultiplier => instance != null ? instance.damageTaken : 1f;
    public static float AttackSpeedMultiplier => instance != null ? instance.attackSpeed : 1f;
    public static float MoveSpeedMultiplier => instance != null ? instance.moveSpeed : 1f;
    public static float CoinMultiplier => instance != null ? instance.coins : 1f;

    /// <summary>Makes sure the player has one. Called by the PlayerController.</summary>
    public static void EnsureOn(GameObject player)
    {
        if (player.GetComponent<PlayerRings>() == null) { player.AddComponent<PlayerRings>(); }
    }

    private void Awake() { instance = this; }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; }
    }

    private void Update()
    {
        PlayerWeaponManager weapons = PlayerWeaponManager.instance;
        if (weapons == null || weapons.equippedRings == null) { return; }

        // Recalculate only when a ring slot changed
        Item[] rings = weapons.equippedRings;
        bool changed = false;
        for (int i = 0; i < lastRings.Length; i++)
        {
            Item ring = i < rings.Length ? rings[i] : null;
            if (lastRings[i] != ring) { lastRings[i] = ring; changed = true; }
        }
        if (changed) { Recalculate(); }
    }

    private void Recalculate()
    {
        float health = 0f, stamina = 0f;
        staminaRegen = blockStamina = damageDealt = damageTaken = attackSpeed = moveSpeed = coins = 1f;

        foreach (Item ring in lastRings)
        {
            if (ring == null) { continue; }
            RingEffect effect = ring.GetComponent<RingEffect>();
            if (effect == null) { continue; }

            health += effect.maxHealthBonus;
            stamina += effect.maxStaminaBonus;
            staminaRegen *= effect.staminaRegenMultiplier;
            blockStamina *= effect.blockStaminaMultiplier;
            damageDealt *= effect.damageDealtMultiplier;
            damageTaken *= effect.damageTakenMultiplier;
            attackSpeed *= effect.attackSpeedMultiplier;
            moveSpeed *= effect.moveSpeedMultiplier;
            coins *= effect.coinMultiplier;
        }

        PlayerController player = PlayerController.instance;
        if (player == null) { return; }

        // Max HP: add the difference; putting a ring on also fills the new HP, taking it off trims it
        float healthChange = health - appliedHealthBonus;
        if (!Mathf.Approximately(healthChange, 0f))
        {
            HealthComponent hp = player.CharacterHealthComponent;
            float newMax = Mathf.Max(1f, hp.MaxHP + healthChange);
            hp.SetMaxHP(newMax);
            if (healthChange > 0f) { hp.Heal(healthChange); }
            else if (hp.CurrentHP > newMax) { hp.SetHP(newMax); }
            appliedHealthBonus = health;
        }

        float staminaChange = stamina - appliedStaminaBonus;
        if (!Mathf.Approximately(staminaChange, 0f))
        {
            StaminaComponent st = player.CharacterStaminaComponent;
            float newMax = Mathf.Max(1f, st.MaxStamina + staminaChange);
            st.SetMaxStamina(newMax);
            if (staminaChange > 0f) { st.Recover(staminaChange); }
            else if (st.CurrentStamina > newMax) { st.SetStamina(newMax); }
            appliedStaminaBonus = stamina;
        }
    }
}
