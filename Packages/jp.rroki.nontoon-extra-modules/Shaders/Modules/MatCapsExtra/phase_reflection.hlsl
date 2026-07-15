// 追加マットキャップ (乗算 / 加算 各 1 スロット)。
// NonToon 標準の MatCaps モジュールと同じ挙動: ビュー空間 (ワールド上方向で安定化した基底) に
// 法線を投影した標準的なマットキャップ UV でサンプルする。
if (_Enable)
{
    half3 extraMcN_VD = vertex.Head;
    half3 extraMcB_VD = normalize(float3(0,1,0) - extraMcN_VD * extraMcN_VD.y * 0.9);
    half3 extraMcT_VD = cross(extraMcN_VD, extraMcB_VD);
    half3x3 extraMcTBN_VD = half3x3(extraMcT_VD, extraMcB_VD, extraMcN_VD);
    half2 extraMcUV = mul(extraMcTBN_VD, sd.N).xy * 0.5 + 0.5;
    half2 extraMcUVDetail = mul(extraMcTBN_VD, sd.N_detail).xy * 0.5 + 0.5;

    // 色相シフト (0-1 = 一周、シームレスにループ可能)。乗算/加算スロット共通。
    half extraMcHue = frac(_MatCapHueShift + _MatCapHueShiftSpeed * _Time.y);
    half3 extraMcMulColor = RrokiNTHueRotate(_MatCapMultiplyColor.rgb, extraMcHue);
    half3 extraMcAddColor = RrokiNTHueRotate(_MatCapAddColor.rgb, extraMcHue);

    sd.col.rgb *= lerp(1, SCSampleClamp(_MatCapMultiply, lerp(extraMcUV, extraMcUVDetail, _MatCapMultiplyDetail)).rgb * extraMcMulColor, sd.mask[_MatCapMultiplyMaskChannel]);
    sd.add += SCSampleClamp(_MatCapAdd, lerp(extraMcUV, extraMcUVDetail, _MatCapAddDetail)).rgb * extraMcAddColor * sd.mask[_MatCapAddMaskChannel];
}
