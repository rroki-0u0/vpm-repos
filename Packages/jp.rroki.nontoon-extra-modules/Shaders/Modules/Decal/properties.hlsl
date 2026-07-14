SC_uint(_Enable, 0, [SCInHeader][SCToggle][SCConstValue(1,pixel)], "", "")

SC_Box
SC_Texture2D(_Decal0Texture, "black", [], "Decal 1", "")
SC_uint(_Decal0UV, 0, [SCEnum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)], "__UV", "")
SC_float4(_Decal0Position, (0.5,0.5,0,0), [], "Position (XY)", "")
SC_float4(_Decal0Scale, (1,1,0,0), [], "Scale (XY)", "")
SC_float(_Decal0Rotation, 0, [SCRange(0,360)], "Rotation", "")
SC_color(_Decal0Color, (1,1,1,1), [], "__Color", "")
SC_float(_Decal0Alpha, 1, [SCRange(0,1)], "Alpha", "")
SC_uint(_Decal0Blend, 0, [SCEnum(Replace, 0, Multiply, 1, Add, 2)], "Blend", "")
SC_uint(_Decal0Tiled, 0, [SCToggle], "Tiled", "")
SC_float(_Decal0Emission, 0, [SCRange(0,20)], "Emission", "")
SC_BoxEnd

SC_Box
SC_Texture2D(_Decal1Texture, "black", [], "Decal 2", "")
SC_uint(_Decal1UV, 0, [SCEnum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)], "__UV", "")
SC_float4(_Decal1Position, (0.5,0.5,0,0), [], "Position (XY)", "")
SC_float4(_Decal1Scale, (1,1,0,0), [], "Scale (XY)", "")
SC_float(_Decal1Rotation, 0, [SCRange(0,360)], "Rotation", "")
SC_color(_Decal1Color, (1,1,1,1), [], "__Color", "")
SC_float(_Decal1Alpha, 1, [SCRange(0,1)], "Alpha", "")
SC_uint(_Decal1Blend, 0, [SCEnum(Replace, 0, Multiply, 1, Add, 2)], "Blend", "")
SC_uint(_Decal1Tiled, 0, [SCToggle], "Tiled", "")
SC_float(_Decal1Emission, 0, [SCRange(0,20)], "Emission", "")
SC_BoxEnd

SC_Box
SC_Texture2D(_Decal2Texture, "black", [], "Decal 3", "")
SC_uint(_Decal2UV, 0, [SCEnum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)], "__UV", "")
SC_float4(_Decal2Position, (0.5,0.5,0,0), [], "Position (XY)", "")
SC_float4(_Decal2Scale, (1,1,0,0), [], "Scale (XY)", "")
SC_float(_Decal2Rotation, 0, [SCRange(0,360)], "Rotation", "")
SC_color(_Decal2Color, (1,1,1,1), [], "__Color", "")
SC_float(_Decal2Alpha, 1, [SCRange(0,1)], "Alpha", "")
SC_uint(_Decal2Blend, 0, [SCEnum(Replace, 0, Multiply, 1, Add, 2)], "Blend", "")
SC_uint(_Decal2Tiled, 0, [SCToggle], "Tiled", "")
SC_float(_Decal2Emission, 0, [SCRange(0,20)], "Emission", "")
SC_BoxEnd

SC_Box
SC_Texture2D(_Decal3Texture, "black", [], "Decal 4", "")
SC_uint(_Decal3UV, 0, [SCEnum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)], "__UV", "")
SC_float4(_Decal3Position, (0.5,0.5,0,0), [], "Position (XY)", "")
SC_float4(_Decal3Scale, (1,1,0,0), [], "Scale (XY)", "")
SC_float(_Decal3Rotation, 0, [SCRange(0,360)], "Rotation", "")
SC_color(_Decal3Color, (1,1,1,1), [], "__Color", "")
SC_float(_Decal3Alpha, 1, [SCRange(0,1)], "Alpha", "")
SC_uint(_Decal3Blend, 0, [SCEnum(Replace, 0, Multiply, 1, Add, 2)], "Blend", "")
SC_uint(_Decal3Tiled, 0, [SCToggle], "Tiled", "")
SC_float(_Decal3Emission, 0, [SCRange(0,20)], "Emission", "")
SC_BoxEnd
