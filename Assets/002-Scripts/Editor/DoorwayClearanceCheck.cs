using System.IO;
using System.Text;
using DunGen;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Measures how wide each doorway opening really is (free space left/right of the doorway centre at a few
/// heights, just inside and just outside the room) and names what blocks it. Read-only, changes nothing.
/// Tools > Dungeon > Check Doorway Clearance. Report: Library/PrisonDecor/doorway_clearance.txt
/// </summary>
public static class DoorwayClearanceCheck
{
    private const string AutoKey = "DoorwayClearanceCheck_v5";

    private static readonly string[] Rooms =
    {
        "Assets/012-MyDungenAsset/Room/Prison/Slash Room (1).prefab",
        "Assets/012-MyDungenAsset/Room/Prison/Slash Room + 2 Side Rooms(1).prefab",
        "Assets/012-MyDungenAsset/Room/Courtyard/Slash Room (1) Variant.prefab",
        "Assets/012-MyDungenAsset/Room/Castle/Slash Room (1) (Castle).prefab",
        "Assets/012-MyDungenAsset/Room/Prison/I Room (1).prefab", // a room that works, for comparison
    };

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            Run();
        };
    }

    [MenuItem("Tools/Dungeon/Check Doorway Clearance")]
    public static void Run()
    {
        var log = new StringBuilder();
        foreach (string path in Rooms)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.AppendLine("!! missing " + path); continue; }
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
            try
            {
                Physics.SyncTransforms();
                PhysicsScene ps = scene.GetPhysicsScene();
                log.AppendLine("== " + path);
                Tile tile = root.GetComponent<Tile>();
                if (tile != null) { log.AppendLine($"   tile bounds {tile.Placement.Bounds.size}"); }

                // Passage side walls: where their colliders and visible meshes really are
                foreach (Transform child in root.transform)
                {
                    if (!child.name.StartsWith("Small Wall (No Door)")) { continue; }
                    log.AppendLine($" side wall '{child.name}' pos {child.position:F2} yaw {child.eulerAngles.y:F0}");
                    foreach (Collider col in child.GetComponentsInChildren<Collider>(true))
                    {
                        if (!col.enabled || col.isTrigger) { continue; }
                        log.AppendLine($"    collider {Path(col.transform)} ({col.GetType().Name}) min {col.bounds.min:F2} max {col.bounds.max:F2}");
                        if (col is BoxCollider box)
                        {
                            Transform bt = box.transform;
                            log.AppendLine($"      box centre {box.center:F2} size {box.size:F2}  obj pos {bt.position:F2} rot {bt.eulerAngles:F0} lossyScale {bt.lossyScale:F2}");
                            var sb = new StringBuilder("      floor corners:");
                            Vector3 e = box.size * 0.5f;
                            for (int sx = -1; sx <= 1; sx += 2)
                                for (int sy = -1; sy <= 1; sy += 2)
                                    for (int sz = -1; sz <= 1; sz += 2)
                                    {
                                        Vector3 w = bt.TransformPoint(box.center + Vector3.Scale(e, new Vector3(sx, sy, sz)));
                                        if (w.y < 1f) { sb.Append($" ({w.x:F2},{w.z:F2})"); }
                                    }
                            log.AppendLine(sb.ToString());
                        }
                    }
                    foreach (Renderer ren in child.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!ren.enabled) { continue; }
                        log.AppendLine($"    renderer {Path(ren.transform)} min {ren.bounds.min:F2} max {ren.bounds.max:F2}");
                        MeshFilter mf = ren.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
                        {
                            // Floor outline of the visible wall: distinct XZ points of its lowest vertices
                            var pts = new System.Collections.Generic.HashSet<string>();
                            foreach (Vector3 v in mf.sharedMesh.vertices)
                            {
                                Vector3 w = ren.transform.TransformPoint(v);
                                if (w.y < 0.5f) { pts.Add($"({w.x:F2},{w.z:F2})"); }
                            }
                            log.AppendLine($"      mesh floor points ({mf.sharedMesh.vertexCount} verts): {string.Join(" ", pts)}");
                        }
                    }
                }

                foreach (Doorway d in root.GetComponentsInChildren<Doorway>(true))
                {
                    Vector3 c = d.transform.position;
                    Vector3 f = d.transform.forward; // points out of the room
                    Vector3 r = d.transform.right;
                    string socket = d.Socket != null ? $"{d.Socket.name} {d.Socket.Size}" : "none";
                    log.AppendLine($" doorway '{d.name}' at {c:F2} facing {f:F2}  socket {socket}");

                    foreach (float depth in new[] { -0.6f, 0f, 0.6f }) // inside the room, in the frame, outside
                    {
                        foreach (float h in new[] { 0.3f, 0.9f, 1.5f })
                        {
                            Vector3 o = c + f * depth + Vector3.up * h;
                            float left = Cast(ps, o, -r, out string lHit);
                            float right = Cast(ps, o, r, out string rHit);
                            // something right on the centre line?
                            var inside = new Collider[4];
                            int n = ps.OverlapSphere(o, 0.05f, inside, ~0, QueryTriggerInteraction.Ignore);
                            string centre = "";
                            for (int i = 0; i < n; i++) { centre += "  CENTRE: " + Path(inside[i].transform) + " " + inside[i].GetType().Name; }
                            log.AppendLine($"   depth {depth,4:F1} h {h:F1}: width {left + right:F2} (L {left:F2} {lHit} | R {right:F2} {rHit}){centre}");
                        }
                    }

                    // Top-down map of the 3 m in front of the doorway at waist height:
                    // rows = distance inside the room, columns = sideways offset (-1.5 .. +1.5 m), '#' = solid
                    log.AppendLine("   map (rows: metres inside, cols: -1.5 .. +1.5 m sideways, '|' = doorway centre)");
                    var probe = new Collider[2];
                    for (float depthIn = 0.2f; depthIn <= 3.01f; depthIn += 0.2f)
                    {
                        var row = new StringBuilder();
                        int free = 0, best = 0;
                        for (float side = -1.5f; side <= 1.501f; side += 0.1f)
                        {
                            Vector3 o = c - f * depthIn + r * side + Vector3.up * 0.9f;
                            bool solid = ps.OverlapSphere(o, 0.04f, probe, ~0, QueryTriggerInteraction.Ignore) > 0;
                            row.Append(Mathf.Abs(side) < 0.05f ? (solid ? 'X' : '|') : (solid ? '#' : '.'));
                            free = solid ? 0 : free + 1;
                            if (free > best) { best = free; }
                        }
                        log.AppendLine($"   {depthIn:F1} {row}  widest gap {best * 0.1f:F1} m");
                    }

                    // Walk a player-sized capsule from 3 m inside the room out through the doorway
                    Vector3 start = c - f * 3f;
                    var hits = new RaycastHit[16];
                    Vector3 p1 = start + Vector3.up * 0.55f, p2 = start + Vector3.up * 1.3f;
                    int count = 0;
                    if (ps.CapsuleCast(p1, p2, 0.45f, f, out RaycastHit first, 4f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        hits[count++] = first;
                    }
                    log.AppendLine(count == 0
                        ? "   walk-through: CLEAR"
                        : $"   walk-through: first hit {Path(first.collider.transform)} ({first.collider.GetType().Name}) {first.distance:F2} m from 3 m inside, at {first.point:F2}, bounds {first.collider.bounds.size:F2} centre {first.collider.bounds.center:F2}");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/doorway_clearance.txt", log.ToString());
        Debug.Log("[DoorwayCheck] Done. Report: Library/PrisonDecor/doorway_clearance.txt");
    }

    private static float Cast(PhysicsScene ps, Vector3 origin, Vector3 dir, out string hitName)
    {
        if (ps.Raycast(origin, dir, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            hitName = Path(hit.collider.transform);
            return hit.distance;
        }
        hitName = "-";
        return 3f;
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        if (t.parent != null) { p = t.parent.name + "/" + p; }
        return p;
    }
}
