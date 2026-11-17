using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: creates Resources/UI/MinimapSettings with the room icons (shop, shrine, challenge, treasure,
/// exit, boss). Existing settings keep your values; only empty icon slots are filled.
/// Re-run from Tools > UI > Build Minimap Settings.
/// </summary>
public static class MinimapSetup
{
    private const string AutoKey = "MinimapSetup_v1";
    private const string SettingsPath = "Assets/Resources/UI/MinimapSettings.asset";
    private const string IconFolder = "Assets/013-Datas/Minimap";

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

    [MenuItem("Tools/UI/Build Minimap Settings")]
    public static void Build()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
        Directory.CreateDirectory(IconFolder);

        MinimapSettings settings = AssetDatabase.LoadAssetAtPath<MinimapSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<MinimapSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (settings.shopIcon == null) { settings.shopIcon = Load("Assets/013-Datas/Shop/Icons/Icon_Coin.png"); }
        if (settings.shrineIcon == null) { settings.shrineIcon = Load("Assets/013-Datas/Shrines/Icons/Icon_AltarOfFury.png"); }
        if (settings.challengeIcon == null) { settings.challengeIcon = Load("Assets/013-Datas/Challenges/Icon_Challenge.png"); }
        if (settings.treasureIcon == null) { settings.treasureIcon = Load("Assets/013-Datas/Shop/Icons/Icon_Key_Gold.png"); }
        if (settings.exitIcon == null) { settings.exitIcon = ShopPixelArt.SaveSprite(StairsIcon(), IconFolder + "/Icon_Exit.png"); }
        if (settings.bossIcon == null) { settings.bossIcon = ShopPixelArt.SaveSprite(SkullIcon(), IconFolder + "/Icon_Boss.png"); }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MinimapSetup] Done: shop={settings.shopIcon != null} shrine={settings.shrineIcon != null} " +
                  $"challenge={settings.challengeIcon != null} treasure={settings.treasureIcon != null} " +
                  $"exit={settings.exitIcon != null} boss={settings.bossIcon != null}");
    }

    private static readonly Color Ink = ShopPixelArt.Hex("#1a1210");

    // Stairs going down to the next floor
    private static ShopPixelArt.Canvas StairsIcon()
    {
        var c = new ShopPixelArt.Canvas(32, 32);
        Color stone = ShopPixelArt.Hex("#c9c2b4"), dark = ShopPixelArt.Hex("#6e675c");
        for (int i = 0; i < 5; i++)
        {
            int top = 27 - i * 5;
            c.Rect(4 + i * 5, top - 4, 27, top, stone);
            c.Rect(4 + i * 5, top - 4, 27, top - 4, dark);
        }
        c.Outline(Ink);
        return c;
    }

    private static ShopPixelArt.Canvas SkullIcon()
    {
        var c = new ShopPixelArt.Canvas(32, 32);
        Color bone = ShopPixelArt.Hex("#ece4d0");
        c.Disc(16f, 19f, 10f, bone);
        c.Rect(10, 6, 21, 12, bone);           // jaw
        c.Disc(12f, 18f, 2.8f, Ink);           // eyes
        c.Disc(20f, 18f, 2.8f, Ink);
        c.Rect(15, 12, 16, 14, Ink);           // nose
        c.Rect(12, 6, 12, 9, Ink);             // teeth gaps
        c.Rect(15, 6, 15, 9, Ink);
        c.Rect(18, 6, 18, 9, Ink);
        c.Outline(Ink);
        return c;
    }
}
