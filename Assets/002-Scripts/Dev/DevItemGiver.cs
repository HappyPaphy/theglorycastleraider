using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cheat panel (F6), four tabs:
///   Give Item  - every item / weapon, received like a normal pickup (stacking, auto-equip, pickup popup)
///   Add Perk   - every perk card; adds it to a free slot (unlocking one if needed) or sets the owned perk's level
///   Add Coin   - coins and treasure keys
///   Skip Level - jump to any floor (same as F8/F9)
/// The game pauses while it's open. Available in the Editor and in every build, like the floor skipper.
/// Lists come from Resources/Dev/DevItemCatalog (kept up to date by the editor).
/// </summary>
public class DevItemGiver : MonoBehaviour
{
    private static DevItemGiver instance;

    /// <summary>True while the panel is open (player input and cursor locking stop, like other menus).</summary>
    public static bool IsOpen => instance != null && instance.open;

    private enum Tab { Items, Perks, Coins, Floors, Emeralds }
    private static readonly string[] TabNames = { "Give Item", "Add Perk", "Add Coin", "Skip Level", "Emeralds" };
    private static readonly string[] ItemFilters = { "All", "Weapons", "Consumables", "Rings", "Spells" };

    private bool open;
    private Tab tab = Tab.Items;
    private float savedTimeScale = 1f;
    private Vector2 scroll;
    private string search = "";
    private int amount = 1;
    private int itemFilter;
    private string message = "";
    private float messageUntil;

