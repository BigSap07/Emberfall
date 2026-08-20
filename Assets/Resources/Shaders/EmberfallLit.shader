// Stylised faux-lit shader for the prototype's procedural characters and terrain.
//
// Self-contained on purpose: the key light is baked into the fragment shader, so the game
// needs no scene lights, no lightmaps and no shadow maps, and nothing here depends on a
// specific render pipeline. The pass deliberately carries no LightMode tag - the Built-In
// pipeline draws such a pass in its forward path, and URP treats it as SRPDefaultUnlit, so
// one pass covers both.
Shader "Emberfall/Lit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ShadeColor ("Shade Tint", Color) = (0.22,0.18,0.34,1)
        _RimColor ("Rim", Color) = (1,0.72,0.42,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _Emission ("Emission", Range(0,4)) = 0
        _Flash ("Hit Flash", Range(0,1)) = 0
        _Fade ("Dissolve", Range(0,1)) = 1
        _BurnColor ("Burn Edge", Color) = (1,0.5,0.18,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _ShadeColor;
            fixed4 _RimColor;
            float _RimPower;
            float _Emission;
            float _Flash;
            float _Fade;
            fixed4 _BurnColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // death dissolve: hashed noise gated by _Fade, with a hot edge while it burns
                float noise = frac(sin(dot(i.worldPos.xz * 11.3 + i.worldPos.y * 5.9,
                                           float2(12.9898, 78.233))) * 43758.5453);
                float cut = noise - (1.0 - _Fade) * 1.04;
                clip(cut);

                float3 key = normalize(float3(0.42, 0.82, -0.38));
                float3 n = normalize(i.worldNormal);
                float ndl = saturate(dot(n, key) * 0.5 + 0.5);
                ndl *= ndl;

                fixed4 tex = tex2D(_MainTex, i.uv);
                float3 base = tex.rgb * _Color.rgb;
                float3 lit = lerp(_ShadeColor.rgb * base, base, ndl);

                float rim = pow(1.0 - saturate(dot(normalize(i.viewDir), n)), _RimPower);
                lit += _RimColor.rgb * rim * 0.8;
                lit += base * _Emission;

                float burning = step(0.001, 1.0 - _Fade);
                float edge = 1.0 - saturate(cut * 9.0);
                lit = lerp(lit, _BurnColor.rgb * 2.0, edge * burning);

                lit = lerp(lit, float3(1.0, 1.0, 1.0), _Flash);
                return fixed4(lit, 1.0);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
