// Unlit quad shader for every battle effect: glows, slash arcs, ground rings, blob shadows,
// the sky backdrop, and (added for the sprite unit path) a textured character billboard.
// The blend mode is script-controlled, so one shader covers both additive effects and
// alpha-blended surfaces.
//
// _Flash, _Fade and _Emission default to no-ops (0, 1, 0) so every existing caller - none of
// which sets them - renders bit-identical to before. They exist so a sprite-based UnitRig can
// share BattleAssets.NewFx materials with UnitRig.SetFlash/SetFade/SetEmission, the same calls
// the procedural rig's Emberfall/Lit materials already answer to, without UnitActor needing to
// know or care which kind of rig it is animating.
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
        _Flash ("Hit Flash", Range(0,1)) = 0
        _Fade ("Fade", Range(0,1)) = 1
        _Emission ("Emission", Range(0,4)) = 0
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
            float _Flash;
            float _Fade;
            float _Emission;

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
                fixed4 c = tex2D(_MainTex, i.uv) * _Color;
                c.rgb += c.rgb * _Emission;
                c.rgb = lerp(c.rgb, fixed3(1, 1, 1), _Flash);
                c.a *= _Fade;
                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
