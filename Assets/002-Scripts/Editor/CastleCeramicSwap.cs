using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: gives every Castle room ceramic walls and floor, like the Castle Challenge Room.
///  Castle wall + castle stone -> Material_Wall_Ceramic, castle floor -> Material_Floor_Ceramic.
/// Ceilings (objects named "Floor & Poles" / "Ceiling" above 1.5 m) -> Material_Floor_Ceramic.
/// Re-run from Tools > Biomes > Castle Rooms To Ceramic (safe: only swaps the castle materials).
/// </summary>
public static class CastleCeramicSwap
{
    private const string AutoKey = "CastleCeramicSwap_v3";
    private const string RoomFolder = "Assets/012-MyDungenAsset/Room/Castle";
    private const string CastleMats = "Assets/006-Materials/Environment/Castle Interior";

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

    [MenuItem("Tools/Biomes/Castle Rooms To Ceramic")]
    public static void Run()
    {
        var log = new StringBuilder();
        int errors = 0;

        Material wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/006-Materials/Material_Wall_Ceramic.mat");
        Material floor = AssetDatabase.LoadAssetAtPath<Material>("Assets/006-Materials/Material_Floor_Ceramic.mat");
        var map = new Dictionary<Material, Material>();
        void Add(string name, Material to)
        {
            Material from = AssetDatabase.LoadAssetAtPath<Material>($"{CastleMats}/{name}.mat");
            if (from != null) { map[from] = to; } else { log.AppendLine("!! missing " + name); }
        }
        Add("Material_Wall_Castle", wall);
        Add("Material_Stone_Castle-1.5x1.5", wall);
        Add("Material_Stone_Castle-15x1.5", wall);
        Add("Material_Floor_Castle", floor);
        Material plank = AssetDatabase.LoadAssetAtPath<Material>("Assets/006-Materials/Environment/Door/WoodenPlank-1.4x3.mat");

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RoomFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int swapped = 0, ceilings = 0;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = r.sharedMaterials;
                    bool changed = false;
                    bool ceiling = (r.name.StartsWith("Floor & Poles") || r.name.StartsWith("Ceiling")) && r.bounds.min.y > 1.5f;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) { continue; }
                        if (map.TryGetValue(mats[i], out Material to)) { mats[i] = to; changed = true; swapped++; }
                        else if (ceiling && (mats[i] == plank || mats[i] == wall)) { mats[i] = floor; changed = true; swapped++; ceilings++; }
                    }
                    if (changed) { r.sharedMaterials = mats; }
                    if (path.Contains("Small Room (End)") && r.bounds.max.y > 2.2f && !(r is ParticleSystemRenderer))
                    {
                        log.AppendLine($"    high piece '{r.name}' minY={r.bounds.min.y:0.00} size={r.bounds.size} mats=[{string.Join(", ", System.Array.ConvertAll(r.sharedMaterials, m => m != null ? m.name : "null"))}]");
                    }
                }
                if (swapped > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); }
                log.AppendLine($"{Path.GetFileNameWithoutExtension(path)}: {swapped} slot(s) -> ceramic ({ceilings} ceiling)");
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
        File.WriteAllText("Library/PrisonDecor/castle_ceramic.txt", log.ToString());
        Debug.Log($"[CastleCeramicSwap] Done with {errors} error(s). Report: Library/PrisonDecor/castle_ceramic.txt");
    }
}
