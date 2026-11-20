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
/// One-shot: builds castle-only room shapes on the same 4 m grid as every other room, from the same parts:
/// Small Wall (1) on each outer cell edge (exits only where listed; elsewhere the doorway is removed so the
/// ceramic blocker is a solid wall), ProBuilder floor + ceiling slabs, pillars at inner corners, torches
/// (they come with the walls), enemy spawners, item boxes and furniture.
/// Rooms: Great Hall, Cloister, T / U / Z Corridor, Library, Banquet Hall, Armory -> Room/Castle/Shapes/,
/// added to "Tile Set (Castle)". Existing rooms are never overwritten. Re-run from Tools > Biomes > Build Castle Room Shapes.
/// </summary>
public static class CastleRoomShapesBuilder
{
    private const string AutoKey = "CastleRoomShapesBuilder_v2";
    // v2 rebuilds (overwrites) the rooms: v1 placed every wall 90 degrees off and ignored the props' own rotation/height
    private const bool Overwrite = true;

    private const string OutFolder = "Assets/012-MyDungenAsset/Room/Castle/Shapes";
    private const string TileSetPath = "Assets/012-MyDungenAsset/Tile Set/Castle/Tile Set (Castle).asset";
    private const string ReferenceRoom = "Assets/012-MyDungenAsset/Room/Castle/Small Room (1) (Castle).prefab";
    private const string WallPath = "Assets/012-MyDungenAsset/Wall/Small Wall (1).prefab";

    private const float Cell = 4f;
    private const float WallHeight = 2.5f;
    private const float SlabThickness = 0.18f;
    private const float CeilingY = 2.63f;

    private enum Dir { E, N, W, S }
    private static readonly Dictionary<Dir, Vector2Int> Step = new Dictionary<Dir, Vector2Int>
    {
        { Dir.E, new Vector2Int(1, 0) }, { Dir.N, new Vector2Int(0, 1) }, { Dir.W, new Vector2Int(-1, 0) }, { Dir.S, new Vector2Int(0, -1) },
    };
    // Small Wall (1) faces its local +X: rotation that points it at each side
    private static readonly Dictionary<Dir, float> WallYaw = new Dictionary<Dir, float>
    {
        { Dir.E, 0f }, { Dir.S, 90f }, { Dir.W, 180f }, { Dir.N, 270f },
    };

    private class RoomSpec
    {
        public string Name;
        public List<Vector2Int> Cells = new List<Vector2Int>();
        public List<(Vector2Int cell, Dir dir)> Exits = new List<(Vector2Int, Dir)>();
        public List<Vector3> Pillars = new List<Vector3>();
        public List<Vector3> Spawners = new List<Vector3>();
        public System.Action<Builder> Decor;
    }

    // ---------- Room designs (positions in metres, cell (0,0) centred on the origin) ----------

