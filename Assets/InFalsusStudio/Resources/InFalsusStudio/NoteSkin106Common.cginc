// Supplied RGB sheets are opaque atlases, NOT alpha-cut sprites.
// Sample only the fitted body / cap islands. Do not key white out: the white
// edge and center highlights are intentional. Material alpha is independent.
float Band104(float x,float center,float width)
{
    float aa=max(fwidth(x),0.0001);
    return 1-smoothstep(width,width+aa,abs(x-center));
}
float Hash104(float2 p) { return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453); }
float4 Skin106(float2 uv,float2 state,float4 vertexColor)
{
    float u=saturate(uv.x),v=saturate(uv.y),active=saturate(state.x);
    float4 col;
    if(_Mode<0.5)
    {
        float2 source,dx,dy;
        if(state.y<0.5)
        {
            // Mirrored repeats meet at the SAME atlas row. Explicit unwrapped
            // gradients prevent a frac discontinuity selecting a white/blurred mip.
            float phase=2*frac(uv.y*.5),mirror=1-abs(phase-1);
            source=float2(lerp(153.0/2048.0,870.0/2048.0,u),0.015+0.97*mirror);
            float scale=(phase<1?1:-1)*.97;
            dx=float2((717.0/2048.0)*ddx(u),scale*ddx(uv.y));
            dy=float2((717.0/2048.0)*ddy(u),scale*ddy(uv.y));
        }
        else
        {
            source=float2(lerp(1177.0/2048.0,1894.0/2048.0,u),lerp(0.812,0.851,v));
            dx=ddx(source);dy=ddy(source);
        }
        float3 idle=tex2Dgrad(_InactiveTex,source,dx,dy).rgb;
        float3 held=tex2Dgrad(_ActiveTex,source,dx,dy).rgb;
        col=float4(lerp(idle,held,active),lerp(0.90,0.98,active));
        // End caps have the atlas's transverse reflection plus a fine near-edge bevel.
        if(state.y>0.5)col.rgb=lerp(col.rgb,float3(.97,.99,1),0.30*Band104(v,0.05,0.05));
    }
    else if(_Mode<1.5)
    {
        // One-lane Tap: blue glass, TWO side strips and ONE central white glint.
        float3 baseColor=lerp(_Tint.rgb*0.56,_Tint.rgb,0.35+0.65*(1-v));
        float side=max(Band104(u,.017,.017),Band104(u,.983,.017));
        float core=exp(-pow((u-.5)/.031,2))*exp(-pow((v-.36)/.36,2));
        float halo=exp(-pow((u-.5)/.15,2))*.32;
        float bevel=(1-smoothstep(.04,.22,v))*.50;
        baseColor=lerp(baseColor,float3(.64,.96,1),saturate(bevel+halo));
        col=float4(lerp(baseColor,float3(.90,1,1),saturate(side+core*.98)),.97);
    }
    else if(_Mode<2.5)
    {
        // Wide 2/3/4-lane Tap: one continuous white-to-mint panel. No center
        // spine, no per-lane repeating seams, no one-lane white side strips.
        float3 color=lerp(float3(.94,1,.98),_Tint.rgb,smoothstep(.12,1,v));
        color=lerp(color,float3(1,1,1),.55*(1-smoothstep(.02,.16,v)));
        col=float4(color,.96);
    }
    else if(_Mode<3.5)
    {
        // Side notes stay vivid above a dark slope; thin bright ends, coloured body.
        float side=max(Band104(u,.022,.022),Band104(u,.978,.022));
        float center=exp(-pow((u-.5)/.13,2))*.33;
        float shine=(1-smoothstep(.03,.22,v))*.48+center;
        float3 color=lerp(_Tint.rgb*.75,_Tint.rgb,1-v*.5);
        color=lerp(color,lerp(_Tint.rgb,float3(1,1,1),.82),saturate(shine+side));
        col=float4(color,.94);
    }
    else
    {
        // Field interior remains translucent. Ribs and luminous boundary strips
        // are separate geometry, so they follow the actual independently-eased edges.
        float edge=pow(abs(u*2-1),5);
        float2 lattice=float2(u*40,uv.y*10);
        float2 local=frac(lattice)-.5;
        float dots=(1-smoothstep(.09,.20,length(local)))*step(.65,Hash104(floor(lattice)));
        float3 color=lerp(_Tint.rgb,float3(.74,.48,1),edge*.60+dots*.28);
        col=float4(color,lerp(.34,.45,active)+edge*.10+dots*.065);
    }
    col.rgb*=vertexColor.rgb;col.a*=vertexColor.a;
    return col;
}
