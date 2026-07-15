// グリッター: UV グリッドのセルごとに丸い輝点を置き、視線/光源角に依存して煌めかせる
// 加算スパークル。ハッシュ + セルごとのファセット法線という一般的な手法のクリーンルーム実装
// (ライティング合成後に加算)。
#if !defined(OUTLINE)
if (_Enable)
{
    float2 glitterGrid = vertex.uv[_GlitterUV].xy * _GlitterFrequency;
    float2 glitterCell = floor(glitterGrid);
    float2 glitterLocal = frac(glitterGrid);
    half3 glitterRand = RrokiNTHash23(glitterCell);

    // 乱数が (1 - Density) を超えたセルだけがスパークル対象
    half glitterGate = step(1.0 - _GlitterDensity, glitterRand.x);

    // セル内のランダム位置に丸い輝点 (Point Size で半径調整)
    half glitterDist = distance(glitterLocal, glitterRand.yz);
    half glitterShape = 1.0 - smoothstep(_GlitterSize * 0.5, _GlitterSize, glitterDist);

    // 視線/光源角依存: セルごとのファセット法線とハーフベクトルの一致度
    half3 glitterFacetTS = normalize(half3(glitterRand.yz * 2.0 - 1.0, 1.0));
    half3 glitterFacetW = normalize(sd.T * glitterFacetTS.x + sd.B * glitterFacetTS.y + sd.N * glitterFacetTS.z);
    half3 glitterHalf = normalize(vertex.V + sd.L);
    half glitterSpec = pow(saturate(dot(glitterFacetW, glitterHalf)), _GlitterSparkleSharpness);

    // 時間ベースの明滅 (視線非依存分)。Speed = 0 なら常時点灯
    half glitterWave = sin(_Time.y * _GlitterSpeed + glitterRand.x * 40.0) * 0.5 + 0.5;
    half glitterTwinkle = _GlitterSpeed == 0 ? 1.0 : glitterWave * glitterWave * glitterWave * glitterWave;

    // 視線依存の煌めきと時間明滅をブレンド
    half glitterSparkle = lerp(glitterTwinkle, glitterSpec, _GlitterViewDependent);

    half3 glitterCol = lerp(_GlitterColor.rgb, _GlitterColor.rgb * sd.albedoAlpha.rgb, _GlitterMultiplyAlbedo);
    sd.col.rgb += glitterGate * glitterShape * glitterSparkle * glitterCol
                * (_GlitterBrightness * _GlitterColor.a) * sd.mask[_GlitterMaskChannel];
}
#endif
