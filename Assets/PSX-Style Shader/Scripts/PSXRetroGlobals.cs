using UnityEngine;

namespace PSXStyleShader
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PSXRetroGlobals : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;

        [Header("Screen-Space Vertex Snap")]
        [SerializeField] private Vector2Int _snapResolution = new Vector2Int(320, 240);
        [SerializeField] private bool _useScreenResolution;
        [SerializeField, Range(0f, 1f)] private float _snapStrength = 1f;
        [SerializeField, Min(0f)] private float _jitterSpeed = 30f;
        [SerializeField, Range(0f, 2f)] private float _jitterPixels = 1f;

        [Header("Depth Quantization")]
        [SerializeField, Min(1f)] private float _depthSteps = 48f;
        [SerializeField, Range(0f, 1f)] private float _depthStrength = 0.75f;
        [SerializeField, Range(0f, 0.01f)] private float _depthNoise = 0.0015f;

        [Header("UV / Texture")]
        [SerializeField, Min(1f)] private float _uvPrecision = 96f;
        [SerializeField, Range(0f, 1f)] private float _uvSnapStrength = 1f;
        [SerializeField, Range(0f, 1f)] private float _uvWarpStrength = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _affineSwim = 0.25f;
        [SerializeField] private bool _affineUvInterpolation = true;
        [SerializeField, Range(-4f, 4f)] private float _mipBias = -1f;

        [Header("Lighting")]
        [SerializeField] private bool _vertexLighting = true;
        [SerializeField, Range(1f, 16f)] private float _lightSteps = 6f;

        [Header("Color Quantize + Dither")]
        [SerializeField, Range(1f, 8f)] private float _colorBits = 5f;
        [SerializeField, Range(0f, 1f)] private float _ditherStrength = 0.65f;

        private static readonly int EnabledId = Shader.PropertyToID("_PSX_Enabled");
        private static readonly int SnapResolutionId = Shader.PropertyToID("_PSX_SnapResolution");
        private static readonly int UseScreenResolutionId = Shader.PropertyToID("_PSX_UseScreenResolution");
        private static readonly int SnapStrengthId = Shader.PropertyToID("_PSX_SnapStrength");
        private static readonly int JitterSpeedId = Shader.PropertyToID("_PSX_JitterSpeed");
        private static readonly int JitterPixelsId = Shader.PropertyToID("_PSX_JitterPixels");

        private static readonly int DepthStepsId = Shader.PropertyToID("_PSX_DepthSteps");
        private static readonly int DepthStrengthId = Shader.PropertyToID("_PSX_DepthStrength");
        private static readonly int DepthNoiseId = Shader.PropertyToID("_PSX_DepthNoise");

        private static readonly int UvPrecisionId = Shader.PropertyToID("_PSX_UvPrecision");
        private static readonly int UvSnapStrengthId = Shader.PropertyToID("_PSX_UvSnapStrength");
        private static readonly int UvWarpStrengthId = Shader.PropertyToID("_PSX_UvWarpStrength");
        private static readonly int AffineSwimId = Shader.PropertyToID("_PSX_AffineSwim");
        private static readonly int MipBiasId = Shader.PropertyToID("_PSX_MipBias");

        private static readonly int VertexLightingId = Shader.PropertyToID("_PSX_VertexLighting");
        private static readonly int LightStepsId = Shader.PropertyToID("_PSX_LightSteps");

        private static readonly int ColorBitsId = Shader.PropertyToID("_PSX_ColorBits");
        private static readonly int DitherStrengthId = Shader.PropertyToID("_PSX_DitherStrength");

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(EnabledId, 0f);
            Shader.DisableKeyword("PSX_AFFINE");
        }

        private void Update()
        {
            Apply();
        }

        private void Apply()
        {
            Shader.SetGlobalFloat(EnabledId, _enabled ? 1f : 0f);

            Shader.SetGlobalVector(SnapResolutionId, new Vector4(_snapResolution.x, _snapResolution.y, 0f, 0f));
            Shader.SetGlobalFloat(UseScreenResolutionId, _useScreenResolution ? 1f : 0f);
            Shader.SetGlobalFloat(SnapStrengthId, Mathf.Clamp01(_snapStrength));
            Shader.SetGlobalFloat(JitterSpeedId, Mathf.Max(0f, _jitterSpeed));
            Shader.SetGlobalFloat(JitterPixelsId, Mathf.Clamp(_jitterPixels, 0f, 2f));

            Shader.SetGlobalFloat(DepthStepsId, Mathf.Max(1f, _depthSteps));
            Shader.SetGlobalFloat(DepthStrengthId, Mathf.Clamp01(_depthStrength));
            Shader.SetGlobalFloat(DepthNoiseId, Mathf.Clamp(_depthNoise, 0f, 0.01f));

            Shader.SetGlobalFloat(UvPrecisionId, Mathf.Max(1f, _uvPrecision));
            Shader.SetGlobalFloat(UvSnapStrengthId, Mathf.Clamp01(_uvSnapStrength));
            Shader.SetGlobalFloat(UvWarpStrengthId, Mathf.Clamp01(_uvWarpStrength));
            Shader.SetGlobalFloat(AffineSwimId, Mathf.Clamp01(_affineSwim));
            Shader.SetGlobalFloat(MipBiasId, Mathf.Clamp(_mipBias, -4f, 4f));

            Shader.SetGlobalFloat(VertexLightingId, _vertexLighting ? 1f : 0f);
            Shader.SetGlobalFloat(LightStepsId, Mathf.Clamp(_lightSteps, 1f, 16f));

            Shader.SetGlobalFloat(ColorBitsId, Mathf.Clamp(_colorBits, 1f, 8f));
            Shader.SetGlobalFloat(DitherStrengthId, Mathf.Clamp01(_ditherStrength));

            if (_affineUvInterpolation)
            {
                Shader.EnableKeyword("PSX_AFFINE");
            }
            else
            {
                Shader.DisableKeyword("PSX_AFFINE");
            }
        }
    }
}
