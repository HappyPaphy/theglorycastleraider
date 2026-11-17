using System.Collections.Generic;
using System.IO;
using System.Text;
using DunGen;
using DunGen.Graph;
using DunGen.Weighting;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UI;

/// <summary>
/// One-shot builder for the Shop:
///  1. Pixel icons (keys, totem, upgrades, coin) + entrance runner texture
///  2. Shop item definitions + Shop catalog
///  3. Shop UI prefab (Resources/UI/ShopUI)
///  4. Shop counter + Shop entrance (DunGen connector) prefabs
///  5. Shop Room (Prison) built from the Weapon Room: back doorway sealed, counter + shelves, shopkeeper
///  6. Shop Room tile set, injected once into the Prison First / Second floor flows
/// Re-run from Tools > Shop > Build Shop (safe to repeat: assets are updated in place).
/// </summary>
public static class ShopSetupBuilder
{
    private const string AutoKey = "ShopSetupBuilder_v1";

    private const string DataFolder = "Assets/013-Datas/Shop";
    private const string IconFolder = DataFolder + "/Icons";
    private const string ItemFolder = DataFolder + "/Items";
    private const string CatalogPath = DataFolder + "/ShopCatalog (Prison).asset";
    private const string ShopPrefabFolder = "Assets/003-Prefabs/Shop";
    private const string UIPrefabPath = "Assets/Resources/UI/ShopUI.prefab";
    private const string CounterPath = ShopPrefabFolder + "/ShopCounter.prefab";
    private const string EntrancePath = ShopPrefabFolder + "/ShopEntrance.prefab";
    private const string RunnerMaterialPath = "Assets/006-Materials/Furniture/Mat_ShopRunner.mat";

    private const string RoomFolder = "Assets/012-MyDungenAsset/Room/Prison";
    private const string SourceRoomPath = RoomFolder + "/Medium Room (Weapon Room).prefab";
    private const string ShopRoomPath = RoomFolder + "/Shop Room (Prison).prefab";
    private const string TileSetPath = "Assets/012-MyDungenAsset/Tile Set/Prison/Shop Room Set (Prison).asset";
    private static readonly string[] FlowPaths =
    {
        "Assets/012-MyDungenAsset/Dungeon Flow (Prison) (First Floor).asset",
        "Assets/012-MyDungenAsset/Dungeon Flow (Prison) (Second Floor).asset",
    };

    private const string FontPath = "Assets/020-Others/Font/KnightQuest/KnightsQuest SDF.asset";
    private const string PromptSourcePath = "Assets/003-Prefabs/Items/Consumables/HealthPotion_Small.prefab";
    private const string ShelfPath = "Assets/003-Prefabs/Furniture/Shelf.prefab";
    private const string BarrelPath = "Assets/003-Prefabs/Furniture/Barrel.prefab";
    private const string PotTallPath = "Assets/003-Prefabs/Furniture/ClayPot_Tall.prefab";
    private const string PotSmallPath = "Assets/003-Prefabs/Furniture/ClayPot_Small.prefab";
    private const string CounterMaterialPath = "Assets/006-Materials/Furniture/Mat_Shelf.mat";

    private const string Items = "Assets/003-Prefabs/Items";
    private const string Weapons = "Assets/003-Prefabs/Weapons";

    private const float FloorY = 0.09f;
    private const int ObstacleLayer = 8;

