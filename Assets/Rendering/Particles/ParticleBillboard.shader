Shader "Fluid/ParticleBillboard" {
	Properties {

	}
	SubShader {

		Tags {"Queue"="Geometry" }

		Pass {

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.5

			#include "UnityCG.cginc"

			StructuredBuffer<float3> Positions;
			StructuredBuffer<float3> Velocities;
			StructuredBuffer<int> States;
			Texture2D<float4> ColourMap;
			SamplerState linear_clamp_sampler;
			float velocityMax;

			float scale;
			float3 colour;

			float4x4 localToWorld;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 colour : TEXCOORD1;
				float3 normal : NORMAL;
			};

			v2f vert (appdata_full v, uint instanceID : SV_InstanceID)
			{
				v2f o;
				o.uv = v.texcoord;
				o.normal = v.normal;

				float3 centreWorld = Positions[instanceID];
				// Once deposited (state 2), the canvas paint texture is the visual source of truth --
				// hide the raw particle unconditionally instead of only while its speed is above a
				// threshold, which on tilted surfaces flickers on/off for as long as it keeps sliding.
				float hideScale = (States[instanceID] == 2) ? 0.0 : 1.0;
				float3 objectVertPos = v.vertex * scale * 2 * hideScale;
				float speed = length(Velocities[instanceID]);
				float4 viewPos = mul(UNITY_MATRIX_V, float4(centreWorld, 1)) + float4(objectVertPos, 0);
				o.pos = mul(UNITY_MATRIX_P, viewPos);

				float speedT = saturate(speed / velocityMax);
				float colT = speedT;
				o.colour = ColourMap.SampleLevel(linear_clamp_sampler, float2(colT, 0.5), 0);

				return o;
			}

			float4 frag (v2f i) : SV_Target
			{
				float shading = saturate(dot(_WorldSpaceLightPos0.xyz, i.normal));
				shading = (shading + 0.6) / 1.4;
				return float4(i.colour, 1);
			}

			ENDCG
		}
	}
}