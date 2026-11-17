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

/// <summary>
/// One-shot builder for shrines:
///  1. Icons (blood drop, coffer, font drop, flame) + glow sprite + stone material
///  2. Curses (Fragile, Brittle, Greedless, Hunted, Blind Spot, Feeble) and shrines (Blood Altar, Gambler's Idol,
///     Cursed Coffer, Cleansing Font, Altar of Fury) as assets - existing ones are never overwritten
///  3. Resources/Shrines/ShrineSettings
///  4. Shrine altar prefab and the Shrine Room (Prison): Weapon Room copy, back doorway sealed, 3 altars
///  5. Shrine Room tile set on a side branch of the Prison First / Second floors
/// Re-run from Tools > Shrines > Build Shrines.
/// </summary>
public static class ShrineSetupBuilder
{
    private const string AutoKey = "ShrineSetupBuilder_v1";

    private const string DataFolder = "Assets/013-Datas/Shrines";
    private const string IconFolder = DataFolder + "/Icons";
    private const string CurseFolder = DataFolder + "/Curses";
    private const string ShrineFolder = DataFolder + "/Shrines";
    private const string SettingsPath = "Assets/Resources/Shrines/ShrineSettings.asset";
    private const string PrefabFolder = "Assets/003-Prefabs/Shrines";
    private const string AltarPath = PrefabFolder + "/ShrineAltar.prefab";
    private const string StoneMatPath = "Assets/006-Materials/Furniture/Mat_ShrineStone.mat";
    private const string StoneTexPath = "Assets/006-Materials/Furniture/Textures/Tex_ShrineStone.png";
    private const string SourceMatPath = "Assets/006-Materials/Furniture/Mat_Shelf.mat";
    private const string CoinIconPath = "Assets/013-Datas/Shop/Icons/Icon_Coin.png";

    private const string RoomFolder = "Assets/012-MyDungenAsset/Room/Prison";
    private const string SourceRoomPath = RoomFolder + "/Medium Room (Weapon Room).prefab";
    private const string ShrineRoomPath = RoomFolder + "/Shrine Room (Prison).prefab";
    private const string TileSetPath = "Assets/012-MyDungenAsset/Tile Set/Prison/Shrine Room Set (Prison).asset";
    private const string ArchetypePath = "Assets/012-MyDungenAsset/Dungeon Archetype (Prison).asset";
    private static readonly string[] FlowPaths =
    {
        "Assets/012-MyDungenAsset/Dungeon Flow (Prison) (First Floor).asset",
        "Assets/012-MyDungenAsset/Dungeon Flow (Prison) (Second Floor).asset",
    };

    private const float FloorY = 0.09f;
    private const int ObstacleLayer = 8;
    private const int ItemLayer = 14;

    private static StringBuilder log;
    private static int errors;

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

