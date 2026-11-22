using System.IO;
using System.Linq;
using System.Text;
using DunGen;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot: makes Castle generation more forgiving without changing its room list.
///  - Dungeon Archetype (Castle): side branches 4-5 -> 3-4 (still room for the 5 special rooms).
///  - RoguelikeScene's RuntimeDungeon: 20 -> 50 generation attempts before giving up (all biomes;
///    floors that succeed first time are unaffected).
/// Tools > Biomes > Tune Castle Generation.
/// </summary>
public static class CastleGenerationTuning
{
    private const string AutoKey = "CastleGenerationTuning_v1";
    private const string ArchetypePath = "Assets/012-MyDungenAsset/Dungeon Archetype (Castle).asset";
    private const string ScenePath = "Assets/010-Scenes/RoguelikeScene.unity";
    private const int Attempts = 50;

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

    [MenuItem("Tools/Biomes/Tune Castle Generation")]
    public static void Run()
    {
        var log = new StringBuilder();

        DungeonArchetype archetype = AssetDatabase.LoadAssetAtPath<DungeonArchetype>(ArchetypePath);
        var so = new SerializedObject(archetype);
        so.FindProperty("BranchCount.Min").intValue = 3;
        so.FindProperty("BranchCount.Max").intValue = 4;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(archetype);
        AssetDatabase.SaveAssets();
        log.AppendLine("Castle archetype: side branches 3-4");

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }
        bool wasDirty = scene.isDirty;

        RuntimeDungeon runtime = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RuntimeDungeon>(true)).FirstOrDefault();
        if (runtime == null)
        {
            log.AppendLine("!! no RuntimeDungeon in " + ScenePath);
        }
        else
        {
            runtime.Generator.Settings.MaxAttemptCount = Attempts;
            EditorUtility.SetDirty(runtime);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine($"RuntimeDungeon: {Attempts} generation attempts");

            if (!wasDirty || openedHere)
            {
                EditorSceneManager.SaveScene(scene);
                log.AppendLine("saved " + ScenePath);
            }
            else
            {
                log.AppendLine("RoguelikeScene had unsaved changes: attempts set but NOT saved. Save the scene yourself.");
            }
        }
        if (openedHere) { EditorSceneManager.CloseScene(scene, true); }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/castle_tuning.txt", log.ToString());
        Debug.Log("[CastleTuning] Done. Report: Library/PrisonDecor/castle_tuning.txt");
    }
}
