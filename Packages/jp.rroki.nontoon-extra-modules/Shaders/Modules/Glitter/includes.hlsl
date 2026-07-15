// fract-sin ハッシュ (広く知られた fract(sin(dot)) 方式) のクリーンルーム実装
#ifndef RROKI_NT_GLITTER_INCLUDED
#define RROKI_NT_GLITTER_INCLUDED
half RrokiNTHash21(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}
// 2D → 3D 乱数 (輝点位置 xy とゲート/位相 z 用)
half3 RrokiNTHash23(float2 p)
{
    float3 q = float3(dot(p, float2(127.1, 311.7)),
                      dot(p, float2(269.5, 183.3)),
                      dot(p, float2(113.5, 271.9)));
    return frac(sin(q) * 43758.5453);
}
#endif
