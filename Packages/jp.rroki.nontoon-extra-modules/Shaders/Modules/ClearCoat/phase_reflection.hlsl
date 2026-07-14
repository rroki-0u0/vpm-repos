// クリアコート: コート層の環境映り込み。
// EnvReflection モジュールと違い、コート専用の滑らかさ (_CoatSmoothness) でサンプルするため
// 完全鏡面に近いシャープな映り込み (テラテラ感) を出せる。
#if !defined(OUTLINE)
if (_Enable)
{
    half coatReflRough = saturate(1.0 - _CoatSmoothness);
    half3 coatReflDir = reflect(-vertex.V, sd.N_detail);

    half3 coatProbe;
    #if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
        coatProbe = GlossyEnvironmentReflection(coatReflDir, vertex.position, coatReflRough, 1.0);
    #else
        half coatMip = coatReflRough * (1.7 - 0.7 * coatReflRough) * 6.0;
        half4 coatEncoded = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, coatReflDir, coatMip);
        coatProbe = DecodeHDR(coatEncoded, unity_SpecCube0_HDR);
    #endif

    half coatNdotV = saturate(dot(sd.N_detail, vertex.V));
    half coatFresnel = 0.04 + 0.96 * pow(1.0 - coatNdotV, 5.0);
    sd.postadd += coatProbe * _CoatReflection * coatFresnel * sd.mask[_CoatMaskChannel];
}
#endif
