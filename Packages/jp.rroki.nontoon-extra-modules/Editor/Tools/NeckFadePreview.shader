// 首元フェードマスク生成ツールのシーンプレビュー用 (頂点カラー R をヒートマップ表示)。
// 首元(1)=暖色 / 遠方(0)=寒色。エディタ専用。
Shader "Hidden/Rroki/NeckFadePreview"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            Cull Off
            ZTest LEqual
            ZWrite On
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float m = saturate(i.color.r);
                fixed3 c = lerp(fixed3(0.1, 0.15, 0.45), fixed3(1.0, 0.45, 0.1), m);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
