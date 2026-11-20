using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DunGen;
using DunGen.Graph;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot: builds the Castle Interior biome (World 3) from the Prison biome.
///  1. Castle materials + their own texture files (copies of the Prison textures as a starting point)
///     in 001-Sprite/001A-MyAsset/Castle Interior/. Paint over those PNGs to give the castle its look.
///  2. A Castle variant of every Prison room (special rooms included) in Room/Castle/, with the castle materials.
///     Variants follow the Prison room's layout, so layout changes to the Prison room carry over.
///  3. Castle tile sets, archetype and dungeon flows (First Floor, Second Floor, Boss Floor).
///  4. Floors 3-1, 3-2, 3-3 added to the RoguelikeManager in RoguelikeScene.
/// Existing castle assets are never overwritten. Re-run from Tools > Biomes > Build Castle Interior.
/// </summary>
public static class CastleBiomeBuilder
{
    private const string AutoKey = "CastleBiomeBuilder_v1";

    private const string DunGenFolder = "Assets/012-MyDungenAsset";
    private const string PrisonRoomFolder = DunGenFolder + "/Room/Prison";
    private const string CastleRoomFolder = DunGenFolder + "/Room/Castle";
    private const string PrisonTileSetFolder = DunGenFolder + "/Tile Set/Prison";
    private const string CastleTileSetFolder = DunGenFolder + "/Tile Set/Castle";
    private const string CastleMaterialFolder = "Assets/006-Materials/Environment/Castle Interior";
    private const string CastleTextureFolder = "Assets/001-Sprite/001A-MyAsset/Castle Interior";
    private const string ScenePath = "Assets/010-Scenes/RoguelikeScene.unity";

    // Prison material -> castle material name, and a warm tint so the castle reads different before you paint it
    private static readonly (string source, string castleName, Color tint)[] Materials =
    {
        ("Assets/006-Materials/Material_Wall_Prison.mat", "Material_Wall_Castle", new Color(1f, 0.9f, 0.8f)),
        ("Assets/006-Materials/Material_Floor_Prison.mat", "Material_Floor_Castle", new Color(0.85f, 0.7f, 0.6f)),
        ("Assets/006-Materials/Environment/Castle/CastleWall-1.5x1.5.mat", "Material_Stone_Castle-1.5x1.5", new Color(1f, 0.92f, 0.82f)),
        ("Assets/006-Materials/Environment/Castle/CastleWall-15x1.5.mat", "Material_Stone_Castle-15x1.5", new Color(1f, 0.92f, 0.82f)),
    };

    private static readonly (string source, string castle, string floorName)[] Flows =
    {
        ("Dungeon Flow (Prison) (First Floor)", "Dungeon Flow (Castle) (First Floor)", "Floor 3-1"),
        ("Dungeon Flow (Prison) (Second Floor)", "Dungeon Flow (Castle) (Second Floor)", "Floor 3-2"),
        ("Dungeon Flow (Prison) (BossFloor)", "Dungeon Flow (Castle) (BossFloor)", "Floor 3-3"),
    };

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

