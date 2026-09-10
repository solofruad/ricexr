Shader "Custom/BurnLeaf_VR"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Render Faces", Float) = 2

        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] [MainTexture] _BaseMap("Base Map", 2D) = "white" {}

        _BurnProgress("Burn Progress", Range(0, 1)) = 0
        [NoScaleOffset] _NoiseMap("Noise Map", 2D) = "gray" {}
        _NoiseTiling("Noise Tiling", Range(0.1, 20)) = 4

        _EdgeWidth("Burn Edge Width", Range(0.001, 0.3)) = 0.05
        [HDR] _EdgeColorA("Burn Edge Color A (Outer)", Color) = (1, 0.45, 0.05, 1)
        [HDR] _EdgeColorB("Burn Edge Color B (Inner)", Color) = (1, 0.9, 0.2, 1)
        _EdgeIntensity("Burn Edge Intensity", Range(0, 10)) = 2.0

        [Header(Wind)]
        _WindAmplitude("Wind Amplitude (m)", Range(0, 0.15)) = 0.02
        _WindSpeed("Wind Speed", Range(0, 5)) = 1.4
        _WindFrequency("Wind Frequency", Range(0, 10)) = 2.0
        _WindDirection("Wind Direction", Vector) = (1, 0, 0, 0)
        _WindMaskPower("Wind Mask Power", Range(0.1, 5)) = 2.0
        _WindWeight("Wind Weight", Range(0, 1)) = 1.0
        _WindPhase("Wind Phase", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline"
               "UniversalMaterialType"="Lit" "Queue"="AlphaTest" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex   LitVert
            #pragma fragment LitFrag

            // Solo lo necesario para Quest
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _BurnProgress;
                float _NoiseTiling;
                float _EdgeWidth;
                half4 _EdgeColorA;
                half4 _EdgeColorB;
                float _EdgeIntensity;
                float _WindAmplitude;
                float _WindSpeed;
                float _WindFrequency;
                float4 _WindDirection;
                float _WindMaskPower;
                float _WindWeight;
                float _WindPhase;
            CBUFFER_END

            #include "Assets/RiceXR/Shaders/LeafWind.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings LitVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionOS = ApplyLeafWind(IN.positionOS.xyz, IN.color);
                VertexPositionInputs posInputs = GetVertexPositionInputs(positionOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = IN.uv;
                OUT.shadowCoord = GetShadowCoord(posInputs);

                return OUT;
            }

            half4 LitFrag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // ── BURN DISSOLVE ──────────────────────────────────────
                float noise            = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, IN.uv * _NoiseTiling).r;
                float adjustedProgress = _BurnProgress * 1.05 - 0.025;
                clip(noise - adjustedProgress);

                // ── BURN EDGE ──────────────────────────────────────────
                float edgeMask   = smoothstep(adjustedProgress, adjustedProgress + _EdgeWidth, noise);
                float glowWeight = (1.0 - edgeMask) * step(0.001, _BurnProgress);
                half3 edgeColor  = lerp(_EdgeColorB.rgb, _EdgeColorA.rgb, glowWeight)
                                   * (glowWeight * _EdgeIntensity);

                // ── COLOR BASE ─────────────────────────────────────────
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                half3 normalWS = normalize(IN.normalWS);

                // ── LUZ PRINCIPAL ──────────────────────────────────────
                Light mainLight = GetMainLight(IN.shadowCoord);
                half  NoL       = saturate(dot(normalWS, normalize(mainLight.direction)));
                half3 diffuse   = mainLight.color * mainLight.shadowAttenuation * NoL;

                // ── LUCES ADICIONALES ──────────────────────────────────
                // (point lights, spot lights en la escena)
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; i++)
                {
                    Light light  = GetAdditionalLight(i, IN.positionWS);
                    half  addNoL = saturate(dot(normalWS, light.direction));
                    diffuse     += light.color * light.distanceAttenuation * addNoL;
                }

                // ── GI / AMBIENTE ──────────────────────────────────────
                // SampleSH captura el skybox y luces de ambiente de Unity
                half3 ambient = SampleSH(normalWS);

                // ── RESULTADO FINAL ────────────────────────────────────
                half3 litColor   = baseColor.rgb * (ambient + diffuse);
                half3 finalColor = litColor + edgeColor;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // ── SHADOW CASTER ──────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _BurnProgress;
                float _NoiseTiling;
                float _EdgeWidth;
                half4 _EdgeColorA;
                half4 _EdgeColorB;
                float _EdgeIntensity;
                float _WindAmplitude;
                float _WindSpeed;
                float _WindFrequency;
                float4 _WindDirection;
                float _WindMaskPower;
                float _WindWeight;
                float _WindPhase;
            CBUFFER_END

            #include "Assets/RiceXR/Shaders/LeafWind.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionOS = ApplyLeafWind(input.positionOS.xyz, input.color);
                float3 posWS      = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 posCS    = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _MainLightPosition.xyz));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, posCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, posCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionHCS = posCS;
                output.uv          = input.uv;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                float noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.uv * _NoiseTiling).r;
                clip(noise - (_BurnProgress * 1.05 - 0.025));
                return 0;
            }
            ENDHLSL
        }

        // ── DEPTH ONLY ─────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex   DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _BurnProgress;
                float _NoiseTiling;
                float _EdgeWidth;
                half4 _EdgeColorA;
                half4 _EdgeColorB;
                float _EdgeIntensity;
                float _WindAmplitude;
                float _WindSpeed;
                float _WindFrequency;
                float4 _WindDirection;
                float _WindMaskPower;
                float _WindWeight;
                float _WindPhase;
            CBUFFER_END

            #include "Assets/RiceXR/Shaders/LeafWind.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                float3 positionOS = ApplyLeafWind(input.positionOS.xyz, input.color);
                output.positionHCS = TransformObjectToHClip(positionOS);
                output.uv          = input.uv;
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                float noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.uv * _NoiseTiling).r;
                clip(noise - (_BurnProgress * 1.05 - 0.025));
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
