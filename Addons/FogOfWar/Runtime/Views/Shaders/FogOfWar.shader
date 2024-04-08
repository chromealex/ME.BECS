Shader "ME.BECS/FogOfWar/Default"
{
    Properties
    {
        _BaseTex ("Main Tex", 2D) = "white" {}
        _FogTex ("Fow Texture (R - Fog, G - Explored, B - Height)", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0, 0, 1)
        _ExploredColor ("Explored Color", Color) = (0, 0, 0, 1)
        
		_BlurRadius("Blur Radius", Range(0, 10)) = 1
		_BlurStep("Blur Step", Range(0, 1)) = 0.5
    }
    SubShader
    {
		Tags
		{
			"Queue" = "Transparent"
			"RenderType"="Opaque"
			"IgnoreProjector" = "False"
			"PreviewType" = "Fullscreen"
			"RenderPipeline" = "UniversalPipeline"
			"ShaderGraphTargetId"="UniversalFullscreenSubTarget"
		}

        Cull Off
        ZWrite Off
    	
		Pass
        {
            Name "CustomPostPro_ColorPass"
            HLSLPROGRAM
            #pragma target 3.0
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #pragma vertex Vert
            #pragma fragment frag
            
            CBUFFER_START(UnityPerMaterial)
            TEXTURE2D(_FogTex);
            SAMPLER(sampler_FogTex);
            float4 _FogTex_ST;
            half4 _Color;
            half4 _ExploredColor;

            float4 _FogTex_TexelSize;
			float _BlurRadius;
			float _BlurStep;

            uniform sampler2D _HeightTex;
            uniform float _HeightResolution;
            
			uniform float4x4 _InverseMVP;
			uniform float4 _CamPos;
			uniform float4 _Params;
			CBUFFER_END

            inline half4 blur(Texture2D tex, SamplerState texSampler, float2 uv)
            {
				float2 stride = float2(_BlurRadius, _BlurRadius) / _FogTex_TexelSize.zw * float2(_BlurStep, _BlurStep);
				const half blur_size = stride.x;

            	half2 sum = 0;
            	half3 center = SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x, uv.y));
				sum += center.rg * 0.16 * 4;
            	
            	sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x, uv.y - 4.0 * blur_size)).rg * 0.05;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x, uv.y - 3.0 * blur_size)).rg * 0.09;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x, uv.y - 2.0 * blur_size)).rg * 0.12;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x, uv.y - blur_size)).rg * 0.15;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x, uv.y + blur_size)).rg * 0.15;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x, uv.y + 2.0 * blur_size)).rg * 0.12;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x, uv.y + 3.0 * blur_size)).rg * 0.09;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x, uv.y + 4.0 * blur_size)).rg * 0.05;
				
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x - 4.0 * blur_size, uv.y)).rg * 0.05;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x - 3.0 * blur_size, uv.y)).rg * 0.09;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x - 2.0 * blur_size, uv.y)).rg * 0.12;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x - blur_size, uv.y)).rg * 0.15;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x + blur_size, uv.y)).rg * 0.15;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x + 2.0 * blur_size, uv.y)).rg * 0.12;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x + 3.0 * blur_size, uv.y)).rg * 0.09;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x + 4.0 * blur_size, uv.y)).rg * 0.05;

				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x - 4.0 * blur_size, uv.y - 4.0 * blur_size)).rg * 0.05;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x - 3.0 * blur_size, uv.y - 3.0 * blur_size)).rg * 0.09;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x - 2.0 * blur_size, uv.y - 2.0 * blur_size)).rg * 0.12;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x - 1.0 * blur_size, uv.y - blur_size)).rg * 0.15;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x + 1.0 * blur_size, uv.y + blur_size)).rg * 0.15;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x + 2.0 * blur_size, uv.y + 2.0 * blur_size)).rg * 0.12;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x + 3.0 * blur_size, uv.y + 3.0 * blur_size)).rg * 0.09;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x + 4.0 * blur_size, uv.y + 4.0 * blur_size)).rg * 0.05;
				
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x - 4.0 * blur_size, uv.y + 4.0 * blur_size)).rg * 0.05;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x - 3.0 * blur_size, uv.y + 3.0 * blur_size)).rg * 0.09;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x - 2.0 * blur_size, uv.y + 2.0 * blur_size)).rg * 0.12;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x - 1.0 * blur_size, uv.y + blur_size)).rg * 0.15;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x + 1.0 * blur_size, uv.y - blur_size)).rg * 0.15;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x + 2.0 * blur_size, uv.y - 2.0 * blur_size)).rg * 0.12;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x + 3.0 * blur_size, uv.y - 3.0 * blur_size)).rg * 0.09;
				sum += SAMPLE_TEXTURE2D(tex, texSampler, half2(uv.x + 4.0 * blur_size, uv.y - 4.0 * blur_size)).rg * 0.05;

                half4 fog = half4(sum.r, sum.g, center.b, 0);
                return fog;
            }

            inline float3 CamToWorld(in float2 uv, in half depth)
			{
				float4 pos = float4(uv.x, uv.y, depth, 1.0);
				pos.xyz = pos.xyz * 2.0 - 1.0;
				pos = mul(_InverseMVP, pos);
				return pos.xyz / pos.w;
			}
            
            half4 frag(Varyings i) : SV_Target
            {
            	const half4 original = SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, i.texcoord);
            	const half depth = 1.0 - SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.texcoord);
				float3 pos = CamToWorld(i.texcoord, depth);
            	if (pos.y < 0.0)
				{
					// This is a simplified version of the ray-plane intersection formula: t = -( N.O + d ) / ( N.D )
					float3 dir = normalize(pos - _CamPos.xyz);
					pos = _CamPos.xyz - dir * (_CamPos.y / dir.y);
				}
            	
            	float2 uv = pos.xz * _Params.z + _Params.xy;
            	uv.x -= 0.5;
            	uv.y -= 0.5;
            	half4 fog = blur(_FogTex, sampler_FogTex, uv);
            	half4 f = lerp(lerp(original * _Color, original * _ExploredColor, clamp(fog.g, 0, 1)), original, clamp(fog.r, 0, 1));
            	return f;
            }

            ENDHLSL
        }
    }
}
