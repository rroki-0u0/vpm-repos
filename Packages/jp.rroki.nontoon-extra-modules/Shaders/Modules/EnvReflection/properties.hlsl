SC_uint(_Enable, 0, [SCInHeader][SCToggle][SCConstValue(1,pixel)], "", "")

SC_color(_ReflectionTint, (1,1,1,1), [HDR], "__Color", "")
SC_float(_ReflectionStrength, 1, [SCRange(0,1)], "Strength", "")
SC_float(_ReflectionFresnel, 1, [SCRange(0,1)], "Fresnel", "")
SC_float(_ReflectionMultiplyAlbedo, 0, [SCRange(0,1)], "__MultiplyAlbedo", "")
SC_uint(_ReflectionMaskChannel, 3, [SCMaskChannel], "__MaskChannel", "")
