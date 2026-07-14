// 色調整: メインテクスチャ (アルベド) への色相/彩度/明度/ガンマ補正。
// base フェーズの Details より前に実行され、メインの色のみを調整する。
if (_Enable)
{
    half3 adjusted = sd.albedoAlpha.rgb;
    adjusted = RrokiNTHueRotate(adjusted, _AdjustHue);
    half adjustLuminance = dot(adjusted, half3(0.299, 0.587, 0.114));
    adjusted = lerp(adjustLuminance.xxx, adjusted, _AdjustSaturation + 1);
    adjusted = pow(max(adjusted, 0), _AdjustGamma);
    adjusted *= max(_AdjustBrightness + 1, 0);
    sd.albedoAlpha.rgb = lerp(sd.albedoAlpha.rgb, adjusted, sd.mask[_AdjustMaskChannel]);
}
