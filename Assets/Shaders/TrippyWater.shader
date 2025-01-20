Shader "Custom/TrippyWater"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color1 ("Color 1", Color) = (0.2, 0.5, 0.8, 1)
        _Color2 ("Color 2", Color) = (0.4, 0.7, 1.0, 1)
        _Color3 ("Color 3", Color) = (0.6, 0.3, 0.9, 1)
        _WaveSpeed ("Wave Speed", Range(0.1, 10)) = 1
        _WaveScale ("Wave Scale", Range(1, 50)) = 10
        _DistortionStrength ("Distortion", Range(0, 0.1)) = 0.02
        _ColorBlendSpeed ("Color Blend Speed", Range(0.1, 5)) = 1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "PreviewType" = "Plane"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
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
            float4 _Color1;
            float4 _Color2;
            float4 _Color3;
            float _WaveSpeed;
            float _WaveScale;
            float _DistortionStrength;
            float _ColorBlendSpeed;

            // Helper function for creating smooth noise
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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Create time-based movement
                float2 uv = i.uv;
                float time = _Time.y * _WaveSpeed;
                
                // Generate noise-based distortion
                float2 distortion1 = float2(
                    unity_gradientNoise(uv * _WaveScale + float2(time, 0)),
                    unity_gradientNoise(uv * _WaveScale + float2(0, time))
                ) * _DistortionStrength;
                
                float2 distortion2 = float2(
                    unity_gradientNoise(uv * _WaveScale * 0.8 - float2(time * 1.2, 0)),
                    unity_gradientNoise(uv * _WaveScale * 0.8 - float2(0, time * 1.2))
                ) * _DistortionStrength;
                
                // Sample texture with layered distortion
                float2 finalUV = uv + distortion1 + distortion2;
                fixed4 texColor = tex2D(_MainTex, finalUV);
                
                // Create triple color blend
                float3 colorBlend = float3(
                    sin(time * _ColorBlendSpeed) * 0.5 + 0.5,
                    sin(time * _ColorBlendSpeed * 1.2 + 2.0) * 0.5 + 0.5,
                    sin(time * _ColorBlendSpeed * 0.8 + 4.0) * 0.5 + 0.5
                );
                
                // Mix three colors based on UV position and time
                fixed4 col1 = lerp(_Color1, _Color2, colorBlend.x);
                fixed4 col2 = lerp(_Color2, _Color3, colorBlend.y);
                fixed4 finalColor = lerp(col1, col2, colorBlend.z);
                
                // Add wavy pattern
                float wave = sin(uv.x * _WaveScale + time) * 
                           cos(uv.y * _WaveScale + time) * 0.5 + 0.5;
                
                finalColor = lerp(finalColor, finalColor * 1.2, wave);
                
                return finalColor * texColor;
            }
            ENDCG
        }
    }
}