    [MenuItem("Tools/Shrines/Build Shrines")]
    public static void BuildAll()
    {
        log = new StringBuilder();
        errors = 0;
        ShopSetupBuilder.log = log;   // shared helpers log here
        ShopSetupBuilder.errors = 0;

        foreach (string f in new[] { IconFolder, CurseFolder, ShrineFolder, PrefabFolder, "Assets/Resources/Shrines" }) { Directory.CreateDirectory(f); }
        AssetDatabase.Refresh();

        Sprite blood = null, coffer = null, font = null, fury = null, glow = null;
        Step("icons", () =>
        {
            blood = ShopPixelArt.SaveSprite(BloodIcon(), IconFolder + "/Icon_BloodAltar.png");
            coffer = ShopPixelArt.SaveSprite(CofferIcon(), IconFolder + "/Icon_CursedCoffer.png");
            font = ShopPixelArt.SaveSprite(FontIcon(), IconFolder + "/Icon_CleansingFont.png");
            fury = ShopPixelArt.SaveSprite(FuryIcon(), IconFolder + "/Icon_AltarOfFury.png");
            glow = SaveGlow(IconFolder + "/Shrine_Glow.png");
        });
        Sprite coin = AssetDatabase.LoadAssetAtPath<Sprite>(CoinIconPath);

        var curses = new List<CurseDefinition>();
        Step("curses", () =>
        {
            curses.Add(Curse("Fragile", "Take 25% more damage", new Color(0.95f, 0.35f, 0.3f), c => c.damageTakenMultiplier = 1.25f));
            curses.Add(Curse("Brittle", "Blocking costs double stamina", new Color(0.75f, 0.75f, 0.8f), c => c.blockStaminaMultiplier = 2f));
            curses.Add(Curse("Greedless", "Enemies drop no coins", new Color(0.85f, 0.7f, 0.25f), c => c.coinDropMultiplier = 0f));
            curses.Add(Curse("Hunted", "Elites appear far more often", new Color(0.9f, 0.45f, 0.15f), c => c.eliteChanceBonus = 0.25f));
            curses.Add(Curse("Blind Spot", "No warning for attacks from behind", new Color(0.55f, 0.55f, 0.95f), c => c.disableRearWarning = true));
            curses.Add(Curse("Feeble", "Deal 20% less damage", new Color(0.6f, 0.8f, 0.6f), c => c.damageDealtMultiplier = 0.8f));
        });

        var shrines = new List<ShrineDefinition>();
        Step("shrines", () =>
        {
            shrines.Add(Shrine("BloodAltar", s =>
            {
                s.displayName = "Blood Altar"; s.flavour = "Power, paid in blood."; s.color = new Color(0.9f, 0.15f, 0.2f); s.icon = blood;
                s.maxHealthCost = 0.15f; s.energyOrb = OrbReward.Rare; s.weight = 1f;
            }));
            shrines.Add(Shrine("GamblersIdol", s =>
            {
                s.displayName = "Gambler's Idol"; s.flavour = "Fortune favours the bold."; s.color = new Color(1f, 0.82f, 0.3f); s.icon = coin;
                s.coinCost = 40; s.weight = 1.2f;
                s.gambleOutcomes = new List<ShrineDefinition.GambleOutcome>
                {
                    new ShrineDefinition.GambleOutcome { payoutMultiplier = 0f, weight = 45f },
                    new ShrineDefinition.GambleOutcome { payoutMultiplier = 1.5f, weight = 30f },
                    new ShrineDefinition.GambleOutcome { payoutMultiplier = 2.5f, weight = 18f },
                    new ShrineDefinition.GambleOutcome { payoutMultiplier = 5f, weight = 7f },
                };
            }));
            shrines.Add(Shrine("CursedCoffer", s =>
            {
                s.displayName = "Cursed Coffer"; s.flavour = "Its treasure is real. So is its price."; s.color = new Color(0.7f, 0.35f, 0.95f); s.icon = coffer;
                s.curseCount = 1; s.energyOrb = OrbReward.Unique; s.weight = 0.9f;
            }));
            shrines.Add(Shrine("CleansingFont", s =>
            {
                s.displayName = "Cleansing Font"; s.flavour = "Wash away what clings to you."; s.color = new Color(0.35f, 0.8f, 1f); s.icon = font;
                s.coinCost = 30; s.cleanseCurses = true; s.healFraction = 1f; s.weight = 1f;
            }));
            shrines.Add(Shrine("AltarOfFury", s =>
            {
                s.displayName = "Altar of Fury"; s.flavour = "Burn your breath for strength."; s.color = new Color(1f, 0.5f, 0.15f); s.icon = fury;
                s.maxStaminaCost = 0.2f; s.damageBonus = 0.2f; s.weight = 1f;
            }));
        });

        Step("settings", () => BuildSettings(shrines, curses));
        Step("stone", BuildStoneMaterial);
        Step("altar", () => BuildAltar(glow));
        Step("room", BuildRoom);
        Step("dungeon", HookIntoDungeon);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        errors += ShopSetupBuilder.errors;
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/shrine_setup.txt", log.ToString());
        Debug.Log($"[ShrineSetup] Done with {errors} error(s). Report: Library/PrisonDecor/shrine_setup.txt");
    }

