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
/// One-shot builder for Challenge Rooms:
///  1. Icon + iron material
///  2. Challenge assets (The Gauntlet, Elite Rush, Race the Clock) - never overwritten once created
///  3. Resources/Challenges/ChallengeSettings (pool, default enemies, reward chests)
///  4. Gate prefab (iron bars, DunGen connector) and altar prefab
///  5. Challenge Room (Prison): Weapon Room copy, gates on every doorway, altar in the middle, spawn points
///  6. Tile set injected into the Prison First / Second floors
/// Re-run from Tools > Challenges > Build Challenges.
/// </summary>
public static class ChallengeSetupBuilder
{
    private const string AutoKey = "ChallengeSetupBuilder_v3";

    private const string DataFolder = "Assets/013-Datas/Challenges";
    private const string IconPath = DataFolder + "/Icon_Challenge.png";
    private const string SettingsPath = "Assets/Resources/Challenges/ChallengeSettings.asset";
    private const string PrefabFolder = "Assets/003-Prefabs/Challenges";
    private const string GatePath = PrefabFolder + "/ChallengeGate.prefab";
    private const string AltarPath = PrefabFolder + "/ChallengeAltar.prefab";
    private const string IronMatPath = "Assets/006-Materials/Furniture/Mat_ChallengeIron.mat";
    private const string StoneMatPath = "Assets/006-Materials/Furniture/Mat_ShrineStone.mat";
    private const string GlowPath = "Assets/013-Datas/Shrines/Icons/Shrine_Glow.png";
    private const string FontPath = "Assets/020-Others/Font/KnightQuest/KnightsQuest SDF.asset";

    private const string RoomFolder = "Assets/012-MyDungenAsset/Room/Prison";
    // 8 x 8 m with four pillars for cover (the 6 x 6 Weapon Room was too tight to react in)
    private const string SourceRoomPath = RoomFolder + "/Large Room (4 Doors).prefab";
    // Middle of that room in its local space (it spans x -6..2, z -2..6)
    private static readonly Vector3 RoomCenter = new Vector3(-2f, 0f, 2f);
    private const string RoomPath = RoomFolder + "/Challenge Room (Prison).prefab";
    private const string TileSetPath = "Assets/012-MyDungenAsset/Tile Set/Prison/Challenge Room Set (Prison).asset";
    private static readonly string[] FlowPaths =
    {
        "Assets/012-MyDungenAsset/Dungeon Flow (Prison) (First Floor).asset",
        "Assets/012-MyDungenAsset/Dungeon Flow (Prison) (Second Floor).asset",
    };

    private const string Enemies = "Assets/003-Prefabs/Enemy";
    private const string Treasure = "Assets/003-Prefabs/Treasure";
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

    [MenuItem("Tools/Challenges/Build Challenges")]
    public static void BuildAll()
    {
        log = new StringBuilder();
        errors = 0;
        ShopSetupBuilder.log = log;
        ShopSetupBuilder.errors = 0;

        foreach (string f in new[] { DataFolder, PrefabFolder, "Assets/Resources/Challenges" }) { Directory.CreateDirectory(f); }
        AssetDatabase.Refresh();

        Sprite icon = null;
        Step("icon", () => icon = ShopPixelArt.SaveSprite(SwordsIcon(), IconPath));

        var challenges = new List<ChallengeDefinition>();
        Step("challenges", () =>
        {
            challenges.Add(Challenge("Gauntlet", c =>
            {
                c.displayName = "The Gauntlet"; c.flavour = "Three waves. No way out but through."; c.color = new Color(0.95f, 0.75f, 0.3f);
                c.waves = Waves((3, 0f, 0), (4, 0.2f, 0), (5, 0.35f, 1));
                c.rewardChest = TreasureKeyType.Silver; c.bonusCoins = 40;
            }, icon));
            challenges.Add(Challenge("EliteRush", c =>
            {
                c.displayName = "Elite Rush"; c.flavour = "Only the strongest answer the call."; c.color = new Color(0.9f, 0.3f, 0.25f);
                c.waves = Waves((2, 1f, 0), (3, 1f, 0)); c.allElites = true;
                c.rewardChest = TreasureKeyType.Gold; c.weight = 0.7f; c.minFloor = 2;
            }, icon));
            challenges.Add(Challenge("RaceTheClock", c =>
            {
                c.displayName = "Race the Clock"; c.flavour = "Be quick, and be paid."; c.color = new Color(0.4f, 0.9f, 0.5f);
                c.waves = Waves((4, 0.1f, 0), (4, 0.15f, 0), (4, 0.25f, 0));
                c.timeLimit = 60f; c.timeBonusCoins = 100; c.spawnInterval = 0.2f; c.delayBetweenWaves = 1.5f;
                c.rewardChest = TreasureKeyType.Bronze; c.bonusCoins = 30;
            }, icon));
        });

        Step("settings", () => BuildSettings(challenges));
        Step("iron", BuildIronMaterial);
        Step("gate", BuildGate);
        Step("altar", BuildAltar);
        Step("room", BuildRoom);
        Step("dungeon", HookIntoDungeon);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        errors += ShopSetupBuilder.errors;
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/challenge_setup.txt", log.ToString());
        Debug.Log($"[ChallengeSetup] Done with {errors} error(s). Report: Library/PrisonDecor/challenge_setup.txt");
    }

