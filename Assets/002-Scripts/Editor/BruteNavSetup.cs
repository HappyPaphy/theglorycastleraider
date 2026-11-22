using System.IO;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot: a "Large" navmesh agent type for big enemies (the Brute), so he keeps his distance from walls
/// (his sprite no longer sinks into them) but still fits through doors.
///  - Navigation agent type "Large": radius 0.4 m (doors are 1.03 m wide), height 1.0 m (passes under lintels).
///  - RoguelikeScene: a second NavMeshSurface for that agent type next to the Humanoid one (fine voxels so the
///    door gaps stay connected). RoguelikeManager bakes every surface on that object.
///  - Enemy_Brute: uses the Large agent type, radius 0.4; hug holds the player 0.7 m away and 0.9 m up.
/// Tools > Enemies > Set Up Large Navmesh Agent.
/// </summary>
public static class BruteNavSetup
{
    private const string AutoKey = "BruteNavSetup_v1";
    private const string AgentName = "Large";
    private const float Radius = 0.4f, Height = 1.0f, Slope = 45f, Climb = 0.5f, Voxel = 0.08f;
    private const string ScenePath = "Assets/010-Scenes/RoguelikeScene.unity";
    private const string BrutePath = "Assets/003-Prefabs/Enemy/Enemy_Brute.prefab";

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

    [MenuItem("Tools/Enemies/Set Up Large Navmesh Agent")]
    public static void Run()
    {
        var log = new StringBuilder();
        try
        {
            int id = EnsureAgentType(log);
            AddSurface(id, log);
            SetUpBrute(id, log);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            log.AppendLine("!! " + e);
        }
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/brute_nav.txt", log.ToString());
        Debug.Log("[BruteNav] Done. Report: Library/PrisonDecor/brute_nav.txt");
    }

    private static int EnsureAgentType(StringBuilder log)
    {
        Object settingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset").FirstOrDefault();
        if (settingsAsset == null) { throw new System.Exception("NavMeshAreas.asset not found"); }
        var so = new SerializedObject(settingsAsset);
        SerializedProperty settings = so.FindProperty("m_Settings");
        SerializedProperty names = so.FindProperty("m_SettingNames");

        int index = -1;
        for (int i = 0; i < names.arraySize; i++) { if (names.GetArrayElementAtIndex(i).stringValue == AgentName) { index = i; } }

        if (index < 0)
        {
            NavMeshBuildSettings created = NavMesh.CreateSettings();
            so.Update();
            settings = so.FindProperty("m_Settings");
            names = so.FindProperty("m_SettingNames");
            for (int i = 0; i < settings.arraySize; i++)
            {
                if (settings.GetArrayElementAtIndex(i).FindPropertyRelative("agentTypeID").intValue == created.agentTypeID) { index = i; }
            }
            if (index < 0) { throw new System.Exception("could not create the agent type"); }
            while (names.arraySize <= index) { names.arraySize++; }
            names.GetArrayElementAtIndex(index).stringValue = AgentName;
            log.AppendLine("created agent type " + AgentName);
        }

        SerializedProperty s = settings.GetArrayElementAtIndex(index);
        s.FindPropertyRelative("agentRadius").floatValue = Radius;
        s.FindPropertyRelative("agentHeight").floatValue = Height;
        s.FindPropertyRelative("agentSlope").floatValue = Slope;
        s.FindPropertyRelative("agentClimb").floatValue = Climb;
        so.ApplyModifiedPropertiesWithoutUndo();

        int id = s.FindPropertyRelative("agentTypeID").intValue;
        log.AppendLine($"agent type {AgentName} (id {id}): radius {Radius}, height {Height}, climb {Climb}");
        return id;
    }

    private static void AddSurface(int id, StringBuilder log)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }
        bool wasDirty = scene.isDirty;
        try
        {
            RoguelikeManager manager = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RoguelikeManager>(true)).FirstOrDefault();
            NavMeshSurface humanoid = manager != null ? new SerializedObject(manager).FindProperty("navMeshSurface").objectReferenceValue as NavMeshSurface : null;
            if (humanoid == null) { log.AppendLine("!! RoguelikeManager has no NavMeshSurface"); return; }

            if (humanoid.GetComponents<NavMeshSurface>().Any(x => x.agentTypeID == id)) { log.AppendLine("scene already has a Large surface"); }
            else
            {
                NavMeshSurface large = humanoid.gameObject.AddComponent<NavMeshSurface>();
                EditorUtility.CopySerialized(humanoid, large); // same layers / geometry as the Humanoid bake
                large.agentTypeID = id;
                large.overrideVoxelSize = true;
                large.voxelSize = Voxel;
                large.navMeshData = null;
                EditorSceneManager.MarkSceneDirty(scene);
                log.AppendLine($"added a NavMeshSurface for {AgentName} (voxel {Voxel})");
            }

            if (!wasDirty || openedHere) { EditorSceneManager.SaveScene(scene); log.AppendLine("saved " + ScenePath); }
            else { log.AppendLine("RoguelikeScene had unsaved changes: surface added but NOT saved. Save the scene yourself."); }
        }
        finally
        {
            if (openedHere) { EditorSceneManager.CloseScene(scene, true); }
        }
    }

    private static void SetUpBrute(int id, StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BrutePath);
        try
        {
            NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.agentTypeID = id;
                agent.radius = Radius;
            }
            BruteGrab grab = root.GetComponent<BruteGrab>();
            if (grab != null)
            {
                grab.holdDistance = 0.7f;
                grab.liftHeight = 0.9f;
            }
            PrefabUtility.SaveAsPrefabAsset(root, BrutePath);
            log.AppendLine($"Enemy_Brute: agent {AgentName} radius {Radius}; hug 0.7 m in front, lifted 0.9 m");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
