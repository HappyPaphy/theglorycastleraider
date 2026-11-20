using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DunGen;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot: two-storey castle stair rooms (12 x 12 m, two rows of Small Wall (1) stacked = 5 m tall).
///  - Stair Hall: all exits on the ground floor; stairs up the west wall to an L-shaped balcony (north + east)
///    with railings, pillars underneath and spawners up top.
///  - Stair Tower: enter on the ground floor (south); stairs up to an upper floor over the north half, whose
///    exits (north, east) carry the dungeon on one storey higher.
/// Stairs: solid steps (the navmesh is baked from render meshes; 0.25 m steps < 0.5 m agent climb) plus an
/// invisible ramp collider so the player walks up smoothly. Saved to Room/Castle/Shapes/, added to Tile Set (Castle).
/// Existing rooms are never overwritten. Re-run from Tools > Biomes > Build Castle Stair Rooms.
/// </summary>
public static class CastleStairRoomsBuilder
{
    private const string AutoKey = "CastleStairRoomsBuilder_v1";
    private const string OutFolder = "Assets/012-MyDungenAsset/Room/Castle/Shapes";
    private const string TileSetPath = "Assets/012-MyDungenAsset/Tile Set/Castle/Tile Set (Castle).asset";
    private const string ReferenceRoom = "Assets/012-MyDungenAsset/Room/Castle/Small Room (1) (Castle).prefab";
    private const string WallPath = "Assets/012-MyDungenAsset/Wall/Small Wall (1).prefab";

    private const float Cell = 4f;
    private const float Storey = 2.48f;      // one Small Wall row; also the upper floor level (doorway height)
    private const float Slab = 0.18f;
    private const float FloorTop = 0.09f;    // top of a floor slab centred at its level
    private const float CeilingY = 2f * Storey + 0.18f;
    private const int Steps = 10;

