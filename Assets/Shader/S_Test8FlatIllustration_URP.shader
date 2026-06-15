Shader "Custom/FlatIllustration_URP"
{
    // Equivalent URP de S_FlatIllustration.shader
    // Aplat mat + ombre 2 tons + grain papier leger + outline creme

    Properties
    {
        _BaseColor      ("Couleur",                 Color)          = (1,1,1,1)
        _BaseMap        ("Texture",                 2D)             = "white" {}

        // Ombre 2 tons plate (pas de degradé, bord net/doux)
        _ShadowColor     ("Teinte ombre",           Color)          = (0.75, 0.78, 0.85, 1)
        _ShadowThreshold ("Seuil ombre",            Range(-1, 1))   = 0.0
        _ShadowSoftness  ("Fondu bord",             Range(0, 0.3))  = 0.08
        _ShadowStrength  ("Intensité ombre",        Range(0, 1))    = 0.35

        // Grain papier leger (optionnel)
        _GrainTex       ("Grain papier",            2D)             = "white" {}
        _GrainStrength  ("Intensité grain",         Range(0, 0.25)) = 0.07
        _GrainScale     ("Echelle grain",           Range(0.1, 8))  = 2.0

        // Influence lumiere scene (0 = couleur pure, 1 = lumiere complete)
        _LightInfluence ("Influence lumiere",       Range(0, 1))    = 0.15

        // Effet aquarelle
        _WatercolorStrength ("Intensite aquarelle", Range(0, 1))    = 0.35
        _WatercolorSpeed    ("Vitesse animation",   Range(0, 2))    = 0.25
        _WatercolorScale    ("Echelle bruit",       Range(0.5, 8))  = 2.5

        // Volume 3D
        _DiffuseWrap  ("Diffuse doux (volume)",     Range(0, 1))    = 0.30
        _RimColor     ("Couleur rim",               Color)          = (1, 0.95, 0.88, 1)
        _RimStrength  ("Intensite rim",             Range(0, 1))    = 0.25
        _RimSharpness ("Nettete rim",               Range(1, 8))    = 3.0

        // Outline creme tres fin
        _OutlineColor   ("Couleur outline",         Color)          = (0.97, 0.93, 0.87, 1)
        _OutlineWidth   ("Épaisseur outline",       Range(0, 0.02)) = 0.004
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        // PASS 1 — Outline
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   vertOutline
            #pragma fragment fragOutline
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _OutlineColor;
                float  _OutlineWidth;
                half4  _ShadowColor;
                float  _ShadowThreshold;
                float  _ShadowSoftness;
                float  _ShadowStrength;
                float  _GrainStrength;
                float  _GrainScale;
                float  _LightInfluence;
                float  _WatercolorStrength;
                float  _WatercolorSpeed;
                float  _WatercolorScale;
                float  _DiffuseWrap;
                half4  _RimColor;
                float  _RimStrength;
                float  _RimSharpness;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings   { float4 positionCS:SV_POSITION; };

            Varyings vertOutline(Attributes v)
            {
                Varyings o;
                float4 clip  = TransformObjectToHClip(v.positionOS.xyz);
                float3 cn    = TransformWorldToHClip(TransformObjectToWorldNormal(v.normalOS)).xyz;
                float2 off   = normalize(cn.xy);
                off.x       /= _ScreenParams.x / _ScreenParams.y;
                clip.xy     += off * _OutlineWidth * clip.w;
                o.positionCS = clip;
                return o;
            }
            half4 fragOutline(Varyings i):SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        // PASS 2 — Aplat 2 tons + grain
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_GrainTex); SAMPLER(sampler_GrainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _OutlineColor;
                float  _OutlineWidth;
                half4  _ShadowColor;
                float  _ShadowThreshold;
                float  _ShadowSoftness;
                float  _ShadowStrength;
                float  _GrainStrength;
                float  _GrainScale;
                float  _LightInfluence;
                float  _WatercolorStrength;
                float  _WatercolorSpeed;
                float  _WatercolorScale;
                float  _DiffuseWrap;
                half4  _RimColor;
                float  _RimStrength;
                float  _RimSharpness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 posWS     = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS     = TransformWorldToHClip(posWS);
                o.positionWS     = posWS;
                o.normalWS       = TransformObjectToWorldNormal(v.normalOS);
                o.uv             = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            // -------------------------------------------------------
            // Bruit procedural pour effet aquarelle
            // -------------------------------------------------------
            float2 WcHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            // Gradient noise (Perlin-like), range ~[-1, 1]
            float WcNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(dot(WcHash(i + float2(0,0)), f - float2(0,0)),
                         dot(WcHash(i + float2(1,0)), f - float2(1,0)), u.x),
                    lerp(dot(WcHash(i + float2(0,1)), f - float2(0,1)),
                         dot(WcHash(i + float2(1,1)), f - float2(1,1)), u.x),
                    u.y);
            }

            // fBm 4 octaves
            float WcFbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                float2x2 rot = float2x2(1.6, 1.2, -1.2, 1.6);
                for (int k = 0; k < 4; k++)
                {
                    v += a * WcNoise(p);
                    p  = mul(rot, p);
                    a *= 0.5;
                }
                return v;
            }
            // -------------------------------------------------------

            half4 frag(Varyings i) : SV_Target
            {
                // UV animes pour les variations aquarelle
                float  t       = _Time.y * _WatercolorSpeed;
                float2 uvWc    = i.uv * _WatercolorScale;

                // Bruit lent pour distorsion des bords
                float2 warpUV  = uvWc + float2(t * 0.11, t * 0.07);
                float2 warp    = float2(
                    WcFbm(warpUV),
                    WcFbm(warpUV + float2(3.7, 1.9))
                ) * 0.18 * _WatercolorStrength;

                // Variations de luminosite dans la surface (aquarelle qui seche)
                float2 varUV   = uvWc + float2(t * 0.05, t * 0.03) + warp;
                float  wcVar   = WcFbm(varUV);             // ~[-0.5, 0.5]
                float  wcLight = WcFbm(varUV * 0.6 + 1.3); // nuance basse freq

                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                // Ombre 2 tons avec bord perturbe par le bruit aquarelle
                Light  mainLight = GetMainLight();
                float3 normal    = normalize(i.normalWS);
                float  NdotL     = dot(normal, mainLight.direction);

                // Le seuil de bord ombre/lumiere oscille legerement
                float  borderJitter = warp.x * 0.4 * _WatercolorStrength;
                float  toon      = smoothstep(
                    _ShadowThreshold - _ShadowSoftness + borderJitter,
                    _ShadowThreshold + _ShadowSoftness + borderJitter,
                    NdotL
                );
                col.rgb = lerp(
                    col.rgb * _ShadowColor.rgb,
                    col.rgb,
                    lerp(1.0, toon, _ShadowStrength)
                );

                // Diffuse wrap : gradient doux qui s ajoute par-dessus l aplat
                // NdotL remapped de [-1,1] vers [0,1] avec wrap pour eviter le noir dur
                float  diffWrap  = saturate((NdotL + 0.3) / 1.3);  // wrap leger
                float  diffSmooth = diffWrap * diffWrap;            // courbe douce
                col.rgb = lerp(col.rgb, col.rgb * (0.75 + diffSmooth * 0.5), _DiffuseWrap);

                // Influence legere de la couleur de lumiere (sans ecraser les pastels)
                half3 litColor = col.rgb * mainLight.color;
                col.rgb = lerp(col.rgb, litColor, _LightInfluence);

                // Rim light : fresnel sur les contours orientes vers la camera
                float3 viewDir  = normalize(GetCameraPositionWS() - i.positionWS);
                float  NdotV    = saturate(dot(normal, viewDir));
                float  rim      = pow(1.0 - NdotV, _RimSharpness);
                col.rgb        += _RimColor.rgb * rim * _RimStrength;

                // Variations de surface aquarelle :
                // - wcVar  : variations rapides (granularite seche)
                // - wcLight: variations lentes (pooling humide)
                float wcEffect = wcVar * 0.55 + wcLight * 0.45; // ~[-0.5, 0.5]
                col.rgb += wcEffect * 0.18 * _WatercolorStrength;

                // Grain papier leger (UV legerement distordus par le warp)
                float2 grainUV = i.uv * _GrainScale + warp * 0.5;
                float grain    = SAMPLE_TEXTURE2D(_GrainTex, sampler_GrainTex, grainUV).r - 0.5;
                col.rgb       += grain * _GrainStrength;

                // Ambiance legere
                col.rgb += SampleSH(normal) * 0.10 * _LightInfluence;

                return half4(saturate(col.rgb), col.a);
            }
            ENDHLSL
        }

        // PASS 3 — Shadow Caster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual Cull Back ColorMask 0
            HLSLPROGRAM
#pragma vertex   ShadowPassVertex
#pragma fragment ShadowPassFragment
#pragma multi_compile_shadowcaster
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
ENDHLSL
        }
    }
}
