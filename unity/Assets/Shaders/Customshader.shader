Shader "Custom/HeightShader" {
    Properties {
        _Threshold1 ("Threshold 1", Range(-10,10)) = 0.0
        _Threshold2 ("Threshold 2", Range(-10,10)) = 5.0
        _TransitionRange ("Transition Range", Range(0,2)) = 0.5
        
        _Texture0 ("Texture 0", 2D) = "white" {}
        _Scale0 ("Scale 0", Range(0,20)) = 1
        _Color0 ("Color 0", Color) = (1,1,1,1)
        
        _Texture1 ("Texture 1", 2D) = "white" {}
        _Scale1 ("Scale 1", Range(0,20)) = 1
        _Color1 ("Color 1", Color) = (1,1,1,1)
        
        _Texture2 ("Texture 2", 2D) = "white" {}
        _Scale2 ("Scale 2", Range(0,20)) = 1
        _Color2 ("Color 2", Color) = (1,1,1,1)

        _StochasticScale ("Stochastic Scale Multiplier", Range(0, 2)) = 1.3
        _StochasticBlend ("Stochastic Blend", Range(0, 1)) = 0.5
    }
 
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200
 
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0
 
        sampler2D _Texture0;
        sampler2D _Texture1;
        sampler2D _Texture2;
        float _Scale0;
        float _Scale1;
        float _Scale2;
        float _Threshold1;
        float _Threshold2;
        float _TransitionRange;
        fixed4 _Color0;
        fixed4 _Color1;
        fixed4 _Color2;
        float _StochasticScale;
        float _StochasticBlend;
 
        struct Input {
            float3 objectPos;
            float3 localNormal;
            float3 worldPos;
        };
 
        void vert (inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objectPos = v.vertex.xyz;
            o.localNormal = v.normal;
            o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
        }

        // Macro for Triplanar Sampling - Escala consistente basada en derivadas de fragmento
        #define TRIPLANAR_SAMPLE(tex, pos, normal, scale, color, outColor, worldPos) \
            float3 worldDX = ddx(worldPos); \
            float3 worldDY = ddy(worldPos); \
            float pixelScale = max(length(worldDX), length(worldDY)); \
            float2 uvX = pos.zy * scale / max(pixelScale, 0.01); \
            float2 uvY = pos.xz * scale / max(pixelScale, 0.01); \
            float2 uvZ = pos.xy * scale / max(pixelScale, 0.01); \
            float sScale = (scale * _StochasticScale) / max(pixelScale, 0.01); \
            float cosR = 0.866; \
            float sinR = 0.5; \
            float2x2 rot = float2x2(cosR, -sinR, sinR, cosR); \
            float2 uvX2 = mul(rot, pos.zy) * sScale; \
            float2 uvY2 = mul(rot, pos.xz) * sScale; \
            float2 uvZ2 = mul(rot, pos.xy) * sScale; \
            fixed4 colX = tex2D(tex, uvX); \
            fixed4 colY = tex2D(tex, uvY); \
            fixed4 colZ = tex2D(tex, uvZ); \
            fixed4 colX2 = tex2D(tex, uvX2); \
            fixed4 colY2 = tex2D(tex, uvY2); \
            fixed4 colZ2 = tex2D(tex, uvZ2); \
            colX = lerp(colX, colX2, _StochasticBlend); \
            colY = lerp(colY, colY2, _StochasticBlend); \
            colZ = lerp(colZ, colZ2, _StochasticBlend); \
            float3 w = abs(normal); \
            w = w * w * w * w; \
            w /= (w.x + w.y + w.z + 1e-5); \
            outColor = (colX * w.x + colY * w.y + colZ * w.z) * color;

        void surf (Input IN, inout SurfaceOutput o) {
            float height = IN.objectPos.y;
            float3 pos = IN.objectPos;
            float3 normal = normalize(IN.localNormal);

            fixed4 tex0, tex1, tex2;
            
            // Invoke macros with proper scoping
            {
                TRIPLANAR_SAMPLE(_Texture0, pos, normal, _Scale0, _Color0, tex0, IN.worldPos);
            }
            {
                TRIPLANAR_SAMPLE(_Texture1, pos, normal, _Scale1, _Color1, tex1, IN.worldPos);
            }
            {
                TRIPLANAR_SAMPLE(_Texture2, pos, normal, _Scale2, _Color2, tex2, IN.worldPos);
            }
            
            if (height < _Threshold1 - _TransitionRange) {
                o.Albedo = tex0.rgb;
            } else if (height < _Threshold1 + _TransitionRange) {
                float blend = (height - (_Threshold1 - _TransitionRange)) / (2 * _TransitionRange);
                o.Albedo = lerp(tex0, tex1, blend).rgb;
            } else if (height < _Threshold2 - _TransitionRange) {
                o.Albedo = tex1.rgb;
            } else if (height < _Threshold2 + _TransitionRange) {
                float blend = (height - (_Threshold2 - _TransitionRange)) / (2 * _TransitionRange);
                o.Albedo = lerp(tex1, tex2, blend).rgb;
            } else {
                o.Albedo = tex2.rgb;
            }
        }
        ENDCG
    }
 
    FallBack "Diffuse"
}