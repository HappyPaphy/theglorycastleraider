using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tiny pixel-art painter for the shop's generated icons and the entrance runner texture.
/// Everything is drawn in code so there are no external art dependencies.
/// </summary>
public static class ShopPixelArt
{
    public class Canvas
    {
        public readonly int W, H;
        public readonly Color[] Px;

        public Canvas(int w, int h)
        {
            W = w; H = h;
            Px = new Color[w * h];
        }

        public void Set(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) { return; }
            Px[y * W + x] = c;
        }

        public Color Get(int x, int y) => (x < 0 || y < 0 || x >= W || y >= H) ? Color.clear : Px[y * W + x];

        public void Rect(int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, c);
        }

        public void Disc(float cx, float cy, float r, Color c)
        {
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r * r) Set(x, y, c);
            }
        }

        public void Ring(float cx, float cy, float rOuter, float rInner, Color c)
        {
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = dx * dx + dy * dy;
                if (d <= rOuter * rOuter && d >= rInner * rInner) Set(x, y, c);
            }
        }

        public void Line(int x0, int y0, int x1, int y1, Color c, int thickness = 1)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int ox = 0; ox < thickness; ox++) for (int oy = 0; oy < thickness; oy++) Set(x + ox, y + oy, c);
            }
        }

        /// <summary>Dark 1px outline around every filled pixel (classic sprite look).</summary>
        public void Outline(Color c)
        {
            Color[] copy = (Color[])Px.Clone();
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                if (copy[y * W + x].a > 0f) continue;
                bool near = false;
                for (int oy = -1; oy <= 1 && !near; oy++) for (int ox = -1; ox <= 1 && !near; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    int nx = x + ox, ny = y + oy;
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    if (copy[ny * W + nx].a > 0f) near = true;
                }
                if (near) Px[y * W + x] = c;
            }
        }

        /// <summary>Simple top-left light: brighten the top edge of shapes, darken the bottom edge.</summary>
        public void Shade(float amount = 0.18f)
        {
            Color[] copy = (Color[])Px.Clone();
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                Color c = copy[y * W + x];
                if (c.a <= 0f) continue;
                bool topEmpty = y + 1 >= H || copy[(y + 1) * W + x].a <= 0f;
                bool bottomEmpty = y - 1 < 0 || copy[(y - 1) * W + x].a <= 0f;
                if (topEmpty) Px[y * W + x] = Color.Lerp(c, Color.white, amount);
                else if (bottomEmpty) Px[y * W + x] = Color.Lerp(c, Color.black, amount);
            }
        }
    }

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    /// <summary>Writes the canvas as a point-filtered PNG sprite and returns the imported Sprite.</summary>
    public static Sprite SaveSprite(Canvas canvas, string assetPath, int pixelsPerUnit = 32)
    {
        Texture2D tex = new Texture2D(canvas.W, canvas.H, TextureFormat.RGBA32, false);
        tex.SetPixels(canvas.Px);
        tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    // =========================================================
    // ICONS (32x32)
    // =========================================================

    private static readonly Color Ink = Hex("#1a1210");

    public static Canvas Key(Color body, Color dark)
    {
        var c = new Canvas(32, 32);
        c.Ring(10f, 20f, 6.5f, 3f, body);          // bow
        c.Rect(15, 18, 27, 21, body);               // shaft
        c.Rect(22, 13, 24, 17, body);               // teeth
        c.Rect(26, 14, 27, 17, body);
        c.Rect(8, 18, 9, 19, dark);                 // hole shadow
        c.Shade(0.25f);
        c.Outline(Ink);
        return c;
    }

    public static Canvas Coin()
    {
        var c = new Canvas(32, 32);
        Color gold = Hex("#e8b53a"), dark = Hex("#a8741c"), light = Hex("#ffe08a");
        c.Disc(16f, 16f, 11f, dark);
        c.Disc(15.3f, 16.7f, 10f, gold);
        c.Ring(15.3f, 16.7f, 7.5f, 6.4f, dark);
        c.Rect(14, 12, 16, 21, dark);               // stamped bar
        c.Rect(10, 21, 12, 23, light);              // glint
        c.Outline(Ink);
        return c;
    }

    public static Canvas Totem()
    {
        var c = new Canvas(32, 32);
        Color wood = Hex("#8a5a34"), dark = Hex("#5a3720"), bone = Hex("#e6dcc2"), glow = Hex("#7fe3ff");
        c.Rect(11, 3, 20, 27, wood);                // post
        c.Rect(7, 18, 24, 21, dark);                // wings / arms
        c.Rect(5, 19, 7, 20, wood); c.Rect(24, 19, 26, 20, wood);
        c.Rect(12, 23, 14, 25, glow);               // eyes
        c.Rect(17, 23, 19, 25, glow);
        c.Rect(13, 9, 18, 14, bone);                // skull mouth plate
        c.Rect(14, 11, 14, 12, Ink); c.Rect(17, 11, 17, 12, Ink);
        c.Rect(10, 1, 21, 3, dark);                 // base
        c.Rect(13, 28, 18, 30, glow);               // spirit flame
        c.Set(15, 31, glow); c.Set(16, 31, glow);
        c.Shade(0.2f);
        c.Outline(Ink);
        return c;
    }

    public static Canvas AttackSpeed()
    {
        var c = new Canvas(32, 32);
        Color steel = Hex("#cfd6de"), dark = Hex("#7d8894"), hilt = Hex("#8a5a34"), streak = Hex("#ffd34d");
        c.Line(8, 8, 25, 25, steel, 3);             // blade
        c.Line(9, 8, 25, 24, dark, 1);
        c.Line(4, 10, 10, 4, hilt, 2);              // guard
        c.Line(3, 3, 7, 7, hilt, 2);                // grip
        c.Line(2, 18, 9, 18, streak);               // speed streaks
        c.Line(4, 22, 12, 22, streak);
        c.Line(7, 26, 15, 26, streak);
        c.Outline(Ink);
        return c;
    }

    public static Canvas WeaponCarry()
    {
        var c = new Canvas(32, 32);
        Color steel = Hex("#cfd6de"), hilt = Hex("#8a5a34"), plus = Hex("#7be36a");
        c.Line(5, 6, 20, 21, steel, 2);             // sword 1
        c.Line(3, 9, 8, 4, hilt, 2);
        c.Line(20, 6, 5, 21, steel, 2);             // sword 2
        c.Line(18, 4, 23, 9, hilt, 2);
        c.Rect(22, 20, 24, 30, plus);               // plus sign
        c.Rect(18, 24, 28, 26, plus);
        c.Outline(Ink);
        return c;
    }

    // =========================================================
    // ENTRANCE RUNNER (16 x 48, tiles along the doorway)
    // =========================================================

    public static Canvas Runner()
    {
        var c = new Canvas(16, 48);
        Color red = Hex("#7a1f1c"), redDark = Hex("#5a1513"), gold = Hex("#d9a53a"), goldDark = Hex("#9c7024");
        c.Rect(0, 0, 15, 47, red);
        for (int y = 0; y < 48; y += 4) c.Rect(2, y, 13, y, redDark);      // weave
        c.Rect(0, 0, 1, 47, gold); c.Rect(14, 0, 15, 47, gold);           // gold borders
        c.Rect(2, 0, 2, 47, goldDark); c.Rect(13, 0, 13, 47, goldDark);
        c.Rect(0, 0, 15, 1, gold); c.Rect(0, 46, 15, 47, gold);           // fringe ends
        // Coin emblem in the middle
        c.Disc(8f, 24f, 4.5f, goldDark);
        c.Disc(7.6f, 24.4f, 3.6f, gold);
        c.Rect(7, 22, 8, 26, goldDark);
        return c;
    }
}
