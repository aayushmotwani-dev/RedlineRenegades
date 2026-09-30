Shader "Redline/Surface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _BumpMap ("Surface Normal", 2D) = "bump" {}
        _BumpStrength ("Normal Strength", Range(0,2)) = 0
        _Metallic ("Metallic", Range(0,1)) = 0.1
        _Smoothness ("Smoothness", Range(0,1)) = 0.35
        _EmissionColor ("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 250
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        sampler2D _BumpMap;
        half _BumpStrength;
        fixed4 _Color;
        fixed4 _EmissionColor;
        half _Metallic;
        half _Smoothness;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            half3 detailNormal = tex2D(_BumpMap, IN.uv_MainTex).rgb * 2.0h - 1.0h;
            o.Normal = normalize(lerp(half3(0, 0, 1), detailNormal, _BumpStrength));
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
