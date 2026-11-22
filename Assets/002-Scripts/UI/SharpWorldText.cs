using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Keeps floating 3D text readable. The world is drawn at a low render scale (PC_RPAsset Render Scale 0.2,
/// the game's retro look), so a 3D TextMeshPro in the world turns blurry. This hides the 3D text and draws an
/// identical copy on a full-resolution screen overlay, at the same spot and the same apparent size,
/// hidden behind walls and when behind the camera.
/// Add it next to any world-space TextMeshPro; the original keeps working as before (text, colour, fades,
/// moving / rising) and is mirrored every frame.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class SharpWorldText : MonoBehaviour
{
    [Tooltip("Hidden beyond this distance (metres).")]
    public float maxDistance = 30f;
    [Tooltip("Fade out over the last part of maxDistance (metres).")]
    public float fadeDistance = 5f;
    [Tooltip("Hide when a wall is between the camera and the text.")]
    public bool hideBehindWalls = true;
    [Tooltip("Smallest size in pixels before it is hidden (too far to read anyway).")]
    public float minPixelSize = 6f;

    private static Canvas overlay;
    private static readonly RaycastHit[] hits = new RaycastHit[8];
    private static int wallMask = -1;

    private TextMeshPro source;
    private MeshRenderer sourceRenderer;
    private TextMeshProUGUI copy;
    private RectTransform copyRect;
    private float unitsPerPixel; // 3D local units per UI pixel at the same font size (font constant)
    private Transform owner;     // object the text belongs to (ignored by the wall check)

    private void Awake()
    {
        source = GetComponent<TextMeshPro>();
        sourceRenderer = GetComponent<MeshRenderer>();
        owner = transform.parent != null ? transform.parent : transform;
    }

    private void OnEnable()
    {
        EnsureCopy();
        if (sourceRenderer != null) { sourceRenderer.enabled = false; } // the sharp copy replaces it
    }

    private void OnDisable()
    {
        if (copy != null) { copy.gameObject.SetActive(false); }
    }

    private void OnDestroy()
    {
        if (copy != null) { Destroy(copy.gameObject); }
    }

    private static Canvas Overlay
    {
        get
        {
            if (overlay == null)
            {
                var go = new GameObject("Sharp World Text");
                overlay = go.AddComponent<Canvas>();
                overlay.renderMode = RenderMode.ScreenSpaceOverlay;
                overlay.sortingOrder = -10; // under the HUD and menus
                overlay.pixelPerfect = false;
                // no CanvasScaler: 1 canvas unit = 1 screen pixel
            }
            return overlay;
        }
    }

    private static int WallMask
    {
        get
        {
            if (wallMask == -1)
            {
                wallMask = Physics.DefaultRaycastLayers;
                foreach (string layer in new[] { "Player", "Enemy", "Ignore Raycast", "UI", "Water" })
                {
                    int l = LayerMask.NameToLayer(layer);
                    if (l >= 0) { wallMask &= ~(1 << l); }
                }
            }
            return wallMask;
        }
    }

    private void EnsureCopy()
    {
        if (copy != null) { return; }
        var go = new GameObject(name + " (Sharp)", typeof(RectTransform));
        go.transform.SetParent(Overlay.transform, false);
        copy = go.AddComponent<TextMeshProUGUI>();
        copy.raycastTarget = false;
        copyRect = copy.rectTransform;
        copyRect.anchorMin = copyRect.anchorMax = Vector2.zero; // positioned in screen pixels
        copyRect.pivot = new Vector2(0.5f, 0.5f);
    }

    // Ratio between the 3D text and the UI text for the same font size, measured once
    private bool Measure()
    {
        if (unitsPerPixel > 0f) { return true; }
        if (source.font == null) { return false; }

        string savedText = source.text;
        source.text = "Hg";
        source.ForceMeshUpdate(true, true);
        float h3d = source.textBounds.size.y;
        source.text = savedText;
        source.ForceMeshUpdate(true, true);

        copy.font = source.font;
        copy.fontSize = source.fontSize;
        copy.text = "Hg";
        copy.ForceMeshUpdate(true, true);
        float hUi = copy.textBounds.size.y;

        if (h3d <= 0f || hUi <= 0f) { return false; }
        unitsPerPixel = h3d / hUi;
        return true;
    }

    private void LateUpdate()
    {
        if (copy == null) { return; }
        Camera cam = Camera.main;
        string text = source.text;
        if (cam == null || !source.enabled || string.IsNullOrEmpty(text) || !Measure()) { Hide(); return; }

        // The text's own pivot point (the copy uses the same pivot, so alignment/layout match exactly)
        Vector3 pos = transform.position;
        Vector3 toText = pos - cam.transform.position;
        float distance = toText.magnitude;

        // Behind the camera or too far
        if (Vector3.Dot(cam.transform.forward, toText) <= 0.05f || distance > maxDistance) { Hide(); return; }

        // Behind a wall
        if (hideBehindWalls && Blocked(cam.transform.position, toText, distance)) { Hide(); return; }

        // Size: how many screen pixels one world metre covers at the text's distance
        Vector3 screen = cam.WorldToScreenPoint(pos);
        Vector3 up = cam.WorldToScreenPoint(pos + cam.transform.up);
        float pixelsPerMetre = Mathf.Abs(up.y - screen.y);
        float pixelsPerLocal = pixelsPerMetre * Mathf.Abs(transform.lossyScale.y);
        float fontSize = source.fontSize * unitsPerPixel * pixelsPerLocal;
        if (fontSize < minPixelSize) { Hide(); return; }

        if (!copy.gameObject.activeSelf) { copy.gameObject.SetActive(true); }

        // Mirror the text and its look
        if (copy.text != text) { copy.text = text; }
        if (copy.font != source.font) { copy.font = source.font; }
        copy.fontSize = fontSize;
        copy.fontStyle = source.fontStyle;
        copy.alignment = source.alignment;
        copy.richText = source.richText;
        copy.textWrappingMode = source.textWrappingMode;
        copy.overflowMode = TextOverflowModes.Overflow;
        copy.lineSpacing = source.lineSpacing;
        copy.characterSpacing = source.characterSpacing;
        if (!Mathf.Approximately(copy.outlineWidth, source.outlineWidth)) { copy.outlineWidth = source.outlineWidth; }
        if (source.outlineWidth > 0f && !copy.outlineColor.Equals(source.outlineColor)) { copy.outlineColor = source.outlineColor; }

        Color c = source.color;
        float fadeStart = maxDistance - Mathf.Max(0.01f, fadeDistance);
        if (distance > fadeStart) { c.a *= Mathf.Clamp01((maxDistance - distance) / Mathf.Max(0.01f, fadeDistance)); }
        copy.color = c;

        // Same box as the 3D text, in pixels
        Vector2 box = source.rectTransform.rect.size * pixelsPerLocal;
        copyRect.pivot = source.rectTransform.pivot;
        copyRect.sizeDelta = new Vector2(Mathf.Max(box.x, 1f), Mathf.Max(box.y, 1f));
        copyRect.anchoredPosition = new Vector2(screen.x, screen.y);
    }

    private bool Blocked(Vector3 from, Vector3 dir, float distance)
    {
        int n = Physics.RaycastNonAlloc(from, dir / distance, hits, distance - 0.2f, WallMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Transform t = hits[i].collider.transform;
            if (t == owner || t.IsChildOf(owner)) { continue; } // the altar / enemy the text belongs to
            return true;
        }
        return false;
    }

    private void Hide()
    {
        if (copy != null && copy.gameObject.activeSelf) { copy.gameObject.SetActive(false); }
    }

    /// <summary>Adds a SharpWorldText to this 3D text if it doesn't have one.</summary>
    public static SharpWorldText Attach(TextMeshPro text)
    {
        if (text == null) { return null; }
        SharpWorldText s = text.GetComponent<SharpWorldText>();
        return s != null ? s : text.gameObject.AddComponent<SharpWorldText>();
    }
}
