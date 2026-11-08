using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

/// <summary>
/// Builds extra breakable furniture prefabs out of ProBuilder primitives.
/// Every piece is one ProBuilder mesh (the Fracture component slices a single mesh) and gets the exact
/// component setup of WoodenChair: Rigidbody, Fracture, MeshCollider, FractureTrigger, NavMesh modifier/obstacle,
/// DunGen GlobalProp and DropItemOnDead.
/// Pivot = bottom centre. Wall pieces have their back at +Z (same rule as WoodenCabinet).
/// </summary>
public static class ProBuilderFurnitureBuilder
{
    public const string OutputFolder = "Assets/003-Prefabs/Furniture";
    private const string TemplatePath = "Assets/003-Prefabs/WoodenChair.prefab";
    private const string WoodMaterialPath = "Assets/006-Materials/Environment/Door/WoodenPlank-1x1 Unlit.mat";
    private const string ClayMaterialPath = "Assets/006-Materials/Mat_Clay.mat";
    private const string ClaySourceMaterialPath = "Assets/006-Materials/Mat_Brown.mat";

    private const int MaterialTypeWood = 1; // FractureTrigger.MaterialType.Wood
    private const int MaterialTypeVase = 2; // FractureTrigger.MaterialType.Vase

    public static readonly string[] PrefabNames =
    {
        "Barrel", "Bench", "Stool", "Shelf", "PrisonBed", "WeaponRack", "Pillory", "ClayPot_Tall", "ClayPot_Small",
    };

    private static string AutoKey => "ProBuilderFurnitureBuilder.Built.v4." + Application.dataPath;

