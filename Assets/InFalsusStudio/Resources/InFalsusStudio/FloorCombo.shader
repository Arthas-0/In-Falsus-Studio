Shader "InFalsusStudio/FloorCombo"
{
    Properties
    {
        _MainTex("Digit atlas: R coverage / G outline", 2D) = "black" {}
        _FillColor("Translucent pale teal interior", Color) = (0.56,0.76,0.77,0.10)
        _OutlineColor("Fine pale edge", Color) = (0.70,0.88,0.89,0.32)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-60" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _FillColor;
                float4 _OutlineColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 Frag(Varyings i):SV_Target
            {
                half2 mask=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rg;
                half fill=mask.r*_FillColor.a;
                half edge=mask.g*_OutlineColor.a;
                half alpha=fill+edge*(1-fill);
                half3 rgb=lerp(_FillColor.rgb,_OutlineColor.rgb,saturate(edge/max(alpha,0.0001h)));
                return half4(rgb,alpha);
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "Queue"="Transparent-60" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _FillColor,_OutlineColor;
            struct Varyings { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(appdata_base v)
            { Varyings o; o.position=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord.xy; return o; }
            fixed4 Frag(Varyings i):SV_Target
            {
                fixed2 mask=tex2D(_MainTex,i.uv).rg;
                fixed fill=mask.r*_FillColor.a;
                fixed edge=mask.g*_OutlineColor.a;
                fixed alpha=fill+edge*(1-fill);
                fixed3 rgb=lerp(_FillColor.rgb,_OutlineColor.rgb,saturate(edge/max(alpha,0.0001)));
                return fixed4(rgb,alpha);
            }
            ENDCG
        }
    }
}
