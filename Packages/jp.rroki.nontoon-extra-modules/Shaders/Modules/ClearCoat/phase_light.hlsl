// クリアコート: コート層の直接光ハイライト (ライトごとに実行)。
// F0=0.04 の等方性 GGX + Kelemen 可視項 (クリアコート向けの定番構成) のクリーンルーム実装。
#if !defined(OUTLINE)
if (_Enable)
{
    half coatRough = max(1.0 - _CoatSmoothness, 0.04);
    half coatA2 = coatRough * coatRough * coatRough * coatRough;
    half3 coatH = normalize(light.direction + vertex.V);
    half coatNdotL = saturate(dot(sd.N_detail, light.direction));
    half coatNdotH = saturate(dot(sd.N_detail, coatH));
    half coatLdotH = saturate(dot(light.direction, coatH));

    half coatDenom = coatNdotH * coatNdotH * (coatA2 - 1.0) + 1.0;
    half coatD = coatA2 / max(3.1415926 * coatDenom * coatDenom, 1e-4);
    half coatF = 0.04 + 0.96 * pow(1.0 - coatLdotH, 5.0);
    half coatVis = 0.25 / max(coatLdotH * coatLdotH, 1e-3);

    half coatSpec = min(coatD * coatVis * coatF * coatNdotL, 16.0);
    sd.postadd += coatSpec * _CoatSpecular * light.color * sd.mask[_CoatMaskChannel];
}
#endif
