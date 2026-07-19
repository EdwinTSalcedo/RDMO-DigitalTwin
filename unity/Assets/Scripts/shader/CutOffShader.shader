Shader "Custom/HeightFilterShader" {
    Properties {
        _AboveTexture ("Above Texture", 2D) = "white" {}
        _BelowTexture ("Below Texture", 2D) = "white" {}
        _HeightThreshold ("Height Threshold", Range(-1,0)) = 0.05
        _AboveColor ("Above Color", Color) = (1, 1, 1, 1)
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert

        sampler2D _AboveTexture;
        sampler2D _BelowTexture;
        float _HeightThreshold;
        fixed4 _AboveColor;

        struct Input {
            float2 uv_AboveTexture;
            float2 uv_BelowTexture;
            float3 worldPos;
        };

        void surf (Input IN, inout SurfaceOutput o) {
            float height = IN.worldPos.y;

            // Calculate the blending factor
            float blendFactor = smoothstep(_HeightThreshold - 0.01, _HeightThreshold + 0.01, height);

            // Blend the two textures
            fixed4 aboveColor = tex2D(_AboveTexture, IN.uv_AboveTexture) * _AboveColor;
            fixed4 belowColor = tex2D(_BelowTexture, IN.uv_BelowTexture);
            fixed4 finalColor = lerp(belowColor, aboveColor, blendFactor);

            o.Albedo = finalColor.rgb;
            o.Alpha = finalColor.a;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
