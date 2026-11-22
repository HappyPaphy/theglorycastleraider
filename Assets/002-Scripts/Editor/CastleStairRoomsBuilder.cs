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
/// Castle stair rooms, 12 x 12 m, three rows of Small Wall (1) (about 9 m tall). Two-flight staircase:
///   flight 1 up the west wall (0 -> 3.2 m) to a landing in the north-west corner, then
///   flight 2 east across the room (3.2 -> 6.4 m) onto an L-shaped upper level along the north and east walls.
///  - Stair Hall: exits on the ground floor (S, E, N).
///  - Stair Tower: enter on the ground floor (S); the upper level's exits (N, E) carry the dungeon on 6.4 m higher.
/// Steps are solid against the wall (flight 1, landing) and open treads across the room (flight 2), each with
/// an invisible ramp collider so walking up is smooth. The navmesh is baked from render meshes (0.25 m steps).
/// Rebuilding keeps each room's existing "Castle Decor" (your carpets / curtains); only pieces the new stairs
/// would bury are dropped. Tools > Biomes > Build Castle Stair Rooms.
/// </summary>
public static class CastleStairRoomsBuilder
{
    private const string AutoKey = "CastleStairRoomsBuilder_v4"; // v4: Stair Tower = 8 x 8 m square spiral (Stair Hall untouched)
    private const string OutFolder = "Assets/012-MyDungenAsset/Room/Castle/Shapes";
    private const string TileSetPath = "Assets/012-MyDungenAsset/Tile Set/Castle/Tile Set (Castle).asset";
    private const string ReferenceRoom = "Assets/012-MyDungenAsset/Room/Castle/Small Room (1) (Castle).prefab";
    private const string WallPath = "Assets/012-MyDungenAsset/Wall/Small Wall (1).prefab";
    private const string DecorName = "Castle Decor";

