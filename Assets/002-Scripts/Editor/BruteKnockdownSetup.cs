using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One-shot for the Brute's knockdown:
///  - Enemy_Brute_0Blood.controller (the 33/67 Blood override controllers share it): new bool IsKickedLoop with an
///    Any State -> Loop Kicked transition, so after a Damaged animation while he's down he goes straight back to
///    sitting without replaying the Kicked fall.
///  - Enemy_Brute: knocked down (vulnerable) for 4 seconds after a wind-up kick.
/// Tools > Enemies > Set Up Brute Knockdown.
/// </summary>
public static class BruteKnockdownSetup
{
    private const string AutoKey = "BruteKnockdownSetup_v1";
    private const string ControllerPath = "Assets/004-Animations/Enemies/Enemy_Brute/Enemy_Brute_0Blood.controller";
    private const string BrutePath = "Assets/003-Prefabs/Enemy/Enemy_Brute.prefab";
    private const float KnockdownSeconds = 4f;

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

    [MenuItem("Tools/Enemies/Set Up Brute Knockdown")]
    public static void Run()
    {
        var log = new StringBuilder();

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) { log.AppendLine("!! missing " + ControllerPath); }
        else
        {
            const string param = "IsKickedLoop";
            if (!controller.parameters.Any(p => p.name == param)) { controller.AddParameter(param, AnimatorControllerParameterType.Bool); log.AppendLine("added parameter " + param); }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState loop = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Loop Kicked");
            if (loop == null) { log.AppendLine("!! no Loop Kicked state"); }
            else if (!sm.anyStateTransitions.Any(t => t.destinationState == loop && t.conditions.Any(c => c.parameter == param)))
            {
                AnimatorStateTransition t = sm.AddAnyStateTransition(loop);
                t.AddCondition(AnimatorConditionMode.If, 0f, param);
                t.hasExitTime = false;
                t.duration = 0f;
                t.canTransitionToSelf = false;
                log.AppendLine("Any State -> Loop Kicked on " + param);
            }
            EditorUtility.SetDirty(controller);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(BrutePath);
        try
        {
            EnemyThief thief = root.GetComponent<EnemyThief>();
            var so = new SerializedObject(thief);
            so.FindProperty("kickRecoveryTime").floatValue = KnockdownSeconds;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BrutePath);
            log.AppendLine($"Enemy_Brute: knocked down for {KnockdownSeconds} s after a wind-up kick");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/brute_knockdown.txt", log.ToString());
        Debug.Log("[BruteKnockdown] Done. Report: Library/PrisonDecor/brute_knockdown.txt");
    }
}