    private static void Step(string name, System.Action action)
    {
        log.AppendLine("== " + name);
        try { action(); }
        catch (System.Exception e) { errors++; log.AppendLine("!! " + name + ": " + e); }
    }

    // =========================================================
    // DATA ASSETS (created once, never overwritten)
    // =========================================================

    private static CurseDefinition Curse(string name, string description, Color color, System.Action<CurseDefinition> fill)
    {
        string path = $"{CurseFolder}/Curse_{name.Replace(" ", "")}.asset";
        CurseDefinition curse = AssetDatabase.LoadAssetAtPath<CurseDefinition>(path);
        if (curse != null) { return curse; }

        curse = ScriptableObject.CreateInstance<CurseDefinition>();
        curse.displayName = name;
        curse.description = description;
        curse.color = color;
        fill(curse);
        AssetDatabase.CreateAsset(curse, path);
        log.AppendLine("created " + path);
        return curse;
    }

    private static ShrineDefinition Shrine(string file, System.Action<ShrineDefinition> fill)
    {
        string path = $"{ShrineFolder}/Shrine_{file}.asset";
        ShrineDefinition shrine = AssetDatabase.LoadAssetAtPath<ShrineDefinition>(path);
        if (shrine != null) { return shrine; }

        shrine = ScriptableObject.CreateInstance<ShrineDefinition>();
        fill(shrine);
        AssetDatabase.CreateAsset(shrine, path);
        log.AppendLine("created " + path);
        return shrine;
    }

    private static void BuildSettings(List<ShrineDefinition> shrines, List<CurseDefinition> curses)
    {
        ShrineSettings settings = AssetDatabase.LoadAssetAtPath<ShrineSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<ShrineSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        foreach (ShrineDefinition s in shrines) { if (s != null && !settings.shrinePool.Contains(s)) settings.shrinePool.Add(s); }
        foreach (CurseDefinition c in curses) { if (c != null && !settings.cursePool.Contains(c)) settings.cursePool.Add(c); }

        if (settings.energyOrbPrefab == null)
        {
            GameObject orb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/003-Prefabs/EnergyOrb.prefab");
            settings.energyOrbPrefab = orb != null ? orb.GetComponent<EnergyOrbPickup>() : null;
        }
        const string orbs = "Assets/013-Datas/Perks/Energy Orbs";
        if (settings.commonOrb == null) { settings.commonOrb = AssetDatabase.LoadAssetAtPath<EnergyOrbDefinition>(orbs + "/EnergyOrb_Common.asset"); }
        if (settings.rareOrb == null) { settings.rareOrb = AssetDatabase.LoadAssetAtPath<EnergyOrbDefinition>(orbs + "/EnergyOrb_Rare.asset"); }
        if (settings.uniqueOrb == null) { settings.uniqueOrb = AssetDatabase.LoadAssetAtPath<EnergyOrbDefinition>(orbs + "/EnergyOrb_Unique.asset"); }

        EditorUtility.SetDirty(settings);
        log.AppendLine($"settings: {settings.shrinePool.Count} shrines, {settings.cursePool.Count} curses, orb={settings.energyOrbPrefab != null}");
    }

    // =========================================================
    // STONE MATERIAL (4-slot atlas like the furniture: grey blocks)
    // =========================================================

