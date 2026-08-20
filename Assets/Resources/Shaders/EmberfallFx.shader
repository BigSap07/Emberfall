// Unlit quad shader for every battle effect: glows, slash arcs, ground rings, blob shadows
// and the sky backdrop. The blend mode is script-controlled, so one shader covers both
// additive effects and alpha-blended surfaces.
//
// Like Emberfall/Lit, the pass carries no LightMode tag so it renders under both the
// Built-In pipeline and URP.
Shader "Emberfall/Fx"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SrcBlend ("Src Blend", Float) = 5   // SrcAlpha
        _DstBlend ("Dst Blend", Float) = 1   // One
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * _Color;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
