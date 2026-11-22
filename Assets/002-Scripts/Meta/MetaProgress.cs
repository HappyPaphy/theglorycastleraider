using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Meta progression that survives between runs: Emeralds (dropped by enemies, spent at the Camp in the main menu),
/// bought unlocks and a few records. Saved as JSON in Application.persistentDataPath/meta.json.
/// </summary>
public static class MetaProgress
{
    private const int SaveVersion = 2; // 2: Glory became Emeralds
    private static string SavePath => Path.Combine(Application.persistentDataPath, "meta.json");

    [Serializable]
    private class UnlockLevel { public string id; public int level; }

    [Serializable]
    private class SaveData
    {
        public int version = SaveVersion;
        public int emeralds;
        public int lifetimeEmeralds;
        public int glory;          // version 1 only (moved into emeralds on load)
        public int lifetimeGlory;  // version 1 only
        public int runs;
        public int wins;
        public int bestFloor = -1;   // highest floor index reached (0 = Floor 1-1)
        public List<UnlockLevel> unlocks = new List<UnlockLevel>();
    }

    private static SaveData data;
    private static bool dirty;

    /// <summary>Fired when Emeralds or an unlock changes (UI refresh).</summary>
    public static event Action OnChanged;

    private static SaveData Data
    {
        get
        {
            if (data == null) { Load(); }
            return data;
        }
    }

    public static int Emeralds => Data.emeralds;
    public static int LifetimeEmeralds => Data.lifetimeEmeralds;
    public static int Runs => Data.runs;
    public static int Wins => Data.wins;
    public static int BestFloor => Data.bestFloor;

    // =========================================================
    // UNLOCKS
    // =========================================================

    public static int GetLevel(string id)
    {
        foreach (UnlockLevel u in Data.unlocks) { if (u.id == id) { return u.level; } }
        return 0;
    }

    public static bool IsMaxed(MetaUnlocks.Unlock unlock) => GetLevel(unlock.id) >= unlock.MaxLevel;

    /// <summary>Emerald cost of the next level, or -1 if maxed.</summary>
    public static int NextCost(MetaUnlocks.Unlock unlock)
    {
        int level = GetLevel(unlock.id);
        return level < unlock.MaxLevel ? unlock.costs[level] : -1;
    }

    public static bool TryBuy(MetaUnlocks.Unlock unlock)
    {
        int cost = NextCost(unlock);
        if (cost < 0 || Data.emeralds < cost) { return false; }
        Data.emeralds -= cost;
        SetLevel(unlock.id, GetLevel(unlock.id) + 1);
        Save();
        OnChanged?.Invoke();
        return true;
    }

    private static void SetLevel(string id, int level)
    {
        foreach (UnlockLevel u in Data.unlocks) { if (u.id == id) { u.level = level; return; } }
        Data.unlocks.Add(new UnlockLevel { id = id, level = level });
    }

    // =========================================================
    // EMERALDS & RECORDS
    // =========================================================

    /// <summary>Adds Emeralds (Emerald Hunter already applied by the caller). Saved at the next checkpoint.</summary>
    public static void AddEmeralds(int amount)
    {
        if (amount <= 0) { return; }
        Data.emeralds += amount;
        Data.lifetimeEmeralds += amount;
        dirty = true;
        OnChanged?.Invoke();
    }

    public static void RecordRunStart() { Data.runs++; dirty = true; }
    public static void RecordWin() { Data.wins++; dirty = true; }
    public static void RecordFloor(int floorIndex) { if (floorIndex > Data.bestFloor) { Data.bestFloor = floorIndex; dirty = true; } }

    /// <summary>Writes the save if anything changed (called at floor changes, run end and quit).</summary>
    public static void SaveIfDirty() { if (dirty) { Save(); } }

    // =========================================================
    // CHEATS (F6 panel)
    // =========================================================

    public static void CheatAddEmeralds(int amount) { AddEmeralds(amount); Save(); }

    public static void CheatUnlockAll()
    {
        foreach (MetaUnlocks.Unlock u in MetaUnlocks.All) { SetLevel(u.id, u.MaxLevel); }
        Save();
        OnChanged?.Invoke();
    }

    public static void CheatReset()
    {
        data = new SaveData();
        Save();
        OnChanged?.Invoke();
    }

    // =========================================================
    // SAVE / LOAD
    // =========================================================

    private static void Load()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                // Version 1 called the currency Glory: carry it over
                if (data != null && data.version < 2)
                {
                    data.emeralds += data.glory;
                    data.lifetimeEmeralds += data.lifetimeGlory;
                    data.glory = data.lifetimeGlory = 0;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Meta] Couldn't read save, starting fresh: " + e.Message);
            data = null;
        }
        if (data == null) { data = new SaveData(); }
        if (data.unlocks == null) { data.unlocks = new List<UnlockLevel>(); }
        data.version = SaveVersion;

        Application.quitting -= SaveIfDirty;
        Application.quitting += SaveIfDirty;
    }

    private static void Save()
    {
        try
        {
            string tmp = SavePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(Data, true));
            if (File.Exists(SavePath)) { File.Delete(SavePath); }
            File.Move(tmp, SavePath); // write-then-rename: a crash mid-save never corrupts the old file
            dirty = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Meta] Couldn't save: " + e.Message);
        }
    }
}