    private GUIStyle rowLabel, header, small, current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) { return; }
        var go = new GameObject("DevItemGiver");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DevItemGiver>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) { return; }

        if (keyboard.f6Key.wasPressedThisFrame)
        {
            if (open) { Close(); }
            else if (CanOpen()) { Open(); }
        }
        else if (open && PlayerController.instance == null)
        {
            Close(); // left the game scene
        }
    }

    // Only in a run, and not on top of another menu
    private static bool CanOpen()
    {
        if (PlayerController.instance == null || InventoryManager.instance == null) { return false; }
        if (PauseGame.instance != null && PauseGame.instance.IsPaused) { return false; }
        if (ShopUI.IsOpen) { return false; }
        if (PerkSelectionManager.instance != null && PerkSelectionManager.instance.isPanelActive) { return false; }
        if (EquipmentLoadOut.instance != null && EquipmentLoadOut.instance.isPanelActive) { return false; }
        return true;
    }

    private void Open()
    {
        open = true;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        message = "";
    }

    private void Close()
    {
        open = false;
        Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void OnDestroy()
    {
        if (instance == this && open) { Time.timeScale = 1f; }
    }

    // =========================================================
    // ACTIONS
    // =========================================================

    private void GiveItem(Item prefab, int count)
    {
        if (prefab == null || PlayerController.instance == null) { return; }
        int given = 0;
        for (int i = 0; i < count; i++)
        {
            Item item = Instantiate(prefab, PlayerController.instance.transform.position, Quaternion.identity);
            if (item.GiveToPlayer()) { given++; }
            else { Destroy(item.gameObject); break; } // inventory full for this one
        }
        Say(given == count
            ? $"Gave {prefab.equipmentName}{(count > 1 ? $" x{count}" : "")}"
            : given == 0 ? $"Can't carry more {prefab.equipmentName}" : $"Gave {given} (inventory full)");
    }

    // Owned perk: set it to this card's level. New perk: free slot (unlocking one if all are full).
    private void AddPerk(PerkCardDefinition card)
    {
        PerkSelectionManager perks = PerkSelectionManager.instance;
        PerkCardInventory inv = perks != null ? perks.Inventory : null;
        if (card == null || inv == null) { Say("Perk inventory not found"); return; }

        int owned = inv.FindSlotWithPerk(card.PerkStat);
        if (owned >= 0)
        {
            inv.Replace(owned, card, card.Level);
            Say($"{card.DisplayName} set to LV {card.Level}");
        }
        else
        {
            if (!inv.HasFreeUnlockedSlot() && inv.HasLockedSlot) { inv.UnlockNextSlot(); }
            if (inv.TryAdd(card, card.Level, out _)) { Say($"Added {card.DisplayName} LV {card.Level}"); }
            else { Say("No free perk slot (all 9 used)"); return; }
        }
        perks.NotifyChanged(); // refresh the perk UI and the perk effects
    }

    private void GiveCoins(int coins)
    {
        PlayerController player = PlayerController.instance;
        if (player == null || player.coinWallet == null) { return; }
        player.coinWallet.AddCoins(coins);
        Say($"+{coins} coins");
    }

    private void GiveKey(TreasureKeyType type)
    {
        if (PlayerUpgrades.Instance == null) { return; }
        for (int i = 0; i < amount; i++) { PlayerUpgrades.Instance.AddKey(type); }
        Say($"+{amount} {type} key{(amount > 1 ? "s" : "")}");
    }

    private void GoToFloor(int floor)
    {
        RoguelikeManager manager = RoguelikeManager.instance;
        if (manager == null) { return; }
        if (!RoguelikeManager.IsDungeonReady) { Say("Wait until the floor has finished loading"); return; }
        Close(); // the floor transition runs on game time
        manager.SkipToFloor(floor);
    }

    private void Say(string text)
    {
        message = text;
        messageUntil = Time.unscaledTime + 3f;
    }

    // =========================================================
    // PANEL (IMGUI, scaled to a 1920x1080 layout)
    // =========================================================

    private void OnGUI()
    {
        if (!open) { return; }

        // The game re-locks the cursor for gameplay; keep it free while the panel is open
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        float scale = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float w = 780f, h = 880f;
        float x = (Screen.width / scale - w) * 0.5f, y = (1080f - h) * 0.5f;

        BuildStyles();
        GUI.Box(new Rect(x, y, w, h), GUIContent.none);
        GUI.Box(new Rect(x, y, w, h), GUIContent.none); // darker

        GUILayout.BeginArea(new Rect(x + 16f, y + 12f, w - 32f, h - 24f));

        GUILayout.BeginHorizontal();
        GUILayout.Label("Cheats", header);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close  [F6]", GUILayout.Width(130f), GUILayout.Height(32f))) { Close(); }
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);

        Tab newTab = (Tab)GUILayout.Toolbar((int)tab, TabNames, GUILayout.Height(40f));
        if (newTab != tab) { tab = newTab; scroll = Vector2.zero; search = ""; }
        GUILayout.Space(10f);

        switch (tab)
        {
            case Tab.Items: DrawItems(); break;
            case Tab.Perks: DrawPerks(); break;
            case Tab.Coins: DrawCoins(); break;
            case Tab.Floors: DrawFloors(); break;
            case Tab.Emeralds: DrawEmeralds(); break;
        }

        GUILayout.FlexibleSpace();
        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil) { GUILayout.Label(message, rowLabel); }

        GUILayout.EndArea();
        GUI.matrix = Matrix4x4.identity;
    }

    // ---------- Give Item ----------
    private void DrawItems()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search", small, GUILayout.Width(60f));
        search = GUILayout.TextField(search, GUILayout.Width(240f), GUILayout.Height(28f));
        GUILayout.Space(16f);
        DrawAmount();
        GUILayout.EndHorizontal();
        GUILayout.Space(4f);
        itemFilter = GUILayout.Toolbar(itemFilter, ItemFilters, GUILayout.Height(30f));
        GUILayout.Space(6f);

        DevItemCatalog catalog = DevItemCatalog.Load();
        if (catalog == null || catalog.items.Count == 0) { NoCatalog(); return; }

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        foreach (Item item in FilteredItems(catalog.items))
        {
            GUILayout.BeginHorizontal(GUILayout.Height(52f));
            DrawIcon(item.spr_Icon_NoFrame != null ? item.spr_Icon_NoFrame : item.spr_Icon_Frame);
            GUILayout.BeginVertical();
            GUILayout.Label(item.equipmentName, rowLabel);
            GUILayout.Label(Category(item) + Owned(item), small);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(amount > 1 ? $"Give x{amount}" : "Give", GUILayout.Width(110f), GUILayout.Height(40f))) { GiveItem(item, amount); }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    // ---------- Add Perk ----------
    private void DrawPerks()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search", small, GUILayout.Width(60f));
        search = GUILayout.TextField(search, GUILayout.Width(240f), GUILayout.Height(28f));
        GUILayout.FlexibleSpace();
        PerkCardInventory inv = PerkSelectionManager.instance != null ? PerkSelectionManager.instance.Inventory : null;
        if (inv != null) { GUILayout.Label($"Slots unlocked: {inv.UnlockedSlotCount} / {PerkCardInventory.MaxSlots}", small); }
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);

        DevItemCatalog catalog = DevItemCatalog.Load();
        if (catalog == null || catalog.perks == null || catalog.perks.Count == 0) { NoCatalog(); return; }

        string s = search.Trim().ToLowerInvariant();
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        foreach (PerkCardDefinition card in catalog.perks)
        {
            if (card == null) { continue; }
            if (s.Length > 0 && !card.DisplayName.ToLowerInvariant().Contains(s)) { continue; }

            int ownedLevel = inv != null ? inv.GetOwnedLevel(card.PerkStat) : 0;
            GUILayout.BeginHorizontal(GUILayout.Height(56f));
            DrawIcon(card.Icon);
            GUILayout.BeginVertical();
            GUILayout.Label($"{card.DisplayName}  LV {card.Level}", ownedLevel == card.Level ? current : rowLabel);
            GUILayout.Label($"{card.Tier}{(ownedLevel > 0 ? $"   (have LV {ownedLevel})" : "")}   {Short(card.Description)}", small);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            string label = ownedLevel == 0 ? "Add" : ownedLevel == card.Level ? "Owned" : "Set LV";
            GUI.enabled = ownedLevel != card.Level;
            if (GUILayout.Button(label, GUILayout.Width(110f), GUILayout.Height(40f))) { AddPerk(card); }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    // ---------- Add Coin ----------
    private void DrawCoins()
    {
        PlayerController player = PlayerController.instance;
        if (player != null && player.coinWallet != null) { GUILayout.Label($"Coins: {player.coinWallet.Coins}", rowLabel); }
        GUILayout.Space(6f);

        GUILayout.Label("Coins", small);
        GUILayout.BeginHorizontal();
        foreach (int c in new[] { 100, 500, 1000, 5000 })
        {
            if (GUILayout.Button($"+{c}", GUILayout.Height(44f))) { GiveCoins(c); }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(16f);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Treasure keys", small);
        GUILayout.FlexibleSpace();
        DrawAmount();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        foreach (TreasureKeyType key in new[] { TreasureKeyType.Bronze, TreasureKeyType.Silver, TreasureKeyType.Gold })
        {
            int have = PlayerUpgrades.Instance != null ? PlayerUpgrades.Instance.GetKeyCount(key) : 0;
            if (GUILayout.Button($"{key} key  (have {have})", GUILayout.Height(44f))) { GiveKey(key); }
        }
        GUILayout.EndHorizontal();
    }

    // ---------- Skip Level ----------
    private void DrawFloors()
    {
        RoguelikeManager manager = RoguelikeManager.instance;
        if (manager == null || manager.FloorCount == 0) { GUILayout.Label("No floors found", rowLabel); return; }

        GUILayout.Label($"Current floor: {FloorName(manager, manager.dungeonIndex)}", rowLabel);
        GUILayout.Label("Pick a floor to go to (the usual floor transition plays).", small);
        GUILayout.Space(8f);

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        const int perRow = 3; // 3 floors per world
        for (int i = 0; i < manager.FloorCount; i += perRow)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + perRow, manager.FloorCount); j++)
            {
                bool here = j == manager.dungeonIndex;
                GUI.enabled = !here;
                if (GUILayout.Button(here ? $"{FloorName(manager, j)}  (here)" : FloorName(manager, j), GUILayout.Height(52f))) { GoToFloor(j); }
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    // ---------- Emeralds (meta progression) ----------
    private bool confirmReset;

    private void DrawEmeralds()
    {
        GUILayout.Label($"Emeralds: {MetaProgress.Emeralds}   (this run +{MetaRun.RunEmeralds})", rowLabel);
        GUILayout.Label($"Runs {MetaProgress.Runs}   Wins {MetaProgress.Wins}   Lifetime Emeralds {MetaProgress.LifetimeEmeralds}", small);
        GUILayout.Space(10f);

        GUILayout.BeginHorizontal();
        foreach (int g in new[] { 100, 500, 1000, 5000 })
        {
            if (GUILayout.Button($"+{g} Emeralds", GUILayout.Height(44f))) { MetaProgress.CheatAddEmeralds(g); Say($"+{g} Emeralds"); }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(10f);

        if (GUILayout.Button("Unlock everything at the Camp", GUILayout.Height(44f))) { MetaProgress.CheatUnlockAll(); Say("All Camp unlocks bought (boosts apply from the next run)"); }
        GUILayout.Space(10f);

        if (!confirmReset)
        {
            if (GUILayout.Button("Reset meta progression...", GUILayout.Height(44f))) { confirmReset = true; }
        }
        else
        {
            GUILayout.Label("Erase all Emeralds, unlocks and records?", rowLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes, reset", GUILayout.Height(44f))) { MetaProgress.CheatReset(); confirmReset = false; Say("Meta progression reset"); }
            if (GUILayout.Button("Cancel", GUILayout.Height(44f))) { confirmReset = false; }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(14f);
        GUILayout.Label("Camp unlocks:", small);
        foreach (MetaUnlocks.Unlock u in MetaUnlocks.All)
        {
            GUILayout.Label($"  {u.name}: Lv {MetaProgress.GetLevel(u.id)} / {u.MaxLevel}", small);
        }
    }

    private static string FloorName(RoguelikeManager m, int i)
    {
        string n = m.str_DungeonIndex != null && i < m.str_DungeonIndex.Length ? m.str_DungeonIndex[i] : null;
        return string.IsNullOrEmpty(n) ? $"Floor {i / 3 + 1}-{i % 3 + 1}" : n.Replace("\n", " ");
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void DrawAmount()
    {
        GUILayout.Label("Amount", small, GUILayout.Width(64f));
        if (GUILayout.Button("-", GUILayout.Width(30f), GUILayout.Height(28f))) { amount = Mathf.Max(1, amount - 1); }
        GUILayout.Label(amount.ToString(), rowLabel, GUILayout.Width(40f));
        if (GUILayout.Button("+", GUILayout.Width(30f), GUILayout.Height(28f))) { amount = Mathf.Min(99, amount + 1); }
        if (GUILayout.Button("x10", GUILayout.Width(48f), GUILayout.Height(28f))) { amount = 10; }
    }

    private void NoCatalog()
    {
        GUILayout.Label("No catalog found (Tools > Dev > Rebuild Item Catalog in the editor).", rowLabel);
    }

    private IEnumerable<Item> FilteredItems(List<Item> items)
    {
        string s = search.Trim().ToLowerInvariant();
        foreach (Item item in items)
        {
            if (item == null) { continue; }
            if (s.Length > 0 && !item.equipmentName.ToLowerInvariant().Contains(s)) { continue; }
            bool weapon = item is Weapon;
            switch (itemFilter)
            {
                case 1: if (!weapon) continue; break;
                case 2: if (weapon || item.itemCategory != ItemCategory.Consumable) continue; break;
                case 3: if (weapon || item.itemCategory != ItemCategory.Ring) continue; break;
                case 4: if (weapon || (item.itemCategory != ItemCategory.Pyromancy && item.itemCategory != ItemCategory.Magic)) continue; break;
            }
            yield return item;
        }
    }

    private static string Category(Item item)
    {
        if (item is Weapon) { return "Weapon"; }
        switch (item.itemCategory)
        {
            case ItemCategory.Consumable: return "Consumable";
            case ItemCategory.Ring: return "Ring";
            case ItemCategory.Pyromancy: return "Pyromancy";
            case ItemCategory.Magic: return "Sorcery";
            default: return "Item";
        }
    }

    // How many the player already has (helps see limits: 2 per weapon kind, 1 ring/spell, 99 consumables)
    private static string Owned(Item item)
    {
        InventoryManager inv = InventoryManager.instance;
        if (inv == null) { return ""; }
        if (item is Weapon w) { return $"   (have {inv.CountSameKind(w)} / {InventoryManager.WeaponCarryLimit})"; }
        switch (item.itemCategory)
        {
            case ItemCategory.Consumable:
                return inv.consumables.TryGetValue(item.itemType, out int c) ? $"   (have {c})" : "   (have 0)";
            case ItemCategory.Ring: return inv.rings.Exists(i => i.itemType == item.itemType) ? "   (owned)" : "";
            case ItemCategory.Pyromancy: return inv.pyromancies.Exists(i => i.itemType == item.itemType) ? "   (owned)" : "";
            case ItemCategory.Magic: return inv.magics.Exists(i => i.itemType == item.itemType) ? "   (owned)" : "";
            default: return "";
        }
    }

    private static string Short(string text)
    {
        if (string.IsNullOrEmpty(text)) { return ""; }
        text = text.Replace("\n", " ");
        return text.Length > 60 ? text.Substring(0, 57) + "..." : text;
    }

    private static void DrawIcon(Sprite sprite)
    {
        Rect rect = GUILayoutUtility.GetRect(48f, 48f, GUILayout.Width(48f), GUILayout.Height(48f));
        GUILayout.Space(8f);
        if (sprite == null || sprite.texture == null) { return; }
        Rect tr = sprite.textureRect;
        Texture tex = sprite.texture;
        Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        float aspect = tr.width / Mathf.Max(1f, tr.height); // keep aspect
        Rect r = rect;
        if (aspect > 1f) { float hh = r.width / aspect; r.y += (r.height - hh) * 0.5f; r.height = hh; }
        else { float ww = r.height * aspect; r.x += (r.width - ww) * 0.5f; r.width = ww; }
        GUI.DrawTextureWithTexCoords(r, tex, uv);
    }

    private void BuildStyles()
    {
        if (rowLabel != null) { return; }
        rowLabel = new GUIStyle(GUI.skin.label) { fontSize = 20, normal = { textColor = Color.white } };
        current = new GUIStyle(rowLabel) { normal = { textColor = new Color(0.6f, 1f, 0.6f) } };
        header = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.5f) } };
        small = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
        GUI.skin.button.fontSize = 18;
        GUI.skin.textField.fontSize = 18;
    }
}
