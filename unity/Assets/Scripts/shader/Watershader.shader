Shader "Custom/ColorTransparentShader" {
    Properties {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Transparency ("Transparency", Range(0, 1)) = 1
        _MainTex ("Texture", 2D) = "white" {}
        _RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.1
        _RefractionDistortion ("Refraction Distortion", Range(0, 1)) = 0.2
    }
 
    SubShader {
        Tags { "RenderType"="Transparent" }
        LOD 200
 
        Blend SrcAlpha OneMinusSrcAlpha
 
        CGPROGRAM
        #pragma surface surf Lambert alpha:fade
 
        sampler2D _MainTex;
        fixed4 _Color;
        half _Transparency;
        float _RefractionStrength;
        float _RefractionDistortion;
 
        struct Input {
            float2 uv_MainTex;
        };
 
        void surf (Input IN, inout SurfaceOutput o) {
            float2 distortedUV = IN.uv_MainTex + (tex2D(_MainTex, IN.uv_MainTex).rg - 0.5) * _RefractionDistortion * _RefractionStrength;
            fixed4 c = tex2D(_MainTex, distortedUV) * _Color;
            c.a *= _Transparency;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
 
    FallBack "Diffuse"
}
