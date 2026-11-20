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
/// One-shot: brings the Maze Yard (Courtyard) floors up to the Prison's level.
///  1. Maze Yard versions of the Prison's special rooms (Shop, Shrine, Treasure x3, Challenge, Boss):
///     Prefab Variants with hedge walls and the yard floor, in Room/Courtyard/.
///  2. Matching tile sets in Tile Set/Courtyard/.
///  3. Floors 2-1 and 2-2 get the same special-room injection rules as Prison floors 1 and 2
///     (shop, shrine, 2 treasure rooms, challenge room on side branches).
///  4. Maze Yard side branches: 1-2 -> 4-5 (same as the Prison).
///  5. A Maze Yard boss floor flow; floor 2-3 uses it.
/// Existing assets are never overwritten. Re-run from Tools > Biomes > Upgrade Maze Yard.
/// </summary>
public static class MazeYardUpgradeBuilder
{
    private const string AutoKey = "MazeYardUpgradeBuilder_v2";

    private const string Dg = "Assets/012-MyDungenAsset";
    private const string PrisonRooms = Dg + "/Room/Prison";
    private const string YardRooms = Dg + "/Room/Courtyard";
    private const string PrisonSets = Dg + "/Tile Set/Prison";
    private const string YardSets = Dg + "/Tile Set/Courtyard";
    private const string ScenePath = "Assets/010-Scenes/RoguelikeScene.unity";
    private const int BossFloorIndex = 5; // Floor 2-3

    private static readonly (string from, string to)[] MaterialSwaps =
    {
        ("Assets/006-Materials/Material_Wall_Prison.mat", "Assets/006-Materials/Material_Wall_Leaf.mat"),
        ("Assets/006-Materials/Material_Floor_Prison.mat", "Assets/006-Materials/Material_Floor_Courtyard.mat"),
    };

    // Prison room -> Maze Yard room name
    private static readonly (string prison, string yard)[] Rooms =
    {
        ("Shop Room (Prison)", "Shop Room (Courtyard)"),
        ("Shrine Room (Prison)", "Shrine Room (Courtyard)"),
        ("Treasure Room (Bronze) (Prison)", "Treasure Room (Bronze) (Courtyard)"),
        ("Treasure Room (Silver) (Prison)", "Treasure Room (Silver) (Courtyard)"),
        ("Treasure Room (Gold) (Prison)", "Treasure Room (Gold) (Courtyard)"),
        ("Challenge Room (Prison)", "Challenge Room (Courtyard)"),
        ("Boss Room (1)", "Boss Room (Courtyard)"),
    };

