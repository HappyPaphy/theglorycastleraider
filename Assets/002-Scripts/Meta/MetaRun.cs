using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The current run's side of meta progression: Emeralds collected (banked as soon as they're picked up,
/// so dying keeps them), what they came from (run summary), records, and the Camp boosts applied at the start.
/// Enemies drop emeralds (EnemyEntity -> CoinPickup.DropEmeralds); collecting one calls CollectEmeralds.
/// </summary>
public static class MetaRun
{
    public class Line { public string label; public int count; public int emeralds; }

    private static readonly Dictionary<string, Line> lines = new Dictionary<string, Line>();
    private static readonly List<string> order = new List<string>();

    public static bool Active { get; private set; }
    public static int RunEmeralds { get; private set; }
    public static int FloorReached { get; private set; }
    public static bool Won { get; private set; }
    private static bool boostsApplied;

    public static IEnumerable<Line> Lines { get { foreach (string k in order) { yield return lines[k]; } } }

    private static MetaProgressionSettings S => MetaProgressionSettings.Load();

    // =========================================================
    // RUN LIFETIME
    // =========================================================

    public static void BeginRun()
    {
        lines.Clear();
        order.Clear();
        RunEmeralds = 0;
        FloorReached = 0;
        Won = false;
        Active = true;
        boostsApplied = false;
        MetaProgress.RecordRunStart();
        MetaProgress.RecordFloor(0);
        MetaProgress.SaveIfDirty();
    }

    /// <summary>A floor was cleared by reaching its exit: a good moment to save.</summary>
    public static void FloorCleared(int floorIndex)
    {
        if (!Active) { return; }
        MetaProgress.SaveIfDirty();
    }

    /// <summary>Now on this floor (also after a cheat skip, for the record only).</summary>
    public static void EnteredFloor(int floorIndex)
    {
        if (!Active) { return; }
        FloorReached = Mathf.Max(FloorReached, floorIndex);
        MetaProgress.RecordFloor(floorIndex);
    }

    public static void EndRun(bool won)
    {
        if (!Active) { return; }
        Won = won;
        if (won)
        {
            Earn("victory", "Victory bonus", S.victoryBonus, true);
            MetaProgress.RecordWin();
        }
        Active = false;
        MetaProgress.SaveIfDirty();
    }

    // =========================================================
    // EMERALDS
    // =========================================================

    /// <summary>How many emeralds a dying enemy drops (Emerald Hunter included). 0 = none.</summary>
    public static int RollEmeraldDrop(bool boss, bool tough)
    {
        if (!Active) { return 0; }
        int amount;
        if (boss) { amount = Random.Range(S.bossDropMin, S.bossDropMax + 1); }
        else if (tough) { amount = Random.Range(S.toughDropMin, S.toughDropMax + 1); }
        else { amount = Random.value < S.normalDropChance ? S.normalDropAmount : 0; }
        return Boosted(amount);
    }

    /// <summary>Emerald pickups collected (CoinPickup in Emerald mode).</summary>
    public static void CollectEmeralds(int amount)
    {
        if (amount <= 0) { return; }
        Earn("drops", "Emeralds collected", amount, false);
    }

    public static void ChallengeCleared()
    {
        if (!Active) { return; }
        Earn("challenges", "Challenge rooms", S.perChallengeRoom, true);
        MetaProgress.SaveIfDirty();
    }

    // Emerald Hunter: +10% per level; the fraction becomes a chance of one more
    private static int Boosted(int amount)
    {
        if (amount <= 0) { return 0; }
        float boosted = amount * MetaUnlocks.EmeraldMultiplier;
        int whole = Mathf.FloorToInt(boosted);
        return whole + (Random.value < boosted - whole ? 1 : 0);
    }

    private static void Earn(string key, string label, int amount, bool applyHunter)
    {
        if (applyHunter) { amount = Boosted(amount); }
        if (amount <= 0) { return; }
        if (!lines.TryGetValue(key, out Line line))
        {
            line = new Line { label = label };
            lines[key] = line;
            order.Add(key);
        }
        line.count += key == "drops" ? amount : 1;
        line.emeralds += amount;
        RunEmeralds += amount;
        MetaProgress.AddEmeralds(amount);
    }

    // =========================================================
    // CAMP BOOSTS (once per run, when the first floor is ready)
    // =========================================================

    public static void ApplyBoosts()
    {
        if (boostsApplied) { return; }
        PlayerController player = PlayerController.instance;
        if (player == null) { return; }
        boostsApplied = true;

        // Vitality: +max HP (rings add on top of this the usual way)
        int vit = MetaProgress.GetLevel(MetaUnlocks.Vitality.id);
        if (vit > 0)
        {
            float bonus = vit * S.hpPerVitalityLevel;
            HealthComponent hp = player.CharacterHealthComponent;
            hp.SetMaxHP(hp.MaxHP + bonus);
            hp.Heal(bonus);
        }

        // Coin Purse
        int purse = MetaProgress.GetLevel(MetaUnlocks.Purse.id);
        if (purse > 0 && player.coinWallet != null) { player.coinWallet.AddCoins(purse * S.coinsPerPurseLevel); }

        // Packed Satchel: real potions, given like a pickup (stacking / auto-equip)
        int satchel = MetaProgress.GetLevel(MetaUnlocks.Satchel.id);
        if (satchel > 0 && S.startingPotion != null && InventoryManager.instance != null)
        {
            for (int i = 0; i < satchel; i++)
            {
                Item potion = Object.Instantiate(S.startingPotion, player.transform.position, Quaternion.identity);
                if (!potion.GiveToPlayer()) { Object.Destroy(potion.gameObject); }
            }
        }

        // Extra Perk Slot
        if (MetaProgress.GetLevel(MetaUnlocks.PerkSlot.id) > 0)
        {
            PerkSelectionManager perks = PerkSelectionManager.instance;
            if (perks != null && perks.Inventory != null && perks.Inventory.UnlockNextSlot()) { perks.NotifyChanged(); }
        }

        // Spare Totem
        if (MetaProgress.GetLevel(MetaUnlocks.Totem.id) > 0 && PlayerUpgrades.Instance != null) { PlayerUpgrades.Instance.AddReviveTotem(); }
    }
}