    [InitializeOnLoadMethod]
    private static void AutoBuildOnce()
    {
        if (EditorPrefs.GetBool(AutoKey, false)) { return; }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey, true);
            BuildAll();
            PrisonRoomDecorator.ApplyPlan();
        };
    }

    [MenuItem("Tools/Prison Decor/Rebuild ProBuilder Furniture")]
    public static void BuildAll()
    {
        uvRng = new System.Random(1234);

        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets/003-Prefabs", "Furniture");
        }

        RemoveSceneLeftovers();

        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
        Material wood = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);
        Material clay = GetOrCreateClayMaterial();

        if (template == null || wood == null)
        {
            Debug.LogError("[ProBuilderFurnitureBuilder] Template prefab or wood material not found.");
            return;
        }

        // Each piece gets its own generated texture atlas + material (Assets/006-Materials/Furniture)
        Material M(string n) => FurnitureMaterialFactory.GetMaterial(n);

        Build("Barrel", Barrel(), M("Barrel"), template, MaterialTypeWood, 8);
        Build("Bench", Bench(), M("Bench"), template, MaterialTypeWood, 6);
        Build("Stool", Stool(), M("Stool"), template, MaterialTypeWood, 5);
        Build("Shelf", Shelf(), M("Shelf"), template, MaterialTypeWood, 10);
        Build("PrisonBed", PrisonBed(), M("PrisonBed"), template, MaterialTypeWood, 10);
        Build("WeaponRack", WeaponRack(), M("WeaponRack"), template, MaterialTypeWood, 8);
        Build("Pillory", Pillory(), M("Pillory"), template, MaterialTypeWood, 8);
        Build("ClayPot_Tall", ClayPot(true), M("ClayPot_Tall"), template, MaterialTypeVase, 7, clay);
        Build("ClayPot_Small", ClayPot(false), M("ClayPot_Small"), template, MaterialTypeVase, 5, clay);

        AssetDatabase.SaveAssets();
        Debug.Log($"[ProBuilderFurnitureBuilder] Built {PrefabNames.Length} furniture prefabs in {OutputFolder}");
    }

    // =========================================================
    // SHAPES (metres, pivot at floor centre)
    // =========================================================

    private delegate void PartList(List<ProBuilderMesh> parts);

    // Slot = which 32x32 quadrant of the piece's texture atlas the part uses (see FurnitureMaterialFactory)
    private static PartList Barrel() => parts =>
    {
        Cyl(parts, 0.32f, 0f, 0.85f, 0);                 // staves
        Cyl(parts, 0.335f, 0.13f, 0.06f, 1);             // lower iron hoop
        Cyl(parts, 0.335f, 0.66f, 0.06f, 1);             // upper iron hoop
        Cyl(parts, 0.3f, 0.85f, 0.02f, 2);               // lid
    };

    private static PartList Bench() => parts =>
    {
        Box(parts, new Vector3(0f, 0.435f, 0f), new Vector3(1.4f, 0.07f, 0.38f), 0);       // seat
        Box(parts, new Vector3(-0.55f, 0.2f, 0f), new Vector3(0.07f, 0.4f, 0.34f), 1);    // legs
        Box(parts, new Vector3(0.55f, 0.2f, 0f), new Vector3(0.07f, 0.4f, 0.34f), 1);
        Box(parts, new Vector3(0f, 0.12f, 0f), new Vector3(1.1f, 0.05f, 0.05f), 1);       // stretcher
    };

    private static PartList Stool() => parts =>
    {
        Box(parts, new Vector3(0f, 0.445f, 0f), new Vector3(0.38f, 0.05f, 0.38f), 0);
        foreach (float x in new[] { -0.15f, 0.15f })
            foreach (float z in new[] { -0.15f, 0.15f })
                Box(parts, new Vector3(x, 0.21f, z), new Vector3(0.05f, 0.42f, 0.05f), 1);
    };

    private static PartList Shelf() => parts =>
    {
        const float back = 0.05f, depth = 0.38f, h = 1.7f;
        float zc = back - depth * 0.5f;
        Box(parts, new Vector3(-0.475f, h * 0.5f, zc), new Vector3(0.05f, h, depth), 0);    // sides
        Box(parts, new Vector3(0.475f, h * 0.5f, zc), new Vector3(0.05f, h, depth), 0);
        Box(parts, new Vector3(0f, h * 0.5f, back - 0.015f), new Vector3(1.0f, h, 0.03f), 1); // back panel
        foreach (float y in new[] { 0.06f, 0.6f, 1.12f })
            Box(parts, new Vector3(0f, y, zc), new Vector3(0.9f, 0.04f, depth - 0.02f), 2);  // shelves
        Box(parts, new Vector3(0f, h - 0.02f, zc), new Vector3(1.0f, 0.04f, depth), 0);       // top
    };

    private static PartList PrisonBed() => parts =>
    {
        const float back = 0.05f, depth = 0.85f, len = 2.0f;
        float zc = back - depth * 0.5f;
        Box(parts, new Vector3(0f, 0.39f, zc), new Vector3(len, 0.08f, depth), 0);                    // bed frame
        Box(parts, new Vector3(0.05f, 0.47f, zc), new Vector3(len - 0.2f, 0.08f, depth - 0.1f), 1);   // straw mattress
        foreach (float x in new[] { -len * 0.5f + 0.05f, len * 0.5f - 0.05f })
            foreach (float z in new[] { back - 0.05f, back - depth + 0.05f })
                Box(parts, new Vector3(x, 0.175f, z), new Vector3(0.08f, 0.35f, 0.08f), 0);          // legs
        Box(parts, new Vector3(-len * 0.5f + 0.04f, 0.42f, zc), new Vector3(0.08f, 0.84f, depth), 2); // headboard
    };

    private static PartList WeaponRack() => parts =>
    {
        const float back = 0.05f;
        Box(parts, new Vector3(0f, 0.03f, back - 0.15f), new Vector3(1.1f, 0.06f, 0.3f), 0);        // base
        Box(parts, new Vector3(-0.52f, 0.6f, back - 0.04f), new Vector3(0.06f, 1.2f, 0.06f), 0);    // uprights
        Box(parts, new Vector3(0.52f, 0.6f, back - 0.04f), new Vector3(0.06f, 1.2f, 0.06f), 0);
        Box(parts, new Vector3(0f, 1.15f, back - 0.04f), new Vector3(1.1f, 0.06f, 0.06f), 0);       // top bar
        Box(parts, new Vector3(0f, 0.55f, back - 0.04f), new Vector3(1.1f, 0.05f, 0.05f), 0);       // middle bar
        foreach (float x in new[] { -0.28f, 0f, 0.28f })
        {
            Box(parts, new Vector3(x, 0.72f, back - 0.12f), new Vector3(0.035f, 1.32f, 0.035f), 1);  // spear shafts
            Box(parts, new Vector3(x, 1.45f, back - 0.12f), new Vector3(0.07f, 0.16f, 0.02f), 2);    // steel spear heads
        }
    };

    private static PartList Pillory() => parts =>
    {
        foreach (float x in new[] { -0.45f, 0.45f })
        {
            Box(parts, new Vector3(x, 0.8f, 0f), new Vector3(0.1f, 1.6f, 0.1f), 0);    // posts
            Box(parts, new Vector3(x, 0.04f, 0f), new Vector3(0.14f, 0.08f, 0.6f), 1); // feet
        }
        Box(parts, new Vector3(0f, 1.2f, 0f), new Vector3(1.0f, 0.12f, 0.09f), 1);    // upper board
        Box(parts, new Vector3(0f, 1.06f, 0f), new Vector3(1.0f, 0.12f, 0.09f), 1);   // lower board
        Box(parts, new Vector3(0f, 1.62f, 0f), new Vector3(1.05f, 0.06f, 0.12f), 2);  // iron cap
    };

    private static PartList ClayPot(bool tall) => parts =>
    {
        if (tall)
        {
            Cyl(parts, 0.17f, 0f, 0.08f, 1);     // foot
            Cyl(parts, 0.26f, 0.08f, 0.45f, 2);  // painted belly
            Cyl(parts, 0.2f, 0.53f, 0.07f, 0);   // shoulder
            Cyl(parts, 0.12f, 0.6f, 0.12f, 1);   // neck
            Cyl(parts, 0.15f, 0.72f, 0.04f, 1);  // lip
        }
        else
        {
            Cyl(parts, 0.14f, 0f, 0.04f, 1);
            Cyl(parts, 0.2f, 0.04f, 0.3f, 2);    // painted body
            Cyl(parts, 0.11f, 0.34f, 0.08f, 1);
            Cyl(parts, 0.13f, 0.42f, 0.03f, 1);
        }
    };

    private static void Box(List<ProBuilderMesh> parts, Vector3 center, Vector3 size, int slot)
    {
        ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
        Offset(pb, center);
        ApplyAtlasUV(pb, slot);
        parts.Add(pb);
    }

    private static void Cyl(List<ProBuilderMesh> parts, float radius, float bottomY, float height, int slot)
    {
        ProBuilderMesh pb = ShapeGenerator.GenerateCylinder(PivotLocation.Center, 10, radius, height, 0, -1);
        Offset(pb, new Vector3(0f, bottomY + height * 0.5f, 0f));
        ApplyAtlasUV(pb, slot);
        parts.Add(pb);
    }

    // Fit every face of the part onto its atlas slot: planar projection along the face normal,
    // with U along the face's longest side so the wood grain runs lengthwise
    private const float PixelsPerMeter = 24f;
    private const float TilePixels = 32f;
    private static System.Random uvRng = new System.Random(1234);

    private static void ApplyAtlasUV(ProBuilderMesh pb, int slot)
    {
        Rect r = FurnitureMaterialFactory.SlotRect(slot);
        IList<Vector3> pos = pb.positions;
        Vector2[] uvs = new Vector2[pb.vertexCount];

        foreach (Face face in pb.faces)
        {
            IList<int> idx = face.distinctIndexes;
            if (idx.Count < 3) { continue; }

            IList<int> tri = face.indexes;
            Vector3 n = Vector3.Cross(pos[tri[1]] - pos[tri[0]], pos[tri[2]] - pos[tri[0]]).normalized;
            Vector3 helper = Mathf.Abs(n.y) > 0.7f ? Vector3.right : Vector3.up;
            Vector3 axisU = Vector3.Cross(n, helper).normalized;
            Vector3 axisV = Vector3.Cross(n, axisU).normalized;

            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (int i in idx)
            {
                float u = Vector3.Dot(pos[i], axisU), v = Vector3.Dot(pos[i], axisV);
                minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
                minV = Mathf.Min(minV, v); maxV = Mathf.Max(maxV, v);
            }

            bool swap = (maxV - minV) > (maxU - minU);
            float lenU = swap ? maxV - minV : maxU - minU;
            float lenV = swap ? maxU - minU : maxV - minV;

            // Fixed texel density like the original furniture (~24px per metre): a face only uses the part
            // of the tile that matches its real size; faces longer than the tile stretch along the grain
            float spanU = Mathf.Clamp01(lenU * PixelsPerMeter / TilePixels);
            float spanV = Mathf.Clamp01(lenV * PixelsPerMeter / TilePixels);
            float offU = (float)uvRng.NextDouble() * (1f - spanU);
            float offV = 0f; // keep row 0 (the dark seam/edge) on the face's first edge

            foreach (int i in idx)
            {
                float u = Mathf.InverseLerp(minU, maxU, Vector3.Dot(pos[i], axisU));
                float v = Mathf.InverseLerp(minV, maxV, Vector3.Dot(pos[i], axisV));
                if (swap) { float t = u; u = v; v = t; }
                u = offU + u * spanU;
                v = offV + v * spanV;
                uvs[i] = new Vector2(r.x + u * r.width, r.y + v * r.height);
            }

            face.manualUV = true;
        }

        pb.textures = uvs;
        pb.ToMesh();
        pb.Refresh();
    }

    // Bake the offset into the vertices so every part's transform stays at the origin (Combine uses transforms)
    private static void Offset(ProBuilderMesh pb, Vector3 offset)
    {
        Vector3[] positions = new Vector3[pb.positions.Count];
        for (int i = 0; i < positions.Length; i++) { positions[i] = pb.positions[i] + offset; }
        pb.positions = positions;
        pb.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        pb.ToMesh();
        pb.Refresh();
    }

    // =========================================================
    // PREFAB
    // =========================================================

    private static void Build(string name, PartList shape, Material material, GameObject template,
        int materialType, int fragmentCount, Material insideMaterial = null)
    {
        List<ProBuilderMesh> parts = new List<ProBuilderMesh>();
        shape(parts);

        ProBuilderMesh target = parts[0];
        if (parts.Count > 1)
        {
            CombineMeshes.Combine(parts, target);
            for (int i = 1; i < parts.Count; i++) { Object.DestroyImmediate(parts[i].gameObject); }
        }

        target.SetMaterial(target.faces, material);
        target.ToMesh();
        target.Refresh();

        GameObject go = target.gameObject;
        try
        {
        go.name = name;
        go.layer = template.layer;
        go.tag = template.tag;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;

        foreach (Component c in go.GetComponents<Component>())
        {
            if (c.GetType().Name == "ProBuilderShape") { Object.DestroyImmediate(c); }
        }

        CopyGameplayComponents(template, go);
        ConfigureComponents(go, go.GetComponent<MeshFilter>().sharedMesh.bounds, materialType, fragmentCount, insideMaterial);

        string path = $"{OutputFolder}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static readonly string[] SkipComponents =
    {
        "Transform", "ProBuilderMesh", "ProBuilderShape", "MeshFilter", "MeshRenderer",
    };

    private static void CopyGameplayComponents(GameObject template, GameObject target)
    {
        Dictionary<Component, Component> map = new Dictionary<Component, Component>();

        foreach (Component src in template.GetComponents<Component>())
        {
            if (src == null || System.Array.IndexOf(SkipComponents, src.GetType().Name) >= 0) { continue; }

            Component dst = target.GetComponent(src.GetType());
            if (dst == null) { dst = target.AddComponent(src.GetType()); }
            if (dst == null)
            {
                Debug.LogWarning($"[ProBuilderFurnitureBuilder] Could not add {src.GetType().Name} to {target.name}");
                continue;
            }

            EditorUtility.CopySerialized(src, dst);
            map[src] = dst;
        }

        // Point Fracture's callbacks (break sound, drops) at this object's components, not the template's
        foreach (Component c in target.GetComponents<Component>())
        {
            if (c == null || c.GetType().Name != "Fracture") { continue; }

            SerializedObject so = new SerializedObject(c);
            SerializedProperty it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.name != "m_Target") { continue; }
                if (it.objectReferenceValue is Component oldTarget && map.TryGetValue(oldTarget, out Component newTarget))
                {
                    it.objectReferenceValue = newTarget;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureComponents(GameObject go, Bounds meshBounds, int materialType, int fragmentCount, Material insideMaterial)
    {
        MeshCollider col = go.GetComponent<MeshCollider>();
        if (col != null) { col.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh; col.convex = true; }

        NavMeshObstacle obstacle = go.GetComponent<NavMeshObstacle>();
        if (obstacle != null)
        {
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = meshBounds.center;
            obstacle.size = meshBounds.size;
        }

        foreach (Component c in go.GetComponents<Component>())
        {
            if (c == null) { continue; }
            string type = c.GetType().Name;

            if (type == "FractureTrigger")
            {
                SerializedObject so = new SerializedObject(c);
                so.FindProperty("materialType").intValue = materialType;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (type == "Fracture")
            {
                SerializedObject so = new SerializedObject(c);
                so.FindProperty("fractureOptions.fragmentCount").intValue = fragmentCount;
                if (insideMaterial != null)
                {
                    so.FindProperty("fractureOptions.insideMaterial").objectReferenceValue = insideMaterial;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    // A failed build can leave its temporary ProBuilder objects in the open scene
    private static void RemoveSceneLeftovers()
    {
        foreach (ProBuilderMesh pb in Object.FindObjectsByType<ProBuilderMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pb == null || pb.transform.parent != null) { continue; }
            if (PrefabUtility.IsPartOfAnyPrefab(pb.gameObject)) { continue; }

            string n = pb.gameObject.name;
            // Only our furniture names, and only ones carrying the copied Fracture component
            if (System.Array.IndexOf(PrefabNames, n) < 0) { continue; }
            bool hasFracture = false;
            foreach (Component c in pb.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == "Fracture") { hasFracture = true; }
            }
            if (!hasFracture) { continue; }

            Debug.Log($"[ProBuilderFurnitureBuilder] Removed leftover '{n}' from scene '{pb.gameObject.scene.name}'");
            Object.DestroyImmediate(pb.gameObject);
        }
    }

    private static Material GetOrCreateClayMaterial()
    {
        Material clay = AssetDatabase.LoadAssetAtPath<Material>(ClayMaterialPath);
        if (clay != null) { return clay; }

        Material source = AssetDatabase.LoadAssetAtPath<Material>(ClaySourceMaterialPath);
        if (source == null) { return AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath); }

        clay = new Material(source) { name = "Mat_Clay" };
        Color terracotta = new Color(0.62f, 0.34f, 0.2f, 1f);
        if (clay.HasProperty("_BaseColor")) { clay.SetColor("_BaseColor", terracotta); }
        if (clay.HasProperty("_Color")) { clay.SetColor("_Color", terracotta); }

        AssetDatabase.CreateAsset(clay, ClayMaterialPath);
        return clay;
    }
}
