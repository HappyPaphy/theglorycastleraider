using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds Assets/003-Prefabs/EnergyOrb.prefab and sets up the energy orb drop table on ItemBox.
/// Runs automatically once per project, or via Tools > Energy Orb.
/// </summary>
public static class EnergyOrbPrefabBuilder
{
    private const string PrefabPath = "Assets/003-Prefabs/EnergyOrb.prefab";
    private const string ItemBoxPath = "Assets/003-Prefabs/ItemBox.prefab";
    private const string ParticleMaterialPath = "Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Materials/Embers.mat";
    private const string PromptSourcePrefabPath = "Assets/003-Prefabs/Items/Consumables/HealthPotion_Small.prefab";

    private const string CommonOrbPath = "Assets/013-Datas/Perks/Energy Orbs/EnergyOrb_Common.asset";
    private const string RareOrbPath = "Assets/013-Datas/Perks/Energy Orbs/EnergyOrb_Rare.asset";
    private const string UniqueOrbPath = "Assets/013-Datas/Perks/Energy Orbs/EnergyOrb_Unique.asset";

    // Default ItemBox chances (%). One roll, so 24% total chance of any orb.
    private const float CommonChance = 15f;
    private const float RareChance = 7f;
    private const float UniqueChance = 2f;

    private static string AutoBuildKey => "EnergyOrbPrefabBuilder.AutoBuilt." + Application.dataPath;

    [InitializeOnLoadMethod]
    private static void AutoBuildOnce()
    {
        if (EditorPrefs.GetBool(AutoBuildKey, false)) { return; }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }

