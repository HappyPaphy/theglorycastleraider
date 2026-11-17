using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

/// <summary>
/// Gives ItemBox (Equipment) and ItemBox (Energy Orb) a treasure chest look.
/// Only the mesh + material change: the boxes stay breakable (Fracture slices the chest mesh) and keep
/// their drops, and every room that already places them keeps working.
///
/// TEXTURE LAYOUT (hand-paintable): every visible side of the chest has its own region on a 128x128 sheet,
/// upright as seen from that side, at 48 px per metre. See <see cref="Regions"/> and the colour guide
/// Tex_ItemChest_UVGuide.png. The sheet may be scaled up (256, 512...) - the layout is proportional.
/// The texture is only generated when it doesn't exist yet, so your painting is never overwritten.
///
/// Re-run from Tools > Shop > Convert Item Boxes To Chests.
/// </summary>
public static class ItemBoxChestConverter
{
    private const string AutoKey = "ItemBoxChestConverter_v5";

    private static readonly string[] BoxPaths =
    {
        "Assets/003-Prefabs/ItemBox (Equipment).prefab",
        "Assets/003-Prefabs/ItemBox (Energy Orb).prefab",
    };

    private const string SourceMaterialPath = "Assets/006-Materials/Furniture/Mat_Shelf.mat";
    private const string MaterialPath = "Assets/006-Materials/Furniture/Mat_ItemChest.mat";
    private const string TextureFolder = "Assets/006-Materials/Furniture/Textures";
    private const string TexturePath = TextureFolder + "/Tex_ItemChest.png";
    private const string GuidePath = TextureFolder + "/Tex_ItemChest_UVGuide.png";
    private const string ShelfTexturePath = TextureFolder + "/T_Shelf.png";
    // Bumped when the layout changes, so an old-layout texture gets replaced once
    private const string LayoutKey = "ItemBoxChestConverter_TextureLayout_v2";

    // Always the original WoodenBox crate (1 m cube, bottom at -0.09), so re-runs don't shrink the chest
    private static readonly Bounds OriginalBoxBounds = new Bounds(new Vector3(0f, 0.41f, 0f), Vector3.one);

    // =========================================================
    // LAYOUT (pixels on a 128x128 sheet, origin bottom-left)
    // =========================================================

    private const int Sheet = 128;

    public enum Region { BodyFront, BodyBack, BodyLeft, BodyRight, LidFront, LidBack, LidLeft, LidRight, LidTop, IronV, IronH, Gold, Hidden }

    // x, y, width, height
    private static readonly Dictionary<Region, RectInt> Regions = new Dictionary<Region, RectInt>
    {
        { Region.BodyFront, new RectInt(0, 0, 46, 25) },   // front of the chest (lock side)
        { Region.BodyBack,  new RectInt(48, 0, 46, 25) },
        { Region.IronV,     new RectInt(96, 0, 8, 48) },   // vertical iron bands
        { Region.Gold,      new RectInt(106, 0, 12, 12) }, // lock plate + clasp
        { Region.BodyLeft,  new RectInt(0, 27, 33, 25) },  // left = your left when facing the front
        { Region.BodyRight, new RectInt(35, 27, 33, 25) },
        { Region.Hidden,    new RectInt(70, 27, 6, 6) },   // faces you never see (under the lid, bottom)
        { Region.IronH,     new RectInt(78, 27, 48, 6) },  // foot rim + lid seam
        { Region.LidFront,  new RectInt(0, 54, 48, 13) },
        { Region.LidBack,   new RectInt(50, 54, 48, 13) },
        { Region.LidLeft,   new RectInt(0, 69, 34, 13) },
        { Region.LidRight,  new RectInt(36, 69, 34, 13) },
        { Region.LidTop,    new RectInt(0, 84, 48, 34) },  // bottom edge of this region = front of the lid
    };

    // Which region each side of a part uses (null = same region on every side)
    private class Mapping
    {
        public Region front, back, left, right, top, bottom;
        public static Mapping All(Region r) => new Mapping { front = r, back = r, left = r, right = r, top = r, bottom = r };
    }

    private static readonly Mapping BodyMap = new Mapping
    {
        front = Region.BodyFront, back = Region.BodyBack, left = Region.BodyLeft, right = Region.BodyRight,
        top = Region.Hidden, bottom = Region.Hidden,
    };

    private static readonly Mapping LidMap = new Mapping
    {
        front = Region.LidFront, back = Region.LidBack, left = Region.LidLeft, right = Region.LidRight,
        top = Region.LidTop, bottom = Region.Hidden,
    };

