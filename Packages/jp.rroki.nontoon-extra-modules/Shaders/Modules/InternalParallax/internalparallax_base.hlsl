// 内部パララックス (Surface Blend = Replace):
// 沈み込んでいる箇所のアルベドを内部テクスチャの色で「置き換える」。
// base フェーズで実行されるため、置き換えた色は肌と同じライティングを受ける。
//
// Heightmap モード: 固定点反復で各ピクセルの沈み込み位置を解決し、その位置の
//   マップ RGB × 深度グラデーション色 × 深度別明るさ でアルベドを置換。
// LayerAlpha モード: 前後合成の結果をアルベドの上に通常合成する。
#if !defined(OUTLINE)
if (_Enable && _InternalSurfaceBlend == 1)
{
    half3 internalRViewTS = mul(vertex.TBN, vertex.V);
    float2 internalRShift = internalRViewTS.xy / max(abs(internalRViewTS.z), 0.25);
    float2 internalRBaseUV = vertex.uv[_InternalUV].xy * _InternalMap_ST.xy + _InternalMap_ST.zw;

    if (_InternalMode == 0)
    {
        // 固定点反復: 高さ (A, 白=浅い) から沈み込み位置へ収束させる
        half4 internalRSample = SCSampleRepeat(_InternalMap, internalRBaseUV);
        [loop]
        for (uint internalRIndex = 0u; internalRIndex < _InternalIterations; internalRIndex++)
        {
            float internalRDepth = lerp(_InternalDepth.y, _InternalDepth.x, internalRSample.a);
            internalRSample = SCSampleRepeat(_InternalMap, internalRBaseUV - internalRShift * internalRDepth);
        }
        half internalRT = 1.0 - internalRSample.a; // 0=表面, 1=最深
        half3 internalRColor = internalRSample.rgb
            * lerp(_InternalColorNear.rgb, _InternalColorFar.rgb, internalRT)
            * lerp(_InternalFadeNear, _InternalFadeFar, internalRT);
        half internalRCover = saturate(internalRT * 8.0) * saturate(_InternalStrength);
        sd.albedoAlpha.rgb = lerp(sd.albedoAlpha.rgb, internalRColor, internalRCover);
    }
    else
    {
        // LayerAlpha: 前後合成した色をアルベドへ通常合成
        half3 internalRSum = 0;
        half internalRRemain = 1;
        [loop]
        for (uint internalRIndex = 0u; internalRIndex < _InternalIterations; internalRIndex++)
        {
            half internalRT = _InternalIterations > 1u ? internalRIndex / (half)(_InternalIterations - 1u) : 0.0;
            float internalRDepth = lerp(_InternalDepth.x, _InternalDepth.y, internalRT);
            half4 internalRSample = SCSampleRepeat(_InternalMap, internalRBaseUV - internalRShift * internalRDepth);
            half internalROpacity = saturate(internalRSample.a * lerp(_InternalFadeNear, _InternalFadeFar, internalRT));
            half3 internalRTint = lerp(_InternalColorNear.rgb, _InternalColorFar.rgb, internalRT);
            internalRSum += internalRSample.rgb * internalRTint * internalROpacity * internalRRemain;
            internalRRemain *= 1.0 - internalROpacity;
        }
        half internalRCover = saturate(_InternalStrength);
        sd.albedoAlpha.rgb = lerp(sd.albedoAlpha.rgb, internalRSum + sd.albedoAlpha.rgb * internalRRemain, internalRCover);
    }
}
#endif
