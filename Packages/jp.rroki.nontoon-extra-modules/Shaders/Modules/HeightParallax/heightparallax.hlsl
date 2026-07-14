// ハイトマップ視差 (簡易 POM): 接空間の視線方向に沿って UV を後退させ、
// ベーステクスチャ / 共有マスク / ノーマルマップを視差後の UV で再サンプルする。
// base フェーズの先頭 (ColorAdjust / Details より前) で実行される。
//
// 制約: コアのサンプリングは本フェーズより前に済んでいるため「上書き」方式となり、
// Details など vertex.uv を直接参照する後続モジュールには視差がかからない。
// アウトラインパスではコスト削減のため無効。
#if !defined(OUTLINE)
if (_Enable)
{
    // 接空間視線 (TBN は行ベクトルが T/B/N の行列)
    half3 parallaxViewTS = mul(vertex.TBN, vertex.V);
    float2 parallaxDir = parallaxViewTS.xy / max(abs(parallaxViewTS.z), 0.25) * _ParallaxStrength;

    float parallaxStepSize = 1.0 / _ParallaxSteps;
    float2 parallaxUV = sd.uv;
    float parallaxRayHeight = 1.0;
    float parallaxMapHeight = saturate(SCSampleRepeat(_ParallaxHeightMap, parallaxUV).r + _ParallaxHeightOffset);

    [loop]
    for (uint parallaxIndex = 0u; parallaxIndex < _ParallaxSteps; parallaxIndex++)
    {
        if (parallaxMapHeight >= parallaxRayHeight) break;
        parallaxRayHeight -= parallaxStepSize;
        parallaxUV -= parallaxDir * parallaxStepSize;
        parallaxMapHeight = saturate(SCSampleRepeat(_ParallaxHeightMap, parallaxUV).r + _ParallaxHeightOffset);
    }

    // 視差後の UV でコアのサンプリングをやり直す
    sd.uv = parallaxUV;
    sd.albedoAlpha = SCSample(_BaseTexture, sampler_BaseTexture, parallaxUV);
    sd.mask = SCSample(_SharedMask, sampler_BaseTexture, parallaxUV);
    sd.roughness = _Roughness;
    sd.N = SCUnpackNormalAndRoughness(SCSample(_NormalMap, sampler_BaseTexture, parallaxUV), _NormalScale, sd.roughness, sd.normalMapWithRoughness);
    sd.N_detail = sd.N;
}
#endif