    private static List<RoomSpec> Specs()
    {
        var list = new List<RoomSpec>();

        // Great Hall: 8 x 16 m, two rows of pillars, exits at both ends and one per side
        list.Add(new RoomSpec
        {
            Name = "Great Hall (Castle)",
            Cells = Rect(0, 0, 2, 4),
            Exits = { (V(0, 0), Dir.S), (V(1, 3), Dir.N), (V(0, 2), Dir.W), (V(1, 1), Dir.E) },
            Pillars = { P(0.3f, 2), P(3.7f, 2), P(0.3f, 6), P(3.7f, 6), P(0.3f, 10), P(3.7f, 10) },
            Spawners = { P(2, 3), P(2, 7.5f), P(2, 11.5f) },
            Decor = b =>
            {
                b.Place("Barrel", P(-1.35f, -1.35f));
                b.Place("Barrel", P(5.35f, 13.35f));
                b.AlongWall("Bench", P(-1.35f, 1.5f), alongZ: true);
                b.AlongWall("Bench", P(-1.35f, 11.5f), alongZ: true);
                b.AlongWall("Bench", P(5.35f, 8.5f), alongZ: true);
                b.AlongWall("Bench", P(5.35f, 12f), alongZ: true);
                b.Place("ItemBox", P(5.35f, -1.35f));
                b.Place("ItemBox", P(-1.35f, 13.35f));
            },
        });

        // Cloister: 12 x 12 m ring around a solid 4 x 4 m block
        var cloister = Rect(0, 0, 3, 3);
        cloister.Remove(V(1, 1));
        list.Add(new RoomSpec
        {
            Name = "Cloister (Castle)",
            Cells = cloister,
            Exits = { (V(1, 0), Dir.S), (V(1, 2), Dir.N), (V(0, 1), Dir.W), (V(2, 1), Dir.E) },
            Spawners = { P(0, 0), P(8, 8), P(8, 0) },
            Decor = b =>
            {
                b.Place("Barrel", P(-1.35f, 9.35f));
                b.Place("Barrel", P(9.35f, -1.35f));
                b.Place("ItemBox", P(-1.35f, -1.35f));
                b.Place("ItemBox", P(9.35f, 9.35f));
            },
        });

        // T Corridor: bar along the top, stem down the middle
        list.Add(new RoomSpec
        {
            Name = "T Corridor (Castle)",
            Cells = { V(0, 1), V(1, 1), V(2, 1), V(1, 0) },
            Exits = { (V(0, 1), Dir.W), (V(2, 1), Dir.E), (V(1, 0), Dir.S) },
            Spawners = { P(4, 4.5f), P(8, 3.5f) },
            Decor = b =>
            {
                b.Place("Barrel", P(-1.35f, 5.35f));
                b.Place("Barrel", P(9.35f, 5.35f));
                b.Place("ItemBox", P(2.65f, -1.35f));
            },
        });

        // U Corridor: horseshoe, both exits face the same way
        list.Add(new RoomSpec
        {
            Name = "U Corridor (Castle)",
            Cells = { V(0, 0), V(1, 0), V(2, 0), V(0, 1), V(2, 1) },
            Exits = { (V(0, 1), Dir.N), (V(2, 1), Dir.N) },
            Spawners = { P(0, 4), P(8, 4), P(4, -0.5f) },
            Decor = b =>
            {
                b.Place("Barrel", P(-1.35f, -1.35f));
                b.Place("Barrel", P(9.35f, -1.35f));
                b.AlongWall("Bench", P(4, -1.35f), alongZ: false);
                b.Place("ItemBox", P(4, 1.25f));
            },
        });

        // Z Corridor: zig-zag
        list.Add(new RoomSpec
        {
            Name = "Z Corridor (Castle)",
            Cells = { V(0, 0), V(1, 0), V(1, 1), V(2, 1) },
            Exits = { (V(0, 0), Dir.W), (V(2, 1), Dir.E) },
            Spawners = { P(4, 0), P(4, 4.5f) },
            Decor = b =>
            {
                b.Place("Barrel", P(5.35f, -1.35f));
                b.Place("Barrel", P(2.65f, 5.35f));
                b.Place("ItemBox", P(-1.35f, -1.35f));
            },
        });

        // Library: 8 x 12 m, shelf rows with alternating gaps make a zig-zag aisle
        list.Add(new RoomSpec
        {
            Name = "Library (Castle)",
            Cells = Rect(0, 0, 2, 3),
            Exits = { (V(0, 0), Dir.S), (V(1, 2), Dir.N) },
            Spawners = { P(1, 2.7f), P(3, 5.35f), P(1, 8.4f) },
            Decor = b =>
            {
                b.Row("Shelf", -1.7f, 3.4f, 1.3f);   // gap on the east side
                b.Row("Shelf", 0.6f, 5.7f, 4.0f);    // gap on the west side
                b.Row("Shelf", -1.7f, 3.4f, 6.7f);   // gap on the east side
                b.Place("ItemBox", P(-1.35f, 9.35f));
                b.Place("WoodenTable", P(0.5f, 8.6f));
                b.Place("WoodenChair", P(-0.4f, 8.6f), 90f);
            },
        });

        // Banquet Hall: 12 x 8 m, one long table down the middle with chairs on both sides
        list.Add(new RoomSpec
        {
            Name = "Banquet Hall (Castle)",
            Cells = Rect(0, 0, 3, 2),
            Exits = { (V(0, 0), Dir.W), (V(2, 1), Dir.E), (V(1, 1), Dir.N) },
            Spawners = { P(0, 4.6f), P(8, -0.6f), P(6.5f, 4.6f) },
            Decor = b =>
            {
                b.Banquet(-0.2f, 8.2f, 2f);
                b.Place("WoodenCabinet", P(-1.35f, 5.35f), 90f);
                b.Place("WoodenCabinet", P(9.35f, -1.35f), 270f);
                b.Place("ItemBox", P(-1.35f, -1.35f));
                b.Place("Barrel", P(9.35f, 5.35f));
            },
        });

        // Armory: L-shape lined with weapon racks
        list.Add(new RoomSpec
        {
            Name = "Armory (Castle)",
            Cells = { V(0, 0), V(1, 0), V(2, 0), V(0, 1), V(0, 2) },
            Exits = { (V(2, 0), Dir.E), (V(0, 2), Dir.N) },
            Spawners = { P(6, 0), P(0, 5) },
            Decor = b =>
            {
                b.WallRow("WeaponRack", 0.2f, 7.8f, -1.45f, alongZ: false);
                b.WallRow("WeaponRack", 1.2f, 7.4f, -1.45f, alongZ: true);
                b.Place("Barrel", P(4.2f, 1.35f));
                b.Place("Barrel", P(5.4f, 1.35f));
                b.Place("ItemBox", P(1.35f, 9.35f));
                b.Place("WoodenTable", P(1.2f, 5f), 90f);
            },
        });

        return list;
    }

