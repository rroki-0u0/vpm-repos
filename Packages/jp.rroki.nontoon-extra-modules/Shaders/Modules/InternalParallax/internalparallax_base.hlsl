// 内部パララックス (Surface Blend = Replace):
// 沈み込んでいる箇所のアルベドを内部テクスチャの色で「置き換える」。
// base フェーズで実行されるため、置き換えた色は肌と同じライティングを受ける。
//
// Heightmap モード: 内部ハイトフィールドへ視線レイを進めて交差位置を求める
//   (POM: 線形探索 + 交点の線形補間)。交差位置の深度が深いほど強く置換するため、
//   マスク外 (A=表面レベルにベイク済み) や平坦部は自然に効果が消える。
// LayerAlpha モード: 前後合成の結果をアルベドの上に通常合成する。
#if !defined(OUTLINE)
if (_Enable && _InternalSurfaceBlend == 1)
{
    half3 internalRViewTS = mul(vertex.TBN, vertex.V);
    float2 internalRShift = internalRViewTS.xy / max(abs(internalRViewTS.z), 0.25);
    float2 internalRBaseUV = vertex.uv[_InternalUV].xy * _InternalMap_ST.xy + _InternalMap_ST.zw;
    float internalRMin = _InternalDepth.x;
    float internalRMax = max(_InternalDepth.y, internalRMin + 1e-5);
    uint internalRSteps = max(_InternalIterations, 1u);

    // MatCap 色ソース (ビュー依存の球面マップ)。層に依らないため一度だけ計算する。
    // _InternalMatCapParallax で内部視差シフトを UV に加え、奥に沈んだ映り込みを表現できる。
    half3 internalRMcCol = 0;
    if (_InternalColorSource != 0u)
    {
        half2 internalRMcUV = RrokiNTMatCapUV(vertex.Head, sd.N) + internalRShift * _InternalMatCapParallax;
        internalRMcCol = SCSampleClamp(_InternalMatCap, internalRMcUV).rgb;
        internalRMcCol = RrokiNTHueRotate(internalRMcCol, frac(_InternalMatCapHue + _InternalMatCapHueSpeed * _Time.y));
    }

    if (_InternalMode == 0)
    {
        // Heightmap: 内部ハイトフィールド (A=高さ, 白=浅い) への POM 交差
        float2 internalRShiftMax = internalRShift * internalRMax;
        float internalRInvMax = 1.0 / internalRMax;
        float internalRStepSize = 1.0 / internalRSteps;

        float2 internalRUV = internalRBaseUV;
        float internalRRayH = 1.0;
        float internalRMapH = 1.0 - lerp(internalRMax, internalRMin, SCSampleRepeat(_InternalMap, internalRUV).a) * internalRInvMax;
        float2 internalRPrevUV = internalRUV;
        float internalRPrevRayH = internalRRayH;
        float internalRPrevMapH = internalRMapH;

        [loop]
        for (uint internalRIndex = 0u; internalRIndex < internalRSteps; internalRIndex++)
        {
            if (internalRMapH >= internalRRayH) break;
            internalRPrevUV = internalRUV;
            internalRPrevRayH = internalRRayH;
            internalRPrevMapH = internalRMapH;
            internalRRayH -= internalRStepSize;
            internalRUV -= internalRShiftMax * internalRStepSize;
            internalRMapH = 1.0 - lerp(internalRMax, internalRMin, SCSampleRepeat(_InternalMap, internalRUV).a) * internalRInvMax;
        }
        float internalRAfter = internalRMapH - internalRRayH;
        float internalRBefore = internalRPrevMapH - internalRPrevRayH;
        float internalRW = internalRAfter / max(internalRAfter - internalRBefore, 1e-5);
        internalRUV = lerp(internalRUV, internalRPrevUV, internalRW);
        float internalRT = saturate(1.0 - lerp(internalRRayH, internalRPrevRayH, internalRW)); // 0=表面, 1=最深

        half4 internalRSample = SCSampleRepeat(_InternalMap, internalRUV);
        half3 internalRPickRGB = _InternalColorSource == 0u ? internalRSample.rgb
            : (_InternalColorSource == 1u ? internalRMcCol : internalRSample.rgb * internalRMcCol);
        half3 internalRColor = internalRPickRGB
            * lerp(_InternalColorNear.rgb, _InternalColorFar.rgb, internalRT)
            * lerp(_InternalFadeNear, _InternalFadeFar, internalRT);
        // 深度が浅い (=沈んでいない) 箇所は柄を出さない → マスク外/平坦部は自然に効果が消える
        half internalRCover = saturate(internalRT * 8.0) * saturate(_InternalStrength);
        sd.albedoAlpha.rgb = lerp(sd.albedoAlpha.rgb, internalRColor, internalRCover);
    }
    else
    {
        // LayerAlpha: 多層サンプルを前後合成してアルベドへ通常合成
        half3 internalRSum = 0;
        half internalRRemain = 1;
        [loop]
        for (uint internalRIndex = 0u; internalRIndex < internalRSteps; internalRIndex++)
        {
            half internalRT = internalRSteps > 1u ? internalRIndex / (half)(internalRSteps - 1u) : 0.0;
            float internalRDepth = lerp(internalRMin, internalRMax, internalRT);
            half4 internalRSample = SCSampleRepeat(_InternalMap, internalRBaseUV - internalRShift * internalRDepth);
            half internalROpacity = saturate(internalRSample.a * lerp(_InternalFadeNear, _InternalFadeFar, internalRT));
            half3 internalRTint = lerp(_InternalColorNear.rgb, _InternalColorFar.rgb, internalRT);
            half3 internalRPickRGB = _InternalColorSource == 0u ? internalRSample.rgb
                : (_InternalColorSource == 1u ? internalRMcCol : internalRSample.rgb * internalRMcCol);
            internalRSum += internalRPickRGB * internalRTint * internalROpacity * internalRRemain;
            internalRRemain *= 1.0 - internalROpacity;
        }
        half internalRCover = saturate(_InternalStrength);
        sd.albedoAlpha.rgb = lerp(sd.albedoAlpha.rgb, internalRSum + sd.albedoAlpha.rgb * internalRRemain, internalRCover);
    }
}
#endif
