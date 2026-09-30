Shader "Hidden/Redline/PremiumGrade"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Contrast;
            float _Saturation;
            float _Vignette;
            float _Lift;
            float4 _GradeTint;

            fixed4 frag(v2f_img input) : SV_Target
            {
                float3 color = tex2D(_MainTex, input.uv).rgb;
                color += (1.0 - color) * _Lift;
                color = (color - 0.5) * _Contrast + 0.5;
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                color = lerp(luminance.xxx, color, _Saturation) * _GradeTint.rgb;
                float2 centered = input.uv * 2.0 - 1.0;
                centered.x *= _ScreenParams.x / _ScreenParams.y;
                float edge = smoothstep(0.42, 1.35, dot(centered, centered));
                color *= 1.0 - edge * _Vignette;
                return fixed4(max(color, 0.0), 1.0);
            }
            ENDCG
        }
    }
}
