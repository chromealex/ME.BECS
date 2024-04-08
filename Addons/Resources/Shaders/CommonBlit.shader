Shader "ME.BECS/Common/Blit"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite Off Cull Off
        Pass
        {
            Name "Blit"
            HLSLPROGRAM
            // Core.hlsl includes URP basic variables needed for any shader. The Blit.hlsl provides a
            //Vert and Fragment function that abstracts platform differences when handling a full screen shader pass.
            //It also declares a _BlitTex texture that is bound by the Blitter API.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            // This is a simple read shader so we use the default provided Vert and FragNearest
            //functions. If you would like to do a bilinear sample you could use the FragBilinear functions instead.
            #pragma vertex Vert
            #pragma fragment FragNearest
            ENDHLSL
        }
    }
}