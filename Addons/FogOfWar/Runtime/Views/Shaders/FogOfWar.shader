Shader "ME.BECS/FogOfWar/Default"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _FogTex ("Fow Texture (R - fog)", 2D) = "white" {}
        _HeightTex ("Height Texture (R - height)", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0, 0, 1)
        
		_BlurRadius("Blur Radius", Range(0, 10)) = 1
		_BlurHorizontalStep("Blur Horizontal Step", Range(0, 1)) = 0.5
		_BlurVerticalStep("Blur Vertical Step", Range(0, 1)) = 0.5
    	
		_FogSpread("Fog Spread", Float) = 10.0
    }
    SubShader
    {
		Tags
		{
			"Queue" = "Overlay"
			"RenderType" = "Transparent"
			"IgnoreProjector" = "True"
			"PreviewType" = "Plane"
		}

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        LOD 200
        Lighting off
        ZWrite Off
		ZTest Always
		Fog { Mode off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

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

            sampler2D _MainTex;
            sampler2D _FogTex;
            float4 _FogTex_ST;
            float4 _MainTex_ST;
            fixed4 _Color;

            float4 _FogTex_TexelSize;
            float4 _MainTex_TexelSize;
			float _BlurRadius;
			float _BlurHorizontalStep;
			float _BlurVerticalStep;

            uniform sampler2D _HeightTex;
            uniform float _HeightResolution;
            
			sampler2D _CameraDepthTexture;
			uniform float4x4 _InverseMVP;
			uniform float4 _CamPos;
			uniform float4 _Params;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv.xy;
                //o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 blur(float2 uv)
            {
				float2 stride = float2(_BlurRadius, _BlurRadius) / _FogTex_TexelSize.zw * float2(_BlurHorizontalStep, _BlurVerticalStep);

				float r = tex2D(_FogTex, uv).r * 0.02270270270;

            	half f1 = 0.2307692308;
            	half f2 = 0.3846153846;
            	half outf1 = 0.0702702703;
            	half outf2 = 0.3162162162;
				r += tex2D(_FogTex, uv + f1 * float2(-stride.x, -stride.y)).r * outf1;
				r += tex2D(_FogTex, uv + f2 * float2(stride.x, -stride.y)).r * outf2;

				r += tex2D(_FogTex, uv + f2 * float2(-stride.x, stride.y)).r * outf2;
				r += tex2D(_FogTex, uv + f1 * float2(stride.x, stride.y)).r * outf1;

                fixed4 fog = fixed4(0, 0, 0, clamp(r, 0, 1));
                return fog;
            }

            float3 CamToWorld (in float2 uv, in float depth)
			{
				float4 pos = float4(uv.x, uv.y, depth, 1.0);
				pos.xyz = pos.xyz * 2.0 - 1.0;
				pos = mul(_InverseMVP, pos);
				return pos.xyz / pos.w;
			}
            
            fixed4 frag(v2f i) : SV_Target
            {
            	fixed4 original = tex2D(_MainTex, i.uv);
            	float depth = 1.0 - UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture, i.uv));
				float3 pos = CamToWorld(i.uv, depth);
            	if (pos.y < 0.0)
				{
					// This is a simplified version of the ray-plane intersection formula: t = -( N.O + d ) / ( N.D )
					float3 dir = normalize(pos - _CamPos.xyz);
					pos = _CamPos.xyz - dir * (_CamPos.y / dir.y);
				}
            	
            	float2 uv = pos.xz * _Params.z + _Params.xy;
            	uv.x -= 0.5;
            	uv.y -= 0.5;
            	//return //fixed4(uv.x - 0.5, uv.y - 0.5, 1, 1);
            	half4 fog = blur(uv);

            	return lerp(original * _Color, original, fog.a);
            	
            	/*fixed4 color = blur(i);
            	float zsample = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
				float depth = Linear01Depth(zsample);
				depth = clamp(depth * _FogSpread, 0, 1) * IF(depth < 1.0, 1, 0);
				tex *= depth;//tex1D(_FogTex, depth);
            	return tex;*/
                //return fixed4(_Color.r, _Color.g, _Color.b, color.a * _Color.a);
            }

            ENDCG
        }
    }
}
