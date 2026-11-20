using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Renders room prefabs to PNGs (top-down with the ceiling hidden, and an angled view) into Library/PrisonDecor/Snapshots.
/// Tools > Biomes > Snapshot Castle Rooms.
/// </summary>
public static class RoomSnapshot
{
    private const string AutoKey = "RoomSnapshot_v1";
    private const string OutDir = "Library/PrisonDecor/Snapshots";

    private static readonly string[] Rooms =
    {
        "Assets/012-MyDungenAsset/Room/Castle/Large Room (4 Doors) (Castle).prefab",
        "Assets/012-MyDungenAsset/Room/Castle/Shapes/Great Hall (Castle).prefab",
        "Assets/012-MyDungenAsset/Room/Castle/Shapes/Cloister (Castle).prefab",
        "Assets/012-MyDungenAsset/Room/Castle/Shapes/Library (Castle).prefab",
        "Assets/012-MyDungenAsset/Room/Castle/Shapes/Banquet Hall (Castle).prefab",
        "Assets/012-MyDungenAsset/Room/Castle/Shapes/Armory (Castle).prefab",
        "Assets/012-MyDungenAsset/Room/Castle/Shapes/U Corridor (Castle).prefab",
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

    [MenuItem("Tools/Biomes/Snapshot Castle Rooms")]
    public static void Run()
    {
        Directory.CreateDirectory(OutDir);
        var report = new System.Text.StringBuilder();
        foreach (string path in Rooms)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { report.AppendLine("missing " + path); continue; }
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                room.transform.position = Vector3.zero;

                Bounds b = new Bounds(room.transform.position, Vector3.zero);
                foreach (Renderer r in room.GetComponentsInChildren<Renderer>()) { if (!(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds); }
                report.AppendLine($"{prefab.name}: render bounds {b}");

                // Preview scenes have no ambient light: a grid of bright point lights under the ceiling
                for (float x = b.min.x + 2f; x < b.max.x; x += 4f)
                for (float z = b.min.z + 2f; z < b.max.z; z += 4f)
                {
                    var lightGo = new GameObject("Lamp");
                    SceneManager.MoveGameObjectToScene(lightGo, scene);
                    lightGo.transform.position = new Vector3(x, 2.2f, z);
                    Light lamp = lightGo.AddComponent<Light>();
                    lamp.type = LightType.Point;
                    lamp.range = 7f;
                    lamp.intensity = 3f;
                    lamp.shadows = LightShadows.None;
                }

                // Top-down: hide anything above 2.3 m (ceilings, lights' sprites)
                var hidden = room.GetComponentsInChildren<Renderer>().Where(r => r.bounds.min.y > 2.3f || r.name.StartsWith("Ceiling") || r.name.StartsWith("Floor & Poles (1)")).ToList();
                foreach (Renderer r in hidden) { r.enabled = false; }
                Render(scene, prefab.name + "_top", b, top: true);
                Render(scene, prefab.name + "_angle", b, top: false);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        File.WriteAllText(OutDir + "/report.txt", report.ToString());
        Debug.Log("[RoomSnapshot] Done. " + OutDir);
    }

    private static void Render(Scene scene, string name, Bounds b, bool top)
    {
        var camGo = new GameObject("SnapCam");
        SceneManager.MoveGameObjectToScene(camGo, scene);
        Camera cam = camGo.AddComponent<Camera>();
        cam.scene = scene;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        if (top)
        {
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.z) + 1f;
            camGo.transform.position = new Vector3(b.center.x, 30f, b.center.z);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            cam.fieldOfView = 60f;
            float dist = Mathf.Max(b.size.x, b.size.z) * 0.9f + 4f;
            camGo.transform.position = b.center + new Vector3(-dist * 0.6f, dist * 0.8f, -dist * 0.6f);
            camGo.transform.LookAt(new Vector3(b.center.x, 0.5f, b.center.z));
        }

        var rt = new RenderTexture(1024, 1024, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes($"{OutDir}/{name}.png", tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGo);
    }
}