    // ---------- Build ----------

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

    [MenuItem("Tools/Biomes/Build Castle Room Shapes")]
    public static void Build()
    {
        var log = new StringBuilder();
        int errors = 0;
        Scene scene = EditorSceneManager.NewPreviewScene();

        try
        {
            if (!AssetDatabase.IsValidFolder(OutFolder)) { AssetDatabase.CreateFolder("Assets/012-MyDungenAsset/Room/Castle", "Shapes"); }
            var parts = new Parts(log);
            var built = new List<GameObject>();

            foreach (RoomSpec spec in Specs())
            {
                string path = $"{OutFolder}/{spec.Name}.prefab";
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null && !Overwrite) { log.AppendLine("kept " + path); built.Add(existing); continue; }

                try
                {
                    var b = new Builder(spec, parts, scene, log);
                    GameObject prefab = b.Build(path);
                    built.Add(prefab);
                }
                catch (System.Exception e)
                {
                    errors++;
                    log.AppendLine($"!! {spec.Name}: {e}");
                }
            }

            AddToTileSet(built, log);
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
        File.WriteAllText("Library/PrisonDecor/castle_shapes.txt", log.ToString());
        Debug.Log($"[CastleRoomShapes] Done with {errors} error(s). Report: Library/PrisonDecor/castle_shapes.txt");
        RoomSnapshot.Run(); // pictures of the result in Library/PrisonDecor/Snapshots
    }

    private static void AddToTileSet(List<GameObject> rooms, StringBuilder log)
    {
        TileSet set = AssetDatabase.LoadAssetAtPath<TileSet>(TileSetPath);
        if (set == null) { log.AppendLine("!! missing " + TileSetPath); return; }
        foreach (GameObject room in rooms)
        {
            if (set.TileWeights.Weights.Any(w => w.Value == room)) { continue; }
            set.TileWeights.Weights.Add(new GameObjectChance(room, 1f, 1f, set));
            log.AppendLine("added to Tile Set (Castle): " + room.name);
        }
        EditorUtility.SetDirty(set);
    }

    // ---------- Shared parts (materials, prefabs, layers copied from an existing castle room) ----------

    private class Parts
    {
        public GameObject Wall;
        public Material WallMat, FloorMat;
        public Dictionary<Material, Material> WallSwap = new Dictionary<Material, Material>();
        public int FloorLayer, WallLayer;
        public string FloorTag = "Untagged";
        public StaticEditorFlags FloorFlags;
        public Dictionary<string, GameObject> Props = new Dictionary<string, GameObject>();

