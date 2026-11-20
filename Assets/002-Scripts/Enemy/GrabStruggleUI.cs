using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Mash to break free" prompt + struggle bar shown while a Brute holds the player. Builds itself on first use.
/// </summary>
public class GrabStruggleUI : MonoBehaviour
{
    private static GrabStruggleUI instance;

    private CanvasGroup group;
    private RectTransform fill;
    private TextMeshProUGUI label;
    private float shownTime;

    private const float BarWidth = 420f;
    private const float BarHeight = 22f;

    public static void Show()
    {
        Ensure();
        instance.group.alpha = 1f;
        instance.shownTime = Time.time;
        instance.label.text = PromptText();
        SetProgress(0f);
    }

    public static void Hide()
    {
        if (instance != null) { instance.group.alpha = 0f; }
    }

    public static void SetProgress(float value)
    {
        if (instance == null) { return; }
        instance.fill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(value), BarHeight);
    }

    private static string PromptText()
    {
        InputMode mode = InputSchemeManager.instance != null ? InputSchemeManager.instance.CurrentInputMode : InputMode.PC;
        switch (mode)
        {
            case InputMode.Xbox: return "MASH  A  TO BREAK FREE";
            case InputMode.PlayStation: return "MASH  CROSS  TO BREAK FREE";
            default: return "MASH  SPACE  TO BREAK FREE";
        }
    }

    private void Update()
    {
        // Pulse the prompt so it reads as urgent
        if (group.alpha > 0f) { label.transform.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin((Time.time - shownTime) * 9f)) * 0.08f); }
    }

    private static void Ensure()
    {
        if (instance != null) { return; }

        var go = new GameObject("GrabStruggleUI");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GrabStruggleUI>();

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 46;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        instance.group = go.AddComponent<CanvasGroup>();
        instance.group.alpha = 0f;
        instance.group.blocksRaycasts = false;
        instance.group.interactable = false;

        // Below the centre of the screen
        var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(go.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, -170f);
        panel.sizeDelta = new Vector2(BarWidth + 12f, 90f);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(panel, false);
        instance.label = labelGo.AddComponent<TextMeshProUGUI>();
        GameFontSettings.Apply(instance.label);
        instance.label.alignment = TextAlignmentOptions.Center;
        instance.label.fontSize = 34f;
        instance.label.color = new Color(1f, 0.92f, 0.75f);
        var lr = instance.label.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.sizeDelta = new Vector2(0f, 44f);
        lr.anchoredPosition = Vector2.zero;

        var back = new GameObject("Bar", typeof(RectTransform)).GetComponent<RectTransform>();
        back.SetParent(panel, false);
        back.anchorMin = back.anchorMax = new Vector2(0.5f, 0f);
        back.pivot = new Vector2(0.5f, 0f);
        back.sizeDelta = new Vector2(BarWidth + 6f, BarHeight + 6f);
        back.anchoredPosition = new Vector2(0f, 6f);
        back.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.05f, 0.04f, 0.85f);

        instance.fill = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
        instance.fill.SetParent(back, false);
        instance.fill.anchorMin = instance.fill.anchorMax = new Vector2(0f, 0.5f);
        instance.fill.pivot = new Vector2(0f, 0.5f);
        instance.fill.anchoredPosition = new Vector2(3f, 0f);
        instance.fill.gameObject.AddComponent<Image>().color = new Color(0.95f, 0.75f, 0.3f);
        SetProgress(0f);
    }
}
