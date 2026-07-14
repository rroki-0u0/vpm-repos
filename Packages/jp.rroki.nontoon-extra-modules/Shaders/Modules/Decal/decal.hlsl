// デカール: 位置/スケール/回転を指定してアルベドへテクスチャを合成する (4 スロット)。
// base フェーズの ColorAdjust / Details / HeightParallax より後に実行され、
// InternalParallax (Replace) より前に適用される。Emission > 0 でデカール部分が発光する。
#if !defined(OUTLINE)
if (_Enable)
{
    {
        half4 decal = RrokiNTDecalSample(_Decal0Texture, vertex.uv[_Decal0UV].xy, _Decal0Position, _Decal0Scale, _Decal0Rotation, _Decal0Tiled);
        decal.rgb *= _Decal0Color.rgb;
        half decalA = decal.a * _Decal0Color.a * _Decal0Alpha;
        half3 decalBlended = _Decal0Blend == 1u ? sd.albedoAlpha.rgb * decal.rgb : (_Decal0Blend == 2u ? sd.albedoAlpha.rgb + decal.rgb : decal.rgb);
        sd.albedoAlpha.rgb = lerp(sd.albedoAlpha.rgb, decalBlended, decalA);
        sd.postadd += decal.rgb * decalA * _Decal0Emission;
    }
    {
        half4 decal = RrokiNTDecalSample(_Decal1Texture, vertex.uv[_Decal1UV].xy, _Decal1Position, _Decal1Scale, _Decal1Rotation, _Decal1Tiled);
        decal.rgb *= _Decal1Color.rgb;
        half decalA = decal.a * _Decal1Color.a * _Decal1Alpha;
        half3 decalBlended = _Decal1Blend == 1u ? sd.albedoAlpha.rgb * decal.rgb : (_Decal1Blend == 2u ? sd.albedoAlpha.rgb + decal.rgb : decal.rgb);
        sd.albedoAlpha.rgb = lerp(sd.albedoAlpha.rgb, decalBlended, decalA);
        sd.postadd += decal.rgb * decalA * _Decal1Emission;
    }
    {
        half4 decal = RrokiNTDecalSample(_Decal2Texture, vertex.uv[_Decal2UV].xy, _Decal2Position, _Decal2Scale, _Decal2Rotation, _Decal2Tiled);
        decal.rgb *= _Decal2Color.rgb;
        half decalA = decal.a * _Decal2Color.a * _Decal2Alpha;
        half3 decalBlended = _Decal2Blend == 1u ? sd.albedoAlpha.rgb * decal.rgb : (_Decal2Blend == 2u ? sd.albedoAlpha.rgb + decal.rgb : decal.rgb);
        sd.albedoAlpha.rgb = lerp(sd.albedoAlpha.rgb, decalBlended, decalA);
        sd.postadd += decal.rgb * decalA * _Decal2Emission;
    }
    {
        half4 decal = RrokiNTDecalSample(_Decal3Texture, vertex.uv[_Decal3UV].xy, _Decal3Position, _Decal3Scale, _Decal3Rotation, _Decal3Tiled);
        decal.rgb *= _Decal3Color.rgb;
        half decalA = decal.a * _Decal3Color.a * _Decal3Alpha;
        half3 decalBlended = _Decal3Blend == 1u ? sd.albedoAlpha.rgb * decal.rgb : (_Decal3Blend == 2u ? sd.albedoAlpha.rgb + decal.rgb : decal.rgb);
        sd.albedoAlpha.rgb = lerp(sd.albedoAlpha.rgb, decalBlended, decalA);
        sd.postadd += decal.rgb * decalA * _Decal3Emission;
    }
}
#endif