    private static readonly string[] SpecialSets =
    {
        "Shop Room Set (Prison)", "Shrine Room Set (Prison)", "Treasure Room Set (Prison)",
        "Challenge Room Set (Prison)", "Boss Room Set (Prison)",
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

    [MenuItem("Tools/Biomes/Upgrade Maze Yard")]
    public static void Build()
    {
        var log = new StringBuilder();
        int errors = 0;

        try
        {
            var materialMap = MaterialSwaps.ToDictionary(
                m => AssetDatabase.LoadAssetAtPath<Material>(m.from),
                m => AssetDatabase.LoadAssetAtPath<Material>(m.to));
            if (materialMap.Any(kv => kv.Key == null || kv.Value == null)) { throw new System.Exception("a swap material is missing"); }

            var map = new Dictionary<Object, Object>();

            // 1. Rooms
            foreach (var (prisonName, yardName) in Rooms)
            {
                GameObject prison = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrisonRooms}/{prisonName}.prefab");
                if (prison == null) { log.AppendLine("!! missing " + prisonName); errors++; continue; }
                map[prison] = MakeVariant(prison, $"{YardRooms}/{yardName}.prefab", materialMap, log);
            }

            // 2. Tile sets (special rooms) + the existing Maze Yard sets for start / end / archetype
            if (!AssetDatabase.IsValidFolder(YardSets)) { AssetDatabase.CreateFolder(Dg + "/Tile Set", "Courtyard"); }
            var newSets = new List<Object>();
            foreach (string set in SpecialSets)
            {
                newSets.Add(Copy($"{PrisonSets}/{set}.asset", $"{YardSets}/{set.Replace("(Prison)", "(Courtyard)")}.asset", map, log));
            }
            Link($"{PrisonSets}/Start Tile Set (Prison).asset", $"{Dg}/Tile Set/Tile Set (Courtyard) (Start) (General Floor).asset", map);
            Link($"{PrisonSets}/End Tile Set (Prison).asset", $"{Dg}/Tile Set/Tile Set (Courtyard) (End).asset", map);
            Link($"{PrisonSets}/Tile Set (Prison).asset", $"{Dg}/Tile Set/Tile Set (Courtyard).asset", map);
            Link($"{Dg}/Dungeon Archetype (Prison).asset", $"{Dg}/Dungeon Archetype (Courtyard).asset", map);

            foreach (Object set in newSets) { log.AppendLine($"  {set.name}: {Remap(set, map)} reference(s) moved to Maze Yard rooms"); }

            // 3. Special rooms on floors 2-1 and 2-2
            CopyInjectionRules($"{Dg}/Dungeon Flow (Prison) (First Floor).asset", $"{Dg}/Dungeon Flow (Courtyard (First Floor).asset", map, log);
            CopyInjectionRules($"{Dg}/Dungeon Flow (Prison) (Second Floor).asset", $"{Dg}/Dungeon Flow (Courtyard (General Floor).asset", map, log);

            // 4. More side branches
            DungeonArchetype archetype = AssetDatabase.LoadAssetAtPath<DungeonArchetype>($"{Dg}/Dungeon Archetype (Courtyard).asset");
            var aso = new SerializedObject(archetype);
            aso.FindProperty("BranchCount.Min").intValue = 4;
            aso.FindProperty("BranchCount.Max").intValue = 5;
            aso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(archetype);
            log.AppendLine("Maze Yard branches: 4-5");

            // 5. Boss floor
            Object bossFlow = Copy($"{Dg}/Dungeon Flow (Prison) (BossFloor).asset", $"{Dg}/Dungeon Flow (Courtyard (BossFloor).asset", map, log);
            log.AppendLine($"  {bossFlow.name}: {Remap(bossFlow, map)} reference(s) moved to Maze Yard assets");

            // 6. Challenge / Boss rooms build their walls from castle stone: hedges there too (round pillars stay stone)
            HedgeStoneWalls($"{YardRooms}/Challenge Room (Courtyard).prefab", log);
            HedgeStoneWalls($"{YardRooms}/Boss Room (Courtyard).prefab", log);

            AssetDatabase.SaveAssets();
            SetBossFloor((DungeonFlow)bossFlow, log);
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/mazeyard_upgrade.txt", log.ToString());
        Debug.Log($"[MazeYardUpgrade] Done with {errors} error(s). Report: Library/PrisonDecor/mazeyard_upgrade.txt");
    }

    private static void HedgeStoneWalls(string path, StringBuilder log)
    {
        Material hedge = AssetDatabase.LoadAssetAtPath<Material>("Assets/006-Materials/Material_Wall_Leaf.mat");
        var stones = new HashSet<Material>
        {
            AssetDatabase.LoadAssetAtPath<Material>("Assets/006-Materials/Environment/Castle/CastleWall-1.5x1.5.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/006-Materials/Environment/Castle/CastleWall-15x1.5.mat"),
        };

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int swapped = 0;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("Cylinder")) { continue; } // pillars stay stone
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && stones.Contains(mats[i])) { mats[i] = hedge; changed = true; swapped++; }
                }
                if (changed) { r.sharedMaterials = mats; }
            }
            if (swapped > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); }
            log.AppendLine($"{Path.GetFileNameWithoutExtension(path)}: {swapped} stone wall slot(s) -> hedge");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject MakeVariant(GameObject prison, string path, Dictionary<Material, Material> materialMap, StringBuilder log)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) { log.AppendLine("kept " + path); return existing; }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prison);
        try
        {
            instance.name = Path.GetFileNameWithoutExtension(path);
            int swapped = 0;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && materialMap.TryGetValue(mats[i], out Material to)) { mats[i] = to; changed = true; swapped++; }
                }
                if (changed) { r.sharedMaterials = mats; }
            }
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, path);
            log.AppendLine($"created {path} ({swapped} material slot(s) swapped)");
            return variant;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static Object Copy(string from, string to, Dictionary<Object, Object> map, StringBuilder log)
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

    private static void Link(string prisonPath, string yardPath, Dictionary<Object, Object> map)
    {
        Object prison = AssetDatabase.LoadMainAssetAtPath(prisonPath);
        Object yard = AssetDatabase.LoadMainAssetAtPath(yardPath);
        if (prison == null || yard == null) { throw new System.Exception($"missing {prisonPath} or {yardPath}"); }
        map[prison] = yard;
    }

    private static void CopyInjectionRules(string prisonFlowPath, string yardFlowPath, Dictionary<Object, Object> map, StringBuilder log)
    {
        DungeonFlow prison = AssetDatabase.LoadAssetAtPath<DungeonFlow>(prisonFlowPath);
        DungeonFlow yard = AssetDatabase.LoadAssetAtPath<DungeonFlow>(yardFlowPath);

        var yso = new SerializedObject(yard);
        if (yso.FindProperty("TileInjectionRules").arraySize > 0)
        {
            log.AppendLine($"kept {yard.name}: already has {yso.FindProperty("TileInjectionRules").arraySize} injection rule(s)");
            return;
        }

        var pso = new SerializedObject(prison);
        yso.CopyFromSerializedProperty(pso.FindProperty("TileInjectionRules"));
        yso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(yard);
        int moved = Remap(yard, map);
        log.AppendLine($"{yard.name}: {yso.FindProperty("TileInjectionRules").arraySize} special-room rule(s) added ({moved} reference(s) moved to Maze Yard sets)");
    }

    /// <summary>Points every reference to a Prison asset inside this asset at its Maze Yard counterpart.</summary>
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

    private static void SetBossFloor(DungeonFlow bossFlow, StringBuilder log)
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
        if (manager == null) { log.AppendLine("!! no RoguelikeManager in " + ScenePath); return; }

        var so = new SerializedObject(manager);
        SerializedProperty flows = so.FindProperty("dungeonFlows");
        if (flows.arraySize <= BossFloorIndex)
        {
            log.AppendLine("!! run has no floor 2-3 slot");
        }
        else if (flows.GetArrayElementAtIndex(BossFloorIndex).objectReferenceValue != bossFlow)
        {
            flows.GetArrayElementAtIndex(BossFloorIndex).objectReferenceValue = bossFlow;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("run: Floor 2-3 = " + bossFlow.name);
        }

        if (!wasDirty || openedHere)
        {
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("saved " + ScenePath);
        }
        else
        {
            log.AppendLine("RoguelikeScene had unsaved changes: Floor 2-3 set but NOT saved. Save the scene yourself.");
        }
        if (openedHere) { EditorSceneManager.CloseScene(scene, true); }
    }
}
