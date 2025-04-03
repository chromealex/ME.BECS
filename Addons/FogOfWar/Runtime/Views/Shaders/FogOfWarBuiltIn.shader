Shader "ME.BECS/FogOfWar/Default-BuiltIn"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _FogTex ("Fow Texture (R - Fog, G - Explored, B - Height)", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0, 0, 1)
        _ExploredColor ("Explored Color", Color) = (0, 0, 0, 1)
        
		_BorderMin("BorderMin", Range(0, 1)) = 1
		_BorderMax("BorderMax", Range(0, 1)) = 1
        _BorderColor ("Border Color", Color) = (0, 0, 0, 1)
        
        _Padding("Normalized Padding", Vector) = (0, 0, 0, 0)
        
		_BlurRadius("Blur Radius", Range(0, 10)) = 1
		_BlurHorizontalStep("Blur Horizontal Step", Range(0, 1)) = 0.5
		_BlurVerticalStep("Blur Vertical Step", Range(0, 1)) = 0.5
    }
    SubShader
    {
		Tags
		{
		    "Queue"="Transparent"
		    "RenderType"="Opaque"
		    "RenderTexture"="True"
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
            half4 _FogTex_ST;
            half4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _ExploredColor;
            half _BorderMin;
            half _BorderMax;
            half4 _BorderColor;

            half4 _FogTex_TexelSize;
            half4 _MainTex_TexelSize;
			half _BlurRadius;
			half _BlurHorizontalStep;
			half _BlurVerticalStep;
            float4 _Padding;

			sampler2D _CameraDepthTexture;
			uniform float4x4 _InverseMVP;
			uniform half4 _CamPos;
			uniform half4 _Params;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				//o.uv = v.uv.xy;
                o.uv = TRANSFORM_TEX(v.uv, _FogTex);
                return o;
            }

            inline half4 blur(sampler2D tex, float2 uv)
            {
				float2 stride = float2(_BlurRadius, _BlurRadius) / _FogTex_TexelSize.zw * float2(_BlurHorizontalStep, _BlurVerticalStep);
				const half blur_size = stride.x;

            	half2 sum = 0;
            	half3 center = tex2D(tex, half2(uv.x, uv.y));
				sum += center.rg * 0.16 * 4;
            	
            	sum += tex2D(tex, half2(uv.x, uv.y - 4.0 * blur_size)).rg * 0.05;
				sum += tex2D(tex, half2(uv.x, uv.y - 3.0 * blur_size)).rg * 0.09;
				sum += tex2D(tex, half2(uv.x, uv.y - 2.0 * blur_size)).rg * 0.12;
				sum += tex2D(tex, half2(uv.x, uv.y - blur_size)).rg * 0.15;
				sum += tex2D(tex, half2(uv.x, uv.y + blur_size)).rg * 0.15;
				sum += tex2D(tex, half2(uv.x, uv.y + 2.0 * blur_size)).rg * 0.12;
				sum += tex2D(tex, half2(uv.x, uv.y + 3.0 * blur_size)).rg * 0.09;
				sum += tex2D(tex, half2(uv.x, uv.y + 4.0 * blur_size)).rg * 0.05;
				
				sum += tex2D(tex, half2(uv.x - 4.0 * blur_size, uv.y)).rg * 0.05;
				sum += tex2D(tex, half2(uv.x - 3.0 * blur_size, uv.y)).rg * 0.09;
				sum += tex2D(tex, half2(uv.x - 2.0 * blur_size, uv.y)).rg * 0.12;
				sum += tex2D(tex, half2(uv.x - blur_size, uv.y)).rg * 0.15;
				sum += tex2D(tex, half2(uv.x + blur_size, uv.y)).rg * 0.15;
				sum += tex2D(tex, half2(uv.x + 2.0 * blur_size, uv.y)).rg * 0.12;
				sum += tex2D(tex, half2(uv.x + 3.0 * blur_size, uv.y)).rg * 0.09;
				sum += tex2D(tex, half2(uv.x + 4.0 * blur_size, uv.y)).rg * 0.05;

				sum += tex2D(tex, half2(uv.x - 4.0 * blur_size, uv.y - 4.0 * blur_size)).rg * 0.05;
				sum += tex2D(tex, half2(uv.x - 3.0 * blur_size, uv.y - 3.0 * blur_size)).rg * 0.09;
				sum += tex2D(tex, half2(uv.x - 2.0 * blur_size, uv.y - 2.0 * blur_size)).rg * 0.12;
				sum += tex2D(tex, half2(uv.x - 1.0 * blur_size, uv.y - blur_size)).rg * 0.15;
				sum += tex2D(tex, half2(uv.x + 1.0 * blur_size, uv.y + blur_size)).rg * 0.15;
				sum += tex2D(tex, half2(uv.x + 2.0 * blur_size, uv.y + 2.0 * blur_size)).rg * 0.12;
				sum += tex2D(tex, half2(uv.x + 3.0 * blur_size, uv.y + 3.0 * blur_size)).rg * 0.09;
				sum += tex2D(tex, half2(uv.x + 4.0 * blur_size, uv.y + 4.0 * blur_size)).rg * 0.05;
				
				sum += tex2D(tex, half2(uv.x - 4.0 * blur_size, uv.y + 4.0 * blur_size)).rg * 0.05;
				sum += tex2D(tex, half2(uv.x - 3.0 * blur_size, uv.y + 3.0 * blur_size)).rg * 0.09;
				sum += tex2D(tex, half2(uv.x - 2.0 * blur_size, uv.y + 2.0 * blur_size)).rg * 0.12;
				sum += tex2D(tex, half2(uv.x - 1.0 * blur_size, uv.y + blur_size)).rg * 0.15;
				sum += tex2D(tex, half2(uv.x + 1.0 * blur_size, uv.y - blur_size)).rg * 0.15;
				sum += tex2D(tex, half2(uv.x + 2.0 * blur_size, uv.y - 2.0 * blur_size)).rg * 0.12;
				sum += tex2D(tex, half2(uv.x + 3.0 * blur_size, uv.y - 3.0 * blur_size)).rg * 0.09;
				sum += tex2D(tex, half2(uv.x + 4.0 * blur_size, uv.y - 4.0 * blur_size)).rg * 0.05;

                half4 fog = half4(sum.r, sum.g, center.b, 0);
                return fog;
            }

            float3 CamToWorld (in float2 uv, in float depth)
			{
				float4 pos = float4(uv.x, uv.y, depth, 1.0);
				pos.xyz = pos.xyz * 2.0 - 1.0;
				pos = mul(_InverseMVP, pos);
				return pos.xyz / pos.w;
			}

            float invLerp(float from, float to, float value){
              return (value - from) / (to - from);
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 original = float4(0, 0, 0, 0);
                //half4 fog = tex2D(_FogTex, i.uv);

                half paddingTop = 1 -_Padding.x;
                half paddingRight = 1 - _Padding.y;
                half paddingBottom = _Padding.z;
                half paddingLeft = _Padding.w;

                float2 uvWithPadding = float2(invLerp(paddingLeft, paddingRight, i.uv.x), invLerp(paddingBottom, paddingTop, i.uv.y));
                
                half4 fog = blur(_FogTex, uvWithPadding);
                // half4 fog = blur(_FogTex, i.uv);
                
                /*half r = step(_BorderMin, fog.r);
                half g = step(_BorderMin, fog.g);

                half rMax = step(_BorderMax, fog.r);
                half gMax = step(_BorderMax, fog.g);

                fog.r = rMax;
                fog.g = gMax;*/
                half4 finalColor = lerp(lerp(_Color, _ExploredColor, clamp(fog.g, 0, 1)), original, clamp(fog.r, 0, 1));
                //finalColor = lerp(finalColor, finalColor + _BorderColor, r - rMax);
                //finalColor = lerp(finalColor, finalColor + _BorderColor, g - gMax);
                return finalColor;
                
            	/*float depth = 1.0 - UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture, i.uv));
				float3 pos = CamToWorld(i.uv, depth);
            	if (pos.y < 0.0)
				{
					// This is a simplified version of the ray-plane intersection formula: t = -( N.O + d ) / ( N.D )
					float3 dir = normalize(pos - _CamPos.xyz);
					pos = _CamPos.xyz - dir * (_CamPos.y / dir.y);
				}
            	
            	float2 uv = pos.xz * _Params.z + _Params.xy;
            	//uv.x -= 0.5;
            	//uv.y -= 0.5;
            	//return //fixed4(uv.x - 0.5, uv.y - 0.5, 1, 1);
            	half4 fog = blur(_FogTex, uv);
                //return fog;

            	return lerp(lerp(original * _Color, original * _ExploredColor, clamp(fog.g, 0, 1)), original, clamp(fog.r, 0, 1));
            	*/
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
