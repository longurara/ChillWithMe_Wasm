Shader "HLC/Web/Story Screen"
{
    Properties { _MainTex("Screen",2D)="white"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _ChillEffects, _ChillExtra;
            float Hash(float2 p) { return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453); }
            fixed4 Frag(v2f_img input):SV_Target
            {
                float time=_ChillExtra.z;
                float2 uv=input.uv;
                uv += float2(sin(time*31),cos(time*29))*_ChillExtra.x*.015;
                float row=floor(uv.y*80);
                float noise=Hash(float2(row,floor(time*15)));
                uv.x += step(.8,noise)*(noise-.5)*_ChillEffects.x*.1;
                float split=_ChillEffects.z*.008;
                fixed4 col=tex2D(_MainTex,uv);
                col.r=tex2D(_MainTex,uv+float2(split,0)).r;
                col.b=tex2D(_MainTex,uv-float2(split,0)).b;
                float grey=dot(col.rgb,float3(.2126,.7152,.0722));
                col.rgb=lerp(col.rgb,grey.xxx,_ChillEffects.y);
                col.rgb += (Hash(uv+time)-.5)*_ChillEffects.y*.12;
                col.rgb=lerp(col.rgb,1,_ChillExtra.y);
                col.rgb *= 1-_ChillEffects.w;
                return col;
            }
            ENDCG
        }
    }
}