    private enum Dir { E, N, W, S }
    private static readonly Dictionary<Dir, Vector2Int> Step = new Dictionary<Dir, Vector2Int>
    {
        { Dir.E, new Vector2Int(1, 0) }, { Dir.N, new Vector2Int(0, 1) }, { Dir.W, new Vector2Int(-1, 0) }, { Dir.S, new Vector2Int(0, -1) },
    };
    private static readonly Dictionary<Dir, float> WallYaw = new Dictionary<Dir, float>
    {
        { Dir.E, 0f }, { Dir.S, 90f }, { Dir.W, 180f }, { Dir.N, 270f },
    };

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            Build();
        };
    }

    [MenuItem("Tools/Biomes/Build Castle Stair Rooms")]
    public static void Build()
    {
        var log = new StringBuilder();
        int errors = 0;
        var paths = new List<string>();
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            if (!AssetDatabase.IsValidFolder(OutFolder)) { AssetDatabase.CreateFolder("Assets/012-MyDungenAsset/Room/Castle", "Shapes"); }
            var parts = new Parts(log);
            var built = new List<GameObject>();

            foreach (var (name, build) in new (string, System.Action<Room>)[] { ("Stair Hall (Castle)", BuildStairHall), ("Stair Tower (Castle)", BuildStairTower) })
            {
                string path = $"{OutFolder}/{name}.prefab";
                paths.Add(path);
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null) { log.AppendLine("kept " + path); built.Add(existing); continue; }
                try
                {
                    var room = new Room(name, parts, scene, log);
                    build(room);
                    built.Add(room.Save(path));
                }
                catch (System.Exception e)
                {
                    errors++;
                    log.AppendLine($"!! {name}: {e}");
                }
            }

            TileSet set = AssetDatabase.LoadAssetAtPath<TileSet>(TileSetPath);
            if (set != null)
            {
                foreach (GameObject room in built)
                {
                    if (set.TileWeights.Weights.Any(w => w.Value == room)) { continue; }
                    set.TileWeights.Weights.Add(new GameObjectChance(room, 1f, 1f, set));
                    log.AppendLine("added to Tile Set (Castle): " + room.name);
                }
                EditorUtility.SetDirty(set);
            }
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/castle_stairs.txt", log.ToString());
        Debug.Log($"[CastleStairRooms] Done with {errors} error(s). Report: Library/PrisonDecor/castle_stairs.txt");
        RoomSnapshot.RunFor(paths.ToArray());
    }

    // ---------- Room designs (cells 3 x 3, cell (0,0) centred on the origin: x and z run -2..10) ----------

    private static void BuildStairHall(Room r)
    {
        r.Walls(exits: new[] { (V(1, 0), Dir.S, 0), (V(0, 0), Dir.W, 0), (V(2, 1), Dir.E, 0), (V(1, 2), Dir.N, 0) });

        // L-shaped balcony along the north and east walls
        r.Platform("Balcony North", -2f, 10f, 7f, 10f);
        r.Platform("Balcony East", 7f, 10f, -2f, 7f);
        r.Railing("Railing North", new Vector3(0.1f, 0f, 7.08f), new Vector3(7.15f, 0f, 7.08f));
        r.Railing("Railing East", new Vector3(7.08f, 0f, -2f), new Vector3(7.08f, 0f, 7.08f));
        foreach (Vector3 p in new[] { P(7.3f, -0.5f), P(7.3f, 3.3f), P(7.3f, 7.3f), P(3.6f, 7.3f) }) { r.Pillar(p); }

        // Stairs up the west wall, north-bound, landing on the north balcony
        r.Stairs("Stairs", xMin: -1.9f, xMax: 0.1f, zStart: 2f, zEnd: 7f);

        r.Spawner(P(3f, 2f));
        r.Spawner(P(5f, 4.5f));
        r.Spawner(P(4f, 8.6f), Storey);   // balcony
        r.Spawner(P(8.6f, 2.5f), Storey); // balcony

        r.Prop("Barrel", P(9.35f, 9.35f), 0f, Storey);
        r.Prop("ItemBox", P(9.35f, -1.35f), 0f, Storey);
        r.Prop("Bench", P(1.5f, -1.4f), 0f);
        r.Prop("ItemBox", P(-1.35f, -1.35f), 0f);
    }

    private static void BuildStairTower(Room r)
    {
        r.Walls(exits: new[] { (V(1, 0), Dir.S, 0), (V(1, 2), Dir.N, 1), (V(2, 2), Dir.E, 1) });

        // Upper floor over the north half; the space under it stays open (undercroft)
        r.Platform("Upper Floor", -2f, 10f, 4f, 10f);
        r.Railing("Railing", new Vector3(0.1f, 0f, 4.08f), new Vector3(10f, 0f, 4.08f));
        foreach (Vector3 p in new[] { P(3.5f, 4.3f), P(7f, 4.3f) }) { r.Pillar(p); }

        // Stairs up the west wall, north-bound, landing on the upper floor
        r.Stairs("Stairs", xMin: -1.9f, xMax: 0.1f, zStart: -1f, zEnd: 4f);

        r.Spawner(P(4f, 0.5f));
        r.Spawner(P(4f, 7f), Storey);
        r.Spawner(P(8f, 7.5f), Storey);

        r.Prop("Barrel", P(9.35f, -1.35f), 0f);
        r.Prop("Barrel", P(9.35f, 8.5f), 0f);           // in the undercroft
        r.Prop("ItemBox", P(-1.35f, 9.35f), 0f, Storey); // upstairs corner
    }

    // ---------- Parts ----------

    private class Parts
    {
        public GameObject Wall;
        public Material WallMat, FloorMat;
        public Dictionary<Material, Material> WallSwap = new Dictionary<Material, Material>();
        public int FloorLayer;
        public string FloorTag;
        public StaticEditorFlags Flags;
        public Dictionary<string, GameObject> Props = new Dictionary<string, GameObject>();

        public Parts(StringBuilder log)
        {
            Wall = Load<GameObject>(WallPath);
            WallMat = Load<Material>("Assets/006-Materials/Material_Wall_Ceramic.mat");
            FloorMat = Load<Material>("Assets/006-Materials/Material_Floor_Ceramic.mat");
            WallSwap[Load<Material>("Assets/006-Materials/Material_Wall_Prison.mat")] = WallMat;
            WallSwap[Load<Material>("Assets/006-Materials/Material_IronBar.mat")] = WallMat;

            GameObject reference = Load<GameObject>(ReferenceRoom);
            Transform floor = reference.GetComponentsInChildren<Transform>(true).First(t => t.name == "Floor & Poles");
            Transform slab = floor.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Floor Slab Collider") ?? floor;
            FloorLayer = floor.gameObject.layer;
            FloorTag = slab.gameObject.tag;
            Flags = GameObjectUtility.GetStaticEditorFlags(floor.gameObject);

            Props["Barrel"] = Load<GameObject>("Assets/003-Prefabs/Furniture/Barrel.prefab");
            Props["Bench"] = Load<GameObject>("Assets/003-Prefabs/Furniture/Bench.prefab");
            Props["ItemBox"] = Load<GameObject>("Assets/003-Prefabs/ItemBox (HealthPotion).prefab");
            Props["EnemySpawner"] = Load<GameObject>("Assets/003-Prefabs/Enemy/EnemySpawner.prefab");
        }

        private static T Load<T>(string path) where T : Object
        {
            T a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) { throw new System.Exception("missing " + path); }
            return a;
        }
    }

    private class Room
    {
        private readonly Parts parts;
        private readonly Scene scene;
        private readonly StringBuilder log;
        private readonly GameObject root;
        private readonly Transform env, decor, enemies;
        private readonly HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        private int walls, exits, props;

        public Room(string name, Parts parts, Scene scene, StringBuilder log)
        {
            this.parts = parts; this.scene = scene; this.log = log;
            for (int x = 0; x < 3; x++) for (int z = 0; z < 3; z++) { cells.Add(new Vector2Int(x, z)); }

            root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            env = Child(root.transform, "Environment");
            decor = Child(root.transform, "Decor");
            enemies = Child(env, "Enemies");

            foreach (Vector2Int c in cells)
            {
                Vector3 centre = new Vector3(c.x * Cell, 0f, c.y * Cell);
                Cube($"Floor ({c.x},{c.y})", centre, new Vector3(Cell, Slab, Cell), parts.FloorMat, true);
                Cube($"Ceiling ({c.x},{c.y})", centre + Vector3.up * CeilingY, new Vector3(Cell, Slab, Cell), parts.FloorMat, true);
            }
        }

        /// <summary>Two rows of walls on every outer edge; exits = (cell, side, storey 0 / 1).</summary>
        public void Walls((Vector2Int cell, Dir dir, int level)[] exits)
        {
            foreach (var (cell, dir, level) in exits)
            {
                if (!cells.Contains(cell) || cells.Contains(cell + Step[dir])) { throw new System.Exception($"exit {cell} {dir} is not on the outer edge"); }
            }

            foreach (Vector2Int c in cells)
            {
                foreach (Dir d in Step.Keys)
                {
                    if (cells.Contains(c + Step[d])) { continue; }
                    for (int level = 0; level < 2; level++)
                    {
                        bool isExit = exits.Any(e => e.cell == c && e.dir == d && e.level == level);
                        Wall(c, d, level, isExit);
                    }
                }
            }
        }

        private void Wall(Vector2Int c, Dir d, int level, bool isExit)
        {
            GameObject wall = (GameObject)PrefabUtility.InstantiatePrefab(parts.Wall, env);
            wall.name = $"Wall ({c.x},{c.y}) {d} {(level == 0 ? "Ground" : "Upper")}" + (isExit ? " Exit" : "");
            wall.transform.localPosition = new Vector3(c.x * Cell, level * Storey, c.y * Cell);
            wall.transform.localRotation = Quaternion.Euler(0f, WallYaw[d], 0f) * parts.Wall.transform.localRotation;

            foreach (Renderer r in wall.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && parts.WallSwap.TryGetValue(mats[i], out Material to)) { mats[i] = to; changed = true; }
                }
                if (changed) { r.sharedMaterials = mats; }
            }
            if (!isExit)
            {
                foreach (Doorway doorway in wall.GetComponentsInChildren<Doorway>(true)) { Object.DestroyImmediate(doorway); }
            }
            walls++;
            if (isExit) { exits++; }
        }

        /// <summary>A walkable floor at the upper storey over x0..x1, z0..z1.</summary>
        public void Platform(string name, float x0, float x1, float z0, float z1)
        {
            Cube(name, new Vector3((x0 + x1) * 0.5f, Storey, (z0 + z1) * 0.5f), new Vector3(x1 - x0, Slab, z1 - z0), parts.FloorMat, true);
        }

        /// <summary>A 1 m railing on the upper storey between two points (x/z).</summary>
        public void Railing(string name, Vector3 a, Vector3 b)
        {
            Vector3 mid = (a + b) * 0.5f;
            bool alongX = Mathf.Abs(b.x - a.x) >= Mathf.Abs(b.z - a.z);
            float length = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
            Vector3 size = alongX ? new Vector3(length, 1f, 0.15f) : new Vector3(0.15f, 1f, length);
            Cube(name, new Vector3(mid.x, Storey + FloorTop + 0.5f, mid.z), size, parts.WallMat, false);
        }

        /// <summary>A support pillar from the ground floor up to the underside of the upper storey.</summary>
        public void Pillar(Vector3 p)
        {
            float bottom = FloorTop, top = Storey - Slab * 0.5f;
            Cube("Pillar", new Vector3(p.x, (bottom + top) * 0.5f, p.z), new Vector3(0.5f, top - bottom, 0.5f), parts.WallMat, false);
        }

        /// <summary>Solid steps rising north from zStart (ground) to zEnd (upper storey), plus a smooth ramp collider.</summary>
        public void Stairs(string name, float xMin, float xMax, float zStart, float zEnd)
        {
            Transform stairs = Child(env, name);
            float width = xMax - xMin, run = zEnd - zStart, depth = run / Steps, rise = Storey / Steps;
            float x = (xMin + xMax) * 0.5f;
            for (int i = 0; i < Steps; i++)
            {
                float h = rise * (i + 1);
                GameObject step = Cube($"Step {i + 1}", new Vector3(x, FloorTop + h * 0.5f, zStart + depth * (i + 0.5f)), new Vector3(width, h, depth), parts.FloorMat, true);
                step.transform.SetParent(stairs, true);
            }

            // Invisible ramp along the step noses so walking up is smooth
            var ramp = new GameObject("Ramp Collider");
            ramp.transform.SetParent(stairs, false);
            float angle = Mathf.Atan2(Storey, run) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(run * run + Storey * Storey);
            ramp.transform.localPosition = new Vector3(x, FloorTop + Storey * 0.5f, zStart + run * 0.5f);
            ramp.transform.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            BoxCollider box = ramp.AddComponent<BoxCollider>();
            box.size = new Vector3(width, 0.05f, length);
            ramp.layer = parts.FloorLayer;
            try { ramp.tag = parts.FloorTag; } catch { }
        }

        public void Spawner(Vector3 p, float level = 0f)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(parts.Props["EnemySpawner"], enemies);
            go.transform.localPosition = new Vector3(p.x, level + FloorTop, p.z);
        }

        public void Prop(string prop, Vector3 p, float yaw, float level = 0f)
        {
            GameObject source = parts.Props[prop];
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(source, decor);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * source.transform.localRotation;
            go.transform.localPosition = new Vector3(p.x, 0f, p.z);
            Renderer[] rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
            float bottom = rs.Length > 0 ? rs.Min(r => r.bounds.min.y) : 0f;
            go.transform.localPosition = new Vector3(p.x, level + FloorTop - bottom, p.z);
            props++;
        }

        private GameObject Cube(string name, Vector3 pos, Vector3 size, Material mat, bool floor)
        {
            ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            GameObject go = pb.gameObject;
            SceneManager.MoveGameObjectToScene(go, scene);
            go.name = name;
            go.transform.SetParent(env, false);
            go.transform.localPosition = pos;
            pb.GetComponent<MeshRenderer>().sharedMaterial = mat;
            pb.ToMesh();
            pb.Refresh();
            go.AddComponent<BoxCollider>();
            go.layer = parts.FloorLayer;
            if (floor) { try { go.tag = parts.FloorTag; } catch { } }
            GameObjectUtility.SetStaticEditorFlags(go, parts.Flags);
            return go;
        }

        public GameObject Save(string path)
        {
            Tile tile = root.AddComponent<Tile>();
            tile.OverrideAutomaticTileBounds = true;
            float height = CeilingY + Slab * 0.5f + FloorTop;
            tile.TileBoundsOverride = new Bounds(new Vector3(4f, height * 0.5f - FloorTop, 4f), new Vector3(12f, height, 12f));
            root.AddComponent<TileRenderOptimization>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"created {path}: {walls} wall section(s), {exits} exit(s), {props} prop(s), height {height:0.00} m");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }

    private static Vector2Int V(int x, int z) => new Vector2Int(x, z);
    private static Vector3 P(float x, float z) => new Vector3(x, 0f, z);
}
