Shader "Custom/BurnLeaf"
{
	Properties
	{
		_MainTex ("Base Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)

		_NoiseTex ("Noise Texture", 2D) = "gray" {}
		_NoiseScale ("Noise Scale", Range(0.1, 20)) = 4

		_BurnProgress ("Burn Progress", Range(0, 1)) = 0

		_EdgeWidth ("Burn Edge Width", Range(0.001, 0.3)) = 0.05
		_EdgeColorA ("Burn Edge Color A", Color) = (1, 0.45, 0.05, 1)
		_EdgeColorB ("Burn Edge Color B", Color) = (1, 0.9, 0.2, 1)
		_EdgeIntensity ("Burn Edge Intensity", Range(0, 5)) = 1.7
	}

	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		LOD 200

		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Back
			ZWrite Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Color;

			sampler2D _NoiseTex;
			float4 _NoiseTex_ST;
			float _NoiseScale;

			float _BurnProgress;

			float _EdgeWidth;
			fixed4 _EdgeColorA;
			fixed4 _EdgeColorB;
			float _EdgeIntensity;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			float hash21(float2 p)
			{
				p = frac(p * float2(123.34, 456.21));
				p += dot(p, p + 45.32);
				return frac(p.x * p.y);
			}

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 baseCol = tex2D(_MainTex, i.uv) * _Color;

				float2 noiseUV = i.uv * _NoiseScale;
				float sampledNoise = tex2D(_NoiseTex, noiseUV).r;

				// Mezcla ruido de textura con ruido procedural para evitar patrones repetitivos.
				float procedural = hash21(floor(noiseUV * 32.0));
				float noise = saturate(lerp(sampledNoise, procedural, 0.35));

				float progress = saturate(_BurnProgress);

				float softness = max(_EdgeWidth * 0.5, 0.001);
				float dissolve = smoothstep(progress - softness, progress + softness, noise);

				float edgeBand = 1.0 - saturate(abs(noise - progress) / max(_EdgeWidth, 0.0001));
				float edgeGradient = saturate((noise - (progress - _EdgeWidth)) / max(_EdgeWidth, 0.0001));
				fixed3 edgeColor = lerp(_EdgeColorA.rgb, _EdgeColorB.rgb, edgeGradient) * (edgeBand * _EdgeIntensity);

				fixed4 col;
				col.rgb = baseCol.rgb + edgeColor;
				col.a = baseCol.a * dissolve;

				return col;
			}
			ENDCG
		}
	}
}