    [MenuItem("Tools/Biomes/Build Castle Interior")]
    public static void Build()
    {
        var log = new StringBuilder();
        int errors = 0;

        try
        {
            EnsureFolder(CastleRoomFolder);
            EnsureFolder(CastleTileSetFolder);
            EnsureFolder(CastleMaterialFolder);
            EnsureFolder(CastleTextureFolder);

            Dictionary<Material, Material> materialMap = BuildMaterials(log);

            // Everything Prison -> Castle, used to repoint the copied tile sets / archetype / flows
            var map = new Dictionary<Object, Object>();
            BuildRooms(materialMap, map, log, ref errors);
            List<Object> tileSets = CopyAll(PrisonTileSetFolder, CastleTileSetFolder, "t:TileSet", map, log);
            Object archetype = CopyOne($"{DunGenFolder}/Dungeon Archetype (Prison).asset", $"{DunGenFolder}/Dungeon Archetype (Castle).asset", map, log);

            var flows = new List<DungeonFlow>();
            foreach (var (source, castle, _) in Flows)
            {
                flows.Add((DungeonFlow)CopyOne($"{DunGenFolder}/{source}.asset", $"{DunGenFolder}/{castle}.asset", map, log));
            }

            // Repoint the copies at the castle rooms / tile sets / archetype
            foreach (Object asset in tileSets.Concat(new[] { archetype }).Concat(flows))
            {
                int changed = Remap(asset, map);
                log.AppendLine($"  {asset.name}: {changed} reference(s) moved to castle assets");
            }

            AssetDatabase.SaveAssets();
            AddFloorsToRun(flows, log);
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/castle_biome.txt", log.ToString());
        Debug.Log($"[CastleBiomeBuilder] Done with {errors} error(s). Report: Library/PrisonDecor/castle_biome.txt");
    }

    // ---------- Materials ----------

    private static Dictionary<Material, Material> BuildMaterials(StringBuilder log)
    {
        var map = new Dictionary<Material, Material>();
        var textureCopies = new Dictionary<Texture, Texture>();

        foreach (var (sourcePath, castleName, tint) in Materials)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null) { log.AppendLine("!! missing material " + sourcePath); continue; }

            string castlePath = $"{CastleMaterialFolder}/{castleName}.mat";
            Material castle = AssetDatabase.LoadAssetAtPath<Material>(castlePath);
            if (castle == null)
            {
                AssetDatabase.CopyAsset(sourcePath, castlePath);
                castle = AssetDatabase.LoadAssetAtPath<Material>(castlePath);

                // Own texture file to paint (shared by materials that used the same Prison texture)
                Texture baseTex = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture;
                if (baseTex != null)
                {
                    if (!textureCopies.TryGetValue(baseTex, out Texture castleTex))
                    {
                        string texPath = AssetDatabase.GetAssetPath(baseTex);
                        string newTexPath = $"{CastleTextureFolder}/{Path.GetFileNameWithoutExtension(texPath)}_Castle{Path.GetExtension(texPath)}";
                        if (AssetDatabase.LoadAssetAtPath<Texture>(newTexPath) == null) { AssetDatabase.CopyAsset(texPath, newTexPath); }
                        castleTex = AssetDatabase.LoadAssetAtPath<Texture>(newTexPath);
                        textureCopies[baseTex] = castleTex;
                        log.AppendLine("texture to paint: " + newTexPath);
                    }
                    if (castle.HasProperty("_BaseMap")) { castle.SetTexture("_BaseMap", castleTex); }
                    if (castle.HasProperty("_MainTex")) { castle.SetTexture("_MainTex", castleTex); }
                }

                if (castle.HasProperty("_BaseColor")) { castle.SetColor("_BaseColor", tint); }
                if (castle.HasProperty("_Color")) { castle.SetColor("_Color", tint); }
                EditorUtility.SetDirty(castle);
                log.AppendLine("created " + castlePath);
            }
            else
            {
                log.AppendLine("kept " + castlePath);
            }

            map[source] = castle;
        }