    // =========================================================
    // ENTRY
    // =========================================================

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            ConvertAll();
        };
    }

    [MenuItem("Tools/Shop/Convert Item Boxes To Chests")]
    public static void ConvertAll()
    {
        var log = new StringBuilder();
        int errors = 0;

        Material material = null;
        try { material = BuildMaterial(log); }
        catch (System.Exception e) { errors++; log.AppendLine("!! material: " + e); }

        foreach (string path in BoxPaths)
        {
            try { Convert(path, material, log); }
            catch (System.Exception e) { errors++; log.AppendLine("!! " + path + ": " + e); }
        }

        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/itembox_chest.txt", log.ToString());
        Debug.Log($"[ItemBoxChest] Done with {errors} error(s). Report: Library/PrisonDecor/itembox_chest.txt");
    }

    // =========================================================
    // CONVERT ONE BOX
    // =========================================================

    private static void Convert(string path, Material material, StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ProBuilderMesh pb = root.GetComponent<ProBuilderMesh>();
            if (pb == null) { throw new System.Exception("no ProBuilderMesh on root"); }

            ProBuilderMesh chest = BuildChestMesh(OriginalBoxBounds);
            try
            {
                // (CopyFrom needs unwrap parameters the generated mesh doesn't have: copy the geometry directly)
                var faces = new List<Face>();
                foreach (Face f in chest.faces)
                {
                    // manualUV must stay on, or ProBuilder re-projects the UVs
                    faces.Add(new Face(f) { manualUV = true });
                }
                pb.RebuildWithPositionsAndFaces(chest.positions, faces);
                pb.textures = chest.textures;
            }
            finally
            {
                Object.DestroyImmediate(chest.gameObject);
            }

            if (material != null)
            {
                pb.SetMaterial(pb.faces, material);
                root.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            pb.ToMesh();
            pb.Refresh();

            // The ProBuilderShape would regenerate a cube if anyone touches it in the editor
            foreach (Component c in root.GetComponents<Component>())
            {
                if (c == null || c.GetType().Name != "ProBuilderShape") { continue; }
                try { Object.DestroyImmediate(c); log.AppendLine("  removed ProBuilderShape"); }
                catch (System.Exception e) { log.AppendLine("  kept ProBuilderShape (" + e.Message + ")"); }
            }

            Bounds nb = pb.GetComponent<MeshFilter>().sharedMesh.bounds;
            MeshCollider mc = root.GetComponent<MeshCollider>();
            if (mc != null) { mc.sharedMesh = pb.GetComponent<MeshFilter>().sharedMesh; mc.convex = true; }

            NavMeshObstacle obstacle = root.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
            {
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = nb.center;
                obstacle.size = nb.size;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"{root.name}: saved chest bounds center={nb.center.ToString("F2")} size={nb.size.ToString("F2")} faces={pb.faceCount}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Closed chest, front (lock) at -Z
    private static ProBuilderMesh BuildChestMesh(Bounds box)
    {
        float w = box.size.x * 0.96f;
        float d = box.size.z * 0.68f;
        float bottom = box.min.y;
        float bodyH = box.size.y * 0.52f;
        float lidH = box.size.y * 0.26f;
        float cx = box.center.x, cz = box.center.z;
        float band = Mathf.Max(0.05f, w * 0.07f);

        var parts = new List<ProBuilderMesh>();

        Box(parts, new Vector3(cx, bottom + bodyH * 0.5f, cz), new Vector3(w, bodyH, d), BodyMap);
        Box(parts, new Vector3(cx, bottom + bodyH + lidH * 0.5f, cz), new Vector3(w + 0.03f, lidH, d + 0.03f), LidMap);

        foreach (float x in new[] { cx - w * 0.33f, cx + w * 0.33f })
        {
            Box(parts, new Vector3(x, bottom + (bodyH + lidH) * 0.5f, cz), new Vector3(band, bodyH + lidH + 0.01f, d + 0.05f), Mapping.All(Region.IronV));
        }
        Box(parts, new Vector3(cx, bottom + 0.035f, cz), new Vector3(w + 0.02f, 0.07f, d + 0.02f), Mapping.All(Region.IronH));
        Box(parts, new Vector3(cx, bottom + bodyH, cz), new Vector3(w + 0.04f, 0.05f, d + 0.04f), Mapping.All(Region.IronH));

        Box(parts, new Vector3(cx, bottom + bodyH - 0.02f, cz - d * 0.5f - 0.02f), new Vector3(w * 0.17f, w * 0.19f, 0.04f), Mapping.All(Region.Gold));
        Box(parts, new Vector3(cx, bottom + bodyH + lidH * 0.35f, cz - d * 0.5f - 0.025f), new Vector3(w * 0.07f, lidH * 0.6f, 0.03f), Mapping.All(Region.Gold));

        ProBuilderMesh target = parts[0];
        CombineMeshes.Combine(parts, target);
        for (int i = 1; i < parts.Count; i++) { Object.DestroyImmediate(parts[i].gameObject); }
        target.ToMesh();
        target.Refresh();
        return target;
    }

    // A box whose every side is mapped upright into its own region of the sheet
    private static void Box(List<ProBuilderMesh> parts, Vector3 center, Vector3 size, Mapping map)
    {
        ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
        Vector3[] positions = new Vector3[pb.positions.Count];
        for (int i = 0; i < positions.Length; i++) { positions[i] = pb.positions[i] + center; }
        pb.positions = positions;
        pb.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Vector2[] uvs = new Vector2[pb.vertexCount];
        foreach (Face face in pb.faces)
        {
            IList<int> tri = face.indexes;
            Vector3 n = Vector3.Cross(positions[tri[1]] - positions[tri[0]], positions[tri[2]] - positions[tri[0]]).normalized;

            // U = "right" and V = "up" as seen by someone looking at this side from outside
            Region region; Vector3 axisU, axisV;
            if (n.z < -0.7f)      { region = map.front;  axisU = Vector3.right;   axisV = Vector3.up; }
            else if (n.z > 0.7f)  { region = map.back;   axisU = Vector3.left;    axisV = Vector3.up; }
            else if (n.x < -0.7f) { region = map.left;   axisU = Vector3.back;    axisV = Vector3.up; }
            else if (n.x > 0.7f)  { region = map.right;  axisU = Vector3.forward; axisV = Vector3.up; }
            else if (n.y > 0.7f)  { region = map.top;    axisU = Vector3.right;   axisV = Vector3.forward; }
            else                  { region = map.bottom; axisU = Vector3.right;   axisV = Vector3.back; }

            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (int i in face.distinctIndexes)
            {
                float u = Vector3.Dot(positions[i], axisU), v = Vector3.Dot(positions[i], axisV);
                minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
                minV = Mathf.Min(minV, v); maxV = Mathf.Max(maxV, v);
            }

            Rect r = RegionUV(region);
            foreach (int i in face.distinctIndexes)
            {
                float u = Mathf.InverseLerp(minU, maxU, Vector3.Dot(positions[i], axisU));
                float v = Mathf.InverseLerp(minV, maxV, Vector3.Dot(positions[i], axisV));
                uvs[i] = new Vector2(r.x + u * r.width, r.y + v * r.height);
            }
            face.manualUV = true;
        }

        pb.textures = uvs;
        pb.ToMesh();
        pb.Refresh();
        parts.Add(pb);
    }

    // Region in 0..1 UV space, inset a quarter pixel so point filtering never bleeds into a neighbour
    private static Rect RegionUV(Region region)
    {
        RectInt p = Regions[region];
        const float inset = 0.25f;
        return new Rect((p.x + inset) / Sheet, (p.y + inset) / Sheet, (p.width - inset * 2f) / Sheet, (p.height - inset * 2f) / Sheet);
    }

    // =========================================================
    // TEXTURE + MATERIAL
    // =========================================================

    private static Material BuildMaterial(StringBuilder log)
    {
        Directory.CreateDirectory(TextureFolder);

        // Colour-coded guide: always regenerated (it's only a painting reference, not used by the material)
        WritePng(PaintGuide(), GuidePath);
        log.AppendLine("guide written " + GuidePath);

        // Starter texture: only when there isn't one in this layout yet, so your painting is kept
        bool layoutCurrent = EditorPrefs.GetBool(LayoutKey + Application.dataPath, false);
        if (!File.Exists(TexturePath) || !layoutCurrent)
        {
            WritePng(PaintStarter(), TexturePath);
            EditorPrefs.SetBool(LayoutKey + Application.dataPath, true);
            log.AppendLine("starter texture written " + TexturePath);
        }
        else
        {
            log.AppendLine("kept existing texture " + TexturePath);
        }

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
            mat = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", tex); mat.SetTextureScale("_BaseMap", Vector2.one); mat.SetTextureOffset("_BaseMap", Vector2.zero); }
        if (mat.HasProperty("_MainTex")) { mat.SetTexture("_MainTex", tex); }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void WritePng(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    // Starter look (planks from the furniture texture, plain iron and gold) so the chest looks fine until you paint it
    private static Texture2D PaintStarter()
    {
        var tex = new Texture2D(Sheet, Sheet, TextureFormat.RGBA32, false);
        var px = new Color[Sheet * Sheet];
        for (int i = 0; i < px.Length; i++) { px[i] = new Color(0f, 0f, 0f, 0f); }

        Texture2D shelf = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        bool hasShelf = File.Exists(ShelfTexturePath) && shelf.LoadImage(File.ReadAllBytes(ShelfTexturePath));
        int half = hasShelf ? shelf.width / 2 : 32;

        Color Wood(int x, int y, bool lid)
        {
            if (!hasShelf) { return lid ? ShopPixelArt.Hex("#5c3820") : ShopPixelArt.Hex("#6e4527"); }
            int sx = (x % half) + (lid ? half : 0);
            int sy = y % half;
            return shelf.GetPixel(sx, sy);
        }

        Color iron = ShopPixelArt.Hex("#55585e"), ironEdge = ShopPixelArt.Hex("#33363b");
        Color gold = ShopPixelArt.Hex("#d9a633"), goldEdge = ShopPixelArt.Hex("#8a6418");
        Color hidden = ShopPixelArt.Hex("#2a1a10");

        foreach (var kv in Regions)
        {
            RectInt r = kv.Value;
            for (int y = 0; y < r.height; y++)
            {
                for (int x = 0; x < r.width; x++)
                {
                    bool edge = x == 0 || y == 0 || x == r.width - 1 || y == r.height - 1;
                    Color c;
                    switch (kv.Key)
                    {
                        case Region.IronV:
                        case Region.IronH: c = edge ? ironEdge : iron; break;
                        case Region.Gold: c = edge ? goldEdge : gold; break;
                        case Region.Hidden: c = hidden; break;
                        case Region.LidFront:
                        case Region.LidBack:
                        case Region.LidLeft:
                        case Region.LidRight:
                        case Region.LidTop: c = Wood(x, y, true); break;
                        default: c = Wood(x, y, false); break;
                    }
                    c.a = 1f;
                    px[(r.y + y) * Sheet + r.x + x] = c;
                }
            }
        }

        Object.DestroyImmediate(shelf);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Each region in its own flat colour with a 1px dark border, and a small arrow-ish mark at the top edge
    // of every side region so you can see which way is up
    private static Texture2D PaintGuide()
    {
        var colors = new Dictionary<Region, Color>
        {
            { Region.BodyFront, ShopPixelArt.Hex("#e74c3c") }, { Region.BodyBack, ShopPixelArt.Hex("#c0392b") },
            { Region.BodyLeft, ShopPixelArt.Hex("#e67e22") },  { Region.BodyRight, ShopPixelArt.Hex("#d35400") },
            { Region.LidFront, ShopPixelArt.Hex("#3498db") },  { Region.LidBack, ShopPixelArt.Hex("#2471a3") },
            { Region.LidLeft, ShopPixelArt.Hex("#1abc9c") },   { Region.LidRight, ShopPixelArt.Hex("#148f77") },
            { Region.LidTop, ShopPixelArt.Hex("#9b59b6") },
            { Region.IronV, ShopPixelArt.Hex("#7f8c8d") },     { Region.IronH, ShopPixelArt.Hex("#95a5a6") },
            { Region.Gold, ShopPixelArt.Hex("#f1c40f") },      { Region.Hidden, ShopPixelArt.Hex("#2c2c2c") },
        };

        var tex = new Texture2D(Sheet, Sheet, TextureFormat.RGBA32, false);
        var px = new Color[Sheet * Sheet];
        for (int i = 0; i < px.Length; i++) { px[i] = new Color(0.1f, 0.1f, 0.1f, 1f); }

        foreach (var kv in Regions)
        {
            RectInt r = kv.Value;
            Color c = colors[kv.Key];
            for (int y = 0; y < r.height; y++)
            {
                for (int x = 0; x < r.width; x++)
                {
                    bool edge = x == 0 || y == 0 || x == r.width - 1 || y == r.height - 1;
                    bool upMark = y == r.height - 2 && Mathf.Abs(x - r.width / 2) <= 1; // white tick = top
                    px[(r.y + y) * Sheet + r.x + x] = edge ? Color.black : (upMark ? Color.white : c);
                }
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
