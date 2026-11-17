using TMPro;
using UnityEngine;

/// <summary>
/// The font used by text the game creates at runtime (elite labels, curse list, challenge banner, coin pop-ups...).
/// Lives at Resources/UI/GameFontSettings. Change the font there to restyle all of it at once.
/// </summary>
[CreateAssetMenu(fileName = "GameFontSettings", menuName = "UI/Game Font Settings")]
public class GameFontSettings : ScriptableObject
{
    public const string ResourcePath = "UI/GameFontSettings";

    [Tooltip("Font for runtime-created text.")]
    public TMP_FontAsset font;

    private static GameFontSettings cached;
    private static bool loaded;

    public static TMP_FontAsset Font
    {
        get
        {
            if (!loaded) { cached = Resources.Load<GameFontSettings>(ResourcePath); loaded = true; }
            return cached != null ? cached.font : null;
        }
    }

    /// <summary>Gives this text the game font (does nothing if no font is set).</summary>
    public static void Apply(TMP_Text text)
    {
        TMP_FontAsset f = Font;
        if (text == null || f == null) { return; }
        text.font = f;
        text.fontSharedMaterial = f.material;
    }
}