        public Parts(StringBuilder log)
        {
            Wall = Load<GameObject>(WallPath);
            WallMat = Load<Material>("Assets/006-Materials/Material_Wall_Ceramic.mat");
            FloorMat = Load<Material>("Assets/006-Materials/Material_Floor_Ceramic.mat");
            WallSwap[Load<Material>("Assets/006-Materials/Material_Wall_Prison.mat")] = WallMat;
            WallSwap[Load<Material>("Assets/006-Materials/Material_IronBar.mat")] = WallMat;

            // Floor layer / tag / static flags like the existing castle rooms (navmesh + footsteps rely on them)
            GameObject reference = Load<GameObject>(ReferenceRoom);
            Transform floor = reference.GetComponentsInChildren<Transform>(true).First(t => t.name == "Floor & Poles");
            Transform slab = floor.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Floor Slab Collider") ?? floor;
            FloorLayer = floor.gameObject.layer;
            FloorTag = slab.gameObject.tag;
            FloorFlags = GameObjectUtility.GetStaticEditorFlags(floor.gameObject);
            Transform wall = reference.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Small Wall"));
            WallLayer = wall.gameObject.layer;
            log.AppendLine($"floor layer {LayerMask.LayerToName(FloorLayer)} tag {FloorTag} flags {FloorFlags}; wall layer {LayerMask.LayerToName(WallLayer)}");

            Props["Barrel"] = Load<GameObject>("Assets/003-Prefabs/Furniture/Barrel.prefab");
            Props["Bench"] = Load<GameObject>("Assets/003-Prefabs/Furniture/Bench.prefab");
            Props["Shelf"] = Load<GameObject>("Assets/003-Prefabs/Furniture/Shelf.prefab");
            Props["WeaponRack"] = Load<GameObject>("Assets/003-Prefabs/Furniture/WeaponRack.prefab");
            Props["WoodenTable"] = Load<GameObject>("Assets/003-Prefabs/WoodenTable.prefab");
            Props["WoodenChair"] = Load<GameObject>("Assets/003-Prefabs/WoodenChair.prefab");
            Props["WoodenCabinet"] = Load<GameObject>("Assets/003-Prefabs/WoodenCabinet.prefab");
            Props["ItemBox"] = Load<GameObject>("Assets/003-Prefabs/ItemBox (HealthPotion).prefab");
            Props["EnemySpawner"] = Load<GameObject>("Assets/003-Prefabs/Enemy/EnemySpawner.prefab");
        }

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { throw new System.Exception("missing " + path); }
            return asset;
        }
    }

    private class Builder
    {
        private readonly RoomSpec spec;
        private readonly Parts parts;
        private readonly Scene scene;
        private readonly StringBuilder log;
        private readonly HashSet<Vector2Int> cells;
        private GameObject root;
        private Transform environment, decor, enemies;
        private int decorCount;

        public Builder(RoomSpec spec, Parts parts, Scene scene, StringBuilder log)
        {
            this.spec = spec; this.parts = parts; this.scene = scene; this.log = log;
            cells = new HashSet<Vector2Int>(spec.Cells);
        }

        public GameObject Build(string path)
        {
            root = new GameObject(spec.Name);
            SceneManager.MoveGameObjectToScene(root, scene);
            environment = Child(root.transform, "Environment");
            decor = Child(root.transform, "Decor");
            enemies = Child(environment, "Enemies");

            ValidateExits();

            // Floor + ceiling per cell
            foreach (Vector2Int c in cells)
            {
                Vector3 centre = new Vector3(c.x * Cell, 0f, c.y * Cell);
                Slab($"Floor ({c.x},{c.y})", centre, parts.FloorMat);
                Slab($"Ceiling ({c.x},{c.y})", centre + Vector3.up * CeilingY, parts.FloorMat);
            }

            // Walls on every outer edge; exits keep their doorway
            int walls = 0, exits = 0;
            foreach (Vector2Int c in cells)
            {
                foreach (Dir d in Step.Keys)
                {
                    if (cells.Contains(c + Step[d])) { continue; }
                    bool isExit = spec.Exits.Any(e => e.cell == c && e.dir == d);
                    PlaceWall(c, d, isExit);
                    walls++;
                    if (isExit) { exits++; }
                }
            }

            // Pillars: listed ones + every inner (concave) corner
            foreach (Vector3 p in spec.Pillars) { Pillar(p, 0.6f); }
            foreach (Vector3 corner in InnerCorners()) { Pillar(corner, 0.5f); }

            foreach (Vector3 p in spec.Spawners) { Prop("EnemySpawner", p, 0f, enemies); }
            spec.Decor?.Invoke(this);

            // Tile with bounds that match the grid (doorways sit on its faces)
            Tile tile = root.AddComponent<Tile>();
            int minX = cells.Min(c => c.x), maxX = cells.Max(c => c.x), minZ = cells.Min(c => c.y), maxZ = cells.Max(c => c.y);
            Vector3 size = new Vector3((maxX - minX + 1) * Cell, 2.7f, (maxZ - minZ + 1) * Cell);
            Vector3 center = new Vector3((minX + maxX) * Cell * 0.5f, 1.3f, (minZ + maxZ) * Cell * 0.5f);
            tile.OverrideAutomaticTileBounds = true;
            tile.TileBoundsOverride = new Bounds(center, size);
            root.AddComponent<TileRenderOptimization>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"created {path}: {cells.Count} cells, {walls} wall(s), {exits} exit(s), {decorCount} prop(s), bounds {size}");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Exits must lie on the outside of the room's bounding box, or DunGen can't attach rooms to them
        private void ValidateExits()
        {
            int minX = cells.Min(c => c.x), maxX = cells.Max(c => c.x), minZ = cells.Min(c => c.y), maxZ = cells.Max(c => c.y);
            foreach (var (cell, dir) in spec.Exits)
            {
                bool ok = cells.Contains(cell) && !cells.Contains(cell + Step[dir]) &&
                          ((dir == Dir.E && cell.x == maxX) || (dir == Dir.W && cell.x == minX) ||
                           (dir == Dir.N && cell.y == maxZ) || (dir == Dir.S && cell.y == minZ));
                if (!ok) { throw new System.Exception($"exit {cell} {dir} is not on the room's outer edge"); }
            }
        }

        private void PlaceWall(Vector2Int c, Dir d, bool isExit)
        {
            GameObject wall = (GameObject)PrefabUtility.InstantiatePrefab(parts.Wall, environment);
            wall.name = $"Wall ({c.x},{c.y}) {d}" + (isExit ? " Exit" : "");
            wall.transform.localPosition = new Vector3(c.x * Cell, 0f, c.y * Cell);
            // Small Wall (1)'s root is saved turned 90 degrees: add our turn on top of it
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
                // No exit here: drop the doorway so its blocker stays as a solid wall
                foreach (Doorway doorway in wall.GetComponentsInChildren<Doorway>(true)) { Object.DestroyImmediate(doorway); }
            }
        }

        private IEnumerable<Vector3> InnerCorners()
        {
            var corners = new HashSet<Vector2Int>();
            foreach (Vector2Int c in cells)
            {
                for (int dx = 0; dx <= 1; dx++)
                for (int dz = 0; dz <= 1; dz++) { corners.Add(new Vector2Int(c.x + dx, c.y + dz)); }
            }
            foreach (Vector2Int v in corners)
            {
                // Grid vertex v sits between cells (v.x-1..v.x, v.y-1..v.y)
                int around = 0;
                for (int dx = -1; dx <= 0; dx++)
                for (int dz = -1; dz <= 0; dz++) { if (cells.Contains(new Vector2Int(v.x + dx, v.y + dz))) { around++; } }
                if (around == 3) { yield return new Vector3(v.x * Cell - Cell * 0.5f, 0f, v.y * Cell - Cell * 0.5f); }
            }
        }

        private void Slab(string name, Vector3 centre, Material mat)
        {
            ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(Cell, SlabThickness, Cell));
            Finish(pb, name, centre, mat, parts.FloorLayer, parts.FloorTag);
        }

        private void Pillar(Vector3 pos, float width)
        {
            ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(width, WallHeight, width));
            Finish(pb, "Pillar", pos + Vector3.up * (WallHeight * 0.5f + 0.09f), parts.WallMat, parts.WallLayer, "Untagged");
        }

        private void Finish(ProBuilderMesh pb, string name, Vector3 pos, Material mat, int layer, string tag)
        {
            GameObject go = pb.gameObject;
            SceneManager.MoveGameObjectToScene(go, scene);
            go.name = name;
            go.transform.SetParent(environment, false);
            go.transform.localPosition = pos;
            pb.GetComponent<MeshRenderer>().sharedMaterial = mat;
            pb.ToMesh();
            pb.Refresh();
            go.AddComponent<BoxCollider>();
            go.layer = layer;
            try { go.tag = tag; } catch { /* tag missing: keep Untagged */ }
            GameObjectUtility.SetStaticEditorFlags(go, parts.FloorFlags);
        }

        // ---------- Decor helpers (used by the room designs) ----------

        public GameObject Place(string prop, Vector3 pos, float yaw = 0f) => Prop(prop, pos, yaw, decor);

        /// <summary>Puts a prop against a wall with its long side along the wall.</summary>
        public void AlongWall(string prop, Vector3 pos, bool alongZ)
        {
            Vector3 size = Size(prop);
            bool longAlongX = size.x >= size.z;
            float yaw = (alongZ == longAlongX) ? 90f : 0f;
            Prop(prop, pos, yaw, decor);
        }

        /// <summary>A row of props along X at depth z, from x0 to x1, long side along the row.</summary>
        public void Row(string prop, float x0, float x1, float z) => Line(prop, x0, x1, z, alongZ: false, gap: 0.05f);

        /// <summary>A row of props against a wall: along X at z = fixed, or along Z at x = fixed.</summary>
        public void WallRow(string prop, float from, float to, float fixedCoord, bool alongZ) => Line(prop, from, to, fixedCoord, alongZ, 0.4f);

        private void Line(string prop, float from, float to, float fixedCoord, bool alongZ, float gap)
        {
            Vector3 size = Size(prop);
            bool longAlongX = size.x >= size.z;
            float yaw = (alongZ == longAlongX) ? 90f : 0f;
            float length = Mathf.Max(size.x, size.z);
            for (float t = from + length * 0.5f; t + length * 0.5f <= to + 0.01f; t += length + gap)
            {
                Vector3 pos = alongZ ? new Vector3(fixedCoord, 0f, t) : new Vector3(t, 0f, fixedCoord);
                Prop(prop, pos, yaw, decor);
            }
        }

        /// <summary>Long table along X at depth z with chairs on both sides.</summary>
        public void Banquet(float x0, float x1, float z)
        {
            Vector3 table = Size("WoodenTable");
            bool longAlongX = table.x >= table.z;
            float yaw = longAlongX ? 0f : 90f;
            float length = Mathf.Max(table.x, table.z), depth = Mathf.Min(table.x, table.z);
            for (float t = x0 + length * 0.5f; t + length * 0.5f <= x1 + 0.01f; t += length)
            {
                Prop("WoodenTable", new Vector3(t, 0f, z), yaw, decor);
                Prop("WoodenChair", new Vector3(t, 0f, z + depth * 0.5f + 0.45f), 180f, decor);
                Prop("WoodenChair", new Vector3(t, 0f, z - depth * 0.5f - 0.45f), 0f, decor);
            }
        }

        private GameObject Prop(string prop, Vector3 pos, float yaw, Transform parent)
        {
            GameObject source = parts.Props[prop];
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            // Keep the prop's own saved rotation and stand its lowest point on the floor top (y 0.09)
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * source.transform.localRotation;
            go.transform.localPosition = new Vector3(pos.x, 0f, pos.z);
            float bottom = Bottom(go);
            go.transform.localPosition = new Vector3(pos.x, 0.09f + pos.y + (go.transform.localPosition.y - bottom), pos.z);
            if (parent == decor) { decorCount++; }
            return go;
        }

        private readonly Dictionary<string, Vector3> sizes = new Dictionary<string, Vector3>();
        private Vector3 Size(string prop)
        {
            if (sizes.TryGetValue(prop, out Vector3 s)) { return s; }
            GameObject tmp = (GameObject)PrefabUtility.InstantiatePrefab(parts.Props[prop], scene);
            tmp.transform.position = Vector3.zero;
            tmp.transform.rotation = parts.Props[prop].transform.localRotation;
            Renderer[] rs = tmp.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            foreach (Renderer r in rs) { b.Encapsulate(r.bounds); }
            Object.DestroyImmediate(tmp);
            sizes[prop] = b.size;
            log.AppendLine($"  {prop} size {b.size}");
            return b.size;
        }

        // Lowest renderer point of a placed prop, in the room's local space (room root sits at the origin)
        private static float Bottom(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (rs.Length == 0) { return go.transform.localPosition.y; }
            return rs.Min(r => r.bounds.min.y);
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }

    // ---------- Small helpers ----------

    private static Vector2Int V(int x, int z) => new Vector2Int(x, z);
    private static Vector3 P(float x, float z) => new Vector3(x, 0f, z);

    private static List<Vector2Int> Rect(int x0, int z0, int w, int d)
    {
        var list = new List<Vector2Int>();
        for (int x = x0; x < x0 + w; x++)
        for (int z = z0; z < z0 + d; z++) { list.Add(new Vector2Int(x, z)); }
        return list;
    }
}
