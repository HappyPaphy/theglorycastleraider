using System.Collections.Generic;

/// <summary>
/// Everything the Camp sells. Costs are per level (costs.Length = max level).
/// Content unlocks add variety (locked perks join the orb pool); boosts are deliberately small.
/// </summary>
public static class MetaUnlocks
{
    public enum Kind { Content, Boost }

    public class Unlock
    {
        public string id;
        public string name;
        public string description; // {0} = level-based value where it makes sense
        public Kind kind;
        public int[] costs;
        public PerkType[] perks; // perk packs only
        public int MaxLevel => costs.Length;
    }

    // ---------- Content: perk packs (these perks are locked until bought) ----------
    public static readonly Unlock BrawlerPack = new Unlock
    {
        id = "pack_brawler", name = "Brawler's Manual", kind = Kind.Content, costs = new[] { 150 },
        description = "Adds Warden's Boot, Battering Ram and Greased Slide to the perk orbs.",
        perks = new[] { PerkType.WardensBoot, PerkType.BatteringRam, PerkType.GreasedSlide },
    };
    public static readonly Unlock DuelistPack = new Unlock
    {
        id = "pack_duelist", name = "Duelist's Codex", kind = Kind.Content, costs = new[] { 200 },
        description = "Adds Riposte Echo, Headsman's Toll and Glass Jaw to the perk orbs.",
        perks = new[] { PerkType.RiposteEcho, PerkType.HeadsmansToll, PerkType.GlassJaw },
    };
    public static readonly Unlock PyrePack = new Unlock
    {
        id = "pack_pyre", name = "Pyre Rites", kind = Kind.Content, costs = new[] { 250 },
        description = "Adds Ember Guard, Splinter Storm and Last Stand to the perk orbs.",
        perks = new[] { PerkType.EmberGuard, PerkType.SplinterStorm, PerkType.LastStand },
    };

    // ---------- Boosts (small on purpose) ----------
    public static readonly Unlock Vitality = new Unlock
    {
        id = "vitality", name = "Vitality", kind = Kind.Boost, costs = new[] { 60, 120, 200 },
        description = "+10 max HP per level at the start of each run.",
    };
    public static readonly Unlock Satchel = new Unlock
    {
        id = "satchel", name = "Packed Satchel", kind = Kind.Boost, costs = new[] { 50, 120 },
        description = "Start each run with a small health potion per level.",
    };
    public static readonly Unlock Purse = new Unlock
    {
        id = "purse", name = "Coin Purse", kind = Kind.Boost, costs = new[] { 40, 80, 140 },
        description = "Start each run with +50 coins per level.",
    };
    public static readonly Unlock Haggler = new Unlock
    {
        id = "haggler", name = "Haggler", kind = Kind.Boost, costs = new[] { 100, 200 },
        description = "Shop prices -10% per level.",
    };
    public static readonly Unlock PerkSlot = new Unlock
    {
        id = "perk_slot", name = "Extra Perk Slot", kind = Kind.Boost, costs = new[] { 250 },
        description = "Start each run with one more perk card slot unlocked.",
    };
    public static readonly Unlock Totem = new Unlock
    {
        id = "totem", name = "Spare Totem", kind = Kind.Boost, costs = new[] { 400 },
        description = "Start each run with a Revive Totem.",
    };
    public static readonly Unlock EmeraldHunter = new Unlock
    {
        id = "glory_hunter", name = "Emerald Hunter", kind = Kind.Boost, costs = new[] { 120, 240 },
        description = "+10% Emeralds per level.",
    };

    public static readonly IReadOnlyList<Unlock> All = new[]
    {
        BrawlerPack, DuelistPack, PyrePack,
        Vitality, Satchel, Purse, Haggler, PerkSlot, Totem, EmeraldHunter,
    };

    // ---------- Queries used by gameplay ----------

    /// <summary>False for perks in a pack that hasn't been bought yet.</summary>
    public static bool IsPerkUnlocked(PerkType perk)
    {
        foreach (Unlock u in All)
        {
            if (u.perks == null) { continue; }
            foreach (PerkType p in u.perks)
            {
                if (p == perk) { return MetaProgress.GetLevel(u.id) > 0; }
            }
        }
        return true; // not in any pack = always available
    }

    public static float ShopPriceMultiplier => 1f - 0.1f * MetaProgress.GetLevel(Haggler.id);
    public static float EmeraldMultiplier => 1f + 0.1f * MetaProgress.GetLevel(EmeraldHunter.id);

    /// <summary>The cheapest unlock not yet maxed (for the "next unlock" bar), or null.</summary>
    public static Unlock NextGoal()
    {
        Unlock best = null;
        int bestCost = int.MaxValue;
        foreach (Unlock u in All)
        {
            int c = MetaProgress.NextCost(u);
            if (c >= 0 && c < bestCost) { best = u; bestCost = c; }
        }
        return best;
    }
}
