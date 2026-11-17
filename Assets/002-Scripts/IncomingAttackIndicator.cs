using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Flashing red chevron at the edge of the screen that points at an enemy winding up an attack the player
/// can't block (outside the guard cone, e.g. from behind). It keeps tracking the attacker while you turn,
/// and fades out as soon as you face it (then you can block) or when the attack lands.
/// Built entirely in code: call <see cref="Warn"/> from an enemy's attack wind-up, no scene setup needed.
/// </summary>
public class IncomingAttackIndicator : MonoBehaviour
{
    /// <summary>
    /// Your own icon: put an image at Assets/Resources/UI/IncomingAttackIcon.png (any size, transparent background).
    /// Draw it pointing UP: up = towards the screen edge = towards the attacker. Its own colours are used as-is.
    /// Without that file the built-in red chevron is used.
    /// </summary>
    public const string CustomIconResource = "UI/IncomingAttackIcon";

    [Header("Look")]
    [Tooltip("Tint for the built-in chevron. A custom icon keeps its own colours.")]
    [SerializeField] private Color color = new Color(1f, 0.12f, 0.08f, 1f);
    [Tooltip("Distance of the icon from the screen centre (reference 1920x1080).")]
    [SerializeField] private float radius = 360f;
    [Tooltip("Width of the icon on a 1920x1080 screen. The height follows the icon's own proportions.")]
    [SerializeField] private float iconWidth = 240f;
    [Tooltip("Flashes per second.")]
    [SerializeField] private float flashRate = 9f;
    [Tooltip("Extra time the warning stays after the wind-up, so it overlaps the hit.")]
    [SerializeField] private float linger = 0.2f;
    [SerializeField] private float fadeOutSpeed = 8f;

    private class Warning
    {
        public Transform attacker;
        public RectTransform pivot;
        public Image image;
        public float timeLeft;
        public float alpha;
        public float age;
    }

    private static IncomingAttackIndicator instance;
    private readonly List<Warning> warnings = new List<Warning>();
    private readonly Stack<Warning> pool = new Stack<Warning>();
    private RectTransform root;
    private Sprite chevron;
    private bool isCustomIcon;
    private bool ownsSprite;
    private Color tint;

    // =========================================================
    // API
    // =========================================================

    /// <summary>
    /// An enemy is about to attack. Shows the warning if the attack comes from outside the player's guard cone.
    /// </summary>
    /// <param name="attacker">The attacking enemy.</param>
    /// <param name="windup">Seconds until the hit lands.</param>
    public static void Warn(Transform attacker, float windup)
    {
        if (attacker == null) { return; }
        if (PlayerCurses.RearWarningDisabled) { return; } // curse: Blind Spot

        PlayerController player = PlayerController.instance;
        if (player == null || player.CharacterHealthComponent.CurrentHP <= 0f) { return; }

        // Only attacks you can't block right now
        if (InteractFacing.IsPlayerFacing(attacker.position, player.GuardAngle)) { return; }

        if (instance == null) { instance = new GameObject("IncomingAttackIndicator").AddComponent<IncomingAttackIndicator>(); }
        instance.AddWarning(attacker, windup);
    }

