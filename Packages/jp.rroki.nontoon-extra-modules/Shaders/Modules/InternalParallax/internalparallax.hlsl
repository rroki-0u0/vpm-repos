// 内部パララックス: 模様が表面の下に沈んで見える表現。2 つのモードを持つ。
//
// Heightmap (0): マップ = RGB が色、A が高さ (白=浅い)。深度ごとに UV をずらした層を重ね、
//   「その層の深さまで模様が沈んでいる」ピクセルだけを加算蓄積する。
//   層ごとに深度グラデーション (Color/Brightness の Near→Far) が乗る。
//   Poiyomi の HeightMap モードの内部パララックス相当のクリーンルーム実装。
// LayerAlpha (1): 深度ごとに UV をずらした RGBA レイヤーを手前から奥へアルファ合成する。
//
// Lit ON (既定) ではライティング前の加算 (sd.add) となり、肌と同じ光と影を受ける
// (Poiyomi の Surface Blend Mode: Add 相当)。OFF ではライティング後の加算 (発光扱い)。
// add フェーズで実行される (RimLight 等と同じ)。
#if !defined(OUTLINE)
if (_Enable && _InternalSurfaceBlend == 0)
{
    half3 internalViewTS = mul(vertex.TBN, vertex.V);
    float2 internalShift = internalViewTS.xy / max(abs(internalViewTS.z), 0.25);
    float2 internalBaseUV = vertex.uv[_InternalUV].xy * _InternalMap_ST.xy + _InternalMap_ST.zw;

    // MatCap 色ソース (ビュー依存の球面マップ)。層に依らないため一度だけ計算する。
    half3 internalMcCol = 0;
    if (_InternalColorSource != 0u)
    {
        half2 internalMcUV = RrokiNTMatCapUV(vertex.Head, sd.N) + internalShift * _InternalMatCapParallax;
        internalMcCol = SCSampleClamp(_InternalMatCap, internalMcUV).rgb;
        internalMcCol = RrokiNTHueRotate(internalMcCol, frac(_InternalMatCapHue + _InternalMatCapHueSpeed * _Time.y));
    }

    half3 internalSum = 0;
    if (_InternalMode == 0)
    {
        // Heightmap: 層深度に届いている模様だけを加算蓄積 (A=高さ, 1-A=模様の沈み込み深さ)
        [loop]
        for (uint internalIndex = 0u; internalIndex < _InternalIterations; internalIndex++)
        {
            half internalT = _InternalIterations > 1u ? internalIndex / (half)(_InternalIterations - 1u) : 0.0;
            float internalDepth = lerp(_InternalDepth.x, _InternalDepth.y, internalT);
            half4 internalSample = SCSampleRepeat(_InternalMap, internalBaseUV - internalShift * internalDepth);
            half internalGate = step(internalT, 1.0 - internalSample.a);
            half3 internalTint = lerp(_InternalColorNear.rgb, _InternalColorFar.rgb, internalT);
            half internalBrightness = lerp(_InternalFadeNear, _InternalFadeFar, internalT);
            half3 internalPickRGB = _InternalColorSource == 0u ? internalSample.rgb
                : (_InternalColorSource == 1u ? internalMcCol : internalSample.rgb * internalMcCol);
            internalSum += internalPickRGB * internalTint * internalBrightness * internalGate;
        }
    }
    else
    {
        // LayerAlpha: 多層サンプルの前後合成
        half internalRemain = 1;
        [loop]
        for (uint internalIndex = 0u; internalIndex < _InternalIterations; internalIndex++)
        {
            half internalT = _InternalIterations > 1u ? internalIndex / (half)(_InternalIterations - 1u) : 0.0;
            float internalDepth = lerp(_InternalDepth.x, _InternalDepth.y, internalT);
            half4 internalSample = SCSampleRepeat(_InternalMap, internalBaseUV - internalShift * internalDepth);
            half internalOpacity = saturate(internalSample.a * lerp(_InternalFadeNear, _InternalFadeFar, internalT));
            half3 internalTint = lerp(_InternalColorNear.rgb, _InternalColorFar.rgb, internalT);
            half3 internalPickRGB = _InternalColorSource == 0u ? internalSample.rgb
                : (_InternalColorSource == 1u ? internalMcCol : internalSample.rgb * internalMcCol);
            internalSum += internalPickRGB * internalTint * internalOpacity * internalRemain;
            internalRemain *= 1.0 - internalOpacity;
        }
    }

    internalSum *= _InternalStrength;
    if (_InternalLit) sd.add += internalSum;
    else sd.postadd += internalSum;
}
#endif
