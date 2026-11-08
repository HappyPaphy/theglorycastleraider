using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates one pixel-art texture + URP Unlit material per ProBuilder furniture piece.
/// Each texture is a 64x64 atlas of four 32x32 slots (see <see cref="SlotRect"/>):
///   slot 0 = bottom-left, 1 = bottom-right, 2 = top-left, 3 = top-right.
/// The furniture builder maps each part of a piece onto one slot, with the slot's U axis along
/// the part face's longest side (so wood grain always runs lengthwise).
/// </summary>
public static class FurnitureMaterialFactory
{
    public const string MaterialFolder = "Assets/006-Materials/Furniture";
    public const string TextureFolder = MaterialFolder + "/Textures";
    private const string SourceMaterialPath = "Assets/006-Materials/Environment/Door/WoodenPlank-1x1 Unlit.mat";

    private const int Size = 64;

    // The furniture uses URP Unlit, so brightness comes only from texture x base colour (no scene light).
    // These match the existing WoodenPlank-1x1 Unlit furniture: its texture averages ~0.35 and is tinted 0.58.
    private const float TextureBrightness = 0.6f;
    private static readonly Color FallbackBaseColor = new Color(0.58f, 0.58f, 0.58f, 1f);

    private static string RefreshKey => "FurnitureMaterialFactory.Refreshed.v3." + Application.dataPath;

