using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the item popup line prefab (Assets/Resources/UI/ItemPickupPopupEntry.prefab) with the default look,
/// only if it doesn't exist yet, then selects it. Open the prefab to change sprites / layout / text.
/// Tools > UI > Build Item Pickup Popup Prefab (never overwrites an existing prefab).
/// </summary>
public static class ItemPickupPopupPrefabBuilder
{
    private const string AutoKey = "ItemPickupPopupPrefabBuilder_v1";
    private const string Path = "Assets/Resources/UI/ItemPickupPopupEntry.prefab";

    private const float Width = 330f, Height = 62f;

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

    [MenuItem("Tools/UI/Build Item Pickup Popup Prefab")]
    public static void Build()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("[ItemPopup] Prefab already exists, left as is: " + Path);
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) { AssetDatabase.CreateFolder("Assets", "Resources"); }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) { AssetDatabase.CreateFolder("Assets/Resources", "UI"); }

        // Root: background panel
        var root = new GameObject("ItemPickupPopupEntry", typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)root.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(Width, Height);
        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.055f, 0.04f, 0.85f);
        bg.raycastTarget = false;

        // Gold accent bar on the left edge
        RectTransform accent = Child("Accent", rt);
        accent.anchorMin = new Vector2(0f, 0f);
        accent.anchorMax = new Vector2(0f, 1f);
        accent.pivot = new Vector2(0f, 0.5f);
        accent.sizeDelta = new Vector2(4f, 0f);
        Image accentImage = accent.gameObject.AddComponent<Image>();
        accentImage.color = new Color(0.85f, 0.7f, 0.4f, 1f);
        accentImage.raycastTarget = false;

        // Icon group (put a frame image in here if you like; it hides together with the icon)
        float iconSize = Height - 14f;
        RectTransform iconGroup = Child("IconGroup", rt);
        iconGroup.anchorMin = iconGroup.anchorMax = new Vector2(0f, 0.5f);
        iconGroup.pivot = new Vector2(0f, 0.5f);
        iconGroup.sizeDelta = Vector2.one * iconSize;
        iconGroup.anchoredPosition = new Vector2(12f, 0f);

        RectTransform iconRt = Child("Icon", iconGroup);
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
        Image icon = iconRt.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        float textLeft = 12f + iconSize + 10f;
        TMP_Text title = Text("Title", rt, "Item Name", 22f, new Color(1f, 0.95f, 0.85f, 1f), FontStyles.Bold, textLeft, 8f);
        TMP_Text subtitle = Text("Subtitle", rt, "Category", 16f, new Color(0.8f, 0.72f, 0.58f, 1f), FontStyles.Normal, textLeft, 34f);

        ItemPickupPopupEntry view = root.AddComponent<ItemPickupPopupEntry>();
        view.icon = icon;
        view.iconGroup = iconGroup.gameObject;
        view.title = title;
        view.subtitle = subtitle;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, Path);
        Object.DestroyImmediate(root);

        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);
        Debug.Log("[ItemPopup] Prefab built: " + Path);
    }

    private static RectTransform Child(string name, Transform parent)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static TMP_Text Text(string name, RectTransform parent, string text, float size, Color color, FontStyles style, float left, float top)
    {
        RectTransform rt = Child(name, parent);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(left, -top - size * 1.2f);
        rt.offsetMax = new Vector2(-10f, -top);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = GameFontSettings.Font;
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }
}
