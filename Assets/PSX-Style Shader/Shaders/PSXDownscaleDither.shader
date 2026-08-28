Shader "Hidden/PSX/Downscale Dither"
{
    Properties
    {
        _MainTex("Main", 2D) = "white" {}
        _TargetResolution("Target Resolution", Vector) = (320,240,0,0)
        _UseScreenResolution("Use Screen Resolution", Float) = 0
        _ColorBits("Color Bits", Range(1,8)) = 5
        _DitherStrength("Dither Strength", Range(0,1)) = 0.8
    }

        SubShader
        {
            Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }

            Pass
            {
                ZTest Always Cull Off ZWrite Off

                CGPROGRAM
                #pragma target 3.0
                #pragma vertex vert_img
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _MainTex;
                float4 _TargetResolution;
                float _UseScreenResolution;
                float _ColorBits;
                float _DitherStrength;

                float Bayer4(float2 pixel)
                {
                    float2 p = floor(pixel);
                    float x = fmod(p.x, 4.0);
                    float y = fmod(p.y, 4.0);

                    float v0 = (x == 0.0) ? 0.0 : ((x == 1.0) ? 8.0 : ((x == 2.0) ? 2.0 : 10.0));
                    float v1 = (x == 0.0) ? 12.0 : ((x == 1.0) ? 4.0 : ((x == 2.0) ? 14.0 : 6.0));
                    float v2 = (x == 0.0) ? 3.0 : ((x == 1.0) ? 11.0 : ((x == 2.0) ? 1.0 : 9.0));
                    float v3 = (x == 0.0) ? 15.0 : ((x == 1.0) ? 7.0 : ((x == 2.0) ? 13.0 : 5.0));

                    float v = (y == 0.0) ? v0 : ((y == 1.0) ? v1 : ((y == 2.0) ? v2 : v3));
                    return (v / 16.0) - 0.5;
                }

                float3 QuantizeRgb(float3 rgb, float bits, float dither)
                {
                    float b = clamp(bits, 1.0, 8.0);
                    float levels = (pow(2.0, b) - 1.0);

                    float3 x = saturate(rgb);
                    x = (x * levels) + dither;
                    x = floor(x + 0.5);
                    return saturate(x / levels);
                }

                fixed4 frag(v2f_img i) : SV_Target
                {
                    float2 targetRes = max(_TargetResolution.xy, 1.0);
                    float2 screenRes = max(_ScreenParams.xy, 1.0);
                    float useScreen = saturate(_UseScreenResolution);

                    float2 res = lerp(targetRes, screenRes, useScreen);

                    float2 uv = i.uv;
                    float2 pix = floor(uv * res) + 0.5;
                    float2 uvQ = pix / res;

                    float3 col = tex2D(_MainTex, uvQ).rgb;

                    float2 pixelCoord = uv * _ScreenParams.xy;
                    float d = Bayer4(pixelCoord) * saturate(_DitherStrength);

                    col = QuantizeRgb(col, _ColorBits, d);

                    return fixed4(col, 1.0);
                }
                ENDCG
            }
        }
}
