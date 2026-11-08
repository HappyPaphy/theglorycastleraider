using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Decorates the DunGen Prison rooms with breakable (Fracture) furniture.
/// - Apply: places the furniture in <see cref="Plan"/> under a "Breakable Decor" child of each room
///   (re-running replaces that child, so it's safe to tweak the plan and apply again).
/// - Dump: writes a layout report + a top-down floor plan PNG of each room to Library/PrisonDecor.
/// </summary>
public static class PrisonRoomDecorator
{
    public const string RoomFolder = "Assets/012-MyDungenAsset/Room/Prison";
    public const string OutputFolder = "Library/PrisonDecor";
    public const string DecorGroupName = "Breakable Decor";

    private const string Box = "Assets/003-Prefabs/WoodenBox.prefab";
    private const string Cabinet = "Assets/003-Prefabs/WoodenCabinet.prefab";
    private const string Chair = "Assets/003-Prefabs/WoodenChair.prefab";
    private const string Table = "Assets/003-Prefabs/WoodenTable.prefab";

    // ProBuilder furniture (see ProBuilderFurnitureBuilder). Wall pieces follow the cabinet rotation rule.
    private const string Barrel = "Assets/003-Prefabs/Furniture/Barrel.prefab";
    private const string Bench = "Assets/003-Prefabs/Furniture/Bench.prefab";
    private const string Stool = "Assets/003-Prefabs/Furniture/Stool.prefab";
    private const string Shelf = "Assets/003-Prefabs/Furniture/Shelf.prefab";
    private const string Bed = "Assets/003-Prefabs/Furniture/PrisonBed.prefab";
    private const string Rack = "Assets/003-Prefabs/Furniture/WeaponRack.prefab";
    private const string Pillory = "Assets/003-Prefabs/Furniture/Pillory.prefab";
    private const string PotTall = "Assets/003-Prefabs/Furniture/ClayPot_Tall.prefab";
    private const string PotSmall = "Assets/003-Prefabs/Furniture/ClayPot_Small.prefab";

    private static readonly string[] FurniturePrefabs =
    {
        Box, Cabinet, Chair, Table,
        Barrel, Bench, Stool, Shelf, Bed, Rack, Pillory, PotTall, PotSmall,
        "Assets/003-Prefabs/ItemBox (Equipment).prefab",
        "Assets/003-Prefabs/ItemBox (Energy Orb).prefab",
    };

    // Relative scales matching the furniture already placed by hand in Large Room (4 Doors) / Long Room
    private static readonly Vector3 ChairScale = Vector3.one * 0.66f;
    private static readonly Vector3 TableScale = new Vector3(0.9f, 1f, 0.55f);

    private struct Decor
    {
        public string room, prefab;
        public float x, z, rotY;
        public Vector3 scale;
    }

    private static Decor D(string room, string prefab, float x, float z, float rotY, float scale = 1f)
        => new Decor { room = room, prefab = prefab, x = x, z = z, rotY = rotY, scale = Vector3.one * scale };

    private static Decor D(string room, string prefab, float x, float z, float rotY, Vector3 scale)
        => new Decor { room = room, prefab = prefab, x = x, z = z, rotY = rotY, scale = scale };