    private const float Cell = 4f;
    private const float Storey = 2.48f;                     // one Small Wall row
    private static readonly float[] Levels = { 0f, 3.2f, 6.4f }; // wall rows; 3.2 = landing, 6.4 = upper level
    private const float Landing = 3.2f;
    private const float Upper = 6.4f;
    private const float Slab = 0.18f;
    private const float FloorTop = 0.09f;
    private const float CeilingY = Upper + Storey + 0.18f;
    private const int StepsPerFlight = 13;                  // 3.2 m / 13 = about 0.25 m per step

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
            Build("Stair Tower (Castle)");
        };
    }

    [MenuItem("Tools/Biomes/Build Castle Stair Rooms")]
    public static void BuildAll() => Build();

    /// <summary>Builds the named rooms (all of them if none are named).</summary>
    public static void Build(params string[] only)
    {
        var log = new StringBuilder();
        int errors = 0;
        var paths = new List<string>();
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            if (!AssetDatabase.IsValidFolder(OutFolder)) { AssetDatabase.CreateFolder("Assets/012-MyDungenAsset/Room/Castle", "Shapes"); }
            var parts = new Parts();
            var built = new List<GameObject>();

            foreach (var (name, build, sizeX, sizeZ) in new (string, System.Action<Room>, int, int)[] { ("Stair Hall (Castle)", BuildStairHall, 3, 3), ("Stair Tower (Castle)", BuildStairTower, 2, 2) })
            {
                if (only != null && only.Length > 0 && !only.Contains(name)) { continue; }
                string path = $"{OutFolder}/{name}.prefab";
                paths.Add(path);
                try
                {
                    GameObject oldDecor = TakeDecor(path, scene);
                    var room = new Room(name, parts, scene, log, sizeX, sizeZ);
                    build(room);
                    if (oldDecor != null) { room.KeepDecor(oldDecor); }
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

    // A copy of the room's current decor (carpets / curtains), so a rebuild keeps them
    private static GameObject TakeDecor(string path, Scene scene)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { return null; }
        GameObject old = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform decor = old.transform.Find(DecorName);
            if (decor == null) { return null; }
            GameObject copy = Object.Instantiate(decor.gameObject);
            copy.name = DecorName;
            SceneManager.MoveGameObjectToScene(copy, scene);
            copy.transform.SetPositionAndRotation(decor.position, decor.rotation);
            return copy;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(old);
        }
    }

    // ---------- Room designs (cells 3 x 3, cell (0,0) centred on the origin: x and z run -2..10) ----------

    // Shared layout: two flights, landing, L-shaped upper level (north + east)
    private static void StairsAndUpperLevel(Room r)
    {
        r.Platform("Upper North", -2f, 10f, 7f, 10f, Upper);
        r.Platform("Upper East", 7f, 10f, -2f, 7f, Upper);
        r.Railing("Railing North", new Vector3(-2f, 0f, 7.08f), new Vector3(6.3f, 0f, 7.08f), Upper);
        r.Railing("Railing East", new Vector3(7.08f, 0f, -2f), new Vector3(7.08f, 0f, 5f), Upper);
        foreach (Vector3 p in new[] { P(7.3f, -0.5f), P(7.3f, 3f), P(7.3f, 7.3f), P(3.5f, 7.3f) }) { r.Pillar(p, Upper); }

        // Flight 1: up the west wall, solid
        r.Flight("Flight 1", -1.9f, 0.1f, -1.5f, 5f, alongX: false, ascendPositive: true, fromY: 0f, toY: Landing, steps: StepsPerFlight, solid: true);
        // Landing in the north-west corner
        r.Block("Landing", -1.9f, 0.5f, 5f, 7f, Landing);
        r.Railing("Railing Landing", new Vector3(-1.9f, 0f, 7.0f), new Vector3(0.5f, 0f, 7.0f), Landing);
        // Flight 2: east across the room, open treads, onto the upper level's corner
        r.Flight("Flight 2", 0.5f, 7f, 5f, 7f, alongX: true, ascendPositive: true, fromY: Landing, toY: Upper, steps: StepsPerFlight, solid: false);
    }

    private static void BuildStairHall(Room r)
    {
        r.Walls(exitList: new[] { (V(1, 0), Dir.S, 0), (V(2, 0), Dir.E, 0), (V(1, 2), Dir.N, 0) });
        StairsAndUpperLevel(r);

        r.Spawner(P(3f, 2f));
        r.Spawner(P(5f, 3.5f));
        r.Spawner(P(-0.7f, 6f), Landing);
        r.Spawner(P(4f, 8.6f), Upper);
        r.Spawner(P(8.6f, 2.5f), Upper);

        r.Prop("Barrel", P(9.35f, 9.35f), 0f, Upper);
        r.Prop("ItemBox", P(9.35f, -1.35f), 0f, Upper);
        r.Prop("ItemBox", P(5.5f, -1.35f), 0f);
    }

    // 8 x 8 m tower (x and z run -2..6). Square spiral around the walls: four flights of 1.6 m with a landing
    // in each corner, an open well in the middle, and an L-shaped top walkway (west + north) at 6.4 m.
    private static void BuildStairTower(Room r)
    {
        r.Walls(exitList: new[] { (V(1, 0), Dir.S, 0), (V(0, 0), Dir.W, 2), (V(1, 1), Dir.N, 2) });

        const float w0 = -1.9f, w1 = -0.5f;   // west strip   (x)
        const float e0 = 4.5f, e1 = 5.9f;     // east strip   (x)
        const float s0 = -1.9f, s1 = -0.5f;   // south strip  (z)
        const float n0 = 4.5f, n1 = 5.9f;     // north strip  (z)
        const float quarter = Upper / 4f;     // 1.6 m per flight
        const int steps = 7;                  // about 0.23 m per step

        r.Flight("Flight 1 (west)", w0, w1, s1, n0, alongX: false, ascendPositive: true, fromY: 0f, toY: quarter, steps: steps, solid: true);
        r.Block("Landing NW", w0, w1, n0, n1, quarter);
        r.Flight("Flight 2 (north)", w1, e0, n0, n1, alongX: true, ascendPositive: true, fromY: quarter, toY: quarter * 2f, steps: steps, solid: true);
        r.Block("Landing NE", e0, e1, n0, n1, quarter * 2f);
        r.Flight("Flight 3 (east)", e0, e1, s1, n0, alongX: false, ascendPositive: false, fromY: quarter * 2f, toY: quarter * 3f, steps: steps, solid: true);
        r.Block("Landing SE", e0, e1, s0, s1, quarter * 3f);
        // Over the entrance: open treads so the doorway below stays clear
        r.Flight("Flight 4 (south)", w1, e0, s0, s1, alongX: true, ascendPositive: false, fromY: quarter * 3f, toY: Upper, steps: steps, solid: false);

        // Top walkway along the west and north walls (above flights 1 and 2), railings over the well
        r.Platform("Top West", -2f, w1, -2f, 6f, Upper);
        r.Platform("Top North", w1, 6f, n0, 6f, Upper);
        r.Railing("Railing West", new Vector3(w1 + 0.08f, 0f, s1), new Vector3(w1 + 0.08f, 0f, n0), Upper);
        r.Railing("Railing North", new Vector3(w1, 0f, n0 - 0.08f), new Vector3(6f, 0f, n0 - 0.08f), Upper);

        r.Spawner(P(2f, 2f));
        r.Spawner(P(3f, 0.8f));
        r.Spawner(P(5.2f, 5.2f), quarter * 2f);  // NE landing
        r.Spawner(P(-1.2f, 2.5f), Upper);
        r.Spawner(P(2.5f, 5.3f), Upper);

        r.Prop("Barrel", P(3.3f, 3.3f), 0f);
        r.Prop("ItemBox", P(-1.35f, 5.35f), 0f, Upper); // top corner
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

        public Parts()
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
        private readonly List<Rect> stairFootprints = new List<Rect>();   // where kept decor would be buried
        private int walls, exits, props, droppedDecor;

        private readonly int sizeX, sizeZ;

        public Room(string name, Parts parts, Scene scene, StringBuilder log, int sizeX, int sizeZ)
        {
            this.parts = parts; this.scene = scene; this.log = log; this.sizeX = sizeX; this.sizeZ = sizeZ;
            for (int x = 0; x < sizeX; x++) for (int z = 0; z < sizeZ; z++) { cells.Add(new Vector2Int(x, z)); }

            root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            env = Child(root.transform, "Environment");
            decor = Child(root.transform, "Props");
            enemies = Child(env, "Enemies");

            foreach (Vector2Int c in cells)
            {
                Vector3 centre = new Vector3(c.x * Cell, 0f, c.y * Cell);
                Cube($"Floor ({c.x},{c.y})", centre, new Vector3(Cell, Slab, Cell), parts.FloorMat, true);
                Cube($"Ceiling ({c.x},{c.y})", centre + Vector3.up * CeilingY, new Vector3(Cell, Slab, Cell), parts.FloorMat, true);
            }
        }

        /// <summary>Three rows of walls (plus stone bands between them) on every outer edge; exits = (cell, side, row 0/1/2).</summary>
        public void Walls((Vector2Int cell, Dir dir, int level)[] exitList)
        {
            foreach (var (cell, dir, _) in exitList)
            {
                if (!cells.Contains(cell) || cells.Contains(cell + Step[dir])) { throw new System.Exception($"exit {cell} {dir} is not on the outer edge"); }
            }

            foreach (Vector2Int c in cells)
            {
                foreach (Dir d in Step.Keys)
                {
                    if (cells.Contains(c + Step[d])) { continue; }
                    for (int level = 0; level < Levels.Length; level++)
                    {
                        Wall(c, d, level, exitList.Any(e => e.cell == c && e.dir == d && e.level == level));

                        if (level == 0) { continue; }
                        // Stone band closing the gap between the row below (top = its base + 2.57) and this row
                        float bandBottom = Levels[level - 1] + Storey + FloorTop, bandTop = Levels[level] + FloorTop;
                        Vector3 outward = new Vector3(Step[d].x, 0f, Step[d].y);
                        Vector3 along = new Vector3(Mathf.Abs(Step[d].y), 0f, Mathf.Abs(Step[d].x));
                        Vector3 pos = new Vector3(c.x * Cell, (bandBottom + bandTop) * 0.5f, c.y * Cell) + outward * 1.87f;
                        Vector3 size = along * Cell + new Vector3(Mathf.Abs(outward.x), 0f, Mathf.Abs(outward.z)) * 0.25f + Vector3.up * (bandTop - bandBottom);
                        Cube("Wall Band", pos, size, parts.WallMat, false);
                    }
                }
            }
        }

        private void Wall(Vector2Int c, Dir d, int level, bool isExit)
        {
            GameObject wall = (GameObject)PrefabUtility.InstantiatePrefab(parts.Wall, env);
            wall.name = $"Wall ({c.x},{c.y}) {d} Row {level}" + (isExit ? " Exit" : "");
            wall.transform.localPosition = new Vector3(c.x * Cell, Levels[level], c.y * Cell);
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

        public void Platform(string name, float x0, float x1, float z0, float z1, float level)
        {
            Cube(name, new Vector3((x0 + x1) * 0.5f, level, (z0 + z1) * 0.5f), new Vector3(x1 - x0, Slab, z1 - z0), parts.FloorMat, true);
        }

        /// <summary>A solid block from the floor up to a walking surface at this level (e.g. the landing).</summary>
        public void Block(string name, float x0, float x1, float z0, float z1, float level)
        {
            float h = level + FloorTop - FloorTop;
            Cube(name, new Vector3((x0 + x1) * 0.5f, FloorTop + h * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, h, z1 - z0), parts.FloorMat, true);
            stairFootprints.Add(Rect.MinMaxRect(x0, z0, x1, z1));
        }

        public void Railing(string name, Vector3 a, Vector3 b, float level)
        {
            Vector3 mid = (a + b) * 0.5f;
            bool alongX = Mathf.Abs(b.x - a.x) >= Mathf.Abs(b.z - a.z);
            float length = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
            Vector3 size = alongX ? new Vector3(length, 1f, 0.15f) : new Vector3(0.15f, 1f, length);
            Cube(name, new Vector3(mid.x, level + FloorTop + 0.5f, mid.z), size, parts.WallMat, false);
        }

        /// <summary>A pillar from the ground floor up to the underside of a level.</summary>
        public void Pillar(Vector3 p, float level)
        {
            float bottom = FloorTop, top = level - Slab * 0.5f;
            Cube("Pillar", new Vector3(p.x, (bottom + top) * 0.5f, p.z), new Vector3(0.5f, top - bottom, 0.5f), parts.WallMat, false);
        }


        /// <summary>
        /// One flight over the rectangle, climbing from fromY to toY along x or z. ascendPositive = it climbs toward
        /// +x / +z (otherwise toward -x / -z). Solid = blocks down to the floor (against a wall); otherwise 0.3 m
        /// thick open treads with two posts underneath.
        /// </summary>
        public void Flight(string name, float x0, float x1, float z0, float z1, bool alongX, bool ascendPositive, float fromY, float toY, int steps, bool solid)
        {
            Transform flight = Child(env, name);
            float riseTotal = toY - fromY, rise = riseTotal / steps;
            float run = alongX ? x1 - x0 : z1 - z0, width = alongX ? z1 - z0 : x1 - x0, depth = run / steps;
            float startEdge = ascendPositive ? (alongX ? x0 : z0) : (alongX ? x1 : z1);
            float dir = ascendPositive ? 1f : -1f;
            for (int i = 0; i < steps; i++)
            {
                float top = fromY + FloorTop + rise * (i + 1);
                float bottom = solid ? FloorTop : top - 0.3f;
                float along = startEdge + dir * depth * (i + 0.5f);
                Vector3 pos = alongX ? new Vector3(along, (top + bottom) * 0.5f, (z0 + z1) * 0.5f) : new Vector3((x0 + x1) * 0.5f, (top + bottom) * 0.5f, along);
                Vector3 size = alongX ? new Vector3(depth, top - bottom, width) : new Vector3(width, top - bottom, depth);
                Cube($"Step {i + 1}", pos, size, parts.FloorMat, true).transform.SetParent(flight, true);
            }

            // Posts under open treads, a third and two thirds of the way along
            if (!solid)
            {
                foreach (float f in new[] { 0.33f, 0.66f })
                {
                    float a = startEdge + dir * run * f;
                    float treadBottom = fromY + FloorTop + riseTotal * f - 0.3f;
                    Vector3 post = alongX ? new Vector3(a, (FloorTop + treadBottom) * 0.5f, (z0 + z1) * 0.5f) : new Vector3((x0 + x1) * 0.5f, (FloorTop + treadBottom) * 0.5f, a);
                    Cube("Stair Support", post, new Vector3(0.35f, treadBottom - FloorTop, 0.35f), parts.WallMat, false).transform.SetParent(flight, true);
                }
            }

            // Invisible ramp along the step noses so walking up is smooth
            var ramp = new GameObject("Ramp Collider");
            ramp.transform.SetParent(flight, false);
            float angle = Mathf.Atan2(riseTotal, run) * Mathf.Rad2Deg * dir;
            float length = Mathf.Sqrt(run * run + riseTotal * riseTotal);
            ramp.transform.localPosition = new Vector3((x0 + x1) * 0.5f, fromY + FloorTop + riseTotal * 0.5f, (z0 + z1) * 0.5f);
            ramp.transform.localRotation = alongX ? Quaternion.Euler(0f, 0f, angle) : Quaternion.Euler(-angle, 0f, 0f);
            BoxCollider box = ramp.AddComponent<BoxCollider>();
            box.size = alongX ? new Vector3(length, 0.05f, width) : new Vector3(width, 0.05f, length);
            ramp.layer = parts.FloorLayer;
            try { ramp.tag = parts.FloorTag; } catch { }

            if (solid) { stairFootprints.Add(Rect.MinMaxRect(x0, z0, x1, z1)); }
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

        /// <summary>Re-attach the previous decor; drop only what the new solid stairs / landing would bury.</summary>
        public void KeepDecor(GameObject oldDecor)
        {
            oldDecor.transform.SetParent(root.transform, true);
            Rect interior = Rect.MinMaxRect(-2f, -2f, sizeX * Cell - 2f, sizeZ * Cell - 2f);
            foreach (Renderer r in oldDecor.GetComponentsInChildren<Renderer>(true).ToArray())
            {
                if (r == null) { continue; }
                Bounds b = r.bounds;
                if (b.min.x < interior.xMin - 0.05f || b.max.x > interior.xMax + 0.05f || b.min.z < interior.yMin - 0.05f || b.max.z > interior.yMax + 0.05f || b.max.y > CeilingY)
                {
                    Object.DestroyImmediate(r.gameObject); // outside this room (e.g. on a wall that no longer exists)
                    droppedDecor++;
                    continue;
                }
                if (b.min.y > Landing + 0.2f) { continue; } // above the solid stairs
                Rect fp = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                if (stairFootprints.Any(s => s.Overlaps(fp)))
                {
                    Object.DestroyImmediate(r.gameObject);
                    droppedDecor++;
                }
            }
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
            tile.TileBoundsOverride = new Bounds(new Vector3((sizeX - 1) * Cell * 0.5f, height * 0.5f - FloorTop, (sizeZ - 1) * Cell * 0.5f), new Vector3(sizeX * Cell, height, sizeZ * Cell));
            root.AddComponent<TileRenderOptimization>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"built {path}: {sizeX * Cell} x {sizeZ * Cell} m, {walls} wall section(s), {exits} exit(s), {props} prop(s), height {height:0.00} m, kept decor ({droppedDecor} piece(s) dropped: under stairs or outside the room)");
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