        return map;
    }

    // ---------- Rooms ----------

    private static void BuildRooms(Dictionary<Material, Material> materialMap, Dictionary<Object, Object> map, StringBuilder log, ref int errors)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrisonRoomFolder }))
        {
            string prisonPath = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(prisonPath);
            string castleName = fileName.Contains("(Prison)") ? fileName.Replace("(Prison)", "(Castle)") : fileName + " (Castle)";
            string castlePath = $"{CastleRoomFolder}/{castleName}.prefab";

            GameObject prison = AssetDatabase.LoadAssetAtPath<GameObject>(prisonPath);
            GameObject castle = AssetDatabase.LoadAssetAtPath<GameObject>(castlePath);

            if (castle == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prison);
                try
                {
                    instance.name = castleName;
                    int swapped = 0;
                    foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] mats = r.sharedMaterials;
                        bool changed = false;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] != null && materialMap.TryGetValue(mats[i], out Material castleMat)) { mats[i] = castleMat; changed = true; swapped++; }
                        }
                        if (changed) { r.sharedMaterials = mats; }
                    }

                    castle = PrefabUtility.SaveAsPrefabAsset(instance, castlePath); // a Prefab Variant of the Prison room
                    log.AppendLine($"created {castlePath} ({swapped} material slot(s) swapped)");
                }
                catch (System.Exception e)
                {
                    errors++;
                    log.AppendLine("!! " + castlePath + ": " + e.Message);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
            else
            {
                log.AppendLine("kept " + castlePath);
            }

            if (prison != null && castle != null) { map[prison] = castle; }
        }
    }

    // ---------- DunGen assets ----------

    private static List<Object> CopyAll(string fromFolder, string toFolder, string filter, Dictionary<Object, Object> map, StringBuilder log)
    {
        var result = new List<Object>();
        foreach (string guid in AssetDatabase.FindAssets(filter, new[] { fromFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileName(path).Replace("(Prison)", "(Castle)");
            result.Add(CopyOne(path, $"{toFolder}/{name}", map, log));
        }
        return result;
    }

    private static Object CopyOne(string from, string to, Dictionary<Object, Object> map, StringBuilder log)
    {
        Object source = AssetDatabase.LoadMainAssetAtPath(from);
        if (source == null) { throw new System.Exception("missing " + from); }

        if (AssetDatabase.LoadMainAssetAtPath(to) == null)
        {
            if (!AssetDatabase.CopyAsset(from, to)) { throw new System.Exception("copy failed " + to); }
            log.AppendLine("created " + to);
        }
        else
        {
            log.AppendLine("kept " + to);
        }

        Object copy = AssetDatabase.LoadMainAssetAtPath(to);
        map[source] = copy;
        return copy;
    }

    /// <summary>Points every reference to a Prison asset inside this asset at its Castle copy.</summary>
    private static int Remap(Object asset, Dictionary<Object, Object> map)
    {
        var so = new SerializedObject(asset);
        SerializedProperty it = so.GetIterator();
        int changed = 0;
        while (it.Next(true))
        {
            if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) { continue; }
            if (map.TryGetValue(it.objectReferenceValue, out Object to)) { it.objectReferenceValue = to; changed++; }
        }
        if (changed > 0)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
        return changed;
    }

    // ---------- Run order ----------

    private static void AddFloorsToRun(List<DungeonFlow> flows, StringBuilder log)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }
        bool wasDirty = scene.isDirty;

        RoguelikeManager manager = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<RoguelikeManager>(true))
            .FirstOrDefault();

        if (manager == null)
        {
            log.AppendLine("!! no RoguelikeManager in " + ScenePath);
        }
        else
        {
            var so = new SerializedObject(manager);
            SerializedProperty flowList = so.FindProperty("dungeonFlows");
            SerializedProperty names = so.FindProperty("str_DungeonIndex");

            for (int f = 0; f < flows.Count; f++)
            {
                bool present = false;
                for (int i = 0; i < flowList.arraySize; i++)
                {
                    if (flowList.GetArrayElementAtIndex(i).objectReferenceValue == flows[f]) { present = true; break; }
                }
                if (present) { log.AppendLine("run already has " + flows[f].name); continue; }

                int index = flowList.arraySize;
                flowList.arraySize++;
                flowList.GetArrayElementAtIndex(index).objectReferenceValue = flows[f];

                // Floor name lines up with the flow index
                if (names.arraySize <= index) { names.arraySize = index + 1; }
                names.GetArrayElementAtIndex(index).stringValue = Flows[f].floorName;
                log.AppendLine($"run: floor {index} = {Flows[f].floorName} ({flows[f].name})");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // Save only if nothing else was unsaved in that scene, so your own unsaved work is never saved behind your back
        if (!wasDirty || openedHere)
        {
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("saved " + ScenePath);
        }
        else
        {
            log.AppendLine("RoguelikeScene had unsaved changes: castle floors added but NOT saved. Save the scene yourself.");
        }

        if (openedHere) { EditorSceneManager.CloseScene(scene, true); }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) { return; }
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
