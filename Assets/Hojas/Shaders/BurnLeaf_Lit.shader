Shader "Custom/BurnLeaf_Lit_Simplified"
{
    Properties
    {
        // Opciones de Culling (Render Faces)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Render Faces", Float) = 2 // 0 = Both, 1 = Front, 2 = Back

        // Textura base sin opciones de Tiling/Offset
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] [MainTexture] _BaseMap("Base Map", 2D) = "white" {}

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        // Burn properties
        _BurnProgress("Burn Progress", Range(0, 1)) = 0
        [NoScaleOffset] _NoiseMap("Noise Map", 2D) = "gray" {}
        _NoiseTiling("Noise Tiling", Range(0.1, 20)) = 4

        _EdgeWidth("Burn Edge Width", Range(0.001, 0.3)) = 0.05
        [HDR] _EdgeColorA("Burn Edge Color A (Outer)", Color) = (1, 0.45, 0.05, 1)
        [HDR] _EdgeColorB("Burn Edge Color B (Inner)", Color) = (1, 0.9, 0.2, 1)
        _EdgeIntensity("Burn Edge Intensity", Range(0, 10)) = 2.0
    }

    SubShader
    {
        // Cambiamos a TransparentCutout para que el motor entienda que el objeto puede tener "huecos"
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue"="AlphaTest" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            ZWrite On
            Cull [_Cull] // Usamos la propiedad del inspector

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex LitVert
            #pragma fragment LitFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Metallic;
                float _BurnProgress;
                float _NoiseTiling;
                float _EdgeWidth;
                half4 _EdgeColorA;
                half4 _EdgeColorB;
                float _EdgeIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
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

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.uv = IN.uv; // UV Directo, sin _BaseMap_ST
                
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                return OUT;
            }

            half4 LitFrag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // --- SISTEMA DE QUEMADO / DISOLUCIÓN ---
                float noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, IN.uv * _NoiseTiling).r;
                
                // Ajustamos el progreso para garantizar que al 100% el modelo se borre por completo
                float adjustedProgress = _BurnProgress * 1.05 - 0.025;
                
                // clip() descarta el píxel si el valor entre paréntesis es menor a 0. Esto hace desaparecer el modelo.
                clip(noise - adjustedProgress);

                // --- BORDES INCANDESCENTES ---
                // Calculamos qué tan cerca está este píxel de ser descartado
                float edgeMask = smoothstep(adjustedProgress, adjustedProgress + _EdgeWidth, noise);
                
                // Si _BurnProgress es muy cercano a 0, forzamos el peso a 0 para que no se vea nada de borde
                float glowWeight = (1.0 - edgeMask) * step(0.001, _BurnProgress); 

                // Color del borde con gradiente
                half3 edgeColor = lerp(_EdgeColorB.rgb, _EdgeColorA.rgb, glowWeight) * (glowWeight * _EdgeIntensity);

                // --- COLOR BASE Y LUZ ---
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                Light mainLight = GetMainLight(IN.shadowCoord);
                
                half3 lightDir = normalize(mainLight.direction);
                half NoL = saturate(dot(IN.normalWS, lightDir));
                
                // Iluminación básica de la escena global + luz principal
                half3 ambient = SampleSH(IN.normalWS); // Captura Skybox/Ambient Light de Unity
                
                half3 diffuseLight = ambient + (mainLight.color * NoL);
                
                // Mezcla simple con el color base (ignorando cosas pesadas metálicas)
                half3 litColor = baseColor.rgb * diffuseLight;
                
                // Añadimos el brillo del borde
                half3 finalColor = litColor + edgeColor;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // --- SHADOW CASTER PASS ---
        // Vital para que la sombra también desaparezca al quemarse
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float _BurnProgress;
                float _NoiseTiling;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;

                #if UNITY_REVERSED_Z
                output.positionHCS.z = min(output.positionHCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionHCS.z = max(output.positionHCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                float noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.uv * _NoiseTiling).r;
                float adjustedProgress = _BurnProgress * 1.05 - 0.025;
                
                // Descartar sombra igual que el modelo original
                clip(noise - adjustedProgress);
                
                return 0;
            }
            ENDHLSL
        }

        // --- DEPTH PASS ---
        // Vital para efectos de post-procesado o Depth of Field
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float _BurnProgress;
                float _NoiseTiling;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                float noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.uv * _NoiseTiling).r;
                float adjustedProgress = _BurnProgress * 1.05 - 0.025;
                
                clip(noise - adjustedProgress);
                
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}