using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One-shot: the Brute's own animator (Assets/004-Animations/Enemies/Enemy_Brute).
/// 15 states driven by bools, like the Soldier's controller:
///   Idle, Run, PreAttack, Attack, Kicked -> Loop Kicked, Block Kicked, Block Sword (Left/Right),
///   Damaged Left/Right, Stunned -> Loop Stunned, Died, Executed.
/// Each state starts with a copy of the closest Soldier clip (placeholder): open the Brute_ clips and
/// replace the sprite keys with your Brute frames. Existing clips are never overwritten.
/// Also points Enemy_Brute at the controller and turns off the Soldier's blood overlays on it.
/// Re-run from Tools > Enemies > Set Up Brute Animator.
/// </summary>
public static class BruteAnimatorSetup
{
    private const string AutoKey = "BruteAnimatorSetup_v1";
    private const string Folder = "Assets/004-Animations/Enemies/Enemy_Brute";
    private const string ControllerPath = Folder + "/Enemy_Brute_0Blood.controller"; // renamed by BruteBloodVersions
    private const string SoldierClips = "Assets/004-Animations/Enemies/Enemy_Soldier/Enemy_Soldier_Front/Enemy_Soldier_Front_100%";
    private const string BrutePath = "Assets/003-Prefabs/Enemy/Enemy_Brute.prefab";

    // state name, bool parameter (null = reached by an automatic transition), placeholder clip, loops
    private static readonly (string state, string param, string soldierClip, bool loop)[] States =
    {
        ("Idle", "IsIdle", "001-Soldier_Idle", true),
        ("Run", "IsRun", "002-Soldier_Run", true),
        ("PreAttack", "IsPreAttack", "003-Soldier_Attack1", true),
        ("Attack", "IsAttack", "003-Soldier_Attack2", true),
        ("Kicked", "IsKicked", "008-Soldier_Kicked", false),
        ("Loop Kicked", null, "008-Soldier_Kicked", true),
        ("Block Kicked", "IsBlockKick", "004-Soldier_BlockLeft", true),
        ("Block Sword (Left)", "IsBlockLeft", "004-Soldier_BlockLeft", false),
        ("Block Sword (Right)", "IsBlockRight", "005-Soldier_BlockRight", false),
        ("Damaged Left", "IsDamageLeft", "006-Soldier_DamageLeft", false),
        ("Damaged Right", "IsDamageRight", "007-Soldier_DamageRight", false),
        ("Stunned", "IsStunned", "010-Soldier_Stunned", false),
        ("Loop Stunned", null, "010-Soldier_Stunned", true),
        ("Died", "IsDied", "012-Soldier_Died", false),
        ("Executed", "IsExecuted", "011-Soldier_Executed", false),
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

    [MenuItem("Tools/Enemies/Set Up Brute Animator")]
    public static void Build()
    {
        var log = new StringBuilder();
        int errors = 0;
        try
        {
            EnsureFolder(Folder);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) ?? BuildController(log);
            SetUpPrefab(controller, log);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/brute_animator.txt", log.ToString());
        Debug.Log($"[BruteAnimator] Done with {errors} error(s). Report: Library/PrisonDecor/brute_animator.txt");
    }

    private static AnimatorController BuildController(StringBuilder log)
    {
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState kicked = null, loopKicked = null, stunned = null, loopStunned = null;
        foreach (var (stateName, param, soldierClip, loop) in States)
        {
            AnimationClip clip = CopyClip(stateName, soldierClip, loop, log);
            AnimatorState state = sm.AddState(stateName);
            state.motion = clip;

            if (stateName == "Idle") { sm.defaultState = state; }
            if (stateName == "Kicked") { kicked = state; }
            if (stateName == "Loop Kicked") { loopKicked = state; }
            if (stateName == "Stunned") { stunned = state; }
            if (stateName == "Loop Stunned") { loopStunned = state; }

            if (param == null) { continue; }
            if (!HasParam(controller, param)) { controller.AddParameter(param, AnimatorControllerParameterType.Bool); }

            AnimatorStateTransition t = sm.AddAnyStateTransition(state);
            t.AddCondition(AnimatorConditionMode.If, 0f, param);
            t.hasExitTime = false;
            t.duration = 0f;
            t.canTransitionToSelf = false;
        }

        // The "(Loop)" follow-ups play right after the first clip ends
        Chain(kicked, loopKicked);
        Chain(stunned, loopStunned);

        log.AppendLine("created " + ControllerPath + " with " + States.Length + " states");
        return controller;
    }

    private static void Chain(AnimatorState from, AnimatorState to)
    {
        if (from == null || to == null) { return; }
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 1f;
        t.duration = 0f;
    }

    private static AnimationClip CopyClip(string stateName, string soldierClip, bool loop, StringBuilder log)
    {
        string target = $"{Folder}/Brute_{stateName.Replace("(", "").Replace(")", "").Replace(" ", "")}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(target);
        if (clip == null)
        {
            string source = $"{SoldierClips}/{soldierClip}.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(source) != null && AssetDatabase.CopyAsset(source, target))
            {
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(target);
            }
            else
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, target);
                log.AppendLine("!! no Soldier clip " + source + ": made an empty " + target);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            log.AppendLine($"  {Path.GetFileName(target)} (from {soldierClip}{(loop ? ", loops" : "")})");
        }
        return clip;
    }

    private static void SetUpPrefab(AnimatorController controller, StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BrutePath);
        try
        {
            EnemyThief thief = root.GetComponent<EnemyThief>();
            var so = new SerializedObject(thief);

            Animator anim = so.FindProperty("anim")?.objectReferenceValue as Animator;
            if (anim != null) { anim.runtimeAnimatorController = controller; }
            else { log.AppendLine("!! no animator on the Brute"); }

            // The Soldier's HP-stage controllers and blood overlays don't fit the Brute
            foreach (string field in new[] { "animationController_34HP", "animationController_67HP", "animationController_100HP", "animGore34", "animGore67" })
            {
                SerializedProperty p = so.FindProperty(field);
                if (p != null) { p.objectReferenceValue = null; }
            }
            foreach (string field in new[] { "goreSprite_34HP", "goreSprite_67HP" })
            {
                SerializedProperty p = so.FindProperty(field);
                if (p != null && p.objectReferenceValue is SpriteRenderer gore) { gore.gameObject.SetActive(false); }
                if (p != null) { p.objectReferenceValue = null; }
            }

            // Animation list behaviour: blocks swords like the Soldier, sits for a while when kicked in the wind-up
            SetInt(so, "maxHitsBeforeBlock", 2);
            SetFloat(so, "kickRecoveryTime", 2.5f);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BrutePath);
            log.AppendLine("Enemy_Brute: uses Enemy_Brute.controller, blood overlays off, blocks swords, kicked sit 2.5 s");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetInt(SerializedObject so, string field, int value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.intValue = value; }
    }

    private static void SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.floatValue = value; }
    }

    private static bool HasParam(AnimatorController c, string name)
    {
        foreach (var p in c.parameters) { if (p.name == name) { return true; } }
        return false;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) { return; }
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
