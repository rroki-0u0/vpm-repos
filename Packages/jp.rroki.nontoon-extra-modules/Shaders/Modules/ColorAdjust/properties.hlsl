SC_uint(_Enable, 0, [SCInHeader][SCToggle][SCConstValue(1,pixel)], "", "")

SC_float(_AdjustHue, 0, [SCRange(0,1)], "Hue", "")
SC_float(_AdjustHueSpeed, 0, [], "Hue Speed", "")
SC_float(_AdjustSaturation, 0, [SCRange(-1,2)], "Saturation", "")
SC_float(_AdjustBrightness, 0, [SCRange(-1,2)], "Brightness", "")
SC_float(_AdjustGamma, 1, [SCRange(0.01,5)], "Gamma", "")
SC_uint(_AdjustMaskChannel, 3, [SCMaskChannel], "__MaskChannel", "")
