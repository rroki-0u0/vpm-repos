// バックフェース: 裏面 (vertex.isFront == false) のみ、アルベドの色/テクスチャを差し替える。
// 二重構造の服 (裏地) や、内側を別色・別柄にしたい場合向け。
// base フェーズでライティング前に置換するため、裏面も表面と同じ光と影を受ける。
// Tint = 元アルベド × テクスチャ × 色、Replace = テクスチャ × 色 (元アルベドを無視)。
// アウトラインパスでは無効。
#if !defined(OUTLINE)
if (_Enable && !vertex.isFront)
{
    float2 bfUV = vertex.uv[_BackfaceUV].xy * _BackfaceTexture_ST.xy + _BackfaceTexture_ST.zw;
    half4 bfTex = SCSampleRepeat(_BackfaceTexture, bfUV);
    half3 bfRGB = _BackfaceReplace == 0u
        ? sd.albedoAlpha.rgb * bfTex.rgb * _BackfaceColor.rgb
        : bfTex.rgb * _BackfaceColor.rgb;

    if (_BackfaceHue != 0 || _BackfaceHueSpeed != 0)
        bfRGB = RrokiNTHueRotate(bfRGB, frac(_BackfaceHue + _BackfaceHueSpeed * _Time.y));

    sd.albedoAlpha.rgb = bfRGB;
    if (_BackfaceReplaceAlpha) sd.albedoAlpha.a = bfTex.a * _BackfaceColor.a;
    if (_BackfaceEmission > 0) sd.postadd += bfRGB * _BackfaceEmission;
}
#endif
