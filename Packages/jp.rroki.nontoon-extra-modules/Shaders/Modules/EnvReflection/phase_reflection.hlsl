// 環境リフレクション: リフレクションプローブをラフネス連動ミップでサンプルし、
// フレネル付きでライティング合成後 (postadd) に加算する。
// ラフネスは本体の _Roughness (異方性は平均) を共有する。
// アウトラインパスでは無効 (裏面シェルの反射は不自然なため)。
#if !defined(OUTLINE)
if (_Enable)
{
    half reflRoughness = saturate((sd.roughness.x + sd.roughness.y) * 0.5);
    half3 reflDir = reflect(-vertex.V, sd.N_detail);

    half3 reflProbe;
    #if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
        reflProbe = GlossyEnvironmentReflection(reflDir, vertex.position, reflRoughness, 1.0);
    #else
        // Unity 標準のミップ近似 (perceptual roughness → mip)。LOD 段数は既定の 6
        half reflMip = reflRoughness * (1.7 - 0.7 * reflRoughness) * 6.0;
        half4 reflEncoded = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflDir, reflMip);
        reflProbe = DecodeHDR(reflEncoded, unity_SpecCube0_HDR);
    #endif

    // Schlick フレネル (F0 = 0.04)。_ReflectionFresnel = 0 で全面均一 (金属向け)
    half reflNdotV = saturate(dot(sd.N_detail, vertex.V));
    half reflSchlick = 0.04 + 0.96 * pow(1.0 - reflNdotV, 5.0);
    half reflAmount = _ReflectionStrength * lerp(1.0, reflSchlick, _ReflectionFresnel);

    half3 reflection = reflProbe * _ReflectionTint.rgb * reflAmount;
    reflection = lerp(reflection, reflection * sd.albedoAlpha.rgb, _ReflectionMultiplyAlbedo);
    sd.postadd += reflection * sd.mask[_ReflectionMaskChannel];
}
#endif
