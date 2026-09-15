Shader "FairyGUI/Ripple"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}

	SubShader
	{
		Pass
		{
			ZTest Always
			ZWrite Off
			Cull Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float _Progress;
			float _Strength;
			float _Width;
			float _Frequency;
			float _Aspect;
			float4 _Center;

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			v2f vert(appdata_img v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord;
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv;
				float2 center = _Center.xy;
				float2 dir = uv - center;
				dir.x *= _Aspect;

				float dist = length(dir);
				float2 toCorner = max(center, 1.0 - center);
				toCorner.x *= _Aspect;
				float maxR = length(toCorner) + _Width * 2.0;
				float radius = _Progress * maxR;
				float ring = dist - radius;
				float w = max(_Width, 1e-4);
				float envelope = exp(-(ring * ring) / (w * w));
				float wave = sin(ring * _Frequency) * envelope;
				float fade = 1.0 - _Progress;

				float2 n = dist > 1e-5 ? dir / dist : float2(0, 0);
				n.x /= max(_Aspect, 1e-5);
				uv += n * wave * _Strength * fade;

				fixed4 col = tex2D(_MainTex, uv);
				col.rgb += envelope * fade * 0.12;
				return col;
			}
			ENDCG
		}
	}
	Fallback off
}
