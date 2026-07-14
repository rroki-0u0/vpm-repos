SC_uint(_Enable, 0, [SCInHeader][SCToggle][SCConstValue(1,pixel)], "", "")

SC_float(_CoatSmoothness, 1, [SCRange(0,1)], "Smoothness", "")
SC_float(_CoatSpecular, 1, [SCRange(0,2)], "Specular Highlight", "")
SC_float(_CoatReflection, 1, [SCRange(0,1)], "Reflection", "")
SC_uint(_CoatMaskChannel, 3, [SCMaskChannel], "__MaskChannel", "")
