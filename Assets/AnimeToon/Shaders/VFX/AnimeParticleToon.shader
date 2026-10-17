// Toon particle shader for URP (Unity 6). Cel-shaded puffs, sparks, bursts, rings.
// Texture: R = shape field (1 at the middle, 0 at the edge), G = lit mask (1 = lit, 0 = shadow tone).
// The particle ERODES instead of fading: the vertex color alpha (Color over Lifetime) shrinks the silhouette
// with a hard anime-style edge until it pops out. Use Src/Dst blend for alpha (5/10) or additive (5/1).
Shader "Anime/ParticleToon"
{
    Properties
    {
        _MainTex ("Shape (R) / Lit Mask (G)", 2D) = "white" {}
        [HDR] _LitColor ("Lit Color", Color) = (1,1,1,1)
        [HDR] _ShadowColor ("Shadow Color", Color) = (0.6,0.55,0.8,1)
        _EdgeCut ("Edge Cut", Range(0,0.9)) = 0.12
        _EdgeSoftness ("Edge Softness", Range(0.005,0.5)) = 0.06
        _ErodeAmount ("Erode As It Fades", Range(0,1)) = 1
        _OutlineColor ("Outline Color", Color) = (0.15,0.1,0.2,1)
        _OutlineWidth ("Outline Width", Range(0,0.3)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "AnimeParticle"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _LitColor, _ShadowColor, _OutlineColor;
                half  _EdgeCut, _EdgeSoftness, _ErodeAmount, _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half shape = tex.r;
                half shade = tex.g;

                // vertex alpha (life) pushes the cut toward the core: the particle shrinks and pops out
                half cut = lerp(_EdgeCut, 0.97h, (1.0h - IN.color.a) * _ErodeAmount);
                half a = smoothstep(cut, cut + _EdgeSoftness, shape);
                clip(a - 0.004h);

                half3 col = lerp(_ShadowColor.rgb, _LitColor.rgb, shade);

                // optional outline band just inside the edge
                half band = 1.0h - smoothstep(cut + _OutlineWidth, cut + _OutlineWidth + _EdgeSoftness, shape);
                col = lerp(col, _OutlineColor.rgb, band * step(0.0001h, _OutlineWidth));

                col *= IN.color.rgb;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
