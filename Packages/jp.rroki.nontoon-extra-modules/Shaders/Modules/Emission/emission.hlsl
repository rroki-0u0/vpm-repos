// エミッション: ライティング合成後の加算発光。
// アウトラインパスでは発光させない (アウトライン色との乗算で濁るため)。
#if !defined(OUTLINE)
if (_Enable)
{
    float2 uvEmission = vertex.uv[_EmissionMapUV].xy * _EmissionMap_ST.xy + _EmissionMap_ST.zw + _Time.y * _EmissionScroll.xy;
    half3 emission = SCSampleRepeat(_EmissionMap, uvEmission).rgb * _EmissionColor.rgb * _EmissionStrength;
    emission = lerp(emission, emission * sd.albedoAlpha.rgb, _EmissionMultiplyAlbedo);

    // 色相シフト: 静的シフト + 回転/秒のアニメーション (0-1 = 一周)
    if (_EmissionHueShift != 0 || _EmissionHueShiftSpeed != 0)
        emission = RrokiNTHueRotate(emission, frac(_EmissionHueShift + _EmissionHueShiftSpeed * _Time.y));

    // 点滅: Min と Max の間を正弦波で往復。Speed = 0 のときは Max 固定
    half blinkWave = sin(_Time.y * _EmissionBlink.z + _EmissionBlink.w) * 0.5 + 0.5;
    emission *= lerp(_EmissionBlink.x, _EmissionBlink.y, _EmissionBlink.z == 0 ? 1 : blinkWave);

    sd.col.rgb += emission * sd.mask[_EmissionMaskChannel];
}
#endif
