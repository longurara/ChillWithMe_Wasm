Shader "Hidden/HLC/Native UI preview composite"
{
    Properties { _MainTex ("UI",2D)="white"{} }
    SubShader { Pass { ZTest Always ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
        HLSLPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
        V vert(appdata_img i) { V o;o.position=UnityObjectToClipPos(i.vertex);o.uv=i.texcoord;return o; }
        float4 frag(V i):SV_Target { return tex2D(_MainTex,i.uv); }
        ENDHLSL
    } }
}
