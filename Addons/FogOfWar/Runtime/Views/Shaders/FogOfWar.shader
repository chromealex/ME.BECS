Shader "ME.BECS/FogOfWar/Default"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0, 0, 1)
        
		_BlurRadius("Blur Radius", Range(0, 10)) = 1
		_BlurHorizontalStep("Blur Horizontal Step", Range(0, 1)) = 0.5
		_BlurVerticalStep("Blur Vertical Step", Range(0, 1)) = 0.5
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
		ZTest Off

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
            float4 _MainTex_ST;
            fixed4 _Color;

            float4 _MainTex_TexelSize;
			float _BlurRadius;
			float _BlurHorizontalStep;
			float _BlurVerticalStep;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 blur (v2f i)
            {
				float2 stride = float2(_BlurRadius, _BlurRadius) / _MainTex_TexelSize.zw * float2(_BlurHorizontalStep, _BlurVerticalStep);

				float r = tex2D(_MainTex, i.uv).r * 0.2270270270;

            	half f1 = 3.2307692308;
            	half f2 = 1.3846153846;
            	half outf1 = 0.0702702703;
            	half outf2 = 0.3162162162;
				r += tex2D(_MainTex, i.uv + f1 * float2(-stride.x, -stride.y)).r * outf1;
				r += tex2D(_MainTex, i.uv + f2 * float2(stride.x, -stride.y)).r * outf2;

				r += tex2D(_MainTex, i.uv + f2 * float2(-stride.x, stride.y)).r * outf2;
				r += tex2D(_MainTex, i.uv + f1 * float2(stride.x, stride.y)).r * outf1;

                fixed4 fog = fixed4(0, 0, 0, 1 - clamp(r, 0, 1));
                return fog;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
            	fixed4 color = blur(i);
                return fixed4(_Color.r, _Color.g, _Color.b, color.a * _Color.a);
            }

            ENDCG
        }
    }
}
