using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The curses the player carries. They stack (multipliers multiply, elite chance bonuses add up) and are
/// cleared when the player goes down to the next floor. Shows the active curses at the top left of the screen.
/// Created on first use, no scene setup needed.
/// </summary>
public class PlayerCurses : MonoBehaviour
{
    private static PlayerCurses instance;
    public static PlayerCurses Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<PlayerCurses>();
                if (instance == null) { instance = new GameObject("PlayerCurses").AddComponent<PlayerCurses>(); }
            }
            return instance;
        }
    }

    /// <summary>True if any curse exists (cheap check without creating the object).</summary>
    public static bool HasInstance => instance != null;

    private readonly List<CurseDefinition> curses = new List<CurseDefinition>();
    private TMP_Text hudText;

    public IReadOnlyList<CurseDefinition> Curses => curses;

    // ---------- Combined effects (1 / 0 when there are no curses) ----------
    public static float DamageTakenMultiplier => Combine(c => c.damageTakenMultiplier);
    public static float BlockStaminaMultiplier => Combine(c => c.blockStaminaMultiplier);
    public static float CoinDropMultiplier => Combine(c => c.coinDropMultiplier);
    public static float DamageDealtMultiplier => Combine(c => c.damageDealtMultiplier);

    public static float EliteChanceBonus
    {
        get
        {
            if (instance == null) { return 0f; }
            float sum = 0f;
            foreach (CurseDefinition c in instance.curses) { if (c != null) sum += c.eliteChanceBonus; }
            return sum;
        }
    }

    public static bool RearWarningDisabled
    {
        get
        {
            if (instance == null) { return false; }
            foreach (CurseDefinition c in instance.curses) { if (c != null && c.disableRearWarning) return true; }
            return false;
        }
    }

    private static float Combine(System.Func<CurseDefinition, float> pick)
    {
        if (instance == null) { return 1f; }
        float value = 1f;
        foreach (CurseDefinition c in instance.curses) { if (c != null) value *= pick(c); }
        return value;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        BuildHud();
    }

    // =========================================================
    // API
    // =========================================================

    /// <summary>Adds random curses from the pool (ones you don't have yet first). Returns what was added.</summary>
    public List<CurseDefinition> AddRandomCurses(int count, List<CurseDefinition> pool)
    {
        var added = new List<CurseDefinition>();
        if (pool == null) { return added; }

        for (int n = 0; n < count; n++)
        {
            float total = 0f;
            var options = new List<CurseDefinition>();
            foreach (CurseDefinition c in pool)
            {
                if (c == null || c.weight <= 0f || curses.Contains(c)) { continue; }
                options.Add(c);
                total += c.weight;
            }
            if (options.Count == 0) { break; }

            float roll = Random.value * total;
            CurseDefinition pick = options[options.Count - 1];
            foreach (CurseDefinition c in options)
            {
                roll -= c.weight;
                if (roll <= 0f) { pick = c; break; }
            }

            curses.Add(pick);
            added.Add(pick);
        }

        RefreshHud();
        return added;
    }

    public void ClearAll()
    {
        curses.Clear();
        RefreshHud();
    }

    /// <summary>Called when the player goes to the next floor: curses only last one floor.</summary>
    public static void OnNewFloor()
    {
        if (instance != null) { instance.ClearAll(); }
    }

    // =========================================================
    // HUD (top left, one line per curse in its colour)
    // =========================================================

    // The list sits just under the health / stamina / mana bars (measured on screen, so moving the bars
    // or changing resolution never makes them overlap). DefaultTop is used if the bars can't be found.
    private const float DefaultTop = -150f;
    private const float GapBelowBars = 14f;
    private RectTransform hudRect;
    private Canvas hudCanvas;
    private readonly List<Graphic> barGraphics = new List<Graphic>();
    private readonly Vector3[] corners = new Vector3[4];
    private float nextBarSearch;

    private void LateUpdate()
    {
        if (hudRect == null || hudText == null || string.IsNullOrEmpty(hudText.text)) { return; }

        barGraphics.RemoveAll(g => g == null);
        if (barGraphics.Count == 0 && Time.unscaledTime >= nextBarSearch)
        {
            nextBarSearch = Time.unscaledTime + 1f;
            FindBarGraphics();
        }

        // Lowest screen point of the visible bars
        float lowest = float.MaxValue;
        foreach (Graphic g in barGraphics)
        {
            if (!g.isActiveAndEnabled || g.canvas == null) { continue; }
            Camera cam = g.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : g.canvas.worldCamera;
            g.rectTransform.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++)
            {
                float y = RectTransformUtility.WorldToScreenPoint(cam, corners[i]).y;
                if (y < lowest) { lowest = y; }
            }
        }

        float top = DefaultTop;
        if (lowest < float.MaxValue)
        {
            float scale = hudCanvas != null && hudCanvas.scaleFactor > 0f ? hudCanvas.scaleFactor : 1f;
            float belowBars = -(Screen.height - lowest) / scale - GapBelowBars;
            top = Mathf.Min(DefaultTop, belowBars); // only ever move down, never up into the bars
        }
        if (!Mathf.Approximately(hudRect.anchoredPosition.y, top)) { hudRect.anchoredPosition = new Vector2(32f, top); }
    }

    // Every image/text of the panels that hold the player's bars
    private void FindBarGraphics()
    {
        PlayerController player = PlayerController.instance;
        if (player == null) { return; }
        foreach (Slider bar in player.HudBars)
        {
            if (bar == null) { continue; }
            // Climb to the panel directly under the bar's canvas (bar + its icon, frame and text)
            Transform panel = bar.transform;
            Canvas canvas = bar.GetComponentInParent<Canvas>();
            while (panel.parent != null && canvas != null && panel.parent != canvas.transform && !IsLarge(panel.parent)) { panel = panel.parent; }
            foreach (Graphic g in panel.GetComponentsInChildren<Graphic>(true))
            {
                if (!IsLarge(g.transform) && !barGraphics.Contains(g)) { barGraphics.Add(g); }
            }
        }
    }

    // Full-screen / big containers (HUD roots, overlays) aren't part of the bars
    private bool IsLarge(Transform t)
    {
        RectTransform r = t as RectTransform;
        if (r == null) { return false; }
        Canvas c = t.GetComponentInParent<Canvas>();
        Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
        r.GetWorldCorners(corners);
        float a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]).y;
        float b = RectTransformUtility.WorldToScreenPoint(cam, corners[1]).y;
        return Mathf.Abs(b - a) > Screen.height * 0.4f;
    }

    private void BuildHud()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rt = new GameObject("Curses", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(32f, DefaultTop);
        rt.sizeDelta = new Vector2(600f, 300f);
        hudRect = rt;
        hudCanvas = canvas;

        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        GameFontSettings.Apply(text);
        text.fontSize = 26f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 220);
        hudText = text;
        RefreshHud();
    }

    private void RefreshHud()
    {
        if (hudText == null) { return; }
        if (curses.Count == 0) { hudText.text = string.Empty; return; }

        var sb = new StringBuilder("<size=80%>CURSED (until next floor)</size>\n");
        foreach (CurseDefinition c in curses)
        {
            if (c == null) { continue; }
            sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(c.color)}>{c.displayName}</color>");
            if (!string.IsNullOrEmpty(c.description)) { sb.Append($"<size=70%>  {c.description}</size>"); }
            sb.Append('\n');
        }
        hudText.text = sb.ToString();
    }
}
