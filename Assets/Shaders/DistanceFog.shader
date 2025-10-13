Shader "Custom/DistanceFog"
{
    Properties
    {
    }
   SubShader
   {
       Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
       ZWrite Off Cull Off
       Pass
       {
           Name "BlitWithMaterialPass"

           HLSLPROGRAM
           #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
           //This already defines Varyings, _BlitTexture and _BlitMipLevel
           #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
           //Makes us able to access depth texture through SampleSceneDepth(UV)
           //and contains texture definitions/sampler for _CameraDepthTexture 
           #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl" 

           #pragma vertex Vert
           #pragma fragment Frag

            float _FogDensity;
            float4 _FogColor;

            float4 Frag(Varyings IN) : SV_Target
            {

                float2 UV = IN.texcoord.xy;
                half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, UV, _BlitMipLevel);

                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(UV);
                #else
                    // Adjust Z to match NDC for OpenGL ([-1, 1])
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
                #endif

                float f;

                if(depth < 0.01)
                {
                    f = 0;
                }
                else
                {
                    f = (_FogDensity / sqrt(log(2))) * (max(0.0f, depth));
                    f = exp2(-f * f);
                }

                return lerp(color, _FogColor, f);
           }

           ENDHLSL
       }
   }
}