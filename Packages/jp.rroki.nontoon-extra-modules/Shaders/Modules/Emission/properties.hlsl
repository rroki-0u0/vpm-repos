SC_uint(_Enable, 0, [SCInHeader][SCToggle][SCConstValue(1,pixel)], "", "")

SC_color(_EmissionColor, (1,1,1,1), [HDR], "__Color", "")
SC_float(_EmissionStrength, 1, [SCRange(0,20)], "Strength", "")
SC_Texture2D(_EmissionMap, "white", [], "Emission Map", "")
SC_ScaleOffset(_EmissionMap)
SC_uint(_EmissionMapUV, 0, [SCEnum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)], "__UV", "")
SC_float4(_EmissionScroll, (0,0,0,0), [], "Scroll Speed (XY)", "")
SC_float(_EmissionMultiplyAlbedo, 0, [SCRange(0,1)], "__MultiplyAlbedo", "")
SC_uint(_EmissionMaskChannel, 3, [SCMaskChannel], "__MaskChannel", "")

SC_Box
SC_float4(_EmissionBlink, (1,1,0,0), [], "Blink (Min, Max, Speed, Offset)", "")
SC_BoxEnd

SC_Box
SC_float(_EmissionHueShift, 0, [SCRange(0,1)], "Hue Shift", "")
SC_float(_EmissionHueShiftSpeed, 0, [], "Hue Shift Speed", "")
SC_BoxEnd
