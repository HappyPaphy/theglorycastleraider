using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CameraPostProcessEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Volume postProcessVolume;

    [Header("Settings")]
    [SerializeField] private Color colorVignette;
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
            vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, defaultVignetteIntensity, Time.deltaTime * recoverySpeed);
        }

        if(lensDistortion != null)
        {
            lensDistortion.intensity.value = Mathf.Lerp(lensDistortion.intensity.value, 0f, Time.deltaTime * recoverySpeed);
        }
    }

    /// <summary>
    /// Call this whenever the player takes damage!
    /// </summary>
    public void TriggerDamageEffect()
    {
        if (vignette != null)
        {
            // Instantly punch the vignette intensity up
            vignette.intensity.value = vignetteIntensity;
        }

        if (lensDistortion != null)
        {
            // Optional: You can punch or warp lens distortion on impact too
            lensDistortion.intensity.value = -0.4f;
        }
    }
}
