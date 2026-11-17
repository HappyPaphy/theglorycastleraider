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
/// One-shot builder for the treasure rooms (Bronze / Silver / Gold):
///  1. Tier metal materials
///  2. Locked door prefabs (WoodenDoor without Fracture: can't be broken, only unlocked with a key)
///  3. Treasure chest prefabs with tier rewards
///  4. Treasure Room prefabs (Weapon Room copy, one entrance at -Z with the locked door)
///  5. Treasure Room tile set, injected at the end of a branch in the Prison First / Second floor flows
/// Re-run from Tools > Shop > Build Treasure Rooms.
/// </summary>
public static class TreasureSetupBuilder
{
    private const string AutoKey = "TreasureSetupBuilder_v1";

    private const string PrefabFolder = "Assets/003-Prefabs/Treasure";
    private const string MaterialFolder = "Assets/006-Materials/Furniture";
    private const string WoodenDoorPath = "Assets/003-Prefabs/WoodenDoor.prefab";
    private const string LongWallPath = "Assets/012-MyDungenAsset/Wall/Long Wall (1).prefab";
    private const string WoodMaterialPath = "Assets/006-Materials/Furniture/Mat_Shelf.mat";
    private const string EnergyOrbPrefabPath = "Assets/003-Prefabs/EnergyOrb.prefab";
    private const string OrbFolder = "Assets/013-Datas/Perks/Energy Orbs";
    private const string FontPath = "Assets/020-Others/Font/KnightQuest/KnightsQuest SDF.asset";

    private const string RoomFolder = "Assets/012-MyDungenAsset/Room/Prison";
    private const string SourceRoomPath = RoomFolder + "/Medium Room (Weapon Room).prefab";
    private const string TileSetPath = "Assets/012-MyDungenAsset/Tile Set/Prison/Treasure Room Set (Prison).asset";
    private static readonly string[] FlowPaths =
    {
        "Assets/012-MyDungenAsset/Dungeon Flow (Prison) (First Floor).asset",
        "Assets/012-MyDungenAsset/Dungeon Flow (Prison) (Second Floor).asset",
    };

    private const string Items = "Assets/003-Prefabs/Items";
    private const string Weapons = "Assets/003-Prefabs/Weapons";
    private const float FloorY = 0.09f;
    // Each one is a required rule, so each needs its own side branch (see the Prison archetype's Branch Count)
    private const int TreasureRoomsPerFloor = 2;
    private const int ObstacleLayer = 8;

    private class TierInfo
    {
        public TreasureKeyType key;
        public Color metal;
        public float roomWeight;
        public int coinsMin, coinsMax, itemCount;
        public string orb;
        public string[] items;
    }

    private static readonly TierInfo[] Tiers =
    {
        new TierInfo
        {
            key = TreasureKeyType.Bronze, metal = new Color(0.62f, 0.38f, 0.18f), roomWeight = 3f,
            coinsMin = 40, coinsMax = 70, itemCount = 1, orb = "EnergyOrb_Common",
            items = new[]
            {
                Items + "/Consumables/HealthPotion_Medium.prefab", Items + "/Consumables/ManaPotion_Medium.prefab",
                Items + "/Consumables/HealthPotion_Small.prefab", Items + "/Consumables/ManaPotion_Small.prefab",
            },
        },
        new TierInfo
        {
            key = TreasureKeyType.Silver, metal = new Color(0.66f, 0.69f, 0.74f), roomWeight = 2f,
            coinsMin = 80, coinsMax = 130, itemCount = 2, orb = "EnergyOrb_Rare",
            items = new[]
            {
                Items + "/Consumables/HealthPotion_Big.prefab", Items + "/Consumables/ManaPotion_Big.prefab",
                Items + "/Rings/Ring_Vitality.prefab", Items + "/Rings/Ring_Stamina.prefab",
                Weapons + "/Weapon_ShortSword.prefab", Weapons + "/Weapon_WoodenShield.prefab",
            },
        },
        new TierInfo
        {
            key = TreasureKeyType.Gold, metal = new Color(0.86f, 0.66f, 0.2f), roomWeight = 1f,
            coinsMin = 150, coinsMax = 250, itemCount = 2, orb = "EnergyOrb_Unique",
            items = new[]
            {
                Items + "/Consumables/HealthPotion_Big.prefab", Items + "/Consumables/ManaPotion_Big.prefab",
                Weapons + "/Pyromancy_DimmedHand.prefab", Items + "/Spells/Spell_SmallFireball.prefab",
                Items + "/Spells/Spell_SmallCombustion.prefab", Items + "/Rings/Ring_Vitality.prefab", Items + "/Rings/Ring_Stamina.prefab",
            },
        },
    };