    private static void Step(string name, System.Action action)
    {
        log.AppendLine("== " + name);
        try { action(); }
        catch (System.Exception e) { errors++; log.AppendLine("!! " + name + ": " + e); }
    }

    // =========================================================
    // DATA
    // =========================================================

    private static List<ChallengeDefinition.Wave> Waves(params (int count, float elite, int guaranteed)[] waves)
    {
        var list = new List<ChallengeDefinition.Wave>();
        foreach (var w in waves) { list.Add(new ChallengeDefinition.Wave { enemyCount = w.count, eliteChance = w.elite, guaranteedElites = w.guaranteed }); }
        return list;
    }

    private static ChallengeDefinition Challenge(string file, System.Action<ChallengeDefinition> fill, Sprite icon)
    {
        string path = $"{DataFolder}/Challenge_{file}.asset";
        ChallengeDefinition c = AssetDatabase.LoadAssetAtPath<ChallengeDefinition>(path);
        if (c != null) { return c; }

        c = ScriptableObject.CreateInstance<ChallengeDefinition>();
        fill(c);
        c.icon = icon;
        AssetDatabase.CreateAsset(c, path);
        log.AppendLine("created " + path);
        return c;
    }

    private static void BuildSettings(List<ChallengeDefinition> challenges)
    {
        ChallengeSettings settings = AssetDatabase.LoadAssetAtPath<ChallengeSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<ChallengeSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        foreach (ChallengeDefinition c in challenges) { if (c != null && !settings.challengePool.Contains(c)) settings.challengePool.Add(c); }

        if (settings.defaultEnemies.Count == 0)
        {
            void Add(string file, float weight, int minFloor)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Enemies}/{file}.prefab");
                if (prefab != null) { settings.defaultEnemies.Add(new ChallengeDefinition.EnemyOption { prefab = prefab, weight = weight, minFloor = minFloor }); }
                else { log.AppendLine("missing enemy " + file); }
            }
            Add("Enemy_Soldier (1)", 3f, 1);
            Add("Enemy_Thief (1)", 2.5f, 1);
            Add("Enemy_Archer (1)", 2f, 1);
            Add("Enemy_Stray Dog (1)", 2.5f, 1);
            Add("Enemy_BigKnight (1)", 0.6f, 2);
        }

        if (settings.bronzeChest == null) { settings.bronzeChest = AssetDatabase.LoadAssetAtPath<GameObject>($"{Treasure}/TreasureChest_Bronze.prefab"); }
        if (settings.silverChest == null) { settings.silverChest = AssetDatabase.LoadAssetAtPath<GameObject>($"{Treasure}/TreasureChest_Silver.prefab"); }
        if (settings.goldChest == null) { settings.goldChest = AssetDatabase.LoadAssetAtPath<GameObject>($"{Treasure}/TreasureChest_Gold.prefab"); }

        EditorUtility.SetDirty(settings);
        log.AppendLine($"settings: {settings.challengePool.Count} challenges, {settings.defaultEnemies.Count} enemies, " +
                       $"chests={settings.bronzeChest != null}/{settings.silverChest != null}/{settings.goldChest != null}");
    }

    // =========================================================
    // MATERIAL + PREFABS
    // =========================================================

    private static void BuildIronMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(IronMatPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, IronMatPath);
        }
        Color iron = new Color(0.22f, 0.23f, 0.25f, 1f);
        if (mat.HasProperty("_BaseColor")) { mat.SetColor("_BaseColor", iron); }
        mat.color = iron;
        EditorUtility.SetDirty(mat);
    }

    private static void BuildGate()
    {
        Material iron = AssetDatabase.LoadAssetAtPath<Material>(IronMatPath);

        // Bars fill the 1 m x 1.78 m doorway; DunGen places this at the doorway (bottom centre, +Z out of the room)
        var parts = new List<ProBuilderMesh>();
        for (int i = 0; i < 7; i++)
        {
            float x = -0.45f + i * 0.15f;
            ShopSetupBuilder.Box(parts, new Vector3(x, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.05f), 0);
        }
        ShopSetupBuilder.Box(parts, new Vector3(0f, 0.45f, 0f), new Vector3(1.0f, 0.06f, 0.07f), 0);
        ShopSetupBuilder.Box(parts, new Vector3(0f, 1.35f, 0f), new Vector3(1.0f, 0.06f, 0.07f), 0);
        for (int i = 0; i < 7; i++)
        {
            float x = -0.45f + i * 0.15f;
            ShopSetupBuilder.Box(parts, new Vector3(x, 0.02f, 0f), new Vector3(0.08f, 0.06f, 0.08f), 0); // spikes at the bottom
        }

        ProBuilderMesh target = parts[0];
        CombineMeshes.Combine(parts, target);
        for (int i = 1; i < parts.Count; i++) { Object.DestroyImmediate(parts[i].gameObject); }
        if (iron != null) { target.SetMaterial(target.faces, iron); }
        target.ToMesh();
        target.Refresh();

        GameObject bars = target.gameObject;
        bars.name = "Bars";
        if (iron != null) { bars.GetComponent<MeshRenderer>().sharedMaterial = iron; }
        foreach (Component c in bars.GetComponents<Component>()) { if (c != null && c.GetType().Name == "ProBuilderShape") Object.DestroyImmediate(c); }

        GameObject root = new GameObject("ChallengeGate");
        try
        {
            root.layer = ObstacleLayer;
            bars.layer = ObstacleLayer;
            bars.transform.SetParent(root.transform, false);

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.size = new Vector3(1.05f, 1.8f, 0.15f);
            NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = col.center;
            obstacle.size = col.size;
            obstacle.carving = true;

            ChallengeGate gate = root.AddComponent<ChallengeGate>();
            SerializedObject so = new SerializedObject(gate);
            so.FindProperty("bars").objectReferenceValue = bars.transform;
            so.FindProperty("blocker").objectReferenceValue = col;
            so.FindProperty("obstacle").objectReferenceValue = obstacle;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GatePath);
            log.AppendLine("saved " + GatePath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildAltar()
    {
        Material stone = AssetDatabase.LoadAssetAtPath<Material>(StoneMatPath);
        var parts = new List<ProBuilderMesh>();
        ShopSetupBuilder.Box(parts, new Vector3(0f, 0.1f, 0f), new Vector3(1.1f, 0.2f, 1.1f), 3);
        ShopSetupBuilder.Box(parts, new Vector3(0f, 0.55f, 0f), new Vector3(0.55f, 0.7f, 0.55f), 0);
        ShopSetupBuilder.Box(parts, new Vector3(0f, 0.95f, 0f), new Vector3(0.85f, 0.12f, 0.85f), 2);

        ProBuilderMesh target = parts[0];
        CombineMeshes.Combine(parts, target);
        for (int i = 1; i < parts.Count; i++) { Object.DestroyImmediate(parts[i].gameObject); }
        if (stone != null) { target.SetMaterial(target.faces, stone); }
        target.ToMesh();
        target.Refresh();

        GameObject root = target.gameObject;
        try
        {
            root.name = "ChallengeAltar";
            root.layer = ObstacleLayer;
            if (stone != null) { root.GetComponent<MeshRenderer>().sharedMaterial = stone; }
            foreach (Component c in root.GetComponents<Component>()) { if (c != null && c.GetType().Name == "ProBuilderShape") Object.DestroyImmediate(c); }

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(1.1f, 1.0f, 1.1f);
            NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = col.center;
            obstacle.size = col.size;
            obstacle.carving = true;

            SpriteRenderer glow = Child(root, "Glow", 1.1f, AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath), 0);
            SpriteRenderer iconSr = Child(root, "Icon", 0.45f, AssetDatabase.LoadAssetAtPath<Sprite>(IconPath), 1);

            GameObject labelGo = new GameObject("Label");
            labelGo.layer = ItemLayer;
            labelGo.transform.SetParent(root.transform, false);
            TextMeshPro label = labelGo.AddComponent<TextMeshPro>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) { label.font = font; label.fontSharedMaterial = font.material; }
            label.fontSize = 1.5f;
            label.alignment = TextAlignmentOptions.Bottom;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(4.5f, 1.2f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(0, 0, 0, 220);

            GameObject prompt = ShopSetupBuilder.CopyPrompt(root.transform);
            if (prompt != null) { prompt.transform.localPosition = new Vector3(0f, 1.15f, -0.6f); }

            ChallengeAltar altar = root.AddComponent<ChallengeAltar>();
            SerializedObject so = new SerializedObject(altar);
            so.FindProperty("iconRenderer").objectReferenceValue = iconSr;
            so.FindProperty("glowRenderer").objectReferenceValue = glow;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("uiButtonPrompt").objectReferenceValue = prompt;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, AltarPath);
            log.AppendLine("saved " + AltarPath + (font == null ? "" : ""));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static SpriteRenderer Child(GameObject root, string name, float scale, Sprite sprite, int order)
    {
        GameObject go = new GameObject(name);
        go.layer = ItemLayer;
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        go.transform.localScale = Vector3.one * scale;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    // =========================================================
    // ROOM
    // =========================================================

    private static void BuildRoom()
    {
        // Rebuild from scratch when the source room changed (e.g. Weapon Room -> Large Room)
        string sourceKey = "ChallengeSetupBuilder_RoomSource" + Application.dataPath;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath) != null && EditorPrefs.GetString(sourceKey, "") != SourceRoomPath)
        {
            AssetDatabase.DeleteAsset(RoomPath);
            log.AppendLine("source room changed: rebuilding the Challenge Room");
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath) == null)
        {
            if (!AssetDatabase.CopyAsset(SourceRoomPath, RoomPath)) { throw new System.Exception("copy failed"); }
            AssetDatabase.ImportAsset(RoomPath, ImportAssetOptions.ForceSynchronousImport);
            EditorPrefs.SetString(sourceKey, SourceRoomPath);
        }

        GameObject room = PrefabUtility.LoadPrefabContents(RoomPath);
        try
        {
            room.name = Path.GetFileNameWithoutExtension(RoomPath);
            Transform old = room.transform.Find("Challenge");
            if (old != null) { Object.DestroyImmediate(old.gameObject); }

            // No normal spawners or loot: the challenge brings its own enemies and reward
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

            // Keep the arena floor clear: breakable furniture in the middle goes, cabinets along the walls stay
            int cleared = 0;
            var clear = new HashSet<GameObject>();
            foreach (Fracture f in room.GetComponentsInChildren<Fracture>(true))
            {
                Vector3 p = room.transform.InverseTransformPoint(f.transform.position) - RoomCenter;
                if (Mathf.Abs(p.x) < 2.9f && Mathf.Abs(p.z) < 2.9f) { clear.Add(ShopSetupBuilder.OutermostRemovable(f.gameObject)); }
            }
            foreach (GameObject go in clear) { if (go != null) { Object.DestroyImmediate(go); cleared++; } }
            log.AppendLine($"removed {doomed.Count} spawners/loot, cleared {cleared} decor");

            // Every doorway gets a gate (wins over the neighbour's door, but not over treasure doors)
            GameObject gate = AssetDatabase.LoadAssetAtPath<GameObject>(GatePath);
            int gated = 0;
            foreach (Doorway doorway in room.GetComponentsInChildren<Doorway>(true))
            {
                doorway.ConnectorPrefabs = new WeightedTable<GameObject>();
                if (gate != null) { doorway.ConnectorPrefabs.Entries.Add(new WeightedEntry<GameObject>(gate)); }
                doorway.DoorPrefabPriority = 150;
                doorway.DoorPrefabPositionOffset = Vector3.zero;
                doorway.DoorPrefabRotationOffset = Vector3.zero;
                PrefabUtility.RecordPrefabInstancePropertyModifications(doorway);
                gated++;
            }
            log.AppendLine($"gates on {gated} doorways");

            // Altar, spawn points, chest point
            GameObject group = new GameObject("Challenge");
            group.transform.SetParent(room.transform, false);
            ChallengeRoom challengeRoom = group.AddComponent<ChallengeRoom>();

            GameObject altar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AltarPath), group.transform);
            altar.name = "Challenge Altar";
            altar.transform.localPosition = RoomCenter + new Vector3(0f, FloorY, 0f);

            var points = new List<Transform>();
            // Around the edges of the 8 x 8 arena: four corners (clear of the wall cabinets) and the middle of each side
            Vector3[] offsets =
            {
                new Vector3(-2.6f, 0f, -2.9f), new Vector3(2.6f, 0f, -2.9f),
                new Vector3(-2.6f, 0f, 2.6f), new Vector3(2.6f, 0f, 2.6f),
                new Vector3(0f, 0f, 2.9f), new Vector3(0f, 0f, -2.9f),
                new Vector3(-2.9f, 0f, 0f), new Vector3(2.9f, 0f, 0f),
            };
            var spots = new List<Vector3>();
            foreach (Vector3 o in offsets) { spots.Add(RoomCenter + o + new Vector3(0f, FloorY, 0f)); }
            foreach (Vector3 s in spots)
            {
                Transform p = new GameObject("Spawn Point").transform;
                p.SetParent(group.transform, false);
                p.localPosition = s;
                points.Add(p);
            }

            Transform chestPoint = new GameObject("Chest Point").transform;
            chestPoint.SetParent(group.transform, false);
            chestPoint.localPosition = RoomCenter + new Vector3(0f, FloorY, 1.3f); // between the back pillars
            chestPoint.localRotation = Quaternion.identity; // chest front faces -Z (towards the room's middle / altar)

            SerializedObject so = new SerializedObject(challengeRoom);
            so.FindProperty("altar").objectReferenceValue = altar.GetComponent<ChallengeAltar>();
            so.FindProperty("chestPoint").objectReferenceValue = chestPoint;
            SerializedProperty list = so.FindProperty("spawnPoints");
            list.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++) { list.GetArrayElementAtIndex(i).objectReferenceValue = points[i]; }
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(room, RoomPath);
            log.AppendLine("saved " + RoomPath);
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
        GameObject room = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath);
        if (room == null) { throw new System.Exception("challenge room missing"); }

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

            // Four doorways, so it fits on the main path too (the gates only close if you start the challenge)
            flow.TileInjectionRules.RemoveAll(r => r == null || r.TileSet == tileSet);
            flow.TileInjectionRules.Add(new TileInjectionRule
            {
                TileSet = tileSet,
                NormalizedPathDepth = new FloatRange(0.3f, 0.8f),
                NormalizedBranchDepth = new FloatRange(0f, 1f),
                CanAppearOnMainPath = true,
                CanAppearOnBranchPath = true,
                IsRequired = true,
            });
            EditorUtility.SetDirty(flow);
            log.AppendLine("injected into " + Path.GetFileName(flowPath));
        }
    }

    // =========================================================
    // ICON: crossed swords
    // =========================================================

    private static ShopPixelArt.Canvas SwordsIcon()
    {
        var c = new ShopPixelArt.Canvas(32, 32);
        Color steel = ShopPixelArt.Hex("#d8dde4"), shade = ShopPixelArt.Hex("#8a939e"), hilt = ShopPixelArt.Hex("#b07a2a");
        c.Line(6, 6, 25, 25, steel, 2);
        c.Line(25, 6, 6, 25, steel, 2);
        c.Line(7, 6, 25, 24, shade, 1);
        c.Line(24, 6, 6, 24, shade, 1);
        c.Line(3, 9, 9, 3, hilt, 2);   // guards
        c.Line(22, 3, 28, 9, hilt, 2);
        c.Rect(2, 2, 4, 4, hilt);      // pommels
        c.Rect(27, 2, 29, 4, hilt);
        c.Outline(ShopPixelArt.Hex("#1a1210"));
        return c;
    }
}
