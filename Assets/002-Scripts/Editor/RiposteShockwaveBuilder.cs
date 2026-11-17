using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds Assets/Resources/VFX/RiposteShockwave.prefab: the blue energy shockwave released by Riposte Echo.
/// The prefab faces +Z (the swing direction) and destroys itself when finished.
/// Runs once automatically, or via Tools > VFX.
/// </summary>
public static class RiposteShockwaveBuilder
{
    private const string Folder = "Assets/Resources/VFX";
    private const string PrefabPath = Folder + "/RiposteShockwave.prefab";
    private const string MaterialPath = "Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Materials/Embers.mat";

    private static readonly Color CoreBlue = new Color(0.55f, 0.85f, 1f, 1f);
    private static readonly Color EnergyBlue = new Color(0.25f, 0.6f, 1f, 1f);
    private static readonly Color DeepBlue = new Color(0.1f, 0.35f, 1f, 1f);
    private static readonly Color Cyan = new Color(0.6f, 1f, 1f, 1f);

    private static string AutoKey => "RiposteShockwaveBuilder.Built.v1." + Application.dataPath;

    [InitializeOnLoadMethod]
    private static void AutoBuildOnce()
    {
        if (EditorPrefs.GetBool(AutoKey, false)) { return; }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey, true);
            Build();
        };
    }

    [MenuItem("Tools/VFX/Rebuild Riposte Shockwave")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) { AssetDatabase.CreateFolder("Assets", "Resources"); }
        if (!AssetDatabase.IsValidFolder(Folder)) { AssetDatabase.CreateFolder("Assets/Resources", "VFX"); }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        // ---------- Root: bright core flash (also owns the lifetime / auto-destroy) ----------
        GameObject root = new GameObject("RiposteShockwave");
        ParticleSystem flash = AddSystem(root, mat, ParticleSystemRenderMode.Billboard);
        {
            var main = flash.main;
            main.duration = 0.9f; // longer than every child's lifetime, so nothing is cut off when it self-destructs
            main.startLifetime = 0.25f;
            main.startSpeed = 0f;
            main.startSize = 2.4f;
            main.startColor = CoreBlue;
            main.stopAction = ParticleSystemStopAction.Destroy;
            Burst(flash, 1);
            var shape = flash.shape; shape.enabled = false;
            SizeOverLife(flash, 0.4f, 1.6f);
            FadeOut(flash, Color.white, CoreBlue);
        }

        // ---------- Ring: horizontal expanding shockwave of streaks ----------
        ParticleSystem ring = AddChildSystem(root, "Ring", mat, ParticleSystemRenderMode.Stretch, Quaternion.Euler(90f, 0f, 0f));
        {
            var main = ring.main;
            main.startLifetime = 0.3f;
            main.startSpeed = 10f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            main.startColor = EnergyBlue;
            Burst(ring, 48);
            var shape = ring.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;
            shape.radiusThickness = 0f;
            SizeOverLife(ring, 1f, 0.3f);
            FadeOut(ring, Cyan, EnergyBlue);
            Stretch(ring, 0.05f, 2f);
        }

        // ---------- Soft wave: slower, fatter ring for the "body" of the wave ----------
        ParticleSystem wave = AddChildSystem(root, "Wave", mat, ParticleSystemRenderMode.Billboard, Quaternion.Euler(90f, 0f, 0f));
        {
            var main = wave.main;
            main.startLifetime = 0.4f;
            main.startSpeed = 5.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startColor = new Color(EnergyBlue.r, EnergyBlue.g, EnergyBlue.b, 0.6f);
            Burst(wave, 32);
            var shape = wave.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.radiusThickness = 0f;
            SizeOverLife(wave, 0.6f, 1.2f);
            FadeOut(wave, EnergyBlue, DeepBlue);
        }

        // ---------- Fan: forward burst in the swing direction ----------
        ParticleSystem fan = AddChildSystem(root, "Fan", mat, ParticleSystemRenderMode.Stretch, Quaternion.identity);
        {
            var main = fan.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 13f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = DeepBlue;
            Burst(fan, 36);
            var shape = fan.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.1f;
            SizeOverLife(fan, 1f, 0.2f);
            FadeOut(fan, Cyan, DeepBlue);
            Stretch(fan, 0.06f, 2.5f);
        }

        // ---------- Motes: lingering cyan sparkles drifting up ----------
        ParticleSystem motes = AddChildSystem(root, "Motes", mat, ParticleSystemRenderMode.Billboard, Quaternion.identity);
        {
            var main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = Cyan;
            main.gravityModifier = -0.4f;
            Burst(motes, 24);
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f;
            FadeOut(motes, Color.white, Cyan);
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[RiposteShockwaveBuilder] Created {PrefabPath}");
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static ParticleSystem AddSystem(GameObject go, Material mat, ParticleSystemRenderMode mode)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = 0.7f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = mode;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (mat != null) { r.sharedMaterial = mat; }

        return ps;
    }

    private static ParticleSystem AddChildSystem(GameObject parent, string name, Material mat, ParticleSystemRenderMode mode, Quaternion localRotation)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localRotation = localRotation;
        go.layer = parent.layer;
        return AddSystem(go, mat, mode);
    }

    private static void Burst(ParticleSystem ps, int count)
    {
        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    private static void SizeOverLife(ParticleSystem ps, float start, float end)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, start, 1f, end));
    }

    private static void FadeOut(ParticleSystem ps, Color from, Color to)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    private static void Stretch(ParticleSystem ps, float velocityScale, float lengthScale)
    {
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        r.velocityScale = velocityScale;
        r.lengthScale = lengthScale;
    }
}
