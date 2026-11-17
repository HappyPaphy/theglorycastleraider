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

    [Header("Riposte Ready (blue border)")]
    [SerializeField] private Color colorVignette_Riposte = new Color(0.2f, 0.55f, 1f, 1f);
    [SerializeField] private float riposteVignetteIntensity = 0.55f;
    [Tooltip("Seconds the blue border stays at full strength before fading out.")]
    [SerializeField] private float riposteHoldDuration = 0.5f;

    private Vignette vignette;
    private LensDistortion lensDistortion;
    private float vignetteHoldTimer;

    private void Awake()
    {
        // Grab the overrides from the Volume's Profile
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out lensDistortion);
        }
    }

    private void Update()
    {
        // Smoothly fade the vignette intensity back to normal over time
        if (vignette != null)
        {
            if (PlayerController.instance.CharacterHealthComponent.CurrentHP <= 0)
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

            if(vignette.intensity.value <= 0f)
            {
                vignette.color.value = colorVignette_Default;
            }
        }

        if(lensDistortion != null)
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

    /// <summary>
    /// Call this whenever the player takes damage!
    /// </summary>
    public void TriggerDamageEffect()
    {
        vignetteHoldTimer = 0f; // getting hit always overrides other flashes

        if (vignette != null)
        {
            vignette.color.value = colorVignette_Damage;
            vignette.intensity.value = vignetteIntensity;
        }

        if (lensDistortion != null)
        {
            // Optional: You can punch or warp lens distortion on impact too
            lensDistortion.intensity.value = -0.4f;
        }
    }

    public void TriggerHealEffect()
    {
        if (vignette != null)
        {
            vignette.color.value = colorVignette_Heal;
            vignette.intensity.value = vignetteIntensity;
        }

        if (lensDistortion != null)
        {
            // Optional: You can punch or warp lens distortion on impact too
            lensDistortion.intensity.value = -0.4f;
        }
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

    public void TriggerManaEffect()
    {
        if (vignette != null)
        {
            vignette.color.value = colorVignette_Mana;
            vignette.intensity.value = vignetteIntensity;
        }

        if (lensDistortion != null)
        {
            // Optional: You can punch or warp lens distortion on impact too
            lensDistortion.intensity.value = -0.4f;
        }
    }
}
