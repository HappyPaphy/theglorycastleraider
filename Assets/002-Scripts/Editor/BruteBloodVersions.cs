using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// The Brute's three HP stages, switched by the enemy's HP like the Soldier's:
///   above 67% HP  -> Enemy_Brute_0Blood.controller   (the main controller; its Brute_ clips are the ones you edit)
///   67% - 34%     -> Enemy_Brute_33Blood.overrideController
///   below 34%     -> Enemy_Brute_67Blood.overrideController
/// The 33Blood / 67Blood clips are GENERATED from the 0Blood clips: every PixelArt-Brute_0Blood_... frame is
/// swapped for the same-numbered frame of the 33Blood / 67Blood sheet. Only key the 0Blood clips, then run
/// Tools > Enemies > Sync Brute Blood Versions (it also runs once automatically).
/// First run also keys the 0Blood clips whose rows are known (Idle, Run, PreAttack, Stunned, Loop Stunned, Died, Executed).
/// </summary>
public static class BruteBloodVersions
{
    private const string AutoKey = "BruteBloodVersions_v1";
    private const string Folder = "Assets/004-Animations/Enemies/Enemy_Brute";
    private const string OldControllerPath = Folder + "/Enemy_Brute.controller";
    private const string BaseControllerPath = Folder + "/Enemy_Brute_0Blood.controller";
    private const string SpriteFolder = "Assets/001-Sprite/001A-MyAsset/Character/Brute";
    private const string BrutePath = "Assets/003-Prefabs/Enemy/Enemy_Brute.prefab";
    private const float FrameTime = 1f / 12f; // 12 fps, like the Soldier's run / attacks

    private static readonly string[] Stages = { "33Blood", "67Blood" };

