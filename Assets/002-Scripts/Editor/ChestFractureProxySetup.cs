using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: adds FractureProxyMesh to the chest ItemBoxes, so breaking them fractures a plain box instead of the
/// multi-part chest model (which made OpenFracture hang / crash). Tools > Items > Fix Chest Breaking.
/// </summary>
public static class ChestFractureProxySetup
{
    private const string AutoKey = "ChestFractureProxySetup_v1";

    private static readonly string[] Prefabs =
    {
        "Assets/003-Prefabs/ItemBox (HealthPotion).prefab",
        "Assets/003-Prefabs/ItemBox (Energy Orb).prefab",
        "Assets/003-Prefabs/ItemBox (FirstWeapon).prefab",
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

    [MenuItem("Tools/Items/Fix Chest Breaking")]
    public static void Run()
    {
        var log = new StringBuilder();
        foreach (string path in Prefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Fracture fracture = root.GetComponentInChildren<Fracture>(true);
                if (fracture == null) { log.AppendLine("!! no Fracture in " + path); continue; }
                if (fracture.GetComponent<FractureProxyMesh>() != null) { log.AppendLine("kept " + path); continue; }

                fracture.gameObject.AddComponent<FractureProxyMesh>();
                MeshFilter mf = fracture.GetComponent<MeshFilter>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"{Path.GetFileNameWithoutExtension(path)}: breaks as a box now (model has {(mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : 0)} vertices, {(mf != null && mf.sharedMesh != null ? mf.sharedMesh.subMeshCount : 0)} materials)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/chest_fracture.txt", log.ToString());
        Debug.Log("[ChestFracture] Done. Report: Library/PrisonDecor/chest_fracture.txt");
    }
}