    // Cabinet rotY by wall it backs onto: +Z wall = 0, -Z = 180, +X = 90, -X = 270 (pivot ~0.09 in from the wall face)
    // Chair rotY by facing: -Z = 0, +Z = 180, -X = 90, +X = 270
    private static readonly Decor[] Plan =
    {
        // ---------- Small Room (Start): 4x4, doors +Z/-X/-Z, solid +X wall ----------
        D("Small Room (Start)", Cabinet, 1.6f, 0f, 90f),
        D("Small Room (Start)", Box, -1.3f, -1.3f, 10f),
        D("Small Room (Start)", Box, -1.3f, 1.3f, -15f, 0.9f),
        D("Small Room (Start)", Box, 1.3f, 1.3f, 30f, 0.8f),
        D("Small Room (Start)", PotSmall, 1.3f, -1.3f, 0f),

        // ---------- Small Room (End): 4x4, ladder on +X wall ----------
        D("Small Room (End)", Box, -1.3f, 1.3f, 0f),
        D("Small Room (End)", Box, -1.3f, -1.3f, 20f, 0.9f),
        D("Small Room (End)", Box, 1.2f, -1.3f, -10f, 0.8f),
        D("Small Room (End)", Barrel, 1.3f, 1.3f, 0f),

        // ---------- I Room: 1.5m wide corridor ----------
        D("I Room (1)", Cabinet, -0.66f, -3.2f, 270f),
        D("I Room (1)", Box, 0.4f, -4.2f, 15f, 0.65f),
        D("I Room (1)", Box, 0.4f, -1.5f, -10f, 0.65f),
        D("I Room (1)", PotTall, 0.48f, -0.4f, 0f),

        // ---------- Medium Room (First Weapon Room): 6x6, doors centre of each wall, crate in middle ----------
        D("Medium Room (First Weapon Room)", Cabinet, -1.7f, 2.66f, 0f),
        D("Medium Room (First Weapon Room)", Bed, 1.65f, 2.66f, 0f),
        D("Medium Room (First Weapon Room)", Rack, -2.66f, 1.4f, 270f),
        D("Medium Room (First Weapon Room)", Rack, 2.66f, 1.2f, 90f),
        D("Medium Room (First Weapon Room)", Stool, -0.75f, -1.95f, 10f),
        D("Medium Room (First Weapon Room)", Table, -1.8f, -1.85f, 0f, TableScale),
        D("Medium Room (First Weapon Room)", Chair, -1.75f, -1.1f, 0f, ChairScale),
        D("Medium Room (First Weapon Room)", Box, 2.2f, -2.2f, 10f, 0.9f),
        D("Medium Room (First Weapon Room)", Box, 1.45f, -2.3f, -20f, 0.7f),
        D("Medium Room (First Weapon Room)", Box, 2.25f, -1.4f, 5f, 0.7f),

        // ---------- Medium Room (Weapon Room): mirrored layout, orb box in middle ----------
        D("Medium Room (Weapon Room)", Shelf, -1.7f, -2.66f, 180f),
        D("Medium Room (Weapon Room)", Bed, 1.65f, -2.66f, 180f),
        D("Medium Room (Weapon Room)", Rack, -2.66f, -1.4f, 270f),
        D("Medium Room (Weapon Room)", Rack, 2.66f, -1.2f, 90f),
        D("Medium Room (Weapon Room)", Table, 1.8f, 1.85f, 0f, TableScale),
        D("Medium Room (Weapon Room)", Chair, 1.8f, 1.0f, 180f, ChairScale),
        D("Medium Room (Weapon Room)", Box, -2.2f, 2.2f, -10f, 0.9f),
        D("Medium Room (Weapon Room)", Box, -1.45f, 2.3f, 25f, 0.7f),
        D("Medium Room (Weapon Room)", Box, -2.25f, 1.4f, 0f, 0.7f),

        // ---------- Large Room (8 Doors): cabinet in the middle of each wall between its two doors ----------
        D("Large Room (8 Doors)", Cabinet, -2f, 5.66f, 0f),
        D("Large Room (8 Doors)", Cabinet, -2f, -1.66f, 180f),
        D("Large Room (8 Doors)", Cabinet, -5.66f, 2f, 270f),
        D("Large Room (8 Doors)", Cabinet, 1.66f, 2f, 90f),
        D("Large Room (8 Doors)", Box, -2.9f, 5.35f, 10f, 0.8f),
        D("Large Room (8 Doors)", Box, 1.35f, 2.9f, 15f, 0.7f),
        D("Large Room (8 Doors)", Barrel, -1.1f, -1.35f, 0f),
        D("Large Room (8 Doors)", Barrel, -5.3f, 1.05f, 0f),
        D("Large Room (8 Doors)", PotSmall, 1.4f, 1.15f, 0f),
        D("Large Room (8 Doors)", Bench, -2.3f, 0.75f, 0f),
        D("Large Room (8 Doors)", Bench, -2.3f, 3.55f, 0f),

        // ---------- L Shape Room ----------
        D("L Shape Room (1)", Cabinet, -1.6f, -1.66f, 180f),
        D("L Shape Room (1)", Shelf, -0.9f, 1.66f, 0f),
        D("L Shape Room (1)", Table, -3.6f, -1.0f, 0f, TableScale),
        D("L Shape Room (1)", Chair, -3.6f, -0.25f, 0f, ChairScale),
        D("L Shape Room (1)", Box, -5.3f, 2.35f, 10f, 0.8f),
        D("L Shape Room (1)", Barrel, -2.65f, 2.25f, 0f),
        D("L Shape Room (1)", PotTall, -5.3f, 3.0f, 0f),
        D("L Shape Room (1)", Stool, -2.55f, -0.9f, -10f),

        // ---------- S Shape Room ----------
        D("S Shape Room (1)", Cabinet, -1.0f, 3.66f, 0f),
        D("S Shape Room (1)", Shelf, 0f, 0.66f, 0f),
        D("S Shape Room (1)", Box, -1.5f, 6.35f, 20f, 0.7f),
        D("S Shape Room (1)", Barrel, 0.4f, 1.35f, 0f),
        D("S Shape Room (1)", PotSmall, -1.3f, 0.45f, 0f),

        // ---------- Plus Room: one crate against the side of each arm ----------
        D("Plus Room (1)", Box, 2.8f, 0.55f, 10f, 0.7f),
        D("Plus Room (1)", Box, -2.8f, -0.55f, -15f, 0.7f),
        D("Plus Room (1)", Box, 0.55f, -2.8f, 5f, 0.6f),
        D("Plus Room (1)", Box, -0.55f, 2.8f, -5f, 0.6f),

        // ---------- Boss Room: decor on the walls only, arena centre stays clear ----------
        D("Boss Room (1)", Cabinet, -2f, 5.66f, 0f),
        D("Boss Room (1)", Shelf, -6f, -5.66f, 180f),
        D("Boss Room (1)", Box, -9.3f, -2.0f, 15f, 0.8f),
        D("Boss Room (1)", Box, -9.35f, -1.25f, -10f, 0.6f),
        D("Boss Room (1)", Box, 1.3f, -1.6f, -10f, 0.8f),
        D("Boss Room (1)", Pillory, -6f, 5.3f, 0f),
        D("Boss Room (1)", Pillory, -2f, -5.3f, 0f),
        D("Boss Room (1)", Barrel, -9.3f, 2.0f, 0f),
        D("Boss Room (1)", Barrel, 1.3f, 1.8f, 0f),
    };

