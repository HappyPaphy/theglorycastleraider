using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: gives the dagger its own animation clips.
///  - Copies the sword's one-handed clips into HandDagger-OneHanded/ as Dagger_Idle, Dagger_Press1-3,
///    Dagger_Block and Dagger_Execute (starting point: same sprites as the sword).
///  - Speeds up the three attack clips (the dagger attacks every 0.2 s instead of 0.3 s).
///  - Plugs them into HandDagger-OneHanded.overrideController.
/// Then open each Dagger_ clip in the Animation window and replace the sprite keys with your dagger frames.
/// Existing dagger clips are never overwritten. Re-run from Tools > Weapons > Set Up Dagger Animator.
/// </summary>
public static class DaggerAnimatorSetup
{
    private const string AutoKey = "DaggerAnimatorSetup_v1";
    private const string SwordFolder = "Assets/004-Animations/Hands/HandSword-OneHanded";
    private const string DaggerFolder = "Assets/004-Animations/Hands/HandDagger-OneHanded";
    private const string OverridePath = DaggerFolder + "/HandDagger-OneHanded.overrideController";

    // Attack clips play this much faster than the sword's (0.2 s cooldown vs 0.3 s)
    private const float AttackSpeedUp = 0.65f;

    // sword clip -> dagger clip, and whether it's an attack to speed up
    private static readonly (string sword, string dagger, bool attack)[] Clips =
    {
        ("001_HandSword_Idle", "Dagger_Idle", false),
        ("003_HandSword_Press1", "Dagger_Press1", true),
        ("004_HandSword_Press2", "Dagger_Press2", true),
        ("005_HandSword_Press3", "Dagger_Press3", true),
        ("006_HandSword_Block", "Dagger_Block", false),
        ("007_HandSword_Execute", "Dagger_Execute", false),
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

    [MenuItem("Tools/Weapons/Set Up Dagger Animator")]
    public static void Build()
    {
        var log = new StringBuilder();
        int errors = 0;

        AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
        if (controller == null)
        {
            Debug.LogError("[DaggerAnimator] Override controller missing. Run Tools > Weapons > Set Up Dagger first.");
            return;
        }

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(controller.overridesCount);
        controller.GetOverrides(overrides);

        foreach (var (swordName, daggerName, attack) in Clips)
        {
            try
            {
                string swordPath = $"{SwordFolder}/{swordName}.anim";
                string daggerPath = $"{DaggerFolder}/{daggerName}.anim";

                AnimationClip swordClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(swordPath);
                if (swordClip == null) { log.AppendLine("!! missing " + swordPath); errors++; continue; }

                AnimationClip daggerClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(daggerPath);
                if (daggerClip == null)
                {
                    if (!AssetDatabase.CopyAsset(swordPath, daggerPath)) { log.AppendLine("!! copy failed " + daggerPath); errors++; continue; }
                    AssetDatabase.ImportAsset(daggerPath, ImportAssetOptions.ForceSynchronousImport);
                    daggerClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(daggerPath);

                    if (attack) { Retime(daggerClip, AttackSpeedUp); }
                    log.AppendLine($"created {daggerName} ({daggerClip.length:0.###} s)");
                }
                else
                {
                    log.AppendLine($"kept {daggerName}");
                }

                // Point the sword clip's slot at the dagger clip
                for (int i = 0; i < overrides.Count; i++)
                {
                    if (overrides[i].Key == swordClip) { overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(swordClip, daggerClip); }
                }
            }
            catch (System.Exception e)
            {
                errors++;
                log.AppendLine("!! " + daggerName + ": " + e);
            }
        }

        controller.ApplyOverrides(overrides);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/dagger_animator.txt", log.ToString());
        Debug.Log($"[DaggerAnimator] Done with {errors} error(s). Report: Library/PrisonDecor/dagger_animator.txt");
    }

    // Squeeze all sprite keys (and any float curves) of the clip in time
    private static void Retime(AnimationClip clip, float factor)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            for (int i = 0; i < keys.Length; i++) { keys[i].time *= factor; }
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].time *= factor;
                keys[i].inTangent /= factor;
                keys[i].outTangent /= factor;
            }
            curve.keys = keys;
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        EditorUtility.SetDirty(clip);
    }
}
