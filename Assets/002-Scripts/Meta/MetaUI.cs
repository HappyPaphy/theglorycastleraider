using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Small helpers to build the meta progression screens in code (1920x1080 layout, game font).</summary>
public static class MetaUI
{
    public static readonly Color Panel = new Color(0.07f, 0.055f, 0.04f, 0.94f);
    public static readonly Color Gold = new Color(0.95f, 0.78f, 0.42f, 1f);
    public static readonly Color Text = new Color(1f, 0.95f, 0.85f, 1f);
    public static readonly Color Dim = new Color(0.78f, 0.72f, 0.6f, 1f);
    public static readonly Color Good = new Color(0.6f, 0.95f, 0.55f, 1f);
    public static readonly Color Bad = new Color(0.95f, 0.45f, 0.4f, 1f);

    public static Canvas NewCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>Box anchored to a point of the parent (anchor = pivot), at an offset, with a size.</summary>
    public static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        RectTransform rt = Rect(name, parent);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = offset;
        rt.sizeDelta = size;
        return rt;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public static Image Image(RectTransform rt, Color color, bool raycast = false)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    public static TextMeshProUGUI Label(RectTransform rt, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, FontStyles style = FontStyles.Normal)
    {
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        GameFontSettings.Apply(t);
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    /// <summary>Horizontal progress bar (0..1). Returns the fill image.</summary>
    public static Image Bar(RectTransform rt, float value, Color fill)
    {
        Image(rt, new Color(0f, 0f, 0f, 0.55f));
        RectTransform f = Rect("Fill", rt);
        f.anchorMin = Vector2.zero;
        f.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        f.offsetMin = f.offsetMax = Vector2.zero;
        return Image(f, fill);
    }

    public static Button Button(RectTransform rt, string text, float size, out TextMeshProUGUI label)
    {
        Image bg = Image(rt, new Color(0.25f, 0.19f, 0.12f, 1f), true);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        ColorBlock colors = b.colors;
        colors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.highlightedColor = new Color(1.25f, 1.15f, 0.9f, 1f);
        colors.selectedColor = new Color(1.35f, 1.2f, 0.85f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.colorMultiplier = 1.4f;
        b.colors = colors;

        RectTransform l = Rect("Label", rt);
        Stretch(l);
        label = Label(l, text, size, Text, TextAlignmentOptions.Center, FontStyles.Bold);
        return b;
    }
}
