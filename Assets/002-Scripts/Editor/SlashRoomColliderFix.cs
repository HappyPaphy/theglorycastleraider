using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: the Slash Rooms' two side walls are thin walls bent into the slash shape, but each had a single
/// box collider sized to the bent wall's whole outline (about 10 x 4.4 m), which filled the room behind both
/// doors with invisible solid space. This switches those oversized boxes off and gives the wall a mesh
/// collider that follows its real shape (ProBuilder fills in its mesh on load, like the floors).
/// Fixes the two base Prison prefabs; the Maze Yard and Castle Slash Rooms are variants and inherit it.
/// Then re-runs Tools > Dungeon > Check Doorway Clearance. Tools > Dungeon > Fix Slash Room Colliders.
/// </summary>
public static class SlashRoomColliderFix
{
    private const string AutoKey = "SlashRoomColliderFix_v2";

    private static readonly string[] Rooms =
    {
        "Assets/012-MyDungenAsset/Room/Prison/Slash Room (1).prefab",
        "Assets/012-MyDungenAsset/Room/Prison/Slash Room + 2 Side Rooms(1).prefab",
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

    [MenuItem("Tools/Dungeon/Fix Slash Room Colliders")]
    public static void Run()
    {
        var log = new StringBuilder();
        foreach (string path in Rooms)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.AppendLine("!! missing " + path); continue; }
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                log.AppendLine("== " + path);
                foreach (Transform wall in root.transform)
                {
                    if (!wall.name.StartsWith("Small Wall (No Door)")) { continue; }

                    MeshFilter mf = wall.GetComponentInChildren<MeshFilter>(true);
                    bool needsMeshCollider = false;
                    foreach (BoxCollider box in wall.GetComponentsInChildren<BoxCollider>(true))
                    {
                        if (box.isTrigger) { continue; }
                        // Oversized = its mesh-sized box is deep in both horizontal directions (a real wall is thin).
                        // Measured from size, so a box already switched off by the first run still counts.
                        Vector3 s = Vector3.Scale(box.size, box.transform.lossyScale);
                        Vector3 w = box.transform.rotation * s;
                        float sx = Mathf.Abs(w.x), sz = Mathf.Abs(w.z);
                        if (sx <= 1f || sz <= 1f) { log.AppendLine($"  ok   {wall.name}: box {sx:F2} x {sz:F2}"); continue; }

                        needsMeshCollider = true;
                        if (box.enabled)
                        {
                            box.enabled = false;
                            changed = true;
                            log.AppendLine($"  off  {wall.name}: oversized box {sx:F2} x {sz:F2}");
                        }
                    }

                    if (needsMeshCollider && mf != null)
                    {
                        MeshCollider mc = mf.GetComponent<MeshCollider>();
                        if (mc == null)
                        {
                            mc = mf.gameObject.AddComponent<MeshCollider>();
                            changed = true;
                            log.AppendLine($"  add  mesh collider on '{mf.name}'");
                        }
                        if (!mc.enabled || mc.convex || mc.isTrigger)
                        {
                            mc.enabled = true;      // was switched off in the wall prefab
                            mc.convex = false;      // follow the bent shape exactly
                            mc.isTrigger = false;
                            changed = true;
                            log.AppendLine($"  on   mesh collider on '{mf.name}' (layer {LayerMask.LayerToName(mf.gameObject.layer)})");
                        }
                        if (mc.sharedMesh == null) { mc.sharedMesh = mf.sharedMesh; } // ProBuilder assigns it when the mesh is built
                    }
                }
                if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); log.AppendLine("  saved"); }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/slash_room_fix.txt", log.ToString());
        Debug.Log("[SlashRoomFix] Done. Report: Library/PrisonDecor/slash_room_fix.txt");

        DoorwayClearanceCheck.Run(); // prove the doorways are clear now
    }
}
