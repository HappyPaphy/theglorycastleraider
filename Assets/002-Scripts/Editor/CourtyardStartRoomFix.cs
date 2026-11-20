using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot fix for the Maze Yard 2-1 start room (Small Room (Start) (1st Floor) Courtyard):
///  1. Restores the outer yard wall's 3 colliders to their 8 m layout (v1 of this fix reset them by mistake).
///  2. The inner wall behind the WoodenDoor had one solid full-width collider, so the opened door led into
///     an invisible wall. Cuts a door-sized gap into that collider (left piece, right piece, lintel).
/// Re-run from Tools > Biomes > Fix Maze Yard Start Room (safe: skips what is already fixed).
/// </summary>
public static class CourtyardStartRoomFix
{
    private const string AutoKey = "CourtyardStartRoomFix_v2";
    private const string RoomPath = "Assets/012-MyDungenAsset/Room/Courtyard/Small Room (Start) (1st Floor) Courtyard.prefab";

    // Door opening, same as the Prison walls' door gap
    private const float GapWidth = 1.03f;
    private const float GapTop = 1.815f; // world height of the lintel's underside above the floor

    // Outer yard wall colliders as they were (local center / size), by their file id in the wall prefab
    private static readonly Dictionary<long, (Vector3 center, Vector3 size)> OuterWall = new Dictionary<long, (Vector3, Vector3)>
    {
        { 178596347972143875, (new Vector3(2.2484524f, 1.2247607f, 1.8749676f), new Vector3(3.4922376f, 2.2695215f, 0.25007057f)) },
        { 546356043333249255, (new Vector3(-2.2625823f, 1.2247607f, 1.8749676f), new Vector3(3.4745932f, 2.2695215f, 0.25007057f)) },
        { 1096444808738312702, (new Vector3(-0.0114760995f, 2.087833f, 1.8749676f), new Vector3(1.0276195f, 0.5433767f, 0.25007057f)) },
    };

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            Fix();
        };
    }

    [MenuItem("Tools/Biomes/Fix Maze Yard Start Room")]
    public static void Fix()
    {
        var log = new StringBuilder();
        int errors = 0;
        GameObject root = PrefabUtility.LoadPrefabContents(RoomPath);
        try
        {
            RestoreOuterWall(root, log);
            OpenInnerWall(root, log);
            PrefabUtility.SaveAsPrefabAsset(root, RoomPath);
            log.AppendLine("saved " + RoomPath);
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/courtyard_start_fix.txt", log.ToString());
        Debug.Log($"[CourtyardStartRoomFix] Done with {errors} error(s). Report: Library/PrisonDecor/courtyard_start_fix.txt");
    }

    private static void RestoreOuterWall(GameObject root, StringBuilder log)
    {
        // The exit wall: the one holding the DunGen doorway
        DunGen.Doorway exit = root.GetComponentsInChildren<DunGen.Doorway>(true).FirstOrDefault();
        if (exit == null) { log.AppendLine("!! no exit doorway"); return; }

        int restored = 0;
        foreach (BoxCollider box in exit.transform.parent.GetComponentsInChildren<BoxCollider>(true))
        {
            BoxCollider source = PrefabUtility.GetCorrespondingObjectFromSource(box);
            if (source == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string _, out long fileId)) { continue; }
            if (!OuterWall.TryGetValue(fileId, out var values)) { continue; }
            box.center = values.center;
            box.size = values.size;
            restored++;
        }
        log.AppendLine($"outer yard wall: {restored}/3 collider(s) restored");
        Dump(exit.transform.parent, log);
    }

    private static void OpenInnerWall(GameObject root, StringBuilder log)
    {
        Transform door = root.transform.Find("WoodenDoor");
        if (door == null) { log.AppendLine("!! no WoodenDoor"); return; }
        Collider doorCollider = door.GetComponentInChildren<Collider>(true);
        Vector3 doorCenter = doorCollider != null ? doorCollider.bounds.center : door.position;
        log.AppendLine($"WoodenDoor at {doorCenter}");

        // Solid wall boxes the door sits in: same wall line (within 0.4 m), spanning the door, wider than the gap
        var candidates = root.GetComponentsInChildren<BoxCollider>(true)
            .Where(b => b.enabled && b.gameObject.activeInHierarchy && !b.isTrigger && b.name == "Wall Box Colliders")
            .Where(b => SpansDoor(b, doorCenter))
            .ToList();

        if (candidates.Count == 0) { log.AppendLine("inner wall already open (no solid box across the door)"); return; }

        foreach (BoxCollider solid in candidates)
        {
            log.AppendLine($"cutting door gap into '{solid.transform.parent.name}/{solid.name}' center={solid.bounds.center} size={solid.bounds.size}");
            CutGap(solid, doorCenter);
            Dump(solid.transform.parent, log);
        }
    }

    private static bool SpansDoor(BoxCollider b, Vector3 door)
    {
        Bounds w = b.bounds;
        bool alongX = w.size.x >= w.size.z;
        float across = alongX ? Mathf.Abs(w.center.z - door.z) : Mathf.Abs(w.center.x - door.x);
        float along = alongX ? door.x : door.z;
        float min = alongX ? w.min.x : w.min.z, max = alongX ? w.max.x : w.max.z;
        float length = max - min;
        return across < 0.4f && length > GapWidth + 0.2f && along > min + 0.1f && along < max - 0.1f
               && w.min.y < 1f && w.max.y > 1f;
    }

    // Replace one solid box with left / right pieces and a lintel around a door opening
    private static void CutGap(BoxCollider solid, Vector3 doorWorld)
    {
        Transform t = solid.transform;
        Vector3 size = solid.size, center = solid.center;
        Vector3 doorLocal = t.InverseTransformPoint(doorWorld);
        float gapTopLocal = t.InverseTransformPoint(new Vector3(doorWorld.x, GapTop, doorWorld.z)).y;

        bool alongX = Mathf.Abs(size.x) >= Mathf.Abs(size.z);
        float min = (alongX ? center.x : center.z) - (alongX ? size.x : size.z) * 0.5f;
        float max = min + (alongX ? size.x : size.z);
        float mid = alongX ? doorLocal.x : doorLocal.z;
        float bottom = center.y - size.y * 0.5f, top = center.y + size.y * 0.5f;
        float halfGap = GapWidth * 0.5f;

        void Piece(float a, float b, float y0, float y1)
        {
            if (b - a < 0.01f || y1 - y0 < 0.01f) { return; }
            BoxCollider piece = t.gameObject.AddComponent<BoxCollider>();
            piece.size = alongX ? new Vector3(b - a, y1 - y0, size.z) : new Vector3(size.x, y1 - y0, b - a);
            piece.center = alongX
                ? new Vector3((a + b) * 0.5f, (y0 + y1) * 0.5f, center.z)
                : new Vector3(center.x, (y0 + y1) * 0.5f, (a + b) * 0.5f);
        }

        Piece(min, mid - halfGap, bottom, top);
        Piece(mid + halfGap, max, bottom, top);
        Piece(mid - halfGap, mid + halfGap, Mathf.Clamp(gapTopLocal, bottom, top), top);
        solid.enabled = false;
    }

    private static void Dump(Transform wall, StringBuilder log)
    {
        foreach (BoxCollider c in wall.GetComponentsInChildren<BoxCollider>(true))
        {
            log.AppendLine($"    box enabled={c.enabled} center={c.bounds.center} size={c.bounds.size}");
        }
    }
}
