Shader "Custom/Grass"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "Universal"
            "Queue" = "Transparent"
            "PreviewType" = "Plane"
        }

        LOD 100
        Cull Off
        ZWrite On
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        AlphaToMask On

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #pragma shader_feature USE_COLOR
            #pragma shader_feature USE_NOISE

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _ColorTop;
            float4 _ColorBottom;
            float _WindStrength;
            float _WindBendFactor;
            float2 _WindDirection;
            float _NoiseScale;
            float4 _FogColor;
            float _FogDensity;
            float _FogOffset;

            /*
            ###########################################################################################
            Gradient noise code from
            https://docs.unity3d.com/Packages/com.unity.shadergraph@6.9/manual/Gradient-Noise-Node.html
            ###########################################################################################
            */
            float2 unity_gradientNoise_dir(float2 p)
            {
                p = p % 289;
                float x = (34 * p.x + 1) * p.x % 289 + p.y;
                x = (34 * x + 1) * x % 289;
                x = frac(x / 41) * 2 - 1;
                return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
            }

            float unity_gradientNoise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                float d00 = dot(unity_gradientNoise_dir(ip), fp);
                float d01 = dot(unity_gradientNoise_dir(ip + float2(0, 1)), fp - float2(0, 1));
                float d10 = dot(unity_gradientNoise_dir(ip + float2(1, 0)), fp - float2(1, 0));
                float d11 = dot(unity_gradientNoise_dir(ip + float2(1, 1)), fp - float2(1, 1));
                fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
                return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x);
            }

            float2 CalcTranslation(float noise)
            {
                return noise * normalize(_WindDirection) * _WindBendFactor;
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                float noise = unity_gradientNoise(o.worldPos.xz * _NoiseScale + (_Time.y * _WindStrength));

                if(v.uv.y >= 0.1f)
                {
                    v.vertex.xz += CalcTranslation(noise);
                }

                o.vertex = UnityObjectToClipPos(v.vertex);
                
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float4 col = tex2D(_MainTex, i.uv);

                float viewDistance = length(_WorldSpaceCameraPos - i.worldPos);
                float fogFactor = (_FogDensity / sqrt(log(2))) * (max(0.0f, viewDistance - _FogOffset));
                fogFactor = exp2(-fogFactor * fogFactor);

                #ifdef USE_COLOR
                float3 colGrass = lerp(_ColorBottom.rgb, _ColorTop.rgb, i.uv.y);
                float3 finalCol = lerp(_FogColor, colGrass, fogFactor);
                return float4(finalCol.rgb, col.w);
                #endif

                #ifdef USE_NOISE
                float noise = unity_gradientNoise(i.worldPos.xz * _NoiseScale + (_Time.y * _WindStrength));
                float3 finalColNoise = lerp(_FogColor, float3(noise, noise, noise), fogFactor);
                return float4(finalColNoise, col.w);
                #endif
                
                float3 finalColTex = lerp(_FogColor, col.xyz, fogFactor); 

                return float4(finalColTex.rgb, col.w);

            }
            ENDHLSL
        }
    }
}
