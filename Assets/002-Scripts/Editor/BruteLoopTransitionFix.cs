using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One-shot fix for Enemy_Brute_0Blood.controller (the 33Blood / 67Blood override controllers share it):
/// Stunned and Kicked were entered from Any State while IsStunned / IsKicked stayed true, so as soon as the
/// controller moved on to Loop Stunned / Loop Kicked, Any State sent it straight back to Stunned / Kicked.
/// Now they're entered by direct transitions from every other state except their own loop, so the loop holds
/// until the stun / knockdown ends. Re-run from Tools > Enemies > Fix Brute Loop Transitions (safe to repeat).
/// </summary>
public static class BruteLoopTransitionFix
{
    private const string AutoKey = "BruteLoopTransitionFix_v2";
    private const string ControllerPath = "Assets/004-Animations/Enemies/Enemy_Brute/Enemy_Brute_0Blood.controller";

    private static readonly (string enter, string loop, string param)[] Pairs =
    {
        ("Stunned", "Loop Stunned", "IsStunned"),
        ("Kicked", "Loop Kicked", "IsKicked"),
    };

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            Fix();
        };
    }

    [MenuItem("Tools/Enemies/Fix Brute Loop Transitions")]
    public static void Fix()
    {
        var log = new StringBuilder();
        int errors = 0;
        try
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { throw new System.Exception("missing " + ControllerPath); }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState[] states = sm.states.Select(s => s.state).ToArray();

            foreach (var (enterName, loopName, param) in Pairs)
            {
                AnimatorState enter = states.FirstOrDefault(s => s.name == enterName);
                AnimatorState loop = states.FirstOrDefault(s => s.name == loopName);
                if (enter == null || loop == null) { log.AppendLine($"!! missing {enterName} / {loopName}"); errors++; continue; }

                // 1. Remove the Any State entry into this pair
                foreach (AnimatorStateTransition t in sm.anyStateTransitions.Where(t => t.destinationState == enter || t.destinationState == loop).ToArray())
                {
                    string target = t.destinationState.name; // read before the transition is destroyed
                    sm.RemoveAnyStateTransition(t);
                    log.AppendLine($"removed Any State -> {target}");
                }

                // 2. Enter from every other state (not from inside the pair itself)
                int added = 0;
                foreach (AnimatorState from in states)
                {
                    if (from == enter || from == loop) { continue; }
                    bool exists = from.transitions.Any(t => t.destinationState == enter && t.conditions.Any(c => c.parameter == param && c.mode == AnimatorConditionMode.If));
                    if (exists) { continue; }

                    AnimatorStateTransition t = from.AddTransition(enter);
                    t.AddCondition(AnimatorConditionMode.If, 0f, param);
                    t.hasExitTime = false;
                    t.duration = 0f;
                    added++;
                }

                // 3. The first clip still flows into its loop when it finishes
                if (!enter.transitions.Any(t => t.destinationState == loop))
                {
                    AnimatorStateTransition chain = enter.AddTransition(loop);
                    chain.hasExitTime = true;
                    chain.exitTime = 1f;
                    chain.duration = 0f;
                }

                log.AppendLine($"{enterName}: entered from {added} state(s); {enterName} -> {loopName} on clip end; {loopName} holds until {param} is off");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/brute_loop_fix.txt", log.ToString());
        Debug.Log($"[BruteLoopFix] Done with {errors} error(s). Report: Library/PrisonDecor/brute_loop_fix.txt");
    }
}
