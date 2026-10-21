// Light shafts ("god rays") for URP (Unity 6): long soft beams that follow the main light.
// Made of a few large additive quads (see AnimeLightShafts.cs), so it is cheap and needs no post-processing.
// Each beam is anchored at a point on the ground and runs up along the direction to the sun, turning around that
// axis to face the camera. Brightness follows the main light's color and intensity, and the shafts are strongest
// when the sun is low (dusk, dawn) and gentle at noon. They disappear when the sun is below the horizon.
Shader "Anime/LightShaft"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,0.95,0.8,1)
        _Intensity ("Intensity (above 1 feeds bloom)", Range(0,4)) = 1.2
        _HighSunIntensity ("Intensity When The Sun Is High", Range(0,1)) = 0.25
        _EdgeSoftness ("Edge Softness", Range(0.05,1)) = 0.65
        _FadeIn ("Fade In At The Base", Range(0.01,0.5)) = 0.12
        _FadeOut ("Fade Out At The Top", Range(0.01,0.8)) = 0.45
        _StripeScale ("Stripe Scale", Float) = 3
        _StripeContrast ("Stripe Contrast", Range(0,1)) = 0.6
        _StripeSpeed ("Stripe Drift Speed", Float) = 0.15
        _NearFade ("Fade Near The Camera (m)", Float) = 8
        _FarFade ("Fade With Distance (m)", Float) = 110
    }

    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "LightShaft"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half  _Intensity, _HighSunIntensity, _EdgeSoftness, _FadeIn, _FadeOut;
                float _StripeScale;
                half  _StripeContrast;
                float _StripeSpeed, _NearFade, _FarFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // the beam's anchor point, the same for its 4 corners
                float2 uv         : TEXCOORD0;  // x = -1 / 1 across the beam, y = 0 / 1 along it
                float4 data       : TEXCOORD1;  // x = width, y = length, z = seed
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 info       : TEXCOORD1;  // x = seed, y = distance to the camera
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float ShaftNoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash11(i), Hash11(i + 1.0), f);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 anchor = TransformObjectToWorld(IN.positionOS.xyz);
                float3 L = normalize(_MainLightPosition.xyz + float3(0, 1e-4, 0));   // towards the sun
                float3 toCamera = _WorldSpaceCameraPos - anchor;

                // axial billboard: the beam keeps its direction and turns around it to face the camera
                float3 side = cross(L, toCamera);
                float sideLength = length(side);
                side = sideLength > 1e-4 ? side / sideLength : float3(1, 0, 0);

                float3 pos = anchor + L * (IN.uv.y * IN.data.y) + side * (IN.uv.x * IN.data.x * 0.5);

                OUT.positionCS = TransformWorldToHClip(pos);
                OUT.uv = IN.uv;
                OUT.info = float2(IN.data.z, length(toCamera));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float x = IN.uv.x;
                float y = IN.uv.y;
                float seed = IN.info.x;
                float camDist = IN.info.y;

                float side = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, abs(x));
                float along = smoothstep(0.0, _FadeIn, y) * (1.0 - smoothstep(1.0 - _FadeOut, 1.0, y));

                // soft stripes that drift slowly, so the beam is not a flat gradient
                float stripe = ShaftNoise(x * _StripeScale + seed * 37.0 + _Time.y * _StripeSpeed);
                stripe = lerp(1.0, stripe, _StripeContrast);

                float nearFade = smoothstep(_NearFade * 0.5, _NearFade, camDist);
                float farFade = 1.0 - smoothstep(_FarFade * 0.6, _FarFade, camDist);

                // strongest with a low sun, gentle at noon, gone when the sun is below the horizon
                float3 L = normalize(_MainLightPosition.xyz + float3(0, 1e-4, 0));
                float aboveHorizon = smoothstep(-0.05, 0.12, L.y);
                float lowSun = 1.0 - smoothstep(0.12, 0.65, L.y);
                float sunFactor = lerp(_HighSunIntensity, 1.0, lowSun) * aboveHorizon;

                half3 col = _Tint.rgb * _MainLightColor.rgb * (_Intensity * sunFactor);
                half a = saturate(side * along * stripe * nearFade * farFade);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
