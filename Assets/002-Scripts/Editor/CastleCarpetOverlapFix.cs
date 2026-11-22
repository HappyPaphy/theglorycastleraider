using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: makes the carpets in every Castle room stop overlapping each other. Only carpets are touched
/// (curtains and everything else stay as they are).
///  - The centre rug stays where it is.
///  - Runners keep their doorway end; each is shortened so it stops 10 cm before any carpet already kept
///    (rug first, then runners from longest to shortest). A runner shorter than 1 m afterwards is removed.
/// Safe to re-run: Tools > Biomes > Fix Overlapping Castle Carpets.
/// </summary>
public static class CastleCarpetOverlapFix
{
    private const string AutoKey = "CastleCarpetOverlapFix_v1";
    private const string RoomFolder = "Assets/012-MyDungenAsset/Room/Castle";
    private const float Gap = 0.1f;
    private const float MinLength = 1f;

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

    [MenuItem("Tools/Biomes/Fix Overlapping Castle Carpets")]
    public static void Run()
    {
        var log = new StringBuilder();
        int errors = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RoomFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform decor = root.transform.Find("Castle Decor");
                if (decor == null) { continue; }

                List<Transform> carpets = decor.Cast<Transform>().Where(t => t.name.StartsWith("Carpet")).ToList();
                if (carpets.Count < 2) { continue; }

                int shortened = 0, removed = 0;
                var kept = new List<Rect>();

                // Rugs are fixed
                foreach (Transform rug in carpets.Where(t => t.name.StartsWith("Carpet Rug"))) { kept.Add(Footprint(rug)); }

                // Runners: longest first
                foreach (Transform runner in carpets.Where(t => t.name.StartsWith("Carpet Runner")).OrderByDescending(Length).ToList())
                {
                    float length = Length(runner);
                    Vector3 forward = Flat(runner.forward);           // runners point from their doorway into the room
                    Vector3 start = runner.position - forward * (length * 0.5f);
                    float width = Width(runner);

                    float allowed = length;
                    for (float t = 0.05f; t <= length + 0.001f; t += 0.05f)
                    {
                        Rect r = Strip(start, forward, t, width);
                        if (kept.Any(k => Overlaps(k, r, Gap))) { allowed = t - 0.05f; break; }
                    }

                    if (allowed < MinLength)
                    {
                        Object.DestroyImmediate(runner.gameObject);
                        removed++;
                        continue;
                    }

                    if (allowed < length - 0.001f)
                    {
                        Vector3 s = runner.localScale;
                        s.z *= allowed / length;
                        runner.localScale = s;
                        runner.position = start + forward * (allowed * 0.5f) + Vector3.up * (runner.position.y - start.y);
                        shortened++;
                    }
                    kept.Add(Strip(start, forward, allowed, width));
                }

                if (shortened > 0 || removed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.AppendLine($"{Path.GetFileNameWithoutExtension(path)}: {shortened} runner(s) shortened, {removed} removed");
                }
                else
                {
                    log.AppendLine($"{Path.GetFileNameWithoutExtension(path)}: no overlaps");
                }
            }
            catch (System.Exception e)
            {
                errors++;
                log.AppendLine("!! " + path + ": " + e.Message);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/carpet_overlap.txt", log.ToString());
        Debug.Log($"[CarpetOverlapFix] Done with {errors} error(s). Report: Library/PrisonDecor/carpet_overlap.txt");

        RoomSnapshot.RunFor(
            RoomFolder + "/Large Room (4 Doors) (Castle).prefab",
            RoomFolder + "/Small Room (1) (Castle).prefab",
            RoomFolder + "/Shapes/Stair Hall (Castle).prefab",
            RoomFolder + "/Shapes/Great Hall (Castle).prefab");
    }

    // ---------- footprint helpers (carpets are rotated in 90-degree steps) ----------

    private static Vector3 MeshSize(Transform t)
    {
        MeshFilter mf = t.GetComponent<MeshFilter>();
        Vector3 size = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size : Vector3.one;
        return Vector3.Scale(size, t.localScale);
    }

    private static float Length(Transform t) => MeshSize(t).z;
    private static float Width(Transform t) => MeshSize(t).x;

    private static Rect Footprint(Transform t)
    {
        Vector3 size = MeshSize(t);
        Vector3 f = Flat(t.forward);
        bool alongZ = Mathf.Abs(f.z) > Mathf.Abs(f.x);
        float sx = alongZ ? size.x : size.z, sz = alongZ ? size.z : size.x;
        return new Rect(t.position.x - sx * 0.5f, t.position.z - sz * 0.5f, sx, sz);
    }

    // The part of a runner from its start to distance t
    private static Rect Strip(Vector3 start, Vector3 forward, float t, float width)
    {
        Vector3 end = start + forward * t;
        bool alongZ = Mathf.Abs(forward.z) > Mathf.Abs(forward.x);
        float minX = alongZ ? start.x - width * 0.5f : Mathf.Min(start.x, end.x);
        float maxX = alongZ ? start.x + width * 0.5f : Mathf.Max(start.x, end.x);
        float minZ = alongZ ? Mathf.Min(start.z, end.z) : start.z - width * 0.5f;
        float maxZ = alongZ ? Mathf.Max(start.z, end.z) : start.z + width * 0.5f;
        return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
    }

    private static bool Overlaps(Rect a, Rect b, float gap)
    {
        return a.xMin - gap < b.xMax && a.xMax + gap > b.xMin && a.yMin - gap < b.yMax && a.yMax + gap > b.yMin;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