    private static void BuildStoneMaterial()
    {
        const int size = 64, tile = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        var rng = new System.Random(5);
        Color[] bases = { ShopPixelArt.Hex("#6d6a66"), ShopPixelArt.Hex("#5e5b58"), ShopPixelArt.Hex("#7a7672"), ShopPixelArt.Hex("#4f4c4a") };

        for (int slot = 0; slot < 4; slot++)
        {
            int ox = (slot % 2) * tile, oy = (slot / 2) * tile;
            for (int y = 0; y < tile; y++)
            {
                for (int x = 0; x < tile; x++)
                {
                    bool mortarRow = y % 8 == 0;
                    bool mortarCol = (x + ((y / 8) % 2) * 6) % 12 == 0;
                    float j = ((float)rng.NextDouble() - 0.5f) * 0.08f;
                    Color c = bases[slot] + new Color(j, j, j, 0f);
                    if (mortarRow || mortarCol) { c = Color.Lerp(c, Color.black, 0.35f); }
                    if (rng.NextDouble() < 0.03) { c = Color.Lerp(c, Color.black, 0.2f); } // pits
                    c.a = 1f;
                    px[(oy + y) * size + ox + x] = c;
                }
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(StoneTexPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(StoneTexPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(StoneTexPath);
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(StoneMatPath);
        if (mat == null)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMatPath);
            mat = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, StoneMatPath);
        }
        Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(StoneTexPath);
        if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", imported); }
        if (mat.HasProperty("_MainTex")) { mat.SetTexture("_MainTex", imported); }
        EditorUtility.SetDirty(mat);
        log.AppendLine("stone material ok");
    }

    // =========================================================
    // ALTAR PREFAB
    // =========================================================

    private static void BuildAltar(Sprite glow)
    {
        Material stone = AssetDatabase.LoadAssetAtPath<Material>(StoneMatPath);

        var parts = new List<ProBuilderMesh>();
        ShopSetupBuilder.Box(parts, new Vector3(0f, 0.12f, 0f), new Vector3(1.0f, 0.24f, 1.0f), 3);   // plinth
        ShopSetupBuilder.Box(parts, new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.12f, 0.8f), 1);    // step
        ShopSetupBuilder.Box(parts, new Vector3(0f, 0.72f, 0f), new Vector3(0.45f, 0.72f, 0.45f), 0); // column
        ShopSetupBuilder.Box(parts, new Vector3(0f, 1.13f, 0f), new Vector3(0.75f, 0.1f, 0.75f), 2);  // top slab

        ProBuilderMesh target = parts[0];
        CombineMeshes.Combine(parts, target);
        for (int i = 1; i < parts.Count; i++) { Object.DestroyImmediate(parts[i].gameObject); }
        if (stone != null) { target.SetMaterial(target.faces, stone); }
        target.ToMesh();
        target.Refresh();

        GameObject root = target.gameObject;
        try
        {
            root.name = "ShrineAltar";
            root.layer = ObstacleLayer;
            if (stone != null) { root.GetComponent<MeshRenderer>().sharedMaterial = stone; }
            foreach (Component c in root.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == "ProBuilderShape") { Object.DestroyImmediate(c); }
            }

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(1f, 1.2f, 1f);
            NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = col.center;
            obstacle.size = col.size;
            obstacle.carving = true;

            // Glow + icon floating above the slab
            GameObject glowGo = new GameObject("Glow");
            glowGo.layer = ItemLayer;
            glowGo.transform.SetParent(root.transform, false);
            glowGo.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            glowGo.transform.localScale = Vector3.one * 1.1f;
            SpriteRenderer glowSr = glowGo.AddComponent<SpriteRenderer>();
            glowSr.sprite = glow;

            GameObject iconGo = new GameObject("Icon");
            iconGo.layer = ItemLayer;
            iconGo.transform.SetParent(root.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            iconGo.transform.localScale = Vector3.one * 0.45f;
            SpriteRenderer iconSr = iconGo.AddComponent<SpriteRenderer>();
            iconSr.sortingOrder = 1;

            // Label
            GameObject labelGo = new GameObject("Label");
            labelGo.layer = ItemLayer;
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            TextMeshPro label = labelGo.AddComponent<TextMeshPro>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/020-Others/Font/KnightQuest/KnightsQuest SDF.asset");
            if (font != null) { label.font = font; label.fontSharedMaterial = font.material; }
            label.fontSize = 1.5f;
            label.alignment = TextAlignmentOptions.Bottom;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(0, 0, 0, 220);

            // Prompt (from a potion) in front of the slab
            GameObject prompt = ShopSetupBuilder.CopyPrompt(root.transform);
            if (prompt != null) { prompt.transform.localPosition = new Vector3(0f, 1.2f, -0.55f); }

            ShrineAltar altar = root.AddComponent<ShrineAltar>();
            SerializedObject so = new SerializedObject(altar);
            so.FindProperty("iconRenderer").objectReferenceValue = iconSr;
            so.FindProperty("glowRenderer").objectReferenceValue = glowSr;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("uiButtonPrompt").objectReferenceValue = prompt;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, AltarPath);
            log.AppendLine("saved " + AltarPath + (font == null ? " (default font)" : ""));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // =========================================================
    // ROOM
    // =========================================================

    private static void BuildRoom()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ShrineRoomPath) == null)
        {
            if (!AssetDatabase.CopyAsset(SourceRoomPath, ShrineRoomPath)) { throw new System.Exception("copy failed"); }
            AssetDatabase.ImportAsset(ShrineRoomPath, ImportAssetOptions.ForceSynchronousImport);
        }

        GameObject room = PrefabUtility.LoadPrefabContents(ShrineRoomPath);
        try
        {
            room.name = Path.GetFileNameWithoutExtension(ShrineRoomPath);
            Transform old = room.transform.Find("Shrines");
            if (old != null) { Object.DestroyImmediate(old.gameObject); }

            // No enemies or loot boxes in a shrine room
            var doomed = new HashSet<GameObject>();
            foreach (Transform t in room.GetComponentsInChildren<Transform>(true))
            {
                if (t == room.transform) { continue; }
                if (t.GetComponent<EnemySpawner>() != null || t.name.StartsWith("ItemBox") || t.name.StartsWith("RewardBox") ||
                    t.GetComponent<EnergyOrbPickup>() != null || t.GetComponent<Item>() != null)
                {
                    doomed.Add(ShopSetupBuilder.OutermostRemovable(t.gameObject));
                }
            }
            foreach (GameObject go in doomed) { if (go != null) Object.DestroyImmediate(go); }

            // Clear decor where the altars go (back half)
            Transform decor = room.transform.Find(PrisonRoomDecorator.DecorGroupName);
            if (decor != null)
            {
                var clear = new List<GameObject>();
                foreach (Transform child in decor) { if (child.localPosition.z > 0.75f) clear.Add(child.gameObject); }
                foreach (GameObject go in clear) { Object.DestroyImmediate(ShopSetupBuilder.OutermostRemovable(go)); }
                log.AppendLine($"cleared {clear.Count} decor, removed {doomed.Count} spawners/loot");
            }

            // Seal the back (+Z) doorway so the altars can stand against that wall
            foreach (Doorway doorway in room.GetComponentsInChildren<Doorway>(true))
            {
                Vector3 fwd = room.transform.InverseTransformDirection(doorway.transform.forward);
                if (fwd.z < 0.9f) { continue; }

                GameObject wallRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(doorway.gameObject);
                if (wallRoot != null) { PrefabUtility.UnpackPrefabInstance(wallRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); }
                foreach (GameObject blocker in doorway.BlockerSceneObjects) { if (blocker != null) blocker.SetActive(true); }
                Object.DestroyImmediate(doorway);
                log.AppendLine("sealed back doorway");
                break;
            }

            // Three altars in an arc, facing the entrance (-Z)
            GameObject group = new GameObject("Shrines");
            group.transform.SetParent(room.transform, false);
            ShrineRoom shrineRoom = group.AddComponent<ShrineRoom>();

            GameObject altarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AltarPath);
            var altars = new List<ShrineAltar>();
            Vector3[] spots = { new Vector3(-1.9f, FloorY, 1.6f), new Vector3(0f, FloorY, 2.1f), new Vector3(1.9f, FloorY, 1.6f) };
            float[] turns = { 20f, 0f, -20f };
            for (int i = 0; i < spots.Length; i++)
            {
                GameObject a = (GameObject)PrefabUtility.InstantiatePrefab(altarPrefab, group.transform);
                a.name = "Shrine Altar " + (i + 1);
                a.transform.localPosition = spots[i];
                a.transform.localRotation = Quaternion.Euler(0f, turns[i], 0f);
                altars.Add(a.GetComponent<ShrineAltar>());
            }

            SerializedObject so = new SerializedObject(shrineRoom);
            SerializedProperty list = so.FindProperty("altars");
            list.arraySize = altars.Count;
            for (int i = 0; i < altars.Count; i++) { list.GetArrayElementAtIndex(i).objectReferenceValue = altars[i]; }
            so.ApplyModifiedPropertiesWithoutUndo();

            // A couple of pots in the corners
            ShopSetupBuilder.PlacePrefab("Assets/003-Prefabs/Furniture/ClayPot_Tall.prefab", group.transform, new Vector3(-2.4f, FloorY, 2.4f), 0f, "Pot Tall");
            ShopSetupBuilder.PlacePrefab("Assets/003-Prefabs/Furniture/ClayPot_Tall.prefab", group.transform, new Vector3(2.4f, FloorY, 2.4f), 0f, "Pot Tall 2");

            PrefabUtility.SaveAsPrefabAsset(room, ShrineRoomPath);
            log.AppendLine("saved " + ShrineRoomPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(room);
        }
    }

    // =========================================================
    // DUNGEON
    // =========================================================

    private static void HookIntoDungeon()
    {
        GameObject room = AssetDatabase.LoadAssetAtPath<GameObject>(ShrineRoomPath);
        if (room == null) { throw new System.Exception("shrine room missing"); }

        TileSet tileSet = AssetDatabase.LoadAssetAtPath<TileSet>(TileSetPath);
        if (tileSet == null)
        {
            tileSet = ScriptableObject.CreateInstance<TileSet>();
            AssetDatabase.CreateAsset(tileSet, TileSetPath);
        }
        tileSet.Tiles = new WeightedTable<GameObject>();
        tileSet.AddTile(room, 1f, 1f);
        EditorUtility.SetDirty(tileSet);

        foreach (string flowPath in FlowPaths)
        {
            DungeonFlow flow = AssetDatabase.LoadAssetAtPath<DungeonFlow>(flowPath);
            if (flow == null) { errors++; log.AppendLine("!! missing flow " + flowPath); continue; }

            flow.TileInjectionRules.RemoveAll(r => r == null || r.TileSet == tileSet);
            flow.TileInjectionRules.Add(new TileInjectionRule
            {
                TileSet = tileSet,
                NormalizedPathDepth = new FloatRange(0.2f, 0.85f),
                NormalizedBranchDepth = new FloatRange(0f, 1f),
                CanAppearOnMainPath = false,
                CanAppearOnBranchPath = true,
                IsRequired = true,
            });
            EditorUtility.SetDirty(flow);
            log.AppendLine("injected into " + Path.GetFileName(flowPath));
        }

        // Shop + shrine + 2 treasure rooms all want side branches: make sure there are enough
        DungeonArchetype archetype = AssetDatabase.LoadAssetAtPath<DungeonArchetype>(ArchetypePath);
        if (archetype != null && archetype.BranchCount.Min < 4)
        {
            archetype.BranchCount.Min = 4;
            archetype.BranchCount.Max = Mathf.Max(archetype.BranchCount.Max, 5);
            EditorUtility.SetDirty(archetype);
            log.AppendLine($"archetype branches -> {archetype.BranchCount.Min}-{archetype.BranchCount.Max}");
        }
    }

    // =========================================================
    // ICONS (32x32 pixel art)
    // =========================================================

    private static readonly Color Ink = ShopPixelArt.Hex("#1a1210");

    private static ShopPixelArt.Canvas BloodIcon()
    {
        var c = new ShopPixelArt.Canvas(32, 32);
        Color red = ShopPixelArt.Hex("#c8202c"), dark = ShopPixelArt.Hex("#7a1018"), shine = ShopPixelArt.Hex("#ff8a8a");
        c.Disc(16f, 12f, 8f, red);
        for (int y = 12; y < 29; y++)
        {
            float half = Mathf.Lerp(8f, 0.5f, (y - 12) / 16f);
            c.Rect(Mathf.RoundToInt(16 - half), y, Mathf.RoundToInt(15 + half), y, red);
        }
        c.Disc(17.5f, 10.5f, 4f, dark);
        c.Disc(16f, 12f, 6.5f, red);
        c.Rect(11, 14, 12, 18, shine);
        c.Outline(Ink);
        return c;
    }

    private static ShopPixelArt.Canvas CofferIcon()
    {
        var c = new ShopPixelArt.Canvas(32, 32);
        Color wood = ShopPixelArt.Hex("#5a3a24"), band = ShopPixelArt.Hex("#8a3cc8"), glow = ShopPixelArt.Hex("#d8a0ff");
        c.Rect(5, 6, 26, 18, wood);           // body
        c.Rect(4, 18, 27, 24, wood);          // lid
        c.Rect(9, 6, 11, 24, band);           // bands
        c.Rect(20, 6, 22, 24, band);
        c.Rect(14, 15, 17, 19, glow);         // lock
        c.Rect(12, 26, 13, 28, glow);         // curse wisps
        c.Rect(18, 27, 19, 30, glow);
        c.Shade(0.2f);
        c.Outline(Ink);
        return c;
    }

    private static ShopPixelArt.Canvas FontIcon()
    {
        var c = new ShopPixelArt.Canvas(32, 32);
        Color blue = ShopPixelArt.Hex("#3aa8e8"), deep = ShopPixelArt.Hex("#1d5f9a"), shine = ShopPixelArt.Hex("#c8f0ff");
        c.Disc(16f, 11f, 8f, blue);
        for (int y = 11; y < 28; y++)
        {
            float half = Mathf.Lerp(8f, 0.5f, (y - 11) / 16f);
            c.Rect(Mathf.RoundToInt(16 - half), y, Mathf.RoundToInt(15 + half), y, blue);
        }
        c.Disc(18f, 8.5f, 4f, deep);
        c.Disc(16f, 11f, 6.5f, blue);
        c.Rect(11, 13, 12, 17, shine);
        c.Outline(Ink);
        return c;
    }

    private static ShopPixelArt.Canvas FuryIcon()
    {
        var c = new ShopPixelArt.Canvas(32, 32);
        Color outer = ShopPixelArt.Hex("#e8551a"), mid = ShopPixelArt.Hex("#ffa52a"), core = ShopPixelArt.Hex("#fff0a0");
        c.Disc(16f, 10f, 8f, outer);
        for (int y = 10; y < 30; y++)
        {
            float half = Mathf.Lerp(8f, 0.5f, (y - 10) / 19f) * (1f + 0.15f * Mathf.Sin(y * 0.9f));
            c.Rect(Mathf.RoundToInt(16 - half), y, Mathf.RoundToInt(15 + half), y, outer);
        }
        c.Disc(16f, 10f, 5.5f, mid);
        for (int y = 10; y < 22; y++)
        {
            float half = Mathf.Lerp(5f, 0.5f, (y - 10) / 11f);
            c.Rect(Mathf.RoundToInt(16 - half), y, Mathf.RoundToInt(15 + half), y, mid);
        }
        c.Disc(16f, 9f, 2.8f, core);
        c.Outline(Ink);
        return c;
    }

    // Soft round glow (white, tinted by the shrine colour at runtime)
    private static Sprite SaveGlow(string path)
    {
        const int size = 64;
        var canvas = new ShopPixelArt.Canvas(size, size);
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                float a = Mathf.Clamp01(1f - d);
                canvas.Set(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        Sprite sprite = ShopPixelArt.SaveSprite(canvas, path, size);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
