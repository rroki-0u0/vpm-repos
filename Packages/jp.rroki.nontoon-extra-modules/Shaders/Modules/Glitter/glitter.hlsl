// グリッター: UV グリッドのセルごとの乱数で一部のセルだけを明滅させる加算スパークル。
// ハッシュベースの独自実装 (ライティング合成後に加算)。
#if !defined(OUTLINE)
if (_Enable)
{
    float2 glitterGrid = vertex.uv[_GlitterUV].xy * _GlitterFrequency;
    half glitterRand = RrokiNTHash21(floor(glitterGrid));

    // 乱数が (1 - Density) を超えたセルだけがスパークル対象
    half glitterGate = step(1.0 - _GlitterDensity, glitterRand);

    // セルごとに位相をずらした明滅。Speed = 0 なら常時点灯
    half glitterWave = sin(_Time.y * _GlitterSpeed + glitterRand * 40.0) * 0.5 + 0.5;
    half glitterTwinkle = _GlitterSpeed == 0 ? 1 : glitterWave * glitterWave * glitterWave * glitterWave;

    half3 glitterCol = lerp(_GlitterColor.rgb, _GlitterColor.rgb * sd.albedoAlpha.rgb, _GlitterMultiplyAlbedo);
    sd.col.rgb += glitterGate * glitterTwinkle * glitterCol * (_GlitterBrightness * _GlitterColor.a) * sd.mask[_GlitterMaskChannel];
}
#endif