    // =========================================================
    // SETUP
    // =========================================================

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = new GameObject("Warnings", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;

        LoadIcon();
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; }
        if (chevron != null && ownsSprite)
        {
            if (!isCustomIcon) { Destroy(chevron.texture); } // the built-in texture was made at runtime
            Destroy(chevron);
        }
    }

    private void AddWarning(Transform attacker, float windup)
    {
        float duration = Mathf.Max(0.35f, windup) + linger;

        // Same attacker again (combo): just refresh
        foreach (Warning w in warnings)
        {
            if (w.attacker == attacker) { w.timeLeft = Mathf.Max(w.timeLeft, duration); w.age = 0f; return; }
        }

        Warning warning = pool.Count > 0 ? pool.Pop() : CreateWarning();
        warning.attacker = attacker;
        warning.timeLeft = duration;
        warning.alpha = 0f;
        warning.age = 0f;
        warning.pivot.gameObject.SetActive(true);
        warnings.Add(warning);
    }

    private Warning CreateWarning()
    {
        // Pivot at the screen centre, rotated towards the attacker; the chevron sits "up" at the radius
        RectTransform pivot = new GameObject("Warning", typeof(RectTransform)).GetComponent<RectTransform>();
        pivot.SetParent(root, false);
        pivot.sizeDelta = Vector2.zero;

        RectTransform icon = new GameObject("Chevron", typeof(RectTransform)).GetComponent<RectTransform>();
        icon.SetParent(pivot, false);
        icon.anchoredPosition = new Vector2(0f, radius);
        float aspect = chevron.rect.height / Mathf.Max(1f, chevron.rect.width);
        icon.sizeDelta = new Vector2(iconWidth, iconWidth * aspect);

        Image image = icon.gameObject.AddComponent<Image>();
        image.sprite = chevron;
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = new Color(tint.r, tint.g, tint.b, 0f);

        return new Warning { pivot = pivot, image = image };
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        PlayerController player = PlayerController.instance;
        Camera cam = Camera.main;
        bool playerAlive = player != null && player.CharacterHealthComponent.CurrentHP > 0f;

        for (int i = warnings.Count - 1; i >= 0; i--)
        {
            Warning w = warnings[i];
            w.timeLeft -= Time.deltaTime;
            w.age += Time.deltaTime;

            bool attackerGone = w.attacker == null || !w.attacker.gameObject.activeInHierarchy;
            bool nowFacing = !attackerGone && playerAlive && InteractFacing.IsPlayerFacing(w.attacker.position, player.GuardAngle);
            bool active = playerAlive && !attackerGone && !nowFacing && w.timeLeft > 0f;

            // Fade in fast while active, fade out when done / faced
            w.alpha = active ? Mathf.MoveTowards(w.alpha, 1f, Time.deltaTime * 12f)
                             : Mathf.MoveTowards(w.alpha, 0f, Time.deltaTime * fadeOutSpeed);

            if (!active && w.alpha <= 0f)
            {
                w.pivot.gameObject.SetActive(false);
                warnings.RemoveAt(i);
                pool.Push(w);
                continue;
            }

            // Point at the attacker relative to where the camera looks (up on screen = straight ahead)
            if (!attackerGone && cam != null)
            {
                Vector3 forward = cam.transform.forward; forward.y = 0f;
                Vector3 toAttacker = w.attacker.position - cam.transform.position; toAttacker.y = 0f;
                if (forward.sqrMagnitude > 0.0001f && toAttacker.sqrMagnitude > 0.0001f)
                {
                    float angle = Vector3.SignedAngle(forward, toAttacker, Vector3.up); // + = to the right
                    w.pivot.localRotation = Quaternion.Euler(0f, 0f, -angle);
                }
            }

            // Flash: pulse between dim and full, with a small pop when it appears
            float flash = 0.55f + 0.45f * Mathf.Sin(w.age * flashRate * Mathf.PI * 2f);
            Color c = tint;
            c.a = w.alpha * flash;
            w.image.color = c;

            float pop = 1f + Mathf.Max(0f, 0.35f - w.age) * 1.2f;
            w.image.rectTransform.localScale = new Vector3(pop, pop, 1f);
        }
    }

    // Custom icon from Resources (imported as a Sprite or as a plain Texture), otherwise the built-in chevron
    private void LoadIcon()
    {
        Sprite custom = Resources.Load<Sprite>(CustomIconResource);
        if (custom == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(CustomIconResource);
            if (tex != null)
            {
                custom = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                ownsSprite = true;
            }
        }

        if (custom != null)
        {
            chevron = custom;
            isCustomIcon = true;
            tint = Color.white; // keep the artist's colours
            return;
        }

        chevron = BuildChevronSprite();
        ownsSprite = true;
        tint = color;
    }

    // =========================================================
    // SPRITE (thick chevron pointing up = away from the screen centre, soft edges)
    // =========================================================

    private static Sprite BuildChevronSprite()
    {
        const int w = 96, h = 56;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];

        Vector2 tip = new Vector2(w * 0.5f, h - 8f);
        Vector2 left = new Vector2(8f, 10f);
        Vector2 right = new Vector2(w - 8f, 10f);
        const float thickness = 9f;
        const float softness = 2.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(DistanceToSegment(p, left, tip), DistanceToSegment(p, right, tip));
                float a = Mathf.Clamp01((thickness - d) / softness);
                // Faint glow around the stroke
                float glow = Mathf.Clamp01((thickness + 8f - d) / 8f) * 0.25f;
                px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Max(a, glow));
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }
}