    private static string ApplyKey => "PrisonRoomDecorator.Applied.v4." + Application.dataPath;

    [InitializeOnLoadMethod]
    private static void AutoApplyOnce()
    {
        if (EditorPrefs.GetBool(ApplyKey, false)) { return; }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(ApplyKey, true);
            ApplyPlan();
        };
    }

    // =========================================================
    // APPLY
    // =========================================================

    [MenuItem("Tools/Prison Decor/Apply Breakable Furniture")]
    public static void ApplyPlan()
    {
        Directory.CreateDirectory(OutputFolder);
        StringBuilder log = new StringBuilder();

        Dictionary<string, List<Decor>> byRoom = new Dictionary<string, List<Decor>>();
        foreach (Decor d in Plan)
        {
            if (!byRoom.TryGetValue(d.room, out List<Decor> list)) { byRoom[d.room] = list = new List<Decor>(); }
            list.Add(d);
        }

        foreach (KeyValuePair<string, List<Decor>> room in byRoom)
        {
            string path = $"{RoomFolder}/{room.Key}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                log.AppendLine($"!! missing room {path}");
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyRoom(root, room.Value, log, room.Key);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            catch (System.Exception e)
            {
                log.AppendLine($"!! {room.Key}: {e}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        File.WriteAllText(Path.Combine(OutputFolder, "apply.txt"), log.ToString());
        Debug.Log($"[PrisonRoomDecorator] Applied breakable furniture. Log: {Path.GetFullPath(OutputFolder)}/apply.txt");

        DumpAll();
    }

    private static void ApplyRoom(GameObject root, List<Decor> decor, StringBuilder log, string roomName)
    {
        // Replace the group from a previous run
        Transform old = root.transform.Find(DecorGroupName);
        if (old != null) { Object.DestroyImmediate(old.gameObject); }

        GameObject group = new GameObject(DecorGroupName);
        group.transform.SetParent(root.transform, false);

        float floorY = GetFloorY(root);
        log.AppendLine($"# {roomName} floorY={floorY:0.###}");

        // Solids to check overlaps against (walls, pillars, existing props)
        List<KeyValuePair<string, Bounds>> solids = new List<KeyValuePair<string, Bounds>>();
        foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger || !c.enabled || !c.gameObject.activeInHierarchy) { continue; }
            if (c.bounds.size.y < 0.15f || c.bounds.min.y > 1.5f) { continue; }
            if (c.name.ToLowerInvariant().Contains("floor")) { continue; }
            solids.Add(new KeyValuePair<string, Bounds>(c.name, c.bounds));
        }

        List<DunGen.Doorway> doors = new List<DunGen.Doorway>(root.GetComponentsInChildren<DunGen.Doorway>(true));

        foreach (Decor d in decor)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab);
            if (prefab == null) { log.AppendLine($"!! missing prefab {d.prefab}"); continue; }

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            inst.transform.localPosition = new Vector3(d.x, floorY, d.z);
            inst.transform.localRotation = Quaternion.Euler(0f, d.rotY, 0f);
            inst.transform.localScale = Vector3.Scale(prefab.transform.localScale, d.scale);

            Bounds b = GetBounds(inst, true);
            log.AppendLine($"{prefab.name} pos=({d.x},{d.z}) rot={d.rotY} b={B(b)}");

            // Overlap check with a small tolerance
            Bounds shrunk = b;
            shrunk.Expand(-0.04f);
            foreach (KeyValuePair<string, Bounds> s in solids)
            {
                if (shrunk.Intersects(s.Value)) { log.AppendLine($"   !! overlaps {s.Key} {B(s.Value)}"); }
            }

            // Keep a walking lane in front of every doorway
            foreach (DunGen.Doorway door in doors)
            {
                Vector3 inward = -door.transform.forward;
                Vector3 laneCenter = door.transform.position + inward * 0.75f;
                Bounds lane = new Bounds(new Vector3(laneCenter.x, 0.8f, laneCenter.z),
                    new Vector3(Mathf.Abs(inward.x) > 0.5f ? 1.5f : 1.0f, 1.5f, Mathf.Abs(inward.z) > 0.5f ? 1.5f : 1.0f));
                if (shrunk.Intersects(lane)) { log.AppendLine($"   !! blocks doorway at {V(door.transform.position)}"); }
            }

            solids.Add(new KeyValuePair<string, Bounds>(inst.name, b));
        }
    }

    // Floor top = bottom of the walls (floor meshes include poles so their bounds are too tall)
    private static float GetFloorY(GameObject root)
    {
        // Highest wall base below 0.5m: some "No Door" wall pieces start at y=0, below the floor surface
        float best = float.MinValue;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.ToLowerInvariant().Contains("wall")) { continue; }
            float y = r.bounds.min.y;
            if (y < 0.5f) { best = Mathf.Max(best, y); }
        }
        return best == float.MinValue ? 0.09f : best;
    }

    // =========================================================
    // DUMP
    // =========================================================

    [MenuItem("Tools/Prison Decor/Dump Room Layouts")]
    public static void DumpAll()
    {
        Directory.CreateDirectory(OutputFolder);
        StringBuilder sb = new StringBuilder();

        DumpFurnitureSizes(sb);

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RoomFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            try { DumpRoom(path, sb); }
            catch (System.Exception e) { sb.AppendLine($"!! {path}: {e.Message}"); }
        }

        File.WriteAllText(Path.Combine(OutputFolder, "report.txt"), sb.ToString());
        Debug.Log($"[PrisonRoomDecorator] Dumped room layouts to {Path.GetFullPath(OutputFolder)}");
    }

    private static void DumpFurnitureSizes(StringBuilder sb)
    {
        sb.AppendLine("# FURNITURE (bounds at identity, pivot = prefab root)");
        foreach (string p in FurniturePrefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (prefab == null) { sb.AppendLine($"missing {p}"); continue; }

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Bounds b = GetBounds(inst, false);
            sb.AppendLine($"{prefab.name}: center={V(b.center)} size={V(b.size)}");
            Object.DestroyImmediate(inst);
        }
        sb.AppendLine();
    }

    private static void DumpRoom(string path, StringBuilder sb)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            string roomName = Path.GetFileNameWithoutExtension(path);
            sb.AppendLine($"# ROOM {roomName}");

            DunGen.Tile tile = root.GetComponentInChildren<DunGen.Tile>(true);
            if (tile != null)
            {
                sb.AppendLine($"tile override={tile.OverrideAutomaticTileBounds} boundsOverride={B(tile.TileBoundsOverride)}");
            }

            Bounds all = GetBounds(root, true);
            sb.AppendLine($"renderBounds={B(all)}");

            List<Vector3> doorPositions = new List<Vector3>();
            foreach (DunGen.Doorway door in root.GetComponentsInChildren<DunGen.Doorway>(true))
            {
                doorPositions.Add(door.transform.position);
                sb.AppendLine($"door {door.name} pos={V(door.transform.position)} fwd={V(door.transform.forward)}");
            }

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.ToLowerInvariant().Contains("floor")) { sb.AppendLine($"floor {HierPath(r.transform, root.transform)} b={B(r.bounds)}"); }
            }

            List<KeyValuePair<Bounds, Color>> furniture = new List<KeyValuePair<Bounds, Color>>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) { continue; }
                GameObject src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                string srcPath = src != null ? AssetDatabase.GetAssetPath(src) : "";
                if (System.Array.IndexOf(FurniturePrefabs, srcPath) < 0) { continue; }

                Bounds fb = GetBounds(t.gameObject, false);
                bool isNew = t.parent != null && t.parent.name == DecorGroupName;
                furniture.Add(new KeyValuePair<Bounds, Color>(fb, FurnitureColor(srcPath, isNew)));
                sb.AppendLine($"furniture {Path.GetFileNameWithoutExtension(srcPath)} '{HierPath(t, root.transform)}' pos={V(t.position)} rotY={t.eulerAngles.y:0} b={B(fb)}");
            }

            List<Bounds> solids = new List<Bounds>();
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger || !c.enabled || !c.gameObject.activeInHierarchy) { continue; }
                Bounds b = c.bounds;
                if (b.size.y < 0.15f) { continue; }
                sb.AppendLine($"solid {HierPath(c.transform, root.transform)} b={B(b)}");
                if (b.min.y < 1.5f && !c.name.ToLowerInvariant().Contains("floor")) { solids.Add(b); }
            }

            DrawFloorPlan(roomName, all, solids, furniture, doorPositions, sb);
            sb.AppendLine();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Color FurnitureColor(string srcPath, bool isNew)
    {
        if (srcPath.Contains("ItemBox")) { return new Color(0.95f, 0.8f, 0.1f); }   // yellow: loot boxes
        if (!isNew) { return new Color(0.2f, 0.5f, 1f); }                            // blue: existing furniture
        if (srcPath == Cabinet) { return new Color(0.85f, 0.35f, 0.1f); }            // orange: new cabinets
        if (srcPath == Table) { return new Color(0.8f, 0.2f, 0.8f); }                // magenta: new tables
        if (srcPath == Chair) { return new Color(1f, 0.55f, 0.75f); }                // pink: new chairs
        if (srcPath == PotTall || srcPath == PotSmall) { return new Color(0.75f, 0.45f, 0.25f); } // terracotta: pots
        if (srcPath.Contains("/Furniture/")) { return new Color(0.1f, 0.75f, 0.75f); }            // teal: ProBuilder furniture
        return new Color(0.9f, 0.15f, 0.15f);                                        // red: new crates
    }

    // Top-down schematic: dark grey floor, light grey walls/solids, green doorways, coloured furniture. +Z is up.
    private static void DrawFloorPlan(string roomName, Bounds all, List<Bounds> solids,
        List<KeyValuePair<Bounds, Color>> furniture, List<Vector3> doors, StringBuilder sb)
    {
        const float ppu = 48f; // pixels per unit
        int w = Mathf.CeilToInt((all.size.x + 1f) * ppu);
        int h = Mathf.CeilToInt((all.size.z + 1f) * ppu);
        float ox = all.min.x - 0.5f, oz = all.min.z - 0.5f;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) { px[i] = new Color(0.08f, 0.08f, 0.08f); }

        void Fill(Bounds b, Color c, float inset = 0f)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((b.min.x + inset - ox) * ppu), 0, w - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((b.max.x - inset - ox) * ppu), 0, w - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((b.min.z + inset - oz) * ppu), 0, h - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((b.max.z - inset - oz) * ppu), 0, h - 1);
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++) { px[z * w + x] = c; }
        }

        Fill(new Bounds(all.center, new Vector3(all.size.x, 0f, all.size.z)), new Color(0.3f, 0.3f, 0.3f));

        // 1m grid
        for (float gx = Mathf.Ceil(ox); gx < ox + w / ppu; gx += 1f)
        {
            int x = Mathf.RoundToInt((gx - ox) * ppu);
            for (int z = 0; z < h; z++) { px[z * w + x] = Mathf.Approximately(gx, 0f) ? new Color(0.5f, 0.5f, 0.2f) : new Color(0.22f, 0.22f, 0.22f); }
        }
        for (float gz = Mathf.Ceil(oz); gz < oz + h / ppu; gz += 1f)
        {
            int z = Mathf.RoundToInt((gz - oz) * ppu);
            for (int x = 0; x < w; x++) { px[z * w + x] = Mathf.Approximately(gz, 0f) ? new Color(0.5f, 0.5f, 0.2f) : new Color(0.22f, 0.22f, 0.22f); }
        }

        foreach (Bounds s in solids) { Fill(s, new Color(0.75f, 0.75f, 0.75f)); }
        foreach (Vector3 d in doors) { Fill(new Bounds(d, new Vector3(1.0f, 0f, 1.0f)), new Color(0.1f, 0.85f, 0.2f)); }
        foreach (KeyValuePair<Bounds, Color> f in furniture) { Fill(f.Key, Color.black); Fill(f.Key, f.Value, 0.03f); }

        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(OutputFolder, roomName + ".png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        sb.AppendLine($"plan {roomName}.png: {w}x{h}px, {ppu}px/unit, bottom-left = ({ox:0.##},{oz:0.##}), +Z up");
    }

    // =========================================================
    // HELPERS
    // =========================================================

    public static Bounds GetBounds(GameObject go, bool includeInactive)
    {
        bool has = false;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(includeInactive))
        {
            if (r is ParticleSystemRenderer || r is SpriteRenderer) { continue; }
            if (!has) { b = r.bounds; has = true; }
            else { b.Encapsulate(r.bounds); }
        }
        return b;
    }

    private static string HierPath(Transform t, Transform root)
    {
        string p = t.name;
        while (t.parent != null && t.parent != root) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    private static string V(Vector3 v) => $"({v.x:0.##},{v.y:0.##},{v.z:0.##})";
    private static string B(Bounds b) => $"min{V(b.min)} max{V(b.max)}";
}
