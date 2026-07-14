// 2D → 1D ハッシュ (広く知られた fract(sin(dot)) 方式のクリーンルーム実装)
#ifndef RROKI_NT_GLITTER_INCLUDED
#define RROKI_NT_GLITTER_INCLUDED
half RrokiNTHash21(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}
#endif
