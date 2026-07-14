#nullable enable
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// リムライト (Poiyomi: Rim Lighting / Environmental Rim → NonToon: RimLight モジュール)。
    /// NonToon のリムは加算後にライト色 (環境光込み) が乗るため、Poiyomi の環境リム
    /// (環境色のリム) とも意味的に近く、通常リムが未使用ならそちらの受け皿になる。
    /// </summary>
    public sealed class RimLightConversionModule : ConversionModule
    {
        public override int Order => 20;
        public override string DisplayName => "リムライト";

        public override bool ShouldRun(ConversionContext c) =>
            c.Source.GetToggle("_EnableRimLighting") || c.Source.GetToggle("_EnableEnvironmentalRim");

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;

            if (!s.GetToggle("_EnableRimLighting"))
            {
                ConvertEnvironmentalRim(c);
                return;
            }

            if (s.GetToggle("_EnableEnvironmentalRim"))
            {
                c.MarkSourceHandled("_EnableEnvironmentalRim");
                c.Report.Warn("環境リム", "通常リムライトと NonToon の RimLight スロットが競合するため、環境リムは破棄しました");
            }

            int style = s.GetInt("_RimStyle", 0);

            Color color;
            float min, max;
            switch (style)
            {
                case 2: // lilToon スタイル: border が fresnel 値上の中心、blur が幅
                {
                    color = s.GetColor("_RimColor", new Color(0.66f, 0.5f, 0.48f, 1f));
                    float border = s.GetFloat("_RimBorder", 0.5f);
                    float blur = s.GetFloat("_RimBlur", 0.65f);
                    min = Mathf.Clamp01(border - blur * 0.5f);
                    max = Mathf.Clamp01(border + blur * 0.5f);
                    if (Mathf.Abs(s.GetFloat("_RimFresnelPower", 3.5f) - 1f) > 0.01f)
                        c.Report.Approx("リムライト", "Fresnel Power は NonToon 非対応のため幅のみで近似しました");
                    break;
                }
                case 1: // UTS2 スタイル
                    color = s.GetColor("_RimLightColor", Color.white);
                    min = 0.6f; max = 0.9f;
                    c.Report.Approx("リムライト", "UTS2 スタイルのリムは NonToon 標準の範囲で近似しました");
                    break;
                default: // Poiyomi スタイル: width = リムの太さ, blur = 縁のぼかし
                {
                    color = s.GetColor("_RimLightColor", Color.white);
                    float width = Mathf.Clamp01(s.GetFloat("_RimWidth", 0.8f));
                    float blur = Mathf.Clamp01(s.GetFloat("_RimBlur", 0.65f));
                    float brightness = s.GetFloat("_RimBrightness", 1f);
                    min = Mathf.Clamp01(1f - width);
                    max = Mathf.Clamp01(min + blur * (1f - min));
                    color *= brightness;

                    float power = s.GetFloat("_RimPower", 1f);
                    if (Mathf.Abs(power - 1f) > 0.01f)
                        c.Report.Approx("リムライト", $"Rim Power ({power:F2}) は範囲の調整で近似しました");
                    if (s.GetFloat("_RimStrength", 0f) > 0f)
                        c.Report.Warn("リムライト", "エミッシブリム (_RimStrength) は非対応です。NonToon のリムはライティングの影響を受けます");
                    int blend = s.GetInt("_RimPoiBlendMode", 0);
                    if (blend != 0)
                        c.Report.Approx("リムライト", $"ブレンドモード ({blend}) は加算として近似しました (NonToon は加算のみ)");
                    break;
                }
            }

            if (max <= min) max = Mathf.Clamp01(min + 0.05f);

            color.a = 1f;
            c.SetColor(NonToonProps.RimLightColor, color);
            c.SetVector(NonToonProps.RimLightRange, new Vector4(min, max, 0f, 0f));
            c.SetFloat(NonToonProps.RimLightMultiplyAlbedo, Mathf.Clamp01(s.GetFloat("_RimBaseColorMix", 0f)));
            c.RequireScModule(NonToonProps.ModRimLight);

            var mask = s.GetTexture("_RimMask");
            if (mask != null)
            {
                int ch = c.AllocateMaskChannel(new MaskChannelSource
                {
                    Texture = mask,
                    SourceChannel = Mathf.Clamp(s.GetInt("_RimMaskChannel", 0), 0, 3),
                    Invert = s.GetToggle("_RimMaskInvert"),
                    Label = "リムライト",
                });
                if (ch >= 0) c.SetInt(NonToonProps.RimLightMaskChannel, ch);
            }

            c.Report.Approx("リムライト", $"範囲 ({min:F2}〜{max:F2}) へ変換しました。NonToon のリムは影で自動的に隠れます");

            if (s.GetToggle("_EnableRim2Lighting"))
                c.Report.Drop("リムライト2", "2 本目のリムライトは NonToon 非対応のため破棄しました");
        }

        /// <summary>
        /// 環境リム → RimLight モジュール。NonToon のリムは sd.add 経由でライト色
        /// (環境光込み) が乗るため、「環境色のリム」という Poiyomi の環境リムの意図と一致する。
        /// </summary>
        void ConvertEnvironmentalRim(ConversionContext c)
        {
            var s = c.Source;
            c.MarkSourceHandled("_EnableEnvironmentalRim");

            float intensity = Mathf.Clamp01(s.GetFloat("_RimEnviroIntensity", 1f));
            if (intensity <= 0.001f)
            {
                c.Report.Info("環境リム", "強度 0 のためスキップしました");
                return;
            }

            float width = Mathf.Clamp01(s.GetFloat("_RimEnviroWidth", 0.45f));
            float blur = Mathf.Clamp01(s.GetFloat("_RimEnviroBlur", 0.7f));
            float min = Mathf.Clamp01(1f - width);
            float max = Mathf.Clamp01(min + blur * (1f - min));
            if (max <= min) max = Mathf.Clamp01(min + 0.05f);

            c.SetColor(NonToonProps.RimLightColor, new Color(intensity, intensity, intensity, 1f));
            c.SetVector(NonToonProps.RimLightRange, new Vector4(min, max, 0f, 0f));
            c.SetFloat(NonToonProps.RimLightMultiplyAlbedo, 0f);
            c.RequireScModule(NonToonProps.ModRimLight);

            var mask = s.GetTexture("_RimEnviroMask");
            if (mask != null)
            {
                int ch = c.AllocateMaskChannel(new MaskChannelSource
                {
                    Texture = mask,
                    SourceChannel = 0,
                    Label = "環境リム",
                });
                if (ch >= 0) c.SetInt(NonToonProps.RimLightMaskChannel, ch);
            }

            c.Report.Approx("環境リム",
                $"RimLight モジュールへ変換しました (強度 {intensity:F2} → 白リム×ライト色)。NonToon のリムは環境光の色が乗るため、環境リムの意図に近い挙動になります");
        }
    }

    /// <summary>スペキュラ/反射 (Poiyomi: Mochie PBR / Stylized → NonToon: Specular モジュール)。</summary>
    public sealed class SpecularConversionModule : ConversionModule
    {
        public override int Order => 30;
        public override string DisplayName => "スペキュラ";

        public override bool ShouldRun(ConversionContext c) =>
            c.Source.GetToggle("_MochieBRDF") || c.Source.GetToggle("_StylizedSpecular");

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;

            float roughness;
            Color color;
            float multiplyAlbedo;

            if (s.GetToggle("_MochieBRDF"))
            {
                float smoothness = Mathf.Clamp01(s.GetFloat("_MochieRoughnessMultiplier", 1f));
                roughness = Mathf.Clamp(1f - smoothness, 0.002f, 1f);
                float strength = s.GetFloat("_MochieSpecularStrength", 1f);
                color = s.GetColor("_MochieSpecularTint", Color.white) * Mathf.Clamp01(strength);
                multiplyAlbedo = Mathf.Clamp01(s.GetFloat("_MochieMetallicMultiplier", 0f));

                if (strength > 1f)
                    c.Report.Approx("スペキュラ", $"スペキュラ強度 {strength:F2} は 1 にクランプしました");
                if (s.GetTexture("_MochieMetallicMaps") != null)
                    c.Report.Drop("スペキュラ", "メタリック/スムースネスマップは NonToon 非対応です (数値のみ変換)");
                if (s.GetFloat("_MochieReflectionStrength", 1f) > 0f && !c.IsSourceHandled("_MochieReflectionStrength"))
                    c.Report.Drop("リフレクション", "環境リフレクションは jp.rroki.nontoon-extra-modules を導入すると引き継げます");
                if (multiplyAlbedo > 0f)
                    c.Report.Approx("スペキュラ", $"メタリック ({multiplyAlbedo:F2}) はスペキュラ色のアルベド乗算として近似しました");
                c.Report.Approx("スペキュラ", "PBR (Mochie) スペキュラを NonToon の GGX スペキュラへ変換しました");
            }
            else if (s.GetInt("_StylizedReflectionMode", 0) == 1) // lilToon モード
            {
                roughness = Mathf.Clamp(1f - Mathf.Clamp01(s.GetFloat("_Smoothness", 1f)), 0.002f, 1f);
                color = Color.white;
                multiplyAlbedo = Mathf.Clamp01(s.GetFloat("_Metallic", 0f));
                if (s.GetInt("_SpecularToon", 0) == 1)
                    c.Report.Approx("スペキュラ", "トゥーンスペキュラ (境界くっきり) は GGX スペキュラとして近似しました");
                if (s.GetToggle("_ApplyReflection") && !c.IsSourceHandled("_ApplyReflection"))
                    c.Report.Drop("リフレクション", "環境リフレクションは jp.rroki.nontoon-extra-modules を導入すると引き継げます");
                c.Report.Approx("スペキュラ", "lilToon スタイルのスペキュラを変換しました");
            }
            else // UnityChan スタイル
            {
                var high = s.GetColor("_HighColor", Color.white);
                color = high * Mathf.Clamp01(s.GetFloat("_StylizedSpecularStrength", 1f));
                roughness = Mathf.Clamp(s.GetFloat("_HighColor_Power", 0.2f), 0.02f, 1f);
                multiplyAlbedo = 0f;
                c.Report.Approx("スペキュラ", "スタイライズドスペキュラ (UnityChan) を GGX スペキュラとして近似しました");
            }

            color.a = 1f;
            if (color.maxColorComponent <= 0.001f)
            {
                c.Report.Info("スペキュラ", "スペキュラ色が黒のためスキップしました");
                return;
            }

            c.SetFloat(NonToonProps.Roughness, roughness);
            c.SetColor(NonToonProps.SpecularColor, color);
            c.SetFloat(NonToonProps.SpecularMultiplyAlbedo, multiplyAlbedo);
            c.RequireScModule(NonToonProps.ModSpecular);
        }
    }

    /// <summary>ヘアハイライト (Poiyomi: Anisotropics → NonToon: HairSpecular モジュール)。</summary>
    public sealed class HairSpecularConversionModule : ConversionModule
    {
        public override int Order => 40;
        public override string DisplayName => "ヘアスペキュラ";

        public override bool ShouldRun(ConversionContext c) => c.Source.GetToggle("_EnableAniso");

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;

            // NonToon のヘアハイライトは「異方性項 (中心 0.5) を x 軸としたグラデーション」を加算する。
            // Poiyomi の 2 層異方性ハイライトを帯グラデーションとして合成する。
            var tint0 = s.GetColor("_Aniso0Tint", Color.white) * Mathf.Clamp01(s.GetFloat("_Aniso0Strength", 1f));
            float power0 = Mathf.Clamp01(s.GetFloat("_Aniso0Power", 0f));
            float center0 = Mathf.Clamp(0.5f + s.GetFloat("_Aniso0Offset", 0f) * 0.02f, 0.1f, 0.9f);
            float width0 = Mathf.Lerp(0.30f, 0.04f, power0);

            var ramp = GradientSynth.Band(Color.black, tint0, center0, width0);

            float strength1 = Mathf.Clamp01(s.GetFloat("_Aniso1Strength", 1f));
            var tint1 = s.GetColor("_Aniso1Tint", Color.white) * strength1;
            float power1 = Mathf.Clamp01(s.GetFloat("_Aniso1Power", 0.1f));
            if (strength1 > 0.001f && tint1.maxColorComponent > 0.001f && power1 > 0.001f)
            {
                float center1 = Mathf.Clamp(0.5f + s.GetFloat("_Aniso1Offset", 0f) * 0.2f, 0.1f, 0.9f);
                var band1 = GradientSynth.Band(Color.black, tint1, center1, Mathf.Lerp(0.30f, 0.04f, power1));
                for (int i = 0; i < ramp.Length; i++)
                {
                    ramp[i] = new Color(
                        Mathf.Max(ramp[i].r, band1[i].r),
                        Mathf.Max(ramp[i].g, band1[i].g),
                        Mathf.Max(ramp[i].b, band1[i].b), 1f);
                }
            }

            if (GradientSynth.IsNearlyBlack(ramp))
            {
                c.Report.Info("ヘアスペキュラ", "ハイライト色が黒 (効果なし) のためスキップしました");
                return;
            }

            int index = c.AllocateGradient(ramp);
            c.SetInt(NonToonProps.HairSpecularGradientIndex, index);
            c.SetFloat(NonToonProps.HairSpecularMultiplyAlbedo, Mathf.Clamp01(s.GetFloat("_AnisoUseBaseColor", 0f)));
            c.RequireScModule(NonToonProps.ModHairSpecular);
            c.Report.Approx("ヘアスペキュラ", "異方性ハイライトを帯グラデーションとして近似しました。幅/位置は手動調整を推奨します");
        }
    }

    /// <summary>逆光/透過光 (Poiyomi: Backlight → NonToon: 本体のバックライト設定)。</summary>
    public sealed class BacklightConversionModule : ConversionModule
    {
        public override int Order => 70;
        public override string DisplayName => "バックライト";

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            if (!s.GetToggle("_BacklightEnabled"))
            {
                // NonToon の逆光表現はコア機能で常時有効 (既定値のまま)。
                return;
            }

            float border = Mathf.Clamp01(s.GetFloat("_BacklightBorder", 0.35f));
            float blur = Mathf.Clamp01(s.GetFloat("_BacklightBlur", 0.05f));
            c.SetFloat(NonToonProps.BacklightRange, Mathf.Clamp01(1f - border));
            c.SetFloat(NonToonProps.BacklightSharpness, Mathf.Clamp(0.075f / Mathf.Max(blur, 0.01f), 0.2f, 5f));
            c.Report.Approx("バックライト", "バックライトの位置/ぼかしを NonToon の逆光リムへ近似しました");

            var backColor = s.GetColor("_BacklightColor", new Color(0.85f, 0.8f, 0.7f, 1f));
            if (Vector4.Distance(backColor, Color.white) > 0.1f)
                c.Report.Drop("バックライト", "バックライトの色指定は NonToon 非対応です (ライト色ベースになります)");
        }
    }

    /// <summary>最低輝度 (Poiyomi: Min Brightness → NonToon: Lighten モジュール)。</summary>
    public sealed class LightenConversionModule : ConversionModule
    {
        public override int Order => 90;
        public override string DisplayName => "ライト補正";

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            float minBrightness = Mathf.Clamp01(s.GetFloat("_LightingMinLightBrightness", 0f));
            if (minBrightness > 0.001f)
            {
                c.SetFloat(NonToonProps.LightBoost, minBrightness);
                c.SetInt(NonToonProps.LightBoostAsEmission, 1);
                c.RequireScModule(NonToonProps.ModLighten);
                c.Report.Info("ライト補正", $"最低輝度 ({minBrightness:F2}) を Lighten モジュール (エミッション扱い) へ引き継ぎました");
            }

            float cap = s.GetFloat("_LightingCap", 1f);
            if (s.GetToggle("_LightingCapEnabled", true) && Mathf.Abs(cap - 1f) > 0.01f)
                c.Report.Drop("ライト補正", $"最大輝度キャップ ({cap:F2}) は NonToon 非対応です");
            if (s.GetFloat("_LightingMonochromatic", 0f) > 0.01f)
                c.Report.Drop("ライト補正", "グレースケールライティングは NonToon 非対応です");
        }
    }
}
