// デカール用の UV 変換 + サンプリング (位置/スケール/回転、非タイル時は範囲外を透過)
#ifndef RROKI_NT_DECAL_INCLUDED
#define RROKI_NT_DECAL_INCLUDED
half4 RrokiNTDecalSample(Texture2D tex, float2 uv, float4 position, float4 scale, half rotationDeg, uint tiled)
{
    float2 decalUV = uv - position.xy;
    half rad = radians(rotationDeg);
    half sr = sin(rad);
    half cr = cos(rad);
    decalUV = float2(decalUV.x * cr - decalUV.y * sr, decalUV.x * sr + decalUV.y * cr);
    decalUV = decalUV / max(abs(scale.xy), 1e-4) + 0.5;

    half inside = 1.0;
    if (tiled == 0u)
        inside = (decalUV.x >= 0.0 && decalUV.x <= 1.0 && decalUV.y >= 0.0 && decalUV.y <= 1.0) ? 1.0 : 0.0;

    half4 decalSample = tiled != 0u ? SCSampleRepeat(tex, decalUV) : SCSampleClamp(tex, decalUV);
    decalSample.a *= inside;
    return decalSample;
}
#endif
