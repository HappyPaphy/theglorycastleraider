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

    private Vignette vignette;
    private LensDistortion lensDistortion;

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

            vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, defaultVignetteIntensity, Time.deltaTime * recoverySpeed);

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
