using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

/// <summary>
/// Replaces the hollow, non-convex MeshColliders on DunGen walls with solid BoxColliders:
///   wall with a doorway -> LEFT + RIGHT + TOP boxes around the opening (opening stays walkable)
///   wall without a doorway -> one box
/// Also adds a solid slab BoxCollider under every "Floor" so nothing can fall through the floor.
/// The old MeshColliders are disabled, not deleted, so this is easy to undo.
/// Processes wall prefabs (base before variants), then every room prefab.
/// </summary>
public static class WallColliderConverter
{
    private const string WallFolder = "Assets/012-MyDungenAsset/Wall";
    private const string RoomFolder = "Assets/012-MyDungenAsset/Room";
    private const string WallBoxesName = "Wall Box Colliders";
    private const string FloorSlabName = "Floor Slab Collider";
    private const string ReportPath = "Library/PrisonDecor/wall_colliders.txt";

    private const float FloorSlabThickness = 0.18f;
    private const float MinSegment = 0.02f; // skip slivers thinner than this

    private static string AutoKey => "WallColliderConverter.Done.v1." + Application.dataPath;

    [InitializeOnLoadMethod]
    private static void AutoRunOnce()
    {
        if (EditorPrefs.GetBool(AutoKey, false)) { return; }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey, true);
            ConvertAll();
        };
    }

    [MenuItem("Tools/Physics/Convert Wall Colliders To Boxes")]
    public static void ConvertAll()
    {
        StringBuilder log = new StringBuilder();

        // Walls: base prefabs before their variants, so variants only record overrides
        List<string> walls = AssetDatabase.FindAssets("t:Prefab", new[] { WallFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(VariantDepth)
            .ToList();

        foreach (string path in walls) { ProcessPrefab(path, log, false); }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RoomFolder }))
        {
            ProcessPrefab(AssetDatabase.GUIDToAssetPath(guid), log, true);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, log.ToString());
        AssetDatabase.SaveAssets();
        Debug.Log($"[WallColliderConverter] Converted wall colliders to boxes. Report: {Path.GetFullPath(ReportPath)}");
    }

    private static string VerifyKey => "WallColliderConverter.Verified.v1." + Application.dataPath;
    private const string VerifyReportPath = "Library/PrisonDecor/wall_colliders_verify.txt";

    [InitializeOnLoadMethod]
    private static void AutoVerifyOnce()
    {
        if (EditorPrefs.GetBool(VerifyKey, false)) { return; }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(VerifyKey, true);
            VerifyDoorways();
        };
    }

    /// <summary>
    /// For every doorway in every room: the walking path through it (several points across the opening, at
    /// knee/chest height, on both sides of the wall) must not be inside any enabled wall box or floor slab.
    /// </summary>
    [MenuItem("Tools/Physics/Verify Doorways Are Open")]
    public static void VerifyDoorways()
    {
        StringBuilder log = new StringBuilder();
        int doorways = 0, blocked = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RoomFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                List<BoxCollider> boxes = root.GetComponentsInChildren<BoxCollider>(true)
                    .Where(b => b.enabled && b.gameObject.activeInHierarchy &&
                                (b.transform.name == WallBoxesName || b.transform.name == FloorSlabName))
                    .ToList();

                foreach (DunGen.Doorway door in root.GetComponentsInChildren<DunGen.Doorway>(true))
                {
                    doorways++;
                    Transform d = door.transform;
                    Vector3 across = d.right;      // along the wall
                    Vector3 through = d.forward;   // through the opening

                    List<string> hits = new List<string>();
                    foreach (float side in new[] { -0.35f, 0f, 0.35f })
                        foreach (float height in new[] { 0.5f, 1.2f })
                            foreach (float depth in new[] { -0.3f, 0f, 0.3f })
                            {
                                Vector3 p = d.position + across * side + Vector3.up * height + through * depth;
                                foreach (BoxCollider b in boxes)
                                {
                                    if (Contains(b, p)) { hits.Add($"{HierPath(b.transform)} @ {V(p)}"); }
                                }
                            }

                    if (hits.Count > 0)
                    {
                        blocked++;
                        log.AppendLine($"!! BLOCKED {path} :: {HierPath(d)} pos={V(d.position)}");
                        foreach (string h in hits.Distinct().Take(4)) { log.AppendLine($"     {h}"); }
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        log.Insert(0, $"Checked {doorways} doorways, {blocked} blocked by wall boxes.\n");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(VerifyReportPath));
        File.WriteAllText(VerifyReportPath, log.ToString());
        Debug.Log($"[WallColliderConverter] Verify: {doorways} doorways checked, {blocked} blocked. Report: {System.IO.Path.GetFullPath(VerifyReportPath)}");
    }

    private static bool Contains(BoxCollider box, Vector3 worldPoint)
    {
        Vector3 local = box.transform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 half = box.size * 0.5f;
        return Mathf.Abs(local.x) < half.x && Mathf.Abs(local.y) < half.y && Mathf.Abs(local.z) < half.z;
    }

    [MenuItem("Tools/Physics/Restore Wall Mesh Colliders")]
    public static void RestoreAll()
    {
        List<string> paths = AssetDatabase.FindAssets("t:Prefab", new[] { RoomFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Concat(AssetDatabase.FindAssets("t:Prefab", new[] { WallFolder }).Select(AssetDatabase.GUIDToAssetPath).OrderByDescending(VariantDepth))
            .ToList();

        foreach (string path in paths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (t == null) { continue; }
                    if (t.name == WallBoxesName || t.name == FloorSlabName)
                    {
                        if (!PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) { Object.DestroyImmediate(t.gameObject); }
                        else { t.gameObject.SetActive(false); }
                        changed = true;
                    }
                }
                foreach (MeshCollider mc in root.GetComponentsInChildren<MeshCollider>(true))
                {
                    if (IsWall(mc.gameObject) && !mc.enabled) { mc.enabled = true; changed = true; }
                }
                if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("[WallColliderConverter] Restored wall MeshColliders.");
    }

    // =========================================================
    // PER PREFAB
    // =========================================================

    private static void ProcessPrefab(string path, StringBuilder log, bool isRoom)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool changed = false;
            log.AppendLine($"# {path}");

            foreach (MeshCollider mc in root.GetComponentsInChildren<MeshCollider>(true))
            {
                if (mc.convex || mc.isTrigger) { continue; }

                if (IsWall(mc.gameObject))
                {
                    changed |= ConvertWall(mc, log);
                }
                else if (isRoom && mc.gameObject.name.ToLowerInvariant().Contains("floor"))
                {
                    changed |= AddFloorSlab(root, mc, log);
                }
            }

            if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); }
        }
        catch (System.Exception e)
        {
            log.AppendLine($"!! {e.Message}");
            Debug.LogError($"[WallColliderConverter] {path}: {e}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool IsWall(GameObject go)
    {
        string n = go.name.ToLowerInvariant();
        return n.Contains("wall") && go.GetComponentInParent<DunGen.Doorway>() == null; // not the doorway's Blocker
    }

    // =========================================================
    // WALLS
    // =========================================================

    private static bool ConvertWall(MeshCollider mc, StringBuilder log)
    {
        GameObject wall = mc.gameObject;
        Mesh mesh = GetMesh(wall);
        if (mesh == null)
        {
            log.AppendLine($"  !! {wall.name}: no mesh");
            return false;
        }

        Bounds local = mesh.bounds;
        Transform wt = wall.transform;

        // Axes in the wall's local space: up = whichever local axis points up in the world,
        // thin = smallest of the remaining two, long = the other one
        int up = DominantAxis(wt.InverseTransformDirection(Vector3.up));
        int[] others = Enumerable.Range(0, 3).Where(a => a != up).ToArray();
        int thin = local.size[others[0]] <= local.size[others[1]] ? others[0] : others[1];
        int lng = thin == others[0] ? others[1] : others[0];

        // Door opening (in wall-local space) from the doorway Blocker, if the wall has a doorway
        DunGen.Doorway doorway = wall.GetComponentInChildren<DunGen.Doorway>(true);
        Bounds? opening = doorway != null ? GetOpening(doorway, wt) : null;

        // Collect boxes as (min, max) in local space
        List<(Vector3 min, Vector3 max, string label)> boxes = new List<(Vector3, Vector3, string)>();
        Vector3 wMin = local.min, wMax = local.max;

        if (opening.HasValue)
        {
            Bounds o = opening.Value;
            float oMinL = Mathf.Max(o.min[lng], wMin[lng]);
            float oMaxL = Mathf.Min(o.max[lng], wMax[lng]);
            float oTop = Mathf.Min(o.max[up], wMax[up]);

            // Left
            if (oMinL - wMin[lng] > MinSegment)
            {
                Vector3 mx = wMax; mx[lng] = oMinL;
                boxes.Add((wMin, mx, "Left"));
            }
            // Right
            if (wMax[lng] - oMaxL > MinSegment)
            {
                Vector3 mn = wMin; mn[lng] = oMaxL;
                boxes.Add((mn, wMax, "Right"));
            }
            // Top (lintel)
            if (wMax[up] - oTop > MinSegment)
            {
                Vector3 mn = wMin; mn[lng] = oMinL; mn[up] = oTop;
                Vector3 mx = wMax; mx[lng] = oMaxL;
                boxes.Add((mn, mx, "Top"));
            }
        }
        else
        {
            boxes.Add((wMin, wMax, "Solid"));
        }

        // Child holding the boxes (identity transform -> boxes are in wall-local space)
        Transform holder = wt.Find(WallBoxesName);
        if (holder == null)
        {
            holder = new GameObject(WallBoxesName).transform;
            holder.SetParent(wt, false);
        }
        holder.localPosition = Vector3.zero;
        holder.localRotation = Quaternion.identity;
        holder.localScale = Vector3.one;
        holder.gameObject.layer = wall.layer;
        holder.gameObject.tag = wall.tag;
        holder.gameObject.SetActive(true);

        // Reuse existing BoxColliders (keeps inherited ones as overrides in variants/rooms), add/disable as needed
        List<BoxCollider> existing = holder.GetComponents<BoxCollider>().ToList();
        for (int i = 0; i < Mathf.Max(existing.Count, boxes.Count); i++)
        {
            if (i >= boxes.Count)
            {
                if (existing[i].enabled) { existing[i].enabled = false; }
                continue;
            }

            BoxCollider box = i < existing.Count ? existing[i] : holder.gameObject.AddComponent<BoxCollider>();
            Vector3 center = (boxes[i].min + boxes[i].max) * 0.5f;
            Vector3 size = boxes[i].max - boxes[i].min;

            if (!Approximately(box.center, center)) { box.center = center; }
            if (!Approximately(box.size, size)) { box.size = size; }
            if (!box.enabled) { box.enabled = true; }
            if (box.isTrigger) { box.isTrigger = false; }
            box.sharedMaterial = mc.sharedMaterial;
        }

        if (mc.enabled) { mc.enabled = false; }

        log.AppendLine($"  wall {HierPath(wt)}: up={"xyz"[up]} long={"xyz"[lng]} thin={"xyz"[thin]} size={V(local.size)} " +
                       (opening.HasValue ? $"opening long[{opening.Value.min[lng]:0.##}..{opening.Value.max[lng]:0.##}] top={opening.Value.max[up]:0.##}" : "no opening") +
                       " -> " + string.Join(", ", boxes.Select(b => $"{b.label} {V(b.max - b.min)}")));
        return true;
    }

    // Door opening in wall-local space, taken from the doorway's Blocker (or the doorway socket if there's no blocker)
    private static Bounds? GetOpening(DunGen.Doorway doorway, Transform wall)
    {
        Renderer blockerRenderer = null;
        Collider blockerCollider = null;
        foreach (Transform t in doorway.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.ToLowerInvariant().Contains("blocker")) { continue; }
            blockerRenderer = t.GetComponent<Renderer>();
            blockerCollider = t.GetComponent<Collider>();
            break;
        }

        Bounds world;
        if (blockerRenderer != null) { world = blockerRenderer.bounds; }
        else if (blockerCollider != null) { world = blockerCollider.bounds; }
        else
        {
            Vector2 size = doorway.Socket != null ? doorway.Socket.Size : new Vector2(1f, 2f);
            Vector3 c = doorway.transform.position + Vector3.up * size.y * 0.5f;
            world = new Bounds(c, new Vector3(size.x, size.y, size.x));
        }

        // Transform all 8 corners into wall-local space
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? world.min.x : world.max.x,
                (i & 2) == 0 ? world.min.y : world.max.y,
                (i & 4) == 0 ? world.min.z : world.max.z);
            Vector3 l = wall.InverseTransformPoint(corner);
            min = Vector3.Min(min, l);
            max = Vector3.Max(max, l);
        }

        Bounds b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    // =========================================================
    // FLOORS
    // =========================================================

    // A solid slab at the bottom of the floor mesh (the mesh also contains poles, so its MeshCollider stays on)
    private static bool AddFloorSlab(GameObject root, MeshCollider mc, StringBuilder log)
    {
        Bounds b = mc.GetComponent<Renderer>() != null ? mc.GetComponent<Renderer>().bounds : mc.bounds;
        if (b.size.x < 0.5f || b.size.z < 0.5f) { return false; }

        Transform holder = mc.transform.Find(FloorSlabName);
        if (holder == null)
        {
            holder = new GameObject(FloorSlabName).transform;
            holder.SetParent(mc.transform, true);
        }

        // World-axis-aligned slab expressed in the floor's local space
        holder.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        holder.localScale = new Vector3(1f / Mathf.Max(0.0001f, mc.transform.lossyScale.x),
                                        1f / Mathf.Max(0.0001f, mc.transform.lossyScale.y),
                                        1f / Mathf.Max(0.0001f, mc.transform.lossyScale.z));
        holder.gameObject.layer = mc.gameObject.layer;
        holder.gameObject.tag = mc.gameObject.tag;
        holder.gameObject.SetActive(true);

        BoxCollider box = holder.GetComponent<BoxCollider>();
        if (box == null) { box = holder.gameObject.AddComponent<BoxCollider>(); }

        float top = Mathf.Min(b.max.y, b.min.y + FloorSlabThickness);
        Vector3 worldCenter = new Vector3(b.center.x, (b.min.y + top) * 0.5f, b.center.z);
        Vector3 worldSize = new Vector3(b.size.x, Mathf.Max(0.02f, top - b.min.y), b.size.z);

        box.center = holder.InverseTransformPoint(worldCenter);
        box.size = Vector3.Scale(worldSize, new Vector3(1f / holder.lossyScale.x, 1f / holder.lossyScale.y, 1f / holder.lossyScale.z));
        box.isTrigger = false;
        box.enabled = true;

        log.AppendLine($"  floor slab {HierPath(mc.transform)}: world min{V(b.min)} top={top:0.##}");
        return true;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static Mesh GetMesh(GameObject go)
    {
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh == null)
        {
            ProBuilderMesh pb = go.GetComponent<ProBuilderMesh>();
            if (pb != null) { pb.ToMesh(); pb.Refresh(); }
        }
        if (mf != null && mf.sharedMesh != null) { return mf.sharedMesh; }

        MeshCollider mc = go.GetComponent<MeshCollider>();
        return mc != null ? mc.sharedMesh : null;
    }

    private static int VariantDepth(string path)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        int depth = 0;
        GameObject current = asset;
        while (current != null && PrefabUtility.GetPrefabAssetType(current) == PrefabAssetType.Variant && depth < 10)
        {
            current = PrefabUtility.GetCorrespondingObjectFromSource(current);
            depth++;
        }
        return depth;
    }

    private static int DominantAxis(Vector3 v)
    {
        Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        return a.x >= a.y && a.x >= a.z ? 0 : (a.y >= a.z ? 1 : 2);
    }

    private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.000001f;

    private static string HierPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    private static string V(Vector3 v) => $"({v.x:0.##},{v.y:0.##},{v.z:0.##})";
}
