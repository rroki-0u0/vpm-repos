// ディゾルブ: ノイズ閾値でピクセルを消し (clip)、消える境界を HDR カラーで発光させる。
// 「ノイズ + 閾値 + 境界発光」という一般に知られた手法のクリーンルーム実装。
//
// base フェーズで実行する理由:
//   - clip がフォワードだけでなく影/深度パスにも効く (SCPixelClip も __SC_PHASE_base__ を通す)
//     ため、消えた部分の影も正しく欠ける。
//   - 境界発光はライティングを受けない加算 (sd.postadd) として乗せる。
// 消失は不透明マテリアルでも動く (レンダリングモードに依存せず自前で clip する)。
#if !defined(OUTLINE)
if (_Enable && _DissolveAmount > 0.0)
{
    float2 dissolveUV = vertex.uv[_DissolveNoiseUV].xy * _DissolveNoise_ST.xy + _DissolveNoise_ST.zw
                      + _DissolveNoiseScroll.xy * _Time.y;
    half dissolveNoise = SCSampleRepeat(_DissolveNoise, dissolveUV).r;
    if (_DissolveInvert) dissolveNoise = 1.0 - dissolveNoise;

    // マスク外 (mask=0) は field を持ち上げ、消えないよう保護する
    half dissolveMask = sd.mask[_DissolveMaskChannel];
    half dissolveField = dissolveNoise + (1.0 - dissolveMask);

    // field < amount のピクセルを消す
    clip(dissolveField - _DissolveAmount);

    // amount .. amount + width の帯を境界発光 (境界で最大、内側へ減衰)
    half dissolveEdgeT = saturate((dissolveField - _DissolveAmount) / max(_DissolveEdgeWidth, 1e-4));
    half dissolveGlow = pow(1.0 - dissolveEdgeT, _DissolveEdgeSharpness);
    sd.postadd += dissolveGlow * _DissolveEdgeColor.rgb * _DissolveEdgeColor.a;
}
#endif
