// 調べられる物（ドア・村人など）のハイライト。
// 1) 外側の縁取り（裏面を法線方向に広げて描く）
// 2) 表面をうっすら光らせる（ふちほど強く光る + 点滅）
// 元のモデルの上に、同じメッシュをこのマテリアルでもう一枚重ねて描きます（InteractHighlight.cs）。
Shader "MonsterAction/InteractHighlight"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.8, 0.25, 1)
        _OutlineWidth ("Outline Width (pixel)", Range(0, 10)) = 3
        _RimPower ("Rim Power", Range(0.5, 8)) = 2
        _FillStrength ("Fill Strength", Range(0, 1)) = 0.15
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "IgnoreProjector" = "True" }

        // ---- 1) 縁取り（URP では LightMode 無し = SRPDefaultUnlit として描かれる） ----
        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _OutlineWidth;
            float _PulseSpeed;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 offset = mul((float2x2)UNITY_MATRIX_P, viewNormal.xy);
                float len = length(offset);
                if (len > 1e-5) offset /= len;
                // 画面上でだいたい _OutlineWidth ピクセルの太さになるように広げる
                o.pos.xy += offset * (_OutlineWidth * 2.0 / _ScreenParams.xy) * o.pos.w;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float pulse = 0.75 + 0.25 * sin(_Time.y * _PulseSpeed);
                return fixed4(_Color.rgb, _Color.a * pulse);
            }
            ENDCG
        }

        // ---- 2) 表面のうっすら光（加算） ----
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _RimPower;
            float _FillStrength;
            float _PulseSpeed;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 viewDir : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 v = normalize(i.viewDir);
                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
                float pulse = 0.7 + 0.3 * sin(_Time.y * _PulseSpeed);
                float k = (rim + _FillStrength) * pulse * _Color.a;
                return fixed4(_Color.rgb * k, 0);
            }
            ENDCG
        }
    }
}