    // Regenerate the textures/materials in place (same assets, so prefabs keep their references)
    [InitializeOnLoadMethod]
    private static void AutoRefreshOnce()
    {
        if (EditorPrefs.GetBool(RefreshKey, false)) { return; }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(RefreshKey, true);
            RefreshAll();
        };
    }

    [MenuItem("Tools/Prison Decor/Regenerate Furniture Materials")]
    public static void RefreshAll()
    {
        foreach (string n in ProBuilderFurnitureBuilder.PrefabNames) { GetMaterial(n); }
        AssetDatabase.SaveAssets();
        Debug.Log("[FurnitureMaterialFactory] Regenerated furniture materials");
    }
    private const int Tile = 32;

    public static Rect SlotRect(int slot)
    {
        int x = slot % 2, y = slot / 2;
        // Half-pixel inset so point filtering never bleeds into the neighbouring slot
        float inset = 0.5f / Size;
        return new Rect(x * 0.5f + inset, y * 0.5f + inset, 0.5f - inset * 2f, 0.5f - inset * 2f);
    }

    // =========================================================
    // PALETTES
    // =========================================================

    private static readonly Color Pine = new Color(0.66f, 0.48f, 0.29f);
    private static readonly Color PineDark = new Color(0.5f, 0.34f, 0.19f);
    private static readonly Color Oak = new Color(0.47f, 0.31f, 0.17f);
    private static readonly Color OakDark = new Color(0.32f, 0.2f, 0.11f);
    private static readonly Color Walnut = new Color(0.33f, 0.21f, 0.13f);
    private static readonly Color Ash = new Color(0.72f, 0.6f, 0.43f);
    private static readonly Color Aged = new Color(0.46f, 0.42f, 0.36f);
    private static readonly Color AgedDark = new Color(0.33f, 0.3f, 0.26f);
    private static readonly Color Iron = new Color(0.27f, 0.28f, 0.3f);
    private static readonly Color Steel = new Color(0.62f, 0.64f, 0.67f);
    private static readonly Color Straw = new Color(0.8f, 0.68f, 0.37f);
    private static readonly Color Terracotta = new Color(0.68f, 0.37f, 0.2f);
    private static readonly Color ClayDark = new Color(0.45f, 0.23f, 0.12f);
    private static readonly Color ClayBrown = new Color(0.52f, 0.34f, 0.22f);
    private static readonly Color Cream = new Color(0.86f, 0.78f, 0.6f);

    private delegate void TilePainter(Color[] px, int ox, int oy, System.Random rng);

    /// <summary>Creates (or updates in place) the material for a furniture piece and returns it.</summary>
    public static Material GetMaterial(string furnitureName)
    {
        TilePainter[] slots = GetSlots(furnitureName);
        Texture2D tex = WriteTexture(furnitureName, slots);
        return WriteMaterial(furnitureName, tex);
    }

    private static TilePainter[] GetSlots(string name)
    {
        switch (name)
        {
            case "Barrel":
                return new TilePainter[] { Stave(Oak, OakDark), IronBand(), Planks(OakDark, Walnut), Beam(Oak, OakDark) };
            case "Bench":
                return new TilePainter[] { Planks(Pine, PineDark), Beam(PineDark, OakDark), Beam(Pine, PineDark), Beam(Pine, PineDark) };
            case "Stool":
                return new TilePainter[] { Planks(Ash, Pine), Beam(PineDark, OakDark), Beam(Ash, Pine), Beam(Ash, Pine) };
            case "Shelf":
                return new TilePainter[] { Beam(Walnut, OakDark), Planks(OakDark, Walnut), Planks(Oak, Walnut), Beam(Walnut, OakDark) };
            case "PrisonBed":
                return new TilePainter[] { Beam(OakDark, Walnut), StrawTile(), Planks(Oak, OakDark), Beam(OakDark, Walnut) };
            case "WeaponRack":
                return new TilePainter[] { Beam(Oak, OakDark), Beam(Ash, Pine), SteelTile(), Beam(Oak, OakDark) };
            case "Pillory":
                return new TilePainter[] { Beam(Aged, AgedDark), Planks(AgedDark, Iron), IronBand(), Beam(Aged, AgedDark) };
            case "ClayPot_Tall":
                return new TilePainter[] { ClayTile(Terracotta, ClayDark, false), ClayTile(ClayDark, OakDark, false), ClayTile(Terracotta, Cream, true), ClayTile(Terracotta, ClayDark, false) };
            case "ClayPot_Small":
                return new TilePainter[] { ClayTile(ClayBrown, ClayDark, false), ClayTile(ClayDark, OakDark, false), ClayTile(ClayBrown, Cream, true), ClayTile(ClayBrown, ClayDark, false) };
            default:
                return new TilePainter[] { Planks(Oak, OakDark), Beam(Oak, OakDark), Planks(Oak, OakDark), Beam(Oak, OakDark) };
        }
    }

    // =========================================================
    // TILE PAINTERS (32x32, x = U = lengthwise, y = V)
    // Low-res pixel-art style matching PixelArt_Texture_WoodenDoor / PixelArt-WoodenBox:
    // flat tones, 1px dark seams, short grain streaks, almost no per-pixel noise.
    // Faces are mapped at ~24 px per metre, so most faces only see a few pixels of a tile.
    // =========================================================

    private const int PlankHeight = 5; // 4px plank + 1px seam, like the original door texture

    private static Color Shade(Color c, float amount) => new Color(
        Mathf.Clamp01(c.r * amount), Mathf.Clamp01(c.g * amount), Mathf.Clamp01(c.b * amount), 1f);

    private static void Set(Color[] px, int ox, int oy, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= Tile || y >= Tile) { return; }
        px[(oy + y) * Size + ox + x] = c;
    }

    private static void Fill(Color[] px, int ox, int oy, Color c)
    {
        for (int y = 0; y < Tile; y++) for (int x = 0; x < Tile; x++) { Set(px, ox, oy, x, y, c); }
    }

    // Short dark grain streaks running lengthwise
    private static void Grain(Color[] px, int ox, int oy, System.Random rng, Color streak, int count, int yMin, int yMax)
    {
        for (int i = 0; i < count; i++)
        {
            int y = rng.Next(yMin, yMax);
            int x = rng.Next(0, Tile);
            int len = rng.Next(3, 8);
            for (int k = 0; k < len; k++) { Set(px, ox, oy, (x + k) % Tile, y, streak); }
        }
    }

    // Boards stacked across the face: flat plank tones, 1px dark seam, 1px light edge
    private static TilePainter Planks(Color baseColor, Color dark) => (px, ox, oy, rng) =>
    {
        float[] tones = { 0.9f, 1f, 1.1f };
        for (int plank = 0; plank * PlankHeight < Tile; plank++)
        {
            Color tone = Shade(baseColor, tones[rng.Next(tones.Length)]);
            int y0 = plank * PlankHeight;
            for (int y = y0; y < y0 + PlankHeight; y++)
            {
                Color row = y == y0 ? dark : (y == y0 + 1 ? Shade(tone, 1.12f) : tone);
                for (int x = 0; x < Tile; x++) { Set(px, ox, oy, x, y, row); }
            }

            // Butt joint somewhere along the plank
            int joint = rng.Next(6, Tile - 6);
            for (int y = y0 + 1; y < y0 + PlankHeight; y++) { Set(px, ox, oy, joint, y, dark); }

            Grain(px, ox, oy, rng, Shade(tone, 0.82f), 2, y0 + 2, y0 + PlankHeight);
        }
    };

    // A single solid timber (legs, posts, frames): flat tone with a dark edge and sparse grain
    private static TilePainter Beam(Color baseColor, Color dark) => (px, ox, oy, rng) =>
    {
        Fill(px, ox, oy, baseColor);
        for (int x = 0; x < Tile; x++)
        {
            Set(px, ox, oy, x, 0, dark);
            Set(px, ox, oy, x, 1, Shade(baseColor, 1.12f));
        }
        Grain(px, ox, oy, rng, Shade(baseColor, 0.8f), 10, 2, Tile);
    };

    // Barrel stave: each 10-sided side face is ~5px wide, so the dark row 0 reads as the gap between staves
    private static TilePainter Stave(Color baseColor, Color dark) => (px, ox, oy, rng) =>
    {
        Beam(baseColor, dark)(px, ox, oy, rng);
        for (int x = 0; x < Tile; x++) { Set(px, ox, oy, x, 4, Shade(baseColor, 0.9f)); }
    };

    // Forged iron: flat dark metal, highlight row and rivets
    private static TilePainter IronBand() => (px, ox, oy, rng) =>
    {
        Fill(px, ox, oy, Iron);
        for (int x = 0; x < Tile; x++)
        {
            Set(px, ox, oy, x, 0, Shade(Iron, 0.65f));
            Set(px, ox, oy, x, 2, Shade(Iron, 1.45f));
            if (x % 6 == 3) { Set(px, ox, oy, x, 1, Shade(Iron, 1.8f)); }
        }
    };

    // Steel blade: flat with a bright edge
    private static TilePainter SteelTile() => (px, ox, oy, rng) =>
    {
        Fill(px, ox, oy, Steel);
        for (int x = 0; x < Tile; x++)
        {
            Set(px, ox, oy, x, 0, Shade(Steel, 0.7f));
            Set(px, ox, oy, x, 1, Shade(Steel, 1.3f));
        }
    };

    // Straw mattress: two alternating straw tones, darker clumps, dashed stitching
    private static TilePainter StrawTile() => (px, ox, oy, rng) =>
    {
        for (int y = 0; y < Tile; y++)
        {
            Color row = Shade(Straw, y % 2 == 0 ? 1f : 0.88f);
            for (int x = 0; x < Tile; x++) { Set(px, ox, oy, x, y, row); }
        }
        Grain(px, ox, oy, rng, Shade(Straw, 0.65f), 14, 0, Tile);
        for (int x = 0; x < Tile; x++)
        {
            if (x % 4 < 2) { Set(px, ox, oy, x, 2, Shade(Straw, 0.5f)); }
        }
    };

    // Fired clay. On pot sides U runs up the pot (height is the longest side), so bands are COLUMNS.
    private static TilePainter ClayTile(Color baseColor, Color accent, bool painted) => (px, ox, oy, rng) =>
    {
        Fill(px, ox, oy, baseColor);
        for (int y = 0; y < Tile; y++)
        {
            Set(px, ox, oy, 0, y, Shade(baseColor, 0.8f));             // dark rim at the bottom of each section
            if (y % 3 == 0) { Set(px, ox, oy, rng.Next(1, Tile), y, Shade(baseColor, 0.85f)); } // speckles
        }

        if (painted)
        {
            for (int y = 0; y < Tile; y++)
            {
                Set(px, ox, oy, 2, y, Shade(accent, 0.8f));             // thin ring
                Set(px, ox, oy, 4, y, accent);                          // main band (~17cm up)
                Set(px, ox, oy, 5, y, accent);
                if (y % 4 < 2) { Set(px, ox, oy, 6, y, accent); }       // dotted edge
                Set(px, ox, oy, 8, y, Shade(accent, 0.8f));             // thin ring
            }
        }
    };

    // =========================================================
    // ASSETS
    // =========================================================

    private static Texture2D WriteTexture(string name, TilePainter[] slots)
    {
        Directory.CreateDirectory(TextureFolder);

        Color[] px = new Color[Size * Size];
        System.Random rng = new System.Random(name.GetHashCode() & 0x7fffffff);
        for (int s = 0; s < 4; s++)
        {
            slots[s](px, (s % 2) * Tile, (s / 2) * Tile, rng);
        }

        for (int i = 0; i < px.Length; i++) { px[i] = Shade(px[i], TextureBrightness); }

        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();

        string path = $"{TextureFolder}/T_{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material WriteMaterial(string name, Texture2D tex)
    {
        string path = $"{MaterialFolder}/Mat_{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
            mat = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.name = $"Mat_{name}";
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", tex); mat.SetTextureScale("_BaseMap", Vector2.one); }
        if (mat.HasProperty("_MainTex")) { mat.SetTexture("_MainTex", tex); }
        // Same tint as the existing furniture material so everything sits at the same brightness
        Material reference = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
        Color tint = reference != null && reference.HasProperty("_BaseColor") ? reference.GetColor("_BaseColor") : FallbackBaseColor;
        if (mat.HasProperty("_BaseColor")) { mat.SetColor("_BaseColor", tint); }
        if (mat.HasProperty("_Color")) { mat.SetColor("_Color", tint); }

        EditorUtility.SetDirty(mat);
        return mat;
    }
}
