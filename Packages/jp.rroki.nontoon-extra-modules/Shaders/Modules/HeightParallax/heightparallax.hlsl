// ハイトマップ視差 (Parallax Occlusion Mapping): 接空間の視線方向に沿って高さ場へ
// レイを進め、最初に交差した位置の UV でベース/共有マスク/ノーマルを再サンプルする。
// base フェーズの先頭 (ColorAdjust / Details より前) で実行される。
//
// 精度対策:
//   - 視線が寝ているほどステップ数を増やす動的サンプリング (Min→Max)
//   - 交差区間を直前サンプルとの線形補間で refine し、階段状アーティファクトを解消
//
// 制約: コアのサンプリングは本フェーズより前に済んでいるため「上書き」方式となり、
// Details など vertex.uv を直接参照する後続モジュールには視差がかからない。
// アウトラインパスではコスト削減のため無効。
#if !defined(OUTLINE)
if (_Enable)
{
    // 接空間視線 (TBN は行ベクトルが T/B/N の行列)
    half3 parallaxViewTS = mul(vertex.TBN, vertex.V);
    // 視線が寝ているほど後退量が増える (offset limiting)
    float2 parallaxMaxOffset = parallaxViewTS.xy / max(abs(parallaxViewTS.z), 0.25) * _ParallaxStrength;

    // 正面 (z≈1) では Min、斜め (z≈0) では Max ステップ
    float parallaxViewFactor = saturate(abs(parallaxViewTS.z));
    uint parallaxSteps = (uint)round(lerp((float)_ParallaxStepsMax, (float)_ParallaxStepsMin, parallaxViewFactor));
    parallaxSteps = max(parallaxSteps, 1u);
    float parallaxStepSize = 1.0 / parallaxSteps;
    float2 parallaxDeltaUV = parallaxMaxOffset * parallaxStepSize;

    float2 parallaxUV = sd.uv;
    float parallaxRayHeight = 1.0;
    float parallaxMapHeight = saturate(SCSampleRepeat(_ParallaxHeightMap, parallaxUV).r + _ParallaxHeightOffset);

    // 直前サンプル (交点の線形補間に使う)
    float2 parallaxPrevUV = parallaxUV;
    float parallaxPrevRayHeight = parallaxRayHeight;
    float parallaxPrevMapHeight = parallaxMapHeight;

    [loop]
    for (uint parallaxIndex = 0u; parallaxIndex < parallaxSteps; parallaxIndex++)
    {
        if (parallaxMapHeight >= parallaxRayHeight) break;
        parallaxPrevUV = parallaxUV;
        parallaxPrevRayHeight = parallaxRayHeight;
        parallaxPrevMapHeight = parallaxMapHeight;

        parallaxRayHeight -= parallaxStepSize;
        parallaxUV -= parallaxDeltaUV;
        parallaxMapHeight = saturate(SCSampleRepeat(_ParallaxHeightMap, parallaxUV).r + _ParallaxHeightOffset);
    }

    // 交点を直前サンプルとの線形補間で求める (relief mapping の補間法)
    float parallaxAfter = parallaxMapHeight - parallaxRayHeight;               // >= 0 (交差後)
    float parallaxBefore = parallaxPrevMapHeight - parallaxPrevRayHeight;      // <= 0 (交差前)
    float parallaxWeight = parallaxAfter / max(parallaxAfter - parallaxBefore, 1e-5);
    parallaxUV = lerp(parallaxUV, parallaxPrevUV, parallaxWeight);

    // 視差後の UV でコアのサンプリングをやり直す
    sd.uv = parallaxUV;
    sd.albedoAlpha = SCSample(_BaseTexture, sampler_BaseTexture, parallaxUV);
    sd.mask = SCSample(_SharedMask, sampler_BaseTexture, parallaxUV);
    sd.roughness = _Roughness;
    sd.N = SCUnpackNormalAndRoughness(SCSample(_NormalMap, sampler_BaseTexture, parallaxUV), _NormalScale, sd.roughness, sd.normalMapWithRoughness);
    sd.N_detail = sd.N;
}
#endif
