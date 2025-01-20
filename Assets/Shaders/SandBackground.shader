Shader "Custom/GoldenSandBackground"
{
    Properties
    {
        _MainColor ("Sand Color", Color) = (0.98, 0.84, 0.45, 1)  // Warm golden color
        _SecondaryColor ("Secondary Sand Color", Color) = (0.89, 0.72, 0.32, 1)  // Darker golden tone
        _Scale ("Sand Scale", Range(20, 100)) = 60
        _WindSpeed ("Wind Speed", Range(0, 2)) = 0.3
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.2
        _BreezeScale ("Breeze Scale", Range(1, 10)) = 3
        _Roughness ("Sand Roughness", Range(1, 8)) = 4
        _Persistence ("Pattern Persistence", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

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

            float4 _MainColor;
            float4 _SecondaryColor;
            float _Scale;
            float _WindSpeed;
            float _WindStrength;
            float _BreezeScale;
            float _Roughness;
            float _Persistence;

            // Simple hash function
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Value noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a)* u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            // Breeze effect
            float breeze(float2 uv, float time)
            {
                // Create flowing wave pattern
                float wave = sin(uv.x * _BreezeScale + time * _WindSpeed) * 0.5 + 0.5;
                wave *= sin(uv.y * _BreezeScale * 0.5 + time * _WindSpeed * 0.7) * 0.5 + 0.5;
                return wave * _WindStrength;
            }

            // Fractal Brownian Motion
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                
                for(int i = 0; i < _Roughness; i++)
                {
                    value += amplitude * noise(p * frequency);
                    amplitude *= _Persistence;
                    frequency *= 2.0;
                }
                
                return value;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Add breeze displacement to UV
                float2 breezeUV = i.uv;
                breezeUV.x += breeze(i.uv, _Time.y) * 0.1;
                
                // Generate base sand pattern with displacement
                float2 uv = breezeUV * _Scale;
                float pattern = fbm(uv + float2(_Time.y * _WindSpeed * 0.2, 0));
                
                // Add second layer of noise for more detail
                float detail = fbm(uv * 2.0 + float2(_Time.y * _WindSpeed * -0.1, 0)) * 0.5;
                pattern = (pattern + detail) / 1.5;
                
                // Mix colors based on the pattern
                fixed4 col = lerp(_SecondaryColor, _MainColor, pattern);
                
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}