// Square terrain art on a flat quad, clipped to a pointy-top hex (cover UVs, transparent edges).
Shader "Nexus/HexMaskedSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 0.5)
        _Aspect ("Texture aspect (width / height)", Float) = 1
        _HexRadius ("Hex mask radius (UV space)", Float) = 0.46
    }

    SubShader
    {
        Tags { "Queue"="Transparent+50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Offset -1, -1
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Aspect;
            float _HexRadius;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 rawUv : TEXCOORD1;
            };

            bool InsidePointyHex(float2 p, float r)
            {
                [unroll]
                for (int i = 0; i < 6; i++)
                {
                    float a0 = radians(60.0 * i + 30.0);
                    float a1 = radians(60.0 * ((i + 1) % 6) + 30.0);
                    float2 v0 = float2(cos(a0), sin(a0)) * r;
                    float2 v1 = float2(cos(a1), sin(a1)) * r;
                    float2 edge = v1 - v0;
                    float edgeSide = edge.x * (p.y - v0.y) - edge.y * (p.x - v0.x);
                    if (edgeSide < -1e-5)
                        return false;
                }
                return true;
            }

            float2 CoverUv(float2 uv01, float aspect)
            {
                float2 c = uv01 - 0.5;
                float2 scale = aspect > 1.0 ? float2(aspect, 1.0) : float2(1.0, 1.0 / max(aspect, 1e-4));
                return c / scale + 0.5;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.rawUv = v.uv;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 hexP = i.rawUv - 0.5;
                if (!InsidePointyHex(hexP, _HexRadius))
                    discard;

                float2 uv = CoverUv(i.rawUv, _Aspect);
                fixed4 col = tex2D(_MainTex, uv) * _Color;
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
