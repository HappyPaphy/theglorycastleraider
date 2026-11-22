using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CameraPostProcessEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Volume postProcessVolume;

    [Header("Settings")]
    [SerializeField] private Color colorVignette_Default;
    [SerializeField] private Color colorVignette_Damage;
    [SerializeField] private Color colorVignette_Heal;
    [SerializeField] private Color colorVignette_Mana;
    [SerializeField] private float vignetteIntensity = 0.75f; // Peak intensity when hit
    [SerializeField] private float defaultVignetteIntensity = 0f; // Normal baseline
    [SerializeField] private float recoverySpeed = 5f;

    [Header("Flash thickness (damage / health potion / mana potion)")]
    [Tooltip("How far the coloured border reaches into the screen at the peak of a flash (0..1, higher = thicker).")]
    [SerializeField, Range(0f, 1f)] private float flashVignetteSmoothness = 1f;
    [Tooltip("How strongly the flash colour also washes over the whole screen at its peak (0 = border only).")]
    [SerializeField, Range(0f, 1f)] private float flashScreenTint = 0.35f;

    [Header("Riposte Ready (blue border)")]
    [SerializeField] private Color colorVignette_Riposte = new Color(0.2f, 0.55f, 1f, 1f);
    [SerializeField] private float riposteVignetteIntensity = 0.55f;
    [Tooltip("Seconds the blue border stays at full strength before fading out.")]
    [SerializeField] private float riposteHoldDuration = 0.5f;

    [Header("Sword Time (parry slow-down): camera turns blue")]
    [Tooltip("Colour filter while time is slowed (white = no change).")]
    [SerializeField] private Color swordTimeColorFilter = new Color(0.35f, 0.55f, 1f, 1f);
    [Tooltip("Saturation change while slowed (-100..100).")]
    [SerializeField] private float swordTimeSaturation = -45f;
    [Tooltip("Blue border while slowed (0 = none).")]
    [SerializeField, Range(0f, 1f)] private float swordTimeVignette = 0.5f;
    [SerializeField] private Color swordTimeVignetteColor = new Color(0.15f, 0.4f, 1f, 1f);
    [Tooltip("Seconds (real time) to blend in / out.")]
    [SerializeField] private float swordTimeFadeIn = 0.06f;
    [SerializeField] private float swordTimeFadeOut = 0.25f;

    public static CameraPostProcessEffect Instance { get; private set; }

    private Vignette vignette;
    private LensDistortion lensDistortion;
    private ColorAdjustments colorAdjustments;
    private float vignetteHoldTimer;

    // Original grading, restored when no effect is running
    private Color baseColorFilter = Color.white;
    private float baseSaturation;
    private float baseSmoothness = 0.2f;

    // Sword Time blend (0 = normal, 1 = full blue)
    private bool swordTimeOn;
    private float swordTimeBlend;

    // Flash screen wash (1 at the peak, fades with the vignette)
    private float flashAmount;
    private Color flashColor = Color.white;
    private bool gradingDirty;

    private void Awake()
    {
        // Grab the overrides from the Volume's Profile
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out lensDistortion);

            // .profile is this camera's own runtime copy, so adding the override doesn't touch the asset
            if (!postProcessVolume.profile.TryGet(out colorAdjustments))
            {
                colorAdjustments = postProcessVolume.profile.Add<ColorAdjustments>(false);
            }
            baseColorFilter = colorAdjustments.colorFilter.overrideState ? colorAdjustments.colorFilter.value : Color.white;
            baseSaturation = colorAdjustments.saturation.overrideState ? colorAdjustments.saturation.value : 0f;
            if (vignette != null && vignette.smoothness.overrideState) { baseSmoothness = vignette.smoothness.value; }
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; }
    }

    /// <summary>Sword Time slow-down on/off: blends the camera to blue and back (in real time).</summary>
    public void SetSwordTime(bool on) => swordTimeOn = on;

    private void Update()
    {
        // Sword Time blend runs in real time (the game itself is slowed)
        float target = swordTimeOn ? 1f : 0f;
        float blendTime = Mathf.Max(0.01f, swordTimeOn ? swordTimeFadeIn : swordTimeFadeOut);
        swordTimeBlend = Mathf.MoveTowards(swordTimeBlend, target, Time.unscaledDeltaTime / blendTime);

        // Smoothly fade the vignette intensity back to normal over time
        if (vignette != null)
        {
            if (PlayerController.instance != null && PlayerController.instance.CharacterHealthComponent.CurrentHP <= 0)
            {
                defaultVignetteIntensity = 0.8f;
            }

            // Hold the current flash (e.g. Riposte ready) before fading
            if (vignetteHoldTimer > 0f)
            {
                vignetteHoldTimer -= Time.deltaTime;
            }
            else
            {
                vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, defaultVignetteIntensity, Time.deltaTime * recoverySpeed);
            }

            // Thick border at the flash peak, back to the normal edge as it fades
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = Mathf.Lerp(baseSmoothness, flashVignetteSmoothness, Mathf.Max(flashAmount, swordTimeBlend));

            // Sword Time: blue border (a fresh damage/heal flash still shows on top)
            if (swordTimeBlend > 0f)
            {
                if (flashAmount < 0.1f) { vignette.color.value = swordTimeVignetteColor; }
                vignette.intensity.value = Mathf.Max(vignette.intensity.value, swordTimeVignette * swordTimeBlend);
            }

            if (vignette.intensity.value <= 0.001f)
            {
                vignette.color.value = colorVignette_Default;
            }
        }

        // The flash's whole-screen wash fades at the same pace as the border
        flashAmount = Mathf.Lerp(flashAmount, 0f, Time.deltaTime * recoverySpeed);
        if (flashAmount < 0.005f) { flashAmount = 0f; }

        ApplyGrading();

        if (lensDistortion != null && PlayerController.instance != null)
        {
            if (PlayerController.instance.CharacterHealthComponent.CurrentHP > 0)
            {
                lensDistortion.intensity.value = Mathf.Lerp(lensDistortion.intensity.value, 0f, Time.deltaTime * recoverySpeed);
            }
            else
            {
                lensDistortion.intensity.value = Mathf.Lerp(lensDistortion.intensity.value, -0.75f, Time.deltaTime * recoverySpeed);
            }
        }
    }

    // Colour filter / saturation: the original grading, blended toward Sword Time blue and the flash colour
    private void ApplyGrading()
    {
        if (colorAdjustments == null) { return; }
        bool active = swordTimeBlend > 0f || flashAmount > 0f;
        if (!active && !gradingDirty) { return; }

        Color filter = Color.Lerp(baseColorFilter, swordTimeColorFilter, swordTimeBlend);
        filter = Color.Lerp(filter, flashColor, flashAmount * flashScreenTint);

        colorAdjustments.active = true;
        colorAdjustments.colorFilter.overrideState = true;
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.colorFilter.value = filter;
        colorAdjustments.saturation.value = Mathf.Lerp(baseSaturation, swordTimeSaturation, swordTimeBlend);

        gradingDirty = active; // one more pass after everything ends writes the original values back
    }

    // Damage / heal / mana: strong, thick coloured flash
    private void Flash(Color color)
    {
        if (vignette != null)
        {
            vignette.color.value = color;
            vignette.intensity.value = vignetteIntensity;
        }
        flashColor = color;
        flashAmount = 1f;
        gradingDirty = true;

        if (lensDistortion != null)
        {
            // Optional: You can punch or warp lens distortion on impact too
            lensDistortion.intensity.value = -0.4f;
        }
    }

    /// <summary>
    /// Call this whenever the player takes damage!
    /// </summary>
    public void TriggerDamageEffect()
    {
        vignetteHoldTimer = 0f; // getting hit always overrides other flashes
        Flash(colorVignette_Damage);
    }

    public void TriggerHealEffect()
    {
        Flash(colorVignette_Heal);
    }

    public void TriggerManaEffect()
    {
        Flash(colorVignette_Mana);
    }

    /// <summary>Blue screen border: Riposte Echo charges are full and ready to release.</summary>
    public void TriggerRiposteReadyEffect()
    {
        if (vignette != null)
        {
            vignette.color.value = colorVignette_Riposte;
            vignette.intensity.value = riposteVignetteIntensity;
            vignetteHoldTimer = riposteHoldDuration;
        }

        if (lensDistortion != null)
        {
            lensDistortion.intensity.value = -0.2f;
        }
    }
}