    // Rows that are clear from the sheets: clip -> (sheet set, first frame, frame count)
    private static readonly (string clip, string set, int first, int count)[] KnownRows =
    {
        ("Brute_Idle", "SetA", 0, 8),
        ("Brute_Run", "SetA", 8, 8),
        ("Brute_PreAttack", "SetA", 16, 6),
        ("Brute_Stunned", "SetB", 12, 4),
        ("Brute_LoopStunned", "SetB", 16, 4),
        ("Brute_Died", "SetB", 20, 4),
        ("Brute_Executed", "SetB", 24, 4),
    };

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            Run(keyKnownRows: true);
        };
    }

    [MenuItem("Tools/Enemies/Sync Brute Blood Versions")]
    public static void SyncMenu() => Run(keyKnownRows: false);

    private static void Run(bool keyKnownRows)
    {
        var log = new StringBuilder();
        int errors = 0;
        try
        {
            AnimatorController baseController = BaseController(log);
            if (keyKnownRows) { KeyKnownRows(log); }

            var overrides = new Dictionary<string, AnimatorOverrideController>();
            foreach (string stage in Stages) { overrides[stage] = BuildStage(baseController, stage, log); }

            HookPrefab(baseController, overrides["33Blood"], overrides["67Blood"], log);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/brute_blood.txt", log.ToString());
        Debug.Log($"[BruteBlood] Done with {errors} error(s). Report: Library/PrisonDecor/brute_blood.txt");
    }

    // The existing Brute controller becomes the 0Blood one (renamed, so every reference to it stays)
    private static AnimatorController BaseController(StringBuilder log)
    {
        AnimatorController c = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
        if (c != null) { return c; }

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OldControllerPath) == null)
        {
            throw new System.Exception("missing " + OldControllerPath + " (run Tools > Enemies > Set Up Brute Animator first)");
        }
        string error = AssetDatabase.RenameAsset(OldControllerPath, "Enemy_Brute_0Blood");
        if (!string.IsNullOrEmpty(error)) { throw new System.Exception(error); }
        log.AppendLine("renamed Enemy_Brute.controller -> Enemy_Brute_0Blood.controller");
        return AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
    }

    private static void KeyKnownRows(StringBuilder log)
    {
        foreach (var (clipName, set, first, count) in KnownRows)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Folder}/{clipName}.anim");
            if (clip == null) { log.AppendLine("!! missing clip " + clipName); continue; }
            if (UsesBruteSprites(clip)) { log.AppendLine($"kept {clipName} (already has Brute frames)"); continue; }

            Dictionary<string, Sprite> sheet = Sheet("0Blood", set);
            var frames = new List<Sprite>();
            for (int i = first; i < first + count; i++)
            {
                if (sheet.TryGetValue($"PixelArt-Brute_0Blood_{set}_{i}", out Sprite s)) { frames.Add(s); }
            }
            if (frames.Count != count) { log.AppendLine($"!! {clipName}: found {frames.Count}/{count} frames"); continue; }

            SetFrames(clip, frames);
            log.AppendLine($"keyed {clipName}: 0Blood {set} frames {first}-{first + count - 1}");
        }
    }

    private static AnimatorOverrideController BuildStage(AnimatorController baseController, string stage, StringBuilder log)
    {
        string stageFolder = $"{Folder}/{stage}";
        if (!AssetDatabase.IsValidFolder(stageFolder)) { AssetDatabase.CreateFolder(Folder, stage); }

        string overridePath = $"{Folder}/Enemy_Brute_{stage}.overrideController";
        AnimatorOverrideController oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
        if (oc == null)
        {
            oc = new AnimatorOverrideController(baseController) { name = $"Enemy_Brute_{stage}" };
            AssetDatabase.CreateAsset(oc, overridePath);
            log.AppendLine("created " + overridePath);
        }

        var sheets = new Dictionary<string, Dictionary<string, Sprite>> { { "SetA", Sheet(stage, "SetA") }, { "SetB", Sheet(stage, "SetB") } };

        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(oc.overridesCount);
        oc.GetOverrides(pairs);
        int swapped = 0;
        for (int i = 0; i < pairs.Count; i++)
        {
            AnimationClip source = pairs[i].Key;
            string targetPath = $"{stageFolder}/{source.name.Replace("Brute_", $"Brute{stage.Replace("Blood", "")}_")}.anim";
            AnimationClip target = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
            if (target == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), targetPath);
                AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
                target = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, target); // refresh from the 0Blood clip
                target.name = Path.GetFileNameWithoutExtension(targetPath);
            }

            swapped += Remap(target, stage, sheets);
            EditorUtility.SetDirty(target);
            pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(source, target);
        }
        oc.ApplyOverrides(pairs);
        EditorUtility.SetDirty(oc);
        log.AppendLine($"{stage}: {pairs.Count} clips synced, {swapped} frame(s) swapped to the {stage} sheet");
        return oc;
    }

    // Swap every 0Blood frame in the clip for the same-numbered frame of this stage's sheet
    private static int Remap(AnimationClip clip, string stage, Dictionary<string, Dictionary<string, Sprite>> sheets)
    {
        int swapped = 0;
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            for (int k = 0; k < keys.Length; k++)
            {
                if (!(keys[k].value is Sprite s) || !s.name.StartsWith("PixelArt-Brute_0Blood_")) { continue; }
                string newName = s.name.Replace("_0Blood_", $"_{stage}_");
                string set = s.name.Contains("_SetB_") ? "SetB" : "SetA";
                if (sheets[set].TryGetValue(newName, out Sprite replacement)) { keys[k].value = replacement; swapped++; }
            }
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        }
        return swapped;
    }

    private static void SetFrames(AnimationClip clip, List<Sprite> frames)
    {
        EditorCurveBinding binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .FirstOrDefault(b => b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite");
        if (binding.propertyName == null)
        {
            binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        }

        // One key per frame, plus the last frame again so it is shown for a full frame
        var keys = new ObjectReferenceKeyframe[frames.Count + 1];
        for (int i = 0; i < frames.Count; i++) { keys[i] = new ObjectReferenceKeyframe { time = i * FrameTime, value = frames[i] }; }
        keys[frames.Count] = new ObjectReferenceKeyframe { time = frames.Count * FrameTime, value = frames[frames.Count - 1] };

        clip.frameRate = 1f / FrameTime;
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        EditorUtility.SetDirty(clip);
    }

    private static bool UsesBruteSprites(AnimationClip clip)
    {
        foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            foreach (ObjectReferenceKeyframe k in AnimationUtility.GetObjectReferenceCurve(clip, b))
            {
                if (k.value is Sprite s && s.name.StartsWith("PixelArt-Brute_")) { return true; }
            }
        }
        return false;
    }

    private static Dictionary<string, Sprite> Sheet(string stage, string set)
    {
        string path = $"{SpriteFolder}/PixelArt-Brute_{stage}_{set}.png";
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name, s => s);
    }

    private static void HookPrefab(AnimatorController full, AnimatorOverrideController blood33, AnimatorOverrideController blood67, StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BrutePath);
        try
        {
            EnemyThief thief = root.GetComponent<EnemyThief>();
            var so = new SerializedObject(thief);
            so.FindProperty("animationController_100HP").objectReferenceValue = full;
            so.FindProperty("animationController_67HP").objectReferenceValue = blood33;
            so.FindProperty("animationController_34HP").objectReferenceValue = blood67;
            so.ApplyModifiedPropertiesWithoutUndo();

            Animator anim = new SerializedObject(thief).FindProperty("anim").objectReferenceValue as Animator;
            if (anim != null) { anim.runtimeAnimatorController = full; }

            PrefabUtility.SaveAsPrefabAsset(root, BrutePath);
            log.AppendLine("Enemy_Brute: 0Blood above 67% HP, 33Blood 67-34%, 67Blood below 34%");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
