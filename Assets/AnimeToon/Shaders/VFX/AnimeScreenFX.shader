// Full-screen anime effects drawn by a quad parented to the camera (see AnimeSpeedFx):
//   speed lines  radial spokes that re-roll at a low frame rate ("limited animation")
//   impact flash white flash with halftone dots toward the edges
// The vertex shader ignores the quad transform and covers the whole screen. No post-processing needed.
Shader "Anime/ScreenFX"
{
    Properties
    {
        _SpeedLines ("Speed Lines", Range(0,1)) = 0
        _LineColor ("Line Color", Color) = (1,1,1,1)
        _LineCount ("Line Count", Range(10,120)) = 48
        _InnerRadius ("Inner Radius", Range(0,1.2)) = 0.45
        _InnerSoftness ("Inner Softness", Range(0.01,1)) = 0.3
        _LineFPS ("Line Frame Rate", Range(1,30)) = 12
        _Flash ("Flash", Range(0,1)) = 0
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _DotScale ("Halftone Dot Scale", Range(10,120)) = 40
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "AnimeScreenFX"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half  _SpeedLines;
                half4 _LineColor;
                half  _LineCount, _InnerRadius, _InnerSoftness, _LineFPS;
                half  _Flash;
                half4 _FlashColor;
                half  _DotScale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // a Unity Quad spans -0.5..0.5, so xy * 2 covers the whole screen
                float2 xy = IN.positionOS.xy * 2.0;
                if (_ProjectionParams.x < 0.0) xy.y = -xy.y;
                OUT.positionCS = float4(xy, UNITY_NEAR_CLIP_VALUE, 1.0);
                OUT.uv = IN.positionOS.xy + 0.5;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = (IN.uv - 0.5) * 2.0 * float2(aspect, 1.0);
                float r = length(p);

                // ---- speed lines
                float t = atan2(p.y, p.x) / 6.2831853 + 0.5;
                float cells = t * _LineCount;
                float id = floor(cells);
                float f = frac(cells) - 0.5;
                float tick = floor(_Time.y * _LineFPS);
                float rnd = Hash11(id + tick * 13.37);
                float rnd2 = Hash11(id * 1.7 + tick * 7.1 + 3.0);
                float width = lerp(0.05, 0.38, rnd2);
                float lineMask = 1.0 - smoothstep(width * 0.5 - 0.03, width * 0.5 + 0.03, abs(f));
                lineMask *= step(0.35, rnd);
                float inner = _InnerRadius + (rnd - 0.5) * 0.25;
                float radial = smoothstep(inner, inner + _InnerSoftness, r);
                half speedA = (half)(lineMask * radial) * _SpeedLines;

                // ---- impact flash with halftone dots toward the edges
                float edge = smoothstep(0.0, 1.3, r);
                float2 g = frac(p * _DotScale) - 0.5;
                float dotRadius = lerp(0.05, 0.55, edge);
                float dots = 1.0 - smoothstep(dotRadius - 0.06, dotRadius + 0.06, length(g));
                half flashA = _Flash * (half)(0.3 + 0.7 * max(1.0 - edge * 1.2, dots));

                half3 col = _LineColor.rgb;
                half share = saturate(flashA / max(speedA + flashA, 0.0001h));
                col = lerp(col, _FlashColor.rgb, share);
                half a = saturate(speedA + flashA * (1.0h - speedA));
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
