using UnityEngine;

namespace PSXStyleShader
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class PSXDownscaleDitherEffect : MonoBehaviour
    {
        [SerializeField] private Shader _shader;

        [SerializeField] private Vector2Int _targetResolution = new Vector2Int(320, 240);
        [SerializeField, Range(1f, 8f)] private float _colorBits = 5f;
        [SerializeField, Range(0f, 1f)] private float _ditherStrength = 0.8f;

        private Material _material;

        private static readonly int ColorBitsId = Shader.PropertyToID("_ColorBits");
        private static readonly int DitherStrengthId = Shader.PropertyToID("_DitherStrength");

        private void OnEnable()
        {
            if (_shader == null)
            {
                _shader = Shader.Find("Hidden/PSX/Downscale Dither");
            }

            if (_shader == null)
            {
                return;
            }

            if (_material == null)
            {
                _material = new Material(_shader);
                _material.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void OnDisable()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            int w = Mathf.Max(1, _targetResolution.x);
            int h = Mathf.Max(1, _targetResolution.y);

            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, source.format);
            rt.filterMode = FilterMode.Point;

            Graphics.Blit(source, rt);

            _material.SetFloat(ColorBitsId, Mathf.Clamp(_colorBits, 1f, 8f));
            _material.SetFloat(DitherStrengthId, Mathf.Clamp01(_ditherStrength));

            Graphics.Blit(rt, destination, _material);

            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