    internal static StringBuilder log;
    internal static int errors;

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            BuildAll();
        };
    }

    // One-shot: removes the old "Shop" text sign from the already-built Shop Room
    [InitializeOnLoadMethod]
    private static void RemoveSignOnce()
    {
        const string key = "ShopSetupBuilder_RemoveSign_v1";
        if (EditorPrefs.GetBool(key + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(key + Application.dataPath, true);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ShopRoomPath) == null) { return; }

            GameObject room = PrefabUtility.LoadPrefabContents(ShopRoomPath);
            try
            {
                Transform sign = room.transform.Find("Shop/Shop Sign");
                if (sign != null)
                {
                    Object.DestroyImmediate(sign.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(room, ShopRoomPath);
                }
                Debug.Log($"[ShopSetup] Sign removed: {sign != null}");
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        };
    }

    [MenuItem("Tools/Shop/Build Shop")]
    public static void BuildAll()
    {
        log = new StringBuilder();
        errors = 0;

        try
        {
            Directory.CreateDirectory(IconFolder);
            Directory.CreateDirectory(ItemFolder);
            Directory.CreateDirectory(ShopPrefabFolder);
            Directory.CreateDirectory("Assets/Resources/UI");
            AssetDatabase.Refresh();

            Step("Icons", BuildIcons);
            Step("Items", BuildItems);
            Step("UI", BuildUI);
            Step("Counter", BuildCounter);
            Step("Entrance", BuildEntrance);
            Step("Room", BuildRoom);
            Step("Dungeon", HookIntoDungeon);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/shop_setup.txt", log.ToString());
        Debug.Log($"[ShopSetup] Done with {errors} error(s). Report: Library/PrisonDecor/shop_setup.txt");
    }

    private static void Step(string name, System.Action action)
    {
        log.AppendLine("== " + name);
        try { action(); }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + name + ": " + e);
        }
    }

    // =========================================================
    // 1. ICONS
    // =========================================================

    private static Sprite iconCoin, iconBronze, iconSilver, iconGold, iconTotem, iconSpeed, iconCarry;
    private static Texture2D runnerTexture;

    private static void BuildIcons()
    {
        iconCoin = ShopPixelArt.SaveSprite(ShopPixelArt.Coin(), IconFolder + "/Icon_Coin.png");
        iconBronze = ShopPixelArt.SaveSprite(ShopPixelArt.Key(ShopPixelArt.Hex("#c07a3a"), ShopPixelArt.Hex("#6e4220")), IconFolder + "/Icon_Key_Bronze.png");
        iconSilver = ShopPixelArt.SaveSprite(ShopPixelArt.Key(ShopPixelArt.Hex("#c9ced6"), ShopPixelArt.Hex("#6f7782")), IconFolder + "/Icon_Key_Silver.png");
        iconGold = ShopPixelArt.SaveSprite(ShopPixelArt.Key(ShopPixelArt.Hex("#f0c040"), ShopPixelArt.Hex("#9a6e18")), IconFolder + "/Icon_Key_Gold.png");
        iconTotem = ShopPixelArt.SaveSprite(ShopPixelArt.Totem(), IconFolder + "/Icon_ReviveTotem.png");
        iconSpeed = ShopPixelArt.SaveSprite(ShopPixelArt.AttackSpeed(), IconFolder + "/Icon_AttackSpeed.png");
        iconCarry = ShopPixelArt.SaveSprite(ShopPixelArt.WeaponCarry(), IconFolder + "/Icon_WeaponCarry.png");

        Sprite runner = ShopPixelArt.SaveSprite(ShopPixelArt.Runner(), IconFolder + "/Tex_ShopRunner.png", 16);
        runnerTexture = runner != null ? runner.texture : null;

        log.AppendLine($"icons ok: coin={iconCoin != null} keys={iconBronze != null}/{iconSilver != null}/{iconGold != null} totem={iconTotem != null} runner={runnerTexture != null}");
    }

    // =========================================================
    // 2. ITEMS + CATALOG
    // =========================================================

    private static void BuildItems()
    {
        var catalogEntries = new List<ShopCatalog.Entry>();

        void Add(ShopItemDefinition def, bool always, float weight)
        {
            if (def == null) { return; }
            catalogEntries.Add(new ShopCatalog.Entry { item = def, alwaysStocked = always, weight = weight });
        }

        // Potions
        Add(ItemDef("HealthPotion_Small", "Small Health Potion", "Restores a little health.", 30, Items + "/Consumables/HealthPotion_Small.prefab", stock: 3), true, 1f);
        Add(ItemDef("HealthPotion_Medium", "Health Potion", "Restores a good amount of health.", 55, Items + "/Consumables/HealthPotion_Medium.prefab", stock: 2), false, 3f);
        Add(ItemDef("HealthPotion_Big", "Large Health Potion", "Restores a large amount of health.", 85, Items + "/Consumables/HealthPotion_Big.prefab", stock: 1), false, 2f);
        Add(ItemDef("ManaPotion_Small", "Small Mana Potion", "Restores a little mana.", 25, Items + "/Consumables/ManaPotion_Small.prefab", stock: 3), true, 1f);
        Add(ItemDef("ManaPotion_Medium", "Mana Potion", "Restores a good amount of mana.", 45, Items + "/Consumables/ManaPotion_Medium.prefab", stock: 2), false, 2.5f);
        Add(ItemDef("ManaPotion_Big", "Large Mana Potion", "Restores a large amount of mana.", 70, Items + "/Consumables/ManaPotion_Big.prefab", stock: 1), false, 1.5f);

        // Weapons
        Add(ItemDef("Weapon_ShortSword", "Short Sword", "A quick, reliable blade.", 120, Weapons + "/Weapon_ShortSword.prefab", kind: ShopItemKind.Weapon), false, 1.5f);
        Add(ItemDef("Weapon_WoodenShield", "Wooden Shield", "Block and parry incoming blows.", 100, Weapons + "/Weapon_WoodenShield.prefab", kind: ShopItemKind.Weapon), false, 1.5f);
        Add(ItemDef("Pyromancy_DimmedHand", "Pyromancy Hand", "A flame catalyst. Lets you cast pyromancies.", 150, Weapons + "/Pyromancy_DimmedHand.prefab", kind: ShopItemKind.Weapon), false, 1.2f);

        // Rings / spells (unique: the shop refuses if you already own one)
        Add(ItemDef("Ring_Vitality", "Ring of Vitality", "A ring that strengthens the body.", 140, Items + "/Rings/Ring_Vitality.prefab"), false, 0.8f);
        Add(ItemDef("Ring_Stamina", "Ring of Stamina", "A ring that steadies your breath.", 140, Items + "/Rings/Ring_Stamina.prefab"), false, 0.8f);
        Add(ItemDef("Spell_SmallFireball", "Fireball", "Pyromancy. Hurl a ball of fire.", 130, Items + "/Spells/Spell_SmallFireball.prefab"), false, 0.8f);
        Add(ItemDef("Spell_SmallCombustion", "Combustion", "Pyromancy. A short burst of flame.", 130, Items + "/Spells/Spell_SmallCombustion.prefab"), false, 0.8f);

        // Upgrades
        Add(SpecialDef("Upgrade_WeaponCarry", "Weapon Satchel", "Carry one more of each weapon type.", 180, ShopItemKind.WeaponCarryUpgrade, iconCarry, maxRun: 2), false, 1f);
        Add(SpecialDef("Upgrade_AttackSpeed", "Whetstone of Haste", "Your weapons attack 10% faster for the rest of the run.", 150, ShopItemKind.AttackSpeedUpgrade, iconSpeed, maxRun: 3), false, 1.2f);
        Add(SpecialDef("ReviveTotem", "Revive Totem", "When a hit would kill you, the totem breaks and you get back up with half your health.", 250, ShopItemKind.ReviveTotem, iconTotem, maxRun: 2), false, 0.9f);

        // Keys
        Add(KeyDef("Key_Bronze", "Bronze Key", "Opens a bronze treasure room.", 60, TreasureKeyType.Bronze, iconBronze), false, 1.2f);
        Add(KeyDef("Key_Silver", "Silver Key", "Opens a silver treasure room.", 120, TreasureKeyType.Silver, iconSilver), false, 0.8f);
        Add(KeyDef("Key_Gold", "Gold Key", "Opens a gold treasure room.", 200, TreasureKeyType.Gold, iconGold), false, 0.5f);

        ShopCatalog catalog = LoadOrCreate<ShopCatalog>(CatalogPath);
        catalog.itemsPerShop = 8;
        catalog.entries = catalogEntries;
        EditorUtility.SetDirty(catalog);
        log.AppendLine($"catalog: {catalogEntries.Count} entries");
    }

    private static ShopItemDefinition ItemDef(string file, string name, string desc, int price, string prefabPath,
        ShopItemKind kind = ShopItemKind.Item, int stock = 1)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Item item = prefab != null ? prefab.GetComponent<Item>() : null;
        if (item == null)
        {
            errors++;
            log.AppendLine("!! missing item prefab " + prefabPath);
            return null;
        }

        ShopItemDefinition def = Base(file, name, desc, price, kind, null, stock, 0);
        def.itemPrefab = item;
        EditorUtility.SetDirty(def);
        return def;
    }

    private static ShopItemDefinition SpecialDef(string file, string name, string desc, int price, ShopItemKind kind, Sprite icon, int maxRun)
    {
        return Base(file, name, desc, price, kind, icon, 1, maxRun);
    }

    private static ShopItemDefinition KeyDef(string file, string name, string desc, int price, TreasureKeyType key, Sprite icon)
    {
        ShopItemDefinition def = Base(file, name, desc, price, ShopItemKind.Key, icon, 1, 0);
        def.keyType = key;
        EditorUtility.SetDirty(def);
        return def;
    }

    private static ShopItemDefinition Base(string file, string name, string desc, int price, ShopItemKind kind, Sprite icon, int stock, int maxRun)
    {
        ShopItemDefinition def = LoadOrCreate<ShopItemDefinition>($"{ItemFolder}/{file}.asset");
        def.displayName = name;
        def.description = desc;
        def.price = price;
        def.kind = kind;
        def.icon = icon;
        def.stockPerShop = stock;
        def.maxPurchasesPerRun = maxRun;
        def.attackSpeedBonus = 0.1f;
        EditorUtility.SetDirty(def);
        return def;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) { return asset; }
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    // =========================================================
    // 3. UI PREFAB
    // =========================================================

    private static readonly Color Gold = new Color(0.95f, 0.78f, 0.36f);
    private static readonly Color Parchment = new Color(0.92f, 0.87f, 0.76f);
    private static readonly Color Muted = new Color(0.62f, 0.58f, 0.52f);
    private static readonly Color WindowColor = new Color(0.10f, 0.07f, 0.05f, 0.97f);
    private static readonly Color PanelColor = new Color(0.06f, 0.04f, 0.03f, 0.9f);
    private static readonly Color RowColor = new Color(0.17f, 0.12f, 0.08f, 1f);

    private static TMP_FontAsset font;

    private static void BuildUI()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) { log.AppendLine("font not found, using TMP default"); }

        GameObject root = new GameObject("ShopUI", typeof(RectTransform));
        try
        {
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            ShopUI shop = root.AddComponent<ShopUI>();

            // Dim background + fade group
            RectTransform panel = Rect("Panel", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();

            // Window
            RectTransform window = Centered("Window", panel, new Vector2(1240f, 740f), Vector2.zero);
            Image windowBg = window.gameObject.AddComponent<Image>();
            windowBg.color = WindowColor;
            AddOutline(window.gameObject, Gold, 3f);

            TMP_Text title = Text("Title", window, "Wandering Merchant", 58, Gold, TextAlignmentOptions.Center);
            TopStretch((RectTransform)title.transform, 18f, 76f, 40f);

            // Coins (top right)
            RectTransform coinBox = TopRight("Coins", window, new Vector2(220f, 56f), new Vector2(-34f, -30f));
            Image coinIcon = Img("Icon", coinBox, iconCoin, Color.white);
            Pin((RectTransform)coinIcon.transform, new Vector2(0f, 0.5f), new Vector2(44f, 44f), new Vector2(22f, 0f));
            TMP_Text coins = Text("Amount", coinBox, "0", 42, Gold, TextAlignmentOptions.MidlineLeft);
            Fill((RectTransform)coins.transform, new Vector2(54f, 0f), Vector2.zero);

            Image divider = Img("Divider", window, null, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
            TopStretch((RectTransform)divider.transform, 102f, 3f, 40f);

            // List (left)
            RectTransform list = TopLeft("List", window, new Vector2(720f, 520f), new Vector2(40f, -124f));
            list.gameObject.AddComponent<Image>().color = PanelColor;
            VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ShopRowUI row = BuildRow(list);

            // Details (right)
            RectTransform details = TopLeft("Details", window, new Vector2(420f, 520f), new Vector2(780f, -124f));
            details.gameObject.AddComponent<Image>().color = PanelColor;

            Image detailIcon = Img("Icon", details, null, Color.white);
            detailIcon.preserveAspect = true;
            Pin((RectTransform)detailIcon.transform, new Vector2(0.5f, 1f), new Vector2(128f, 128f), new Vector2(0f, -88f));

            TMP_Text detailName = Text("Name", details, "", 40, Gold, TextAlignmentOptions.Center);
            TopStretch((RectTransform)detailName.transform, 168f, 52f, 16f);
            TMP_Text detailPrice = Text("Price", details, "", 32, Gold, TextAlignmentOptions.Center);
            TopStretch((RectTransform)detailPrice.transform, 222f, 40f, 16f);
            TMP_Text detailOwned = Text("Owned", details, "", 24, Muted, TextAlignmentOptions.Center);
            TopStretch((RectTransform)detailOwned.transform, 262f, 32f, 16f);
            TMP_Text detailDesc = Text("Description", details, "", 27, Parchment, TextAlignmentOptions.TopLeft);
            detailDesc.textWrappingMode = TextWrappingModes.Normal;
            RectTransform descRt = (RectTransform)detailDesc.transform;
            descRt.anchorMin = new Vector2(0f, 0f);
            descRt.anchorMax = new Vector2(1f, 1f);
            descRt.offsetMin = new Vector2(26f, 20f);
            descRt.offsetMax = new Vector2(-26f, -306f);

            // Bottom: message + hint
            TMP_Text message = Text("Message", window, "", 32, Gold, TextAlignmentOptions.Center);
            BottomStretch((RectTransform)message.transform, 52f, 40f, 40f);
            TMP_Text hint = Text("Hint", window, "Submit / Click: Buy        Cancel: Leave", 24, Muted, TextAlignmentOptions.Center);
            BottomStretch((RectTransform)hint.transform, 16f, 32f, 40f);

            // Wire ShopUI
            SerializedObject so = new SerializedObject(shop);
            so.FindProperty("panelRoot").objectReferenceValue = group;
            so.FindProperty("window").objectReferenceValue = window;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("coinsText").objectReferenceValue = coins;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("listContent").objectReferenceValue = list;
            so.FindProperty("rowTemplate").objectReferenceValue = row;
            so.FindProperty("detailIcon").objectReferenceValue = detailIcon;
            so.FindProperty("detailName").objectReferenceValue = detailName;
            so.FindProperty("detailDescription").objectReferenceValue = detailDesc;
            so.FindProperty("detailPrice").objectReferenceValue = detailPrice;
            so.FindProperty("detailOwned").objectReferenceValue = detailOwned;
            so.ApplyModifiedPropertiesWithoutUndo();

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            PrefabUtility.SaveAsPrefabAsset(root, UIPrefabPath);
            log.AppendLine("ui saved " + UIPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static ShopRowUI BuildRow(Transform parent)
    {
        RectTransform rt = new GameObject("Row Template", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(0f, 56f);

        Image bg = rt.gameObject.AddComponent<Image>();
        bg.color = RowColor;
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.15f, 1f);
        colors.selectedColor = new Color(1.45f, 1.25f, 0.95f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.55f);
        colors.colorMultiplier = 1.6f;
        button.colors = colors;

        ShopRowUI row = rt.gameObject.AddComponent<ShopRowUI>();

        Image frame = Img("Selection", rt, null, new Color(Gold.r, Gold.g, Gold.b, 0.14f));
        Fill((RectTransform)frame.transform, Vector2.zero, Vector2.zero);
        AddOutline(frame.gameObject, Gold, 2f);
        frame.enabled = false;

        Image icon = Img("Icon", rt, null, Color.white);
        icon.preserveAspect = true;
        Pin((RectTransform)icon.transform, new Vector2(0f, 0.5f), new Vector2(46f, 46f), new Vector2(34f, 0f));

        TMP_Text name = Text("Name", rt, "Item", 30, Color.white, TextAlignmentOptions.MidlineLeft);
        RectTransform nameRt = (RectTransform)name.transform;
        nameRt.anchorMin = new Vector2(0f, 0f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.offsetMin = new Vector2(72f, 0f);
        nameRt.offsetMax = new Vector2(-230f, 0f);

        TMP_Text stock = Text("Stock", rt, "", 24, Muted, TextAlignmentOptions.MidlineRight);
        Pin((RectTransform)stock.transform, new Vector2(1f, 0.5f), new Vector2(70f, 40f), new Vector2(-195f, 0f));

        TMP_Text price = Text("Price", rt, "0", 30, Gold, TextAlignmentOptions.MidlineRight);
        Pin((RectTransform)price.transform, new Vector2(1f, 0.5f), new Vector2(110f, 40f), new Vector2(-103f, 0f));

        Image coin = Img("Coin", rt, iconCoin, Color.white);
        Pin((RectTransform)coin.transform, new Vector2(1f, 0.5f), new Vector2(28f, 28f), new Vector2(-26f, 0f));

        RectTransform soldOut = new GameObject("Sold Out", typeof(RectTransform)).GetComponent<RectTransform>();
        soldOut.SetParent(rt, false);
        Fill(soldOut, Vector2.zero, Vector2.zero);
        Image soldBg = soldOut.gameObject.AddComponent<Image>();
        soldBg.color = new Color(0f, 0f, 0f, 0.45f);
        soldBg.raycastTarget = false;
        TMP_Text soldText = Text("Label", soldOut, "SOLD OUT", 26, new Color(0.85f, 0.3f, 0.25f), TextAlignmentOptions.MidlineRight);
        Fill((RectTransform)soldText.transform, Vector2.zero, new Vector2(-24f, 0f));
        soldOut.gameObject.SetActive(false);

        row.button = button;
        row.icon = icon;
        row.nameText = name;
        row.priceText = price;
        row.stockText = stock;
        row.soldOutOverlay = soldOut.gameObject;
        row.selectionFrame = frame;
        return row;
    }

    // ---------- UI helpers ----------

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
    {
        RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        return rt;
    }

    private static RectTransform Centered(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        RectTransform rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    private static RectTransform TopLeft(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        RectTransform rt = Rect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    private static RectTransform TopRight(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        RectTransform rt = Rect(name, parent, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    private static void TopStretch(RectTransform rt, float top, float height, float margin)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(margin, -top - height);
        rt.offsetMax = new Vector2(-margin, -top);
    }

    private static void BottomStretch(RectTransform rt, float bottom, float height, float margin)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(margin, bottom);
        rt.offsetMax = new Vector2(-margin, bottom + height);
    }

    private static void Fill(RectTransform rt, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }

    private static void Pin(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    private static Image Img(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) { t.font = font; }
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    private static void AddOutline(GameObject go, Color color, float width)
    {
        Outline o = go.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(width, -width);
    }

    // =========================================================
    // 4a. COUNTER (ProBuilder, not breakable)
    // =========================================================

    private static void BuildCounter()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(CounterMaterialPath);
        var parts = new List<ProBuilderMesh>();

        // Faces the customer along -Z (back at +Z, like the other wall furniture). Pivot = bottom centre.
        Box(parts, new Vector3(0f, 1.0f, 0f), new Vector3(2.6f, 0.08f, 0.72f), 0);       // top
        Box(parts, new Vector3(0f, 0.5f, -0.31f), new Vector3(2.5f, 0.92f, 0.06f), 1);   // front panel
        Box(parts, new Vector3(-1.25f, 0.5f, 0f), new Vector3(0.08f, 0.92f, 0.62f), 1);  // sides
        Box(parts, new Vector3(1.25f, 0.5f, 0f), new Vector3(0.08f, 0.92f, 0.62f), 1);
        Box(parts, new Vector3(0f, 0.45f, 0.08f), new Vector3(2.4f, 0.05f, 0.46f), 2);   // inner shelf
        Box(parts, new Vector3(0f, 0.04f, -0.33f), new Vector3(2.62f, 0.08f, 0.1f), 0);  // kick plinth
        for (int i = -2; i <= 2; i++)                                                      // front trim posts
        {
            Box(parts, new Vector3(i * 0.6f, 0.5f, -0.35f), new Vector3(0.08f, 0.92f, 0.04f), 3);
        }

        ProBuilderMesh target = parts[0];
        CombineMeshes.Combine(parts, target);
        for (int i = 1; i < parts.Count; i++) { Object.DestroyImmediate(parts[i].gameObject); }
        if (mat != null) { target.SetMaterial(target.faces, mat); }
        target.ToMesh();
        target.Refresh();

        GameObject go = target.gameObject;
        try
        {
            go.name = "ShopCounter";
            go.layer = ObstacleLayer;
            if (mat != null) { go.GetComponent<MeshRenderer>().sharedMaterial = mat; }
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == "ProBuilderShape") { Object.DestroyImmediate(c); }
            }

            BoxCollider col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.52f, 0f);
            col.size = new Vector3(2.62f, 1.04f, 0.74f);

            NavMeshObstacle obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = col.center;
            obstacle.size = col.size;
            obstacle.carving = true;

            PrefabUtility.SaveAsPrefabAsset(go, CounterPath);
            log.AppendLine("counter saved");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    internal static void Box(List<ProBuilderMesh> parts, Vector3 center, Vector3 size, int slot)
    {
        ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
        Vector3[] positions = new Vector3[pb.positions.Count];
        for (int i = 0; i < positions.Length; i++) { positions[i] = pb.positions[i] + center; }
        pb.positions = positions;
        pb.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // Planar UVs per face, fitted into the atlas slot (24 px per metre like the other furniture)
        Rect r = FurnitureMaterialFactory.SlotRect(slot);
        Vector2[] uvs = new Vector2[pb.vertexCount];
        foreach (Face face in pb.faces)
        {
            IList<int> tri = face.indexes;
            Vector3 n = Vector3.Cross(positions[tri[1]] - positions[tri[0]], positions[tri[2]] - positions[tri[0]]).normalized;
            Vector3 axisU = Vector3.Cross(n, Mathf.Abs(n.y) > 0.7f ? Vector3.right : Vector3.up).normalized;
            Vector3 axisV = Vector3.Cross(n, axisU).normalized;

            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (int i in face.distinctIndexes)
            {
                float u = Vector3.Dot(positions[i], axisU), v = Vector3.Dot(positions[i], axisV);
                minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
                minV = Mathf.Min(minV, v); maxV = Mathf.Max(maxV, v);
            }

            bool swap = (maxV - minV) > (maxU - minU);
            float spanU = Mathf.Clamp01((swap ? maxV - minV : maxU - minU) * 24f / 32f);
            float spanV = Mathf.Clamp01((swap ? maxU - minU : maxV - minV) * 24f / 32f);

            foreach (int i in face.distinctIndexes)
            {
                float u = Mathf.InverseLerp(minU, maxU, Vector3.Dot(positions[i], axisU));
                float v = Mathf.InverseLerp(minV, maxV, Vector3.Dot(positions[i], axisV));
                if (swap) { float t = u; u = v; v = t; }
                uvs[i] = new Vector2(r.x + u * spanU * r.width, r.y + v * spanV * r.height);
            }
            face.manualUV = true;
        }

        pb.textures = uvs;
        pb.ToMesh();
        pb.Refresh();
        parts.Add(pb);
    }

    // =========================================================
    // 4b. ENTRANCE (DunGen connector: a red & gold runner laid through the doorway)
    // =========================================================

    private static void BuildEntrance()
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(CounterMaterialPath);
        Material runnerMat = AssetDatabase.LoadAssetAtPath<Material>(RunnerMaterialPath);
        if (runnerMat == null)
        {
            runnerMat = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(runnerMat, RunnerMaterialPath);
        }
        if (runnerTexture != null)
        {
            if (runnerMat.HasProperty("_BaseMap")) { runnerMat.SetTexture("_BaseMap", runnerTexture); runnerMat.SetTextureScale("_BaseMap", Vector2.one); }
            if (runnerMat.HasProperty("_MainTex")) { runnerMat.SetTexture("_MainTex", runnerTexture); }
        }
        if (runnerMat.HasProperty("_BaseColor")) { runnerMat.SetColor("_BaseColor", new Color(0.72f, 0.72f, 0.72f)); }
        EditorUtility.SetDirty(runnerMat);

        GameObject root = new GameObject("ShopEntrance");
        try
        {
            // DunGen places connectors at the doorway with +Z pointing out of the room.
            // The runner crosses the doorway: 1.6 m into the shop, 1.4 m out into the corridor.
            GameObject runner = new GameObject("Runner");
            runner.transform.SetParent(root.transform, false);
            runner.transform.localPosition = new Vector3(0f, FloorY + 0.012f, -0.1f);
            runner.AddComponent<MeshFilter>().sharedMesh = SaveRunnerMesh(0.9f, 3.0f);
            MeshRenderer mr = runner.AddComponent<MeshRenderer>();
            mr.sharedMaterial = runnerMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            PrefabUtility.SaveAsPrefabAsset(root, EntrancePath);
            log.AppendLine("entrance saved");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Mesh SaveRunnerMesh(float width, float length)
    {
        string path = ShopPrefabFolder + "/ShopRunner_Mesh.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) { mesh = new Mesh(); }

        float w = width * 0.5f, l = length * 0.5f;
        mesh.Clear();
        mesh.vertices = new[] { new Vector3(-w, 0f, -l), new Vector3(w, 0f, -l), new Vector3(-w, 0f, l), new Vector3(w, 0f, l) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (isNew) { AssetDatabase.CreateAsset(mesh, path); }
        else { EditorUtility.SetDirty(mesh); }
        return mesh;
    }

    // =========================================================
    // 5. ROOM
    // =========================================================

    private static void BuildRoom()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ShopRoomPath) == null)
        {
            if (!AssetDatabase.CopyAsset(SourceRoomPath, ShopRoomPath))
            {
                throw new System.Exception("Could not copy " + SourceRoomPath);
            }
            AssetDatabase.ImportAsset(ShopRoomPath, ImportAssetOptions.ForceSynchronousImport);
            log.AppendLine("copied weapon room -> " + ShopRoomPath);
        }
        else
        {
            log.AppendLine("shop room exists, rebuilding its shop parts");
        }

        GameObject room = PrefabUtility.LoadPrefabContents(ShopRoomPath);
        try
        {
            room.name = Path.GetFileNameWithoutExtension(ShopRoomPath);

            // Previous shop parts (re-runs)
            Transform old = room.transform.Find("Shop");
            if (old != null) { Object.DestroyImmediate(old.gameObject); }

            RemoveCombatAndLoot(room);
            ClearCounterZone(room);
            SealBackDoorway(room);
            MarkEntrances(room);
            BuildShopArea(room);

            PrefabUtility.SaveAsPrefabAsset(room, ShopRoomPath);
            log.AppendLine("room saved");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(room);
        }
    }

    // Enemy spawners and the Energy Orb box don't belong in a shop
    private static void RemoveCombatAndLoot(GameObject room)
    {
        var doomed = new List<GameObject>();
        foreach (Transform t in room.GetComponentsInChildren<Transform>(true))
        {
            if (t == room.transform) { continue; }
            string n = t.name;
            bool isSpawner = t.GetComponent<EnemySpawner>() != null;
            bool isLoot = n.StartsWith("ItemBox") || n.StartsWith("RewardBox") || t.GetComponent<EnergyOrbPickup>() != null || t.GetComponent<Item>() != null;
            if (isSpawner || isLoot) { doomed.Add(OutermostRemovable(t.gameObject)); }
        }

        foreach (GameObject go in new HashSet<GameObject>(doomed))
        {
            if (go == null) { continue; }
            log.AppendLine("removed " + go.name);
            Object.DestroyImmediate(go);
        }
    }

    // Decor where the counter, shelves and shop corner go (the +Z half against the back wall)
    private static void ClearCounterZone(GameObject room)
    {
        Transform decor = room.transform.Find(PrisonRoomDecorator.DecorGroupName);
        if (decor == null) { log.AppendLine("no decor group"); return; }

        var doomed = new List<GameObject>();
        foreach (Transform child in decor)
        {
            if (child.localPosition.z > 0.75f) { doomed.Add(child.gameObject); }
        }
        foreach (GameObject go in doomed)
        {
            log.AppendLine("cleared decor " + go.name + " at " + go.transform.localPosition.ToString("F2"));
            Object.DestroyImmediate(OutermostRemovable(go));
        }
    }

    // A prefab instance nested inside the room can only be deleted as a whole: use its outermost root
    internal static GameObject OutermostRemovable(GameObject go)
    {
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        return root != null ? root : go;
    }

    // The back (+Z) doorway gets sealed so the counter can stand against that wall
    private static void SealBackDoorway(GameObject room)
    {
        foreach (Doorway doorway in room.GetComponentsInChildren<Doorway>(true))
        {
            Vector3 fwd = room.transform.InverseTransformDirection(doorway.transform.forward);
            if (fwd.z < 0.9f) { continue; }

            GameObject wallRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(doorway.gameObject);
            if (wallRoot != null)
            {
                PrefabUtility.UnpackPrefabInstance(wallRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            // Without the Doorway, DunGen never connects here and the blocker infill stays in place
            foreach (GameObject blocker in doorway.BlockerSceneObjects)
            {
                if (blocker != null) { blocker.SetActive(true); }
            }
            log.AppendLine("sealed doorway on " + (wallRoot != null ? wallRoot.name : doorway.transform.parent.name));
            Object.DestroyImmediate(doorway);
            return;
        }

        errors++;
        log.AppendLine("!! no +Z doorway found to seal");
    }

    // Remaining doorways use the shop runner as their connector (high priority so it wins over the neighbour)
    private static void MarkEntrances(GameObject room)
    {
        GameObject entrance = AssetDatabase.LoadAssetAtPath<GameObject>(EntrancePath);
        int count = 0;

        foreach (Doorway doorway in room.GetComponentsInChildren<Doorway>(true))
        {
            doorway.ConnectorPrefabs = new WeightedTable<GameObject>();
            if (entrance != null) { doorway.ConnectorPrefabs.Entries.Add(new WeightedEntry<GameObject>(entrance)); }
            doorway.DoorPrefabPriority = 100;
            PrefabUtility.RecordPrefabInstancePropertyModifications(doorway);
            count++;
        }

        log.AppendLine($"entrances marked: {count}");
    }

    private static void BuildShopArea(GameObject room)
    {
        GameObject shop = new GameObject("Shop");
        shop.transform.SetParent(room.transform, false);

        // Counter across the back wall (wall inner face at z = 2.75), customers stand on the -Z side
        GameObject counter = PlacePrefab(CounterPath, shop.transform, new Vector3(0f, FloorY, 1.55f), 0f, "Shop Counter");

        // Shelves behind the counter + a few pots / barrels in the corners
        PlacePrefab(ShelfPath, shop.transform, new Vector3(-1.25f, FloorY, 2.66f), 0f, "Shop Shelf L");
        PlacePrefab(ShelfPath, shop.transform, new Vector3(1.25f, FloorY, 2.66f), 0f, "Shop Shelf R");
        PlacePrefab(BarrelPath, shop.transform, new Vector3(2.3f, FloorY, 2.3f), 0f, "Shop Barrel");
        PlacePrefab(PotTallPath, shop.transform, new Vector3(-2.35f, FloorY, 2.35f), 0f, "Shop Pot Tall");
        PlacePrefab(PotSmallPath, shop.transform, new Vector3(-2.4f, FloorY, 1.75f), 0f, "Shop Pot Small");

        // Wares on the counter (visual only)
        PlaceDisplay(Items + "/Consumables/HealthPotion_Small.prefab", shop.transform, new Vector3(-0.85f, FloorY + 1.04f, 1.55f));
        PlaceDisplay(Items + "/Consumables/ManaPotion_Small.prefab", shop.transform, new Vector3(-0.45f, FloorY + 1.04f, 1.6f));
        PlaceDisplay(Items + "/Consumables/HealthPotion_Big.prefab", shop.transform, new Vector3(0.7f, FloorY + 1.04f, 1.55f));

        // Shopkeeper (interaction)
        GameObject keeperGo = new GameObject("ShopKeeper");
        keeperGo.transform.SetParent(shop.transform, false);
        keeperGo.transform.localPosition = new Vector3(0f, FloorY, 1.55f);
        ShopKeeper keeper = keeperGo.AddComponent<ShopKeeper>();

        GameObject point = new GameObject("Interact Point");
        point.transform.SetParent(keeperGo.transform, false);
        point.transform.localPosition = new Vector3(0f, 0f, -0.7f);

        GameObject prompt = CopyPrompt(keeperGo.transform);
        if (prompt != null) { prompt.transform.localPosition = new Vector3(0f, 1.55f, -0.38f); }

        SerializedObject so = new SerializedObject(keeper);
        so.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
        so.FindProperty("interactPoint").objectReferenceValue = point.transform;
        so.FindProperty("uiButtonPrompt").objectReferenceValue = prompt;
        so.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine($"shop area built (counter={counter != null}, prompt={prompt != null})");
    }

    internal static GameObject PlacePrefab(string path, Transform parent, Vector3 localPos, float rotY, string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { errors++; log.AppendLine("!! missing " + path); return null; }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        return go;
    }

    // A copy of the item's visuals with all gameplay stripped, so it can't be picked up for free
    private static void PlaceDisplay(string path, Transform parent, Vector3 localPos)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { log.AppendLine("display missing " + path); return; }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Display " + prefab.name;

        foreach (Item item in go.GetComponentsInChildren<Item>(true)) { Object.DestroyImmediate(item); }
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) { Object.DestroyImmediate(c); }
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) { Object.DestroyImmediate(rb); }
        foreach (UIButtonPrompt p in go.GetComponentsInChildren<UIButtonPrompt>(true)) { Object.DestroyImmediate(p.gameObject); }
        Transform promptRoot = FindChild(go.transform, "UIButtonPrompt");
        if (promptRoot != null) { Object.DestroyImmediate(promptRoot.gameObject); }

        // Stand it on the counter: move so its lowest visible point sits at localPos.y
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        Bounds b = default;
        bool has = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!has) { b = r.bounds; has = true; } else { b.Encapsulate(r.bounds); }
        }
        if (has)
        {
            float worldBottom = parent.TransformPoint(localPos).y;
            go.transform.position += Vector3.up * (worldBottom - b.min.y);
            log.AppendLine($"display {prefab.name} size={b.size.ToString("F2")}");
        }
    }

    internal static GameObject CopyPrompt(Transform parent)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PromptSourcePath);
        Transform sourcePrompt = source != null ? FindChild(source.transform, "UIButtonPrompt") : null;
        if (sourcePrompt == null) { errors++; log.AppendLine("!! prompt source missing"); return null; }

        GameObject prompt = Object.Instantiate(sourcePrompt.gameObject, parent, false);
        prompt.name = "UIButtonPrompt";
        prompt.transform.localRotation = Quaternion.identity;
        prompt.SetActive(true);
        foreach (Collider c in prompt.GetComponentsInChildren<Collider>(true)) { Object.DestroyImmediate(c); }
        return prompt;
    }

    internal static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) { return child; }
            Transform found = FindChild(child, name);
            if (found != null) { return found; }
        }
        return null;
    }

    // =========================================================
    // 6. TILE SET + FLOW INJECTION
    // =========================================================

    private static void HookIntoDungeon()
    {
        GameObject room = AssetDatabase.LoadAssetAtPath<GameObject>(ShopRoomPath);
        if (room == null) { throw new System.Exception("shop room missing"); }

        TileSet tileSet = AssetDatabase.LoadAssetAtPath<TileSet>(TileSetPath);
        if (tileSet == null)
        {
            tileSet = ScriptableObject.CreateInstance<TileSet>();
            AssetDatabase.CreateAsset(tileSet, TileSetPath);
        }
        tileSet.Tiles = new WeightedTable<GameObject>();
        tileSet.AddTile(room, 1f, 1f);
        EditorUtility.SetDirty(tileSet);
        log.AppendLine("tile set ok");

        foreach (string flowPath in FlowPaths)
        {
            DungeonFlow flow = AssetDatabase.LoadAssetAtPath<DungeonFlow>(flowPath);
            if (flow == null) { errors++; log.AppendLine("!! missing flow " + flowPath); continue; }

            flow.TileInjectionRules.RemoveAll(r => r == null || r.TileSet == tileSet);
            flow.TileInjectionRules.Add(new TileInjectionRule
            {
                TileSet = tileSet,
                // On a side branch that starts around the middle of the main path
                NormalizedPathDepth = new FloatRange(0.4f, 0.6f),
                NormalizedBranchDepth = new FloatRange(0f, 1f),
                CanAppearOnMainPath = false,
                CanAppearOnBranchPath = true,
                IsRequired = true,
            });
            EditorUtility.SetDirty(flow);
            log.AppendLine("injected into " + Path.GetFileName(flowPath));
        }
    }
}