            EditorPrefs.SetBool(AutoBuildKey, true);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) { BuildPrefab(); }
            SetUpItemBoxDrops(false);
        };
    }

    [MenuItem("Tools/Energy Orb/Rebuild Energy Orb Prefab")]
    private static void RebuildMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog("Rebuild Energy Orb",
                $"Overwrite {PrefabPath}? Any manual changes to the prefab will be lost.", "Overwrite", "Cancel"))
        {
            return;
        }

        BuildPrefab();
    }

    [MenuItem("Tools/Energy Orb/Set Up ItemBox Orb Drops")]
    private static void SetUpItemBoxMenu()
    {
        SetUpItemBoxDrops(true);
    }

    // =========================================================
    // PREFAB
    // =========================================================

    private static void BuildPrefab()
    {
        Material particleMat = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        if (particleMat == null)
        {
            Debug.LogWarning($"[EnergyOrbPrefabBuilder] Particle material not found at {ParticleMaterialPath}. Assign one on the particle renderers.");
        }

        GameObject root = new GameObject("EnergyOrb");
        EnergyOrbPickup pickup = root.AddComponent<EnergyOrbPickup>();

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        ParticleSystem core = CreateCore(visual.transform, particleMat);
        ParticleSystem sparks = CreateOrbitSparks(visual.transform, particleMat);
        ParticleSystem embers = CreateRisingEmbers(visual.transform, particleMat);

        GameObject lightGo = new GameObject("Glow Light");
        lightGo.transform.SetParent(visual.transform, false);
        Light glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = 2.5f;
        glow.intensity = 1.5f;
        glow.shadows = LightShadows.None;

        GameObject prompt = CopyButtonPrompt(root.transform, out int playerLayerMask);

        SerializedObject so = new SerializedObject(pickup);
        so.FindProperty("visualRoot").objectReferenceValue = visual.transform;
        so.FindProperty("orbLight").objectReferenceValue = glow;
        so.FindProperty("uiButtonPrompt").objectReferenceValue = prompt;
        so.FindProperty("playerLayer").intValue = playerLayerMask;

        SerializedProperty particles = so.FindProperty("tintedParticles");
        ParticleSystem[] systems = { core, sparks, embers };
        particles.arraySize = systems.Length;
        for (int i = 0; i < systems.Length; i++)
        {
            particles.GetArrayElementAtIndex(i).objectReferenceValue = systems[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        // Item layer, like the other pickups, so the renderer sorts it by distance together
        // with enemy/player sprites instead of drawing it in an earlier pass behind them
        int itemLayer = LayerMask.NameToLayer("Item");
        if (itemLayer >= 0) { SetLayerRecursive(root, itemLayer); }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        Debug.Log($"[EnergyOrbPrefabBuilder] Created {PrefabPath}");
    }

    private static ParticleSystem CreateCore(Transform parent, Material mat)
    {
        ParticleSystem ps = CreateParticleSystem("Core", parent, mat);

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = 0.5f;
        main.startSpeed = 0f;
        main.startSize = 0.55f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 20;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 14f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        SetFadeInOut(ps);

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.8f)));

        return ps;
    }

    private static ParticleSystem CreateOrbitSparks(Transform parent, Material mat)
    {
        ParticleSystem ps = CreateParticleSystem("Orbit Sparks", parent, mat);

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 60;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 30f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;
        shape.radiusThickness = 0f;

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalX = 0f;
        velocity.orbitalY = 3f;
        velocity.orbitalZ = 0f;
        velocity.radial = -0.15f;

        SetFadeInOut(ps);
        return ps;
    }

    private static ParticleSystem CreateRisingEmbers(Transform parent, Material mat)
    {
        ParticleSystem ps = CreateParticleSystem("Rising Embers", parent, mat);

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 8f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        // All three axes must use the same curve mode
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        SetFadeInOut(ps);
        return ps;
    }

    private static ParticleSystem CreateParticleSystem(string name, Transform parent, Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startColor = Color.white;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (mat != null) { renderer.sharedMaterial = mat; }

        return ps;
    }

    private static void SetFadeInOut(ParticleSystem ps)
    {
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        col.color = gradient;
    }

    // Reuse the same world-space Interact prompt the item pickups use
    private static GameObject CopyButtonPrompt(Transform parent, out int playerLayerMask)
    {
        playerLayerMask = 1 << 6;

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PromptSourcePrefabPath);
        if (source == null)
        {
            Debug.LogWarning($"[EnergyOrbPrefabBuilder] {PromptSourcePrefabPath} not found, no button prompt added.");
            return null;
        }

        Item sourceItem = source.GetComponentInChildren<Item>(true);
        if (sourceItem != null)
        {
            SerializedObject itemSo = new SerializedObject(sourceItem);
            playerLayerMask = itemSo.FindProperty("playerLayer").intValue;
        }

        Transform sourcePrompt = FindChildRecursive(source.transform, "UIButtonPrompt");
        if (sourcePrompt == null)
        {
            Debug.LogWarning("[EnergyOrbPrefabBuilder] UIButtonPrompt not found on the potion prefab, no button prompt added.");
            return null;
        }

        GameObject prompt = Object.Instantiate(sourcePrompt.gameObject, parent, false);
        prompt.name = "UIButtonPrompt";
        prompt.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        prompt.SetActive(true);
        return prompt;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) { SetLayerRecursive(child.gameObject, layer); }
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) { return child; }
            Transform found = FindChildRecursive(child, name);
            if (found != null) { return found; }
        }
        return null;
    }

    // =========================================================
    // ITEM BOX
    // =========================================================

    private static void SetUpItemBoxDrops(bool overwrite)
    {
        EnergyOrbPickup orbPrefab = AssetDatabase.LoadAssetAtPath<EnergyOrbPickup>(PrefabPath);
        if (orbPrefab == null || AssetDatabase.LoadAssetAtPath<GameObject>(ItemBoxPath) == null) { return; }

        GameObject contents = PrefabUtility.LoadPrefabContents(ItemBoxPath);
        try
        {
            DropItemOnDead dropper = contents.GetComponentInChildren<DropItemOnDead>(true);
            if (dropper == null)
            {
                Debug.LogWarning("[EnergyOrbPrefabBuilder] ItemBox has no DropItemOnDead component.");
                return;
            }

            SerializedObject so = new SerializedObject(dropper);
            SerializedProperty prefabProp = so.FindProperty("energyOrbPrefab");
            SerializedProperty drops = so.FindProperty("energyOrbDrops");

            if (!overwrite && (prefabProp.objectReferenceValue != null || drops.arraySize > 0)) { return; }

            prefabProp.objectReferenceValue = orbPrefab;

            (string path, float chance)[] table =
            {
                (CommonOrbPath, CommonChance),
                (RareOrbPath, RareChance),
                (UniqueOrbPath, UniqueChance),
            };

            drops.arraySize = table.Length;
            for (int i = 0; i < table.Length; i++)
            {
                SerializedProperty entry = drops.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("orb").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnergyOrbDefinition>(table[i].path);
                entry.FindPropertyRelative("dropChance").floatValue = table[i].chance;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, ItemBoxPath);
            Debug.Log($"[EnergyOrbPrefabBuilder] Set up energy orb drops on {ItemBoxPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
