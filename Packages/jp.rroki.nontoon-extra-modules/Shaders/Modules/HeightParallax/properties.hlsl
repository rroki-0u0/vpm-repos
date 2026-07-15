SC_uint(_Enable, 0, [SCInHeader][SCToggle][SCConstValue(1,pixel)], "", "")

SC_Texture2D(_ParallaxHeightMap, "white", [], "Height Map", "")
SC_float(_ParallaxStrength, 0.01, [SCRange(-0.1,0.1)], "Strength", "")
SC_float(_ParallaxHeightOffset, 0, [SCRange(-1,1)], "Height Offset", "")
SC_uint(_ParallaxStepsMin, 8, [SCRangeInt(1,64)], "Steps (Min)", "")
SC_uint(_ParallaxStepsMax, 32, [SCRangeInt(1,64)], "Steps (Max)", "")
