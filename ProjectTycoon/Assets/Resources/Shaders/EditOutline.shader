// 설계 18 편집 모드 하이라이트(C-2): 몸체 스프라이트 사본(상하좌우 한 칸 오프셋)에 입혀 실루엣을 따라 도는 점선을 만든다.
// 스프라이트 알파가 있는 곳만, 월드 격자(한 칸 _Cell) 대각선 점무늬가 시간에 따라 흐른다. 색은 SpriteRenderer.color(크림 = 옮길 수 있음, 노랑 = 잡음)
Shader "ZooTycoon/EditOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Cell ("Cell (world units)", Float) = 0.025
        _Period ("Dash period (cells)", Float) = 8
        _Speed ("Cells per second", Float) = 12
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
            };

            sampler2D _MainTex;
            float _Cell;
            float _Period;
            float _Speed;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed alpha = tex2D(_MainTex, i.uv).a;
                float k = floor(i.world.x / _Cell) + floor(i.world.y / _Cell) - floor(_Time.y * _Speed);
                float phase = k - _Period * floor(k / _Period);
                float on = step(phase, _Period * 0.5 - 0.5);
                fixed4 c = i.color;
                c.a *= step(0.5, alpha) * on;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
