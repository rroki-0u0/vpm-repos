// オクルージョン (AO): くぼみを常に暗くする静的な遮蔽。指向性のトゥーン影 (Shade/SDF) とは
// 独立した「光非依存の暗さ」として適用する。一般的な AO 乗算のクリーンルーム実装。
//
// Shade (トゥーンランプ) より前に実行する (befores)。首元など別メッシュの境目では、
// 顔・体の両マテリアルに同じ AO を載せることで、指向性影の食い違いとは別に「くぼみは常に暗い」
// を保証でき、境目の明るさ段差を無くせる。
//
// モード:
//   Ramp     … sd.shadow を下げてトゥーンランプの影側へ流し込む (Shade 使用マテリアル向け、既定)
//   Multiply … アルベドへ直接乗算 (Shade を使わないマテリアル向け)
//   Light    … 受光量 (sd.lightColor) を減衰 (直接光+環境を一律に暗く)
#if !defined(OUTLINE)
if (_Enable)
{
    half occ;
    if (_OcclusionSource == 0)
    {
        float2 occlusionUV = vertex.uv[_OcclusionUV].xy * _OcclusionMap_ST.xy + _OcclusionMap_ST.zw;
        occ = SCSample(_OcclusionMap, sampler_BaseTexture, occlusionUV)[_OcclusionMapChannel];
    }
    else
    {
        occ = sd.mask[_OcclusionMaskChannel];
    }
    if (_OcclusionInvert) occ = 1.0 - occ;

    // 明るさ係数 (1=遮蔽なし, 0=全遮蔽)
    half occlusionBlend = lerp(1.0, occ, _OcclusionStrength);
    // 常時最小の暗さ (floor): 遮蔽領域は最低でも (1 - floor) まで暗くする (光非依存の下限)
    occlusionBlend = min(occlusionBlend, 1.0 - _OcclusionFloor * (1.0 - occ));

    if (_OcclusionMode == 0)      sd.shadow *= occlusionBlend;      // トゥーンランプ連動
    else if (_OcclusionMode == 1) sd.col.rgb *= occlusionBlend;     // アルベドへ直接乗算
    else                          sd.lightColor *= occlusionBlend;  // 受光量の減衰
}
#endif
