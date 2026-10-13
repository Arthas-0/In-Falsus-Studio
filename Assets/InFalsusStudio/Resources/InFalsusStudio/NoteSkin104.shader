Shader "InFalsusStudio/NoteSkin104"
{
    Properties
    {
        _InactiveTex("Hold idle atlas",2D)="white"{} _ActiveTex("Hold active atlas",2D)="white"{}
        _Tint("Tint",Color)=(1,1,1,1) _Mode("Skin kind",Float)=0
        _FogColor("Fog",Color)=(.016,.027,.042,1) _FogStart("Fog start",Float)=32 _FogEnd("Fog end",Float)=76
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            sampler2D _InactiveTex;sampler2D _ActiveTex;
            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor,_Tint;float _FogStart,_FogEnd,_Mode;
            CBUFFER_END
            #include "NoteSkin104Common.cginc"
            struct Attributes { float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float2 state:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float2 state:TEXCOORD1;float worldZ:TEXCOORD2; };
            Varyings Vert(Attributes v) { Varyings o;float3 world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(world);o.color=v.color;o.uv=v.uv;o.state=v.state;o.worldZ=world.z;return o; }
            half4 Frag(Varyings i):SV_Target {float4 c=Skin104(i.uv,i.state,i.color);c.a*=1-smoothstep(_FogStart,max(_FogStart+.01,_FogEnd),i.worldZ);return c;}
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _InactiveTex;sampler2D _ActiveTex;
            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor,_Tint;float _FogStart,_FogEnd,_Mode;
            CBUFFER_END
            #include "NoteSkin104Common.cginc"
            struct Attributes { float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float2 state:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float2 state:TEXCOORD1;float worldZ:TEXCOORD2; };
            Varyings Vert(Attributes v) { Varyings o;float3 world=mul(unity_ObjectToWorld,v.positionOS).xyz;o.positionCS=UnityObjectToClipPos(v.positionOS);o.color=v.color;o.uv=v.uv;o.state=v.state;o.worldZ=world.z;return o; }
            half4 Frag(Varyings i):SV_Target {float4 c=Skin104(i.uv,i.state,i.color);c.a*=1-smoothstep(_FogStart,max(_FogStart+.01,_FogEnd),i.worldZ);return c;}
            ENDCG
        }
    }
}