    private static StringBuilder log;
    private static int errors;
    private static TMP_FontAsset font;

    // Where the WoodenDoor sits relative to its doorway in the existing walls
    private static Vector3 doorOffsetPos = Vector3.zero;
    private static Vector3 doorOffsetRot = Vector3.zero;
    private static Vector3 doorScale = Vector3.one;

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

    [MenuItem("Tools/Shop/Build Treasure Rooms")]
    public static void BuildAll()
    {
        log = new StringBuilder();
        errors = 0;
        // The shop builder's helpers log into its own report; point them at ours
        ShopSetupBuilder.log = log;
        ShopSetupBuilder.errors = 0;

        try
        {
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            Step("Door offset", MeasureDoorOffset);

            var tileWeights = new List<(GameObject room, float weight)>();
            foreach (TierInfo tier in Tiers)
            {
                Material metal = null;
                GameObject door = null, chest = null, room = null;
                Step(tier.key + " material", () => metal = MetalMaterial(tier));
                Step(tier.key + " door", () => door = BuildDoor(tier, metal));
                Step(tier.key + " chest", () => chest = BuildChest(tier, metal));
                Step(tier.key + " room", () => room = BuildRoom(tier, door, chest));
                if (room != null) { tileWeights.Add((room, tier.roomWeight)); }
            }

            Step("Dungeon", () => HookIntoDungeon(tileWeights));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }

        errors += ShopSetupBuilder.errors;
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/treasure_setup.txt", log.ToString());
        Debug.Log($"[TreasureSetup] Done with {errors} error(s). Report: Library/PrisonDecor/treasure_setup.txt");
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
    // DOOR OFFSET (measured from Long Wall, which already has a WoodenDoor in its doorway)
    // =========================================================

    private static void MeasureDoorOffset()
    {
        GameObject wall = PrefabUtility.LoadPrefabContents(LongWallPath);
        try
        {
            DoorInteractable door = wall.GetComponentInChildren<DoorInteractable>(true);
            if (door == null)
            {
                // The walls use WoodenDoor as a DunGen connector: copy that doorway's offsets
                foreach (Doorway d in wall.GetComponentsInChildren<Doorway>(true))
                {
                    if (d.ConnectorPrefabs.Entries.Count == 0) { continue; }
                    doorOffsetPos = d.DoorPrefabPositionOffset;
                    doorOffsetRot = d.DoorPrefabRotationOffset;
                    doorScale = Vector3.one;
                    log.AppendLine($"door offset from connector: pos={doorOffsetPos} rot={doorOffsetRot}");
                    return;
                }
                throw new System.Exception("no WoodenDoor in " + LongWallPath);
            }

            Doorway nearest = null;
            float best = float.MaxValue;
            foreach (Doorway d in wall.GetComponentsInChildren<Doorway>(true))
            {
                float dist = (d.transform.position - door.transform.position).sqrMagnitude;
                if (dist < best) { best = dist; nearest = d; }
            }
            if (nearest == null) { throw new System.Exception("no doorway in " + LongWallPath); }

            doorOffsetPos = nearest.transform.InverseTransformPoint(door.transform.position);
            doorOffsetRot = (Quaternion.Inverse(nearest.transform.rotation) * door.transform.rotation).eulerAngles;
            doorScale = door.transform.lossyScale;
            log.AppendLine($"door offset pos={doorOffsetPos.ToString("F3")} rot={doorOffsetRot.ToString("F1")} scale={doorScale.ToString("F3")}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(wall);
        }
    }

    // =========================================================
    // MATERIALS
    // =========================================================

    private static Material MetalMaterial(TierInfo tier)
    {
        string path = $"{MaterialFolder}/Mat_Treasure_{tier.key}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        // Same darkening as the other furniture so it sits in the scene's unlit palette
        Color c = tier.metal * 0.8f;
        c.a = 1f;
        if (mat.HasProperty("_BaseColor")) { mat.SetColor("_BaseColor", c); }
        mat.color = c;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // =========================================================
    // LOCKED DOOR
    // =========================================================

    private static GameObject BuildDoor(TierInfo tier, Material metal)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(WoodenDoorPath);
        GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            PrefabUtility.UnpackPrefabInstance(door, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            door.name = $"TreasureDoor_{tier.key}";
            door.transform.localScale = doorScale;

            // Unbreakable: no Fracture, so attacks / kicks / enemies / Battering Ram ignore it
            foreach (Component c in door.GetComponentsInChildren<Component>(true))
            {
                if (c == null) { continue; }
                string type = c.GetType().Name;
                if (type == "Fracture" || type == "FractureTrigger" || type == "DropItemOnDead") { Object.DestroyImmediate(c); }
            }
            SetLayerRecursive(door.transform, ObstacleLayer, keepLayer: 14);

            // Door size (local) for placing the iron work
            Bounds b = LocalBounds(door);
            log.AppendLine($"door bounds center={b.center.ToString("F2")} size={b.size.ToString("F2")}");

            // Iron bands across both faces
            var bandParts = new List<ProBuilderMesh>();
            foreach (float y in new[] { b.min.y + b.size.y * 0.22f, b.min.y + b.size.y * 0.78f })
            {
                ShopSetupBuilder.Box(bandParts, new Vector3(b.center.x, y, b.min.z - 0.012f), new Vector3(b.size.x * 0.96f, 0.08f, 0.024f), 3);
                ShopSetupBuilder.Box(bandParts, new Vector3(b.center.x, y, b.max.z + 0.012f), new Vector3(b.size.x * 0.96f, 0.08f, 0.024f), 3);
            }
            GameObject bands = Combine(bandParts, "Iron Bands", metal);
            bands.transform.SetParent(door.transform, false);

            // Padlock on each face, at the latch side (hinge is the -X edge)
            float latchX = b.max.x - 0.16f;
            float lockY = b.min.y + b.size.y * 0.5f;
            GameObject lockFront = Padlock(metal, "Padlock Front", new Vector3(latchX, lockY, b.min.z - 0.05f));
            GameObject lockBack = Padlock(metal, "Padlock Back", new Vector3(latchX, lockY, b.max.z + 0.05f));
            lockFront.transform.SetParent(door.transform, false);
            lockBack.transform.SetParent(door.transform, false);

            // Label above the prompt
            GameObject labelGo = new GameObject("Lock Label");
            labelGo.transform.SetParent(door.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            TextMeshPro label = labelGo.AddComponent<TextMeshPro>();
            if (font != null) { label.font = font; }
            label.text = $"Requires {tier.key} Key";
            label.fontSize = 1.6f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(2.4f, 0.4f);
            labelGo.layer = 14;

            DoorInteractable interactable = door.GetComponent<DoorInteractable>();
            SerializedObject so = new SerializedObject(interactable);
            so.FindProperty("isLocked").boolValue = true;
            so.FindProperty("requiredKey").enumValueIndex = (int)tier.key;
            SerializedProperty visuals = so.FindProperty("lockVisuals");
            visuals.arraySize = 2;
            visuals.GetArrayElementAtIndex(0).objectReferenceValue = lockFront;
            visuals.GetArrayElementAtIndex(1).objectReferenceValue = lockBack;
            so.FindProperty("lockLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{PrefabFolder}/TreasureDoor_{tier.key}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(door, path);
            log.AppendLine("saved " + path);
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(door);
        }
    }

    private static GameObject Padlock(Material metal, string name, Vector3 position)
    {
        var parts = new List<ProBuilderMesh>();
        ShopSetupBuilder.Box(parts, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.16f, 0.06f), 3);     // body
        ShopSetupBuilder.Box(parts, new Vector3(-0.05f, 0.12f, 0f), new Vector3(0.03f, 0.1f, 0.03f), 3); // shackle
        ShopSetupBuilder.Box(parts, new Vector3(0.05f, 0.12f, 0f), new Vector3(0.03f, 0.1f, 0.03f), 3);
        ShopSetupBuilder.Box(parts, new Vector3(0f, 0.18f, 0f), new Vector3(0.13f, 0.03f, 0.03f), 3);
        GameObject go = Combine(parts, name, metal);
        go.transform.localPosition = position;
        return go;
    }

    // =========================================================
    // CHEST
    // =========================================================

    private static GameObject BuildChest(TierInfo tier, Material metal)
    {
        Material wood = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);

        // Front faces -Z (towards the room entrance). Pivot = bottom centre.
        const float w = 0.95f, h = 0.5f, d = 0.6f, lidH = 0.24f;

        var body = new List<ProBuilderMesh>();
        ShopSetupBuilder.Box(body, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), 0);
        GameObject root = Combine(body, $"TreasureChest_{tier.key}", wood);
        try
        {
            root.layer = ObstacleLayer;

            var bodyBands = new List<ProBuilderMesh>();
            foreach (float x in new[] { -w * 0.32f, w * 0.32f })
            {
                ShopSetupBuilder.Box(bodyBands, new Vector3(x, h * 0.5f, 0f), new Vector3(0.07f, h + 0.01f, d + 0.02f), 3);
            }
            ShopSetupBuilder.Box(bodyBands, new Vector3(0f, 0.04f, 0f), new Vector3(w + 0.02f, 0.06f, d + 0.02f), 3);  // foot rim
            Child(Combine(bodyBands, "Bands", metal), root.transform, Vector3.zero);

            // Lid: pivot on the back top edge so it swings up and back
            GameObject lid = new GameObject("Lid");
            lid.transform.SetParent(root.transform, false);
            lid.transform.localPosition = new Vector3(0f, h, d * 0.5f);

            var lidWood = new List<ProBuilderMesh>();
            ShopSetupBuilder.Box(lidWood, new Vector3(0f, lidH * 0.5f, -d * 0.5f), new Vector3(w + 0.02f, lidH, d + 0.02f), 1);
            Child(Combine(lidWood, "Lid Wood", wood), lid.transform, Vector3.zero);

            var lidMetal = new List<ProBuilderMesh>();
            foreach (float x in new[] { -w * 0.32f, w * 0.32f })
            {
                ShopSetupBuilder.Box(lidMetal, new Vector3(x, lidH * 0.5f, -d * 0.5f), new Vector3(0.07f, lidH + 0.01f, d + 0.04f), 3);
            }
            ShopSetupBuilder.Box(lidMetal, new Vector3(0f, 0.0f, -d - 0.015f), new Vector3(0.16f, 0.16f, 0.03f), 3); // lock plate
            Child(Combine(lidMetal, "Lid Bands", metal), lid.transform, Vector3.zero);

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, (h + lidH) * 0.5f, 0f);
            col.size = new Vector3(w + 0.04f, h + lidH, d + 0.04f);

            NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = col.center;
            obstacle.size = col.size;
            obstacle.carving = true;

            GameObject prompt = ShopSetupBuilder.CopyPrompt(root.transform);
            if (prompt != null) { prompt.transform.localPosition = new Vector3(0f, 1.15f, -0.45f); }

            TreasureChest chest = root.AddComponent<TreasureChest>();
            SerializedObject so = new SerializedObject(chest);
            so.FindProperty("tier").enumValueIndex = (int)tier.key;
            so.FindProperty("uiButtonPrompt").objectReferenceValue = prompt;
            so.FindProperty("lid").objectReferenceValue = lid.transform;
            so.FindProperty("coinsMin").intValue = tier.coinsMin;
            so.FindProperty("coinsMax").intValue = tier.coinsMax;
            so.FindProperty("itemCount").intValue = tier.itemCount;

            GameObject orbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnergyOrbPrefabPath);
            so.FindProperty("energyOrbPrefab").objectReferenceValue = orbPrefab != null ? orbPrefab.GetComponent<EnergyOrbPickup>() : null;
            so.FindProperty("energyOrb").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnergyOrbDefinition>($"{OrbFolder}/{tier.orb}.asset");

            SerializedProperty pool = so.FindProperty("itemPool");
            var prefabs = new List<GameObject>();
            foreach (string p in tier.items)
            {
                GameObject item = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (item != null) { prefabs.Add(item); } else { log.AppendLine("!! missing reward " + p); errors++; }
            }
            pool.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++) { pool.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i]; }
            so.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine($"chest {tier.key}: orb={(so.FindProperty("energyOrb").objectReferenceValue != null)} prefab={(orbPrefab != null)} items={prefabs.Count}");

            string path = $"{PrefabFolder}/TreasureChest_{tier.key}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine("saved " + path);
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // =========================================================
    // ROOM
    // =========================================================

    private static GameObject BuildRoom(TierInfo tier, GameObject door, GameObject chest)
    {
        string path = $"{RoomFolder}/Treasure Room ({tier.key}) (Prison).prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            if (!AssetDatabase.CopyAsset(SourceRoomPath, path)) { throw new System.Exception("copy failed " + path); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        GameObject room = PrefabUtility.LoadPrefabContents(path);
        try
        {
            room.name = Path.GetFileNameWithoutExtension(path);
            Transform old = room.transform.Find("Treasure");
            if (old != null) { Object.DestroyImmediate(old.gameObject); }

            RemoveSpawnersAndLoot(room);
            ClearDecor(room);

            // One way in: keep the -Z doorway, seal the other three
            int sealedCount = 0;
            foreach (Doorway doorway in room.GetComponentsInChildren<Doorway>(true))
            {
                Vector3 fwd = room.transform.InverseTransformDirection(doorway.transform.forward);
                if (fwd.z < -0.9f)
                {
                    doorway.ConnectorPrefabs = new WeightedTable<GameObject>();
                    if (door != null) { doorway.ConnectorPrefabs.Entries.Add(new WeightedEntry<GameObject>(door)); }
                    doorway.DoorPrefabPriority = 200; // always wins over the neighbour's door
                    doorway.DoorPrefabPositionOffset = doorOffsetPos;
                    doorway.DoorPrefabRotationOffset = doorOffsetRot;
                    doorway.AvoidRotatingDoorPrefab = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(doorway);
                    continue;
                }

                GameObject wallRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(doorway.gameObject);
                if (wallRoot != null) { PrefabUtility.UnpackPrefabInstance(wallRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); }
                foreach (GameObject blocker in doorway.BlockerSceneObjects) { if (blocker != null) { blocker.SetActive(true); } }
                Object.DestroyImmediate(doorway);
                sealedCount++;
            }
            log.AppendLine($"{tier.key} room: sealed {sealedCount} doorways");

            // Chest against the back wall, facing the entrance
            GameObject group = new GameObject("Treasure");
            group.transform.SetParent(room.transform, false);
            if (chest != null)
            {
                GameObject c = (GameObject)PrefabUtility.InstantiatePrefab(chest, group.transform);
                c.transform.localPosition = new Vector3(0f, FloorY, 1.9f);
                c.transform.localRotation = Quaternion.identity;
            }

            // A little dressing in the back corners
            ShopSetupBuilder.PlacePrefab("Assets/003-Prefabs/Furniture/ClayPot_Tall.prefab", group.transform, new Vector3(-2.35f, FloorY, 2.35f), 0f, "Pot Tall");
            ShopSetupBuilder.PlacePrefab("Assets/003-Prefabs/Furniture/ClayPot_Small.prefab", group.transform, new Vector3(-1.75f, FloorY, 2.45f), 0f, "Pot Small");
            ShopSetupBuilder.PlacePrefab("Assets/003-Prefabs/Furniture/Barrel.prefab", group.transform, new Vector3(2.3f, FloorY, 2.3f), 0f, "Barrel");
            if (tier.key != TreasureKeyType.Bronze)
            {
                ShopSetupBuilder.PlacePrefab("Assets/003-Prefabs/Furniture/ClayPot_Small.prefab", group.transform, new Vector3(1.7f, FloorY, 2.5f), 0f, "Pot Small 2");
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(room, path);
            log.AppendLine("saved " + path);
            return saved;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(room);
        }
    }

    private static void RemoveSpawnersAndLoot(GameObject room)
    {
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
        foreach (GameObject go in doomed) { if (go != null) { Object.DestroyImmediate(go); } }
        log.AppendLine($"removed {doomed.Count} spawners / loot");
    }

    // Keep the side decor, clear the back half (chest) and the entrance lane
    private static void ClearDecor(GameObject room)
    {
        Transform decor = room.transform.Find(PrisonRoomDecorator.DecorGroupName);
        if (decor == null) { return; }

        var doomed = new List<GameObject>();
        foreach (Transform child in decor)
        {
            Vector3 p = child.localPosition;
            bool backHalf = p.z > 0.75f;
            bool entranceLane = Mathf.Abs(p.x) < 1.2f && p.z < -1.5f;
            if (backHalf || entranceLane) { doomed.Add(child.gameObject); }
        }
        foreach (GameObject go in doomed) { Object.DestroyImmediate(ShopSetupBuilder.OutermostRemovable(go)); }
        log.AppendLine($"cleared {doomed.Count} decor");
    }

    // =========================================================
    // DUNGEON
    // =========================================================

    private static void HookIntoDungeon(List<(GameObject room, float weight)> rooms)
    {
        if (rooms.Count == 0) { throw new System.Exception("no treasure rooms built"); }

        TileSet tileSet = AssetDatabase.LoadAssetAtPath<TileSet>(TileSetPath);
        if (tileSet == null)
        {
            tileSet = ScriptableObject.CreateInstance<TileSet>();
            AssetDatabase.CreateAsset(tileSet, TileSetPath);
        }
        tileSet.Tiles = new WeightedTable<GameObject>();
        foreach (var (room, weight) in rooms) { tileSet.AddTile(room, weight, weight); }
        EditorUtility.SetDirty(tileSet);

        foreach (string flowPath in FlowPaths)
        {
            DungeonFlow flow = AssetDatabase.LoadAssetAtPath<DungeonFlow>(flowPath);
            if (flow == null) { errors++; log.AppendLine("!! missing flow " + flowPath); continue; }

            // One entrance only, so it has to be a dead end: the end of a side branch
            flow.TileInjectionRules.RemoveAll(r => r == null || r.TileSet == tileSet);
            for (int i = 0; i < TreasureRoomsPerFloor; i++)
            {
                flow.TileInjectionRules.Add(new TileInjectionRule
                {
                    TileSet = tileSet,
                    NormalizedPathDepth = new FloatRange(0f, 1f),
                    NormalizedBranchDepth = new FloatRange(1f, 1f),
                    CanAppearOnMainPath = false,
                    CanAppearOnBranchPath = true,
                    IsRequired = true,
                });
            }
            EditorUtility.SetDirty(flow);
            log.AppendLine("injected into " + Path.GetFileName(flowPath));
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static GameObject Combine(List<ProBuilderMesh> parts, string name, Material material)
    {
        ProBuilderMesh target = parts[0];
        if (parts.Count > 1)
        {
            CombineMeshes.Combine(parts, target);
            for (int i = 1; i < parts.Count; i++) { Object.DestroyImmediate(parts[i].gameObject); }
        }
        if (material != null) { target.SetMaterial(target.faces, material); }
        target.ToMesh();
        target.Refresh();

        GameObject go = target.gameObject;
        go.name = name;
        if (material != null) { go.GetComponent<MeshRenderer>().sharedMaterial = material; }
        foreach (Component c in go.GetComponents<Component>())
        {
            if (c != null && c.GetType().Name == "ProBuilderShape") { Object.DestroyImmediate(c); }
        }
        return go;
    }

    private static void Child(GameObject child, Transform parent, Vector3 localPos)
    {
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPos;
        child.layer = parent.gameObject.layer;
    }

    private static Bounds LocalBounds(GameObject go)
    {
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) { return mf.sharedMesh.bounds; }

        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool has = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Bounds wb = r.bounds;
            Vector3 c = go.transform.InverseTransformPoint(wb.center);
            Bounds lb = new Bounds(c, go.transform.InverseTransformVector(wb.size));
            if (!has) { b = lb; has = true; } else { b.Encapsulate(lb); }
        }
        return b;
    }

    private static void SetLayerRecursive(Transform t, int layer, int keepLayer)
    {
        if (t.gameObject.layer != keepLayer) { t.gameObject.layer = layer; }
        foreach (Transform child in t) { SetLayerRecursive(child, layer, keepLayer); }
    }
}
