#nullable enable
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// 影 (Poiyomi: Shading セクション → NonToon: Shade モジュール)。
    ///
    /// NonToon の影は「ハーフランバート N・L を x 軸としたグラデーションをアルベドに乗算」なので、
    /// Poiyomi の各ライティングモードの見た目を 128px のグラデーションへベイクして
    /// _SharedGradients のレイヤーとして接続する。
    /// </summary>
    public sealed class ShadeConversionModule : ConversionModule
    {
        public override int Order => 10;
        public override string DisplayName => "影";

        public override bool ShouldRun(ConversionContext c) =>
            c.Options.BakeShadeGradient && c.Source.GetToggle("_ShadingEnabled", true);

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            int mode = s.GetInt("_LightingMode", 5);
            float strength = Mathf.Clamp01(s.GetFloat("_ShadowStrength", 1f));
            var globalTint = s.GetColor("_LightingShadowColor", Color.white);

            Color[]? ramp = null;
            switch (mode)
            {
                case 0: // Texture Ramp
                    ramp = BuildFromToonRamp(c, strength, globalTint);
                    break;

                case 1: // Multilayer Math
                {
                    var l1 = (s.GetColor("_ShadowColor", new Color(0.7f, 0.75f, 0.85f, 1f)),
                              s.GetFloat("_ShadowBorder", 0.5f), s.GetFloat("_ShadowBlur", 0.1f), 1f);
                    var c2 = s.GetColor("_Shadow2ndColor", new Color(0, 0, 0, 0));
                    var l2 = (c2, s.GetFloat("_Shadow2ndBorder", 0.5f), s.GetFloat("_Shadow2ndBlur", 0.3f), c2.a);
                    var c3 = s.GetColor("_Shadow3rdColor", new Color(0, 0, 0, 0));
                    var l3 = (c3, s.GetFloat("_Shadow3rdBorder", 0.25f), s.GetFloat("_Shadow3rdBlur", 0.1f), c3.a);
                    ramp = GradientSynth.ApplyStrength(GradientSynth.MultiLayer(l1, l2, l3), strength);
                    if (s.GetTexture("_ShadowColorTex") != null)
                        c.Report.Drop("影", "影色テクスチャ (_ShadowColorTex) はグラデーションに含められませんでした");
                    c.Report.Approx("影", "Multilayer Math の影をグラデーションへベイクしました");
                    break;
                }

                case 4: // ShadeMap
                {
                    var shade1 = s.GetColor("_1st_ShadeColor", Color.white);
                    var shade2 = s.GetColor("_2nd_ShadeColor", Color.white);
                    float step1 = s.GetFloat("_BaseColor_Step", 0.5f);
                    float feather1 = s.GetFloat("_BaseShade_Feather", 0.0001f);
                    float step2 = s.GetFloat("_ShadeColor_Step", 0f);
                    float feather2 = s.GetFloat("_1st2nd_Shades_Feather", 0.0001f);
                    ramp = GradientSynth.ApplyStrength(GradientSynth.MultiLayer(
                        (shade1, step1, feather1, 1f),
                        (shade2, step2, feather2, step2 > 0f ? 1f : 0f)), strength);
                    if (s.GetTexture("_1st_ShadeMap") != null || s.GetTexture("_2nd_ShadeMap") != null)
                        c.Report.Drop("影", "ShadeMap テクスチャは引き継げません (色のみベイク)");
                    c.Report.Approx("影", "ShadeMap の影色をグラデーションへベイクしました");
                    break;
                }

                case 2: // Wrapped
                {
                    float start = s.GetFloat("_LightingGradientStart", 0f);
                    float end = Mathf.Max(s.GetFloat("_LightingGradientEnd", 0.5f), start + 0.01f);
                    var tint = globalTint * s.GetColor("_LightingWrappedColor", Color.white);
                    float border = (start + end) * 0.5f;
                    ramp = GradientSynth.ApplyStrength(
                        GradientSynth.Step(tint, Color.white, border, end - start), strength);
                    c.Report.Approx("影", "Wrapped ライティングをグラデーションへ近似しました");
                    break;
                }

                case 8: // SDF (顔影)
                {
                    var sdfTex = s.GetTexture("_SDFShadingTexture");
                    if (sdfTex != null)
                    {
                        c.SetInt(NonToonProps.SdfType, 1);
                        c.SetTexture(NonToonProps.SdfMap, sdfTex);
                        c.SetFloat(NonToonProps.SdfBlendVertical, 0f);
                        c.Report.Approx("影(SDF)", "SDF マップを引き継ぎました。NonToon は R=左/G=右/B=垂直ブレンドの構成のため、チャンネル構成が異なる場合は再作成してください");
                    }
                    float blur = Mathf.Clamp01(s.GetFloat("_SDFBlur", 0.1f));
                    ramp = GradientSynth.ApplyStrength(
                        GradientSynth.Step(globalTint, Color.white, 0.5f, Mathf.Max(blur, 0.02f)), strength);
                    break;
                }

                case 5: // Flat (既定)
                    ramp = GradientSynth.Step(globalTint, Color.white, 0.5f, 0.02f);
                    break;

                case 3: // Skin
                case 6: // Realistic
                case 7: // Cloth
                    ramp = GradientSynth.ApplyStrength(
                        GradientSynth.Step(globalTint, Color.white, 0.4f, 0.4f), strength);
                    c.Report.Approx("影", $"ライティングモード {ModeName(mode)} はソフトな影グラデーションとして近似しました");
                    break;
            }

            if (ramp == null || GradientSynth.IsNearlyWhite(ramp))
            {
                c.Report.Info("影", "影色が実質白のため、Shade グラデーションは割り当てませんでした (フラット)");
                return;
            }

            int index = c.AllocateGradient(ramp);
            c.SetInt(NonToonProps.ShadeGradientIndex, index);
            c.SetVector(NonToonProps.ShadeGradientRange, new Vector4(0f, 1f, 0f, 0f));
            c.RequireScModule(NonToonProps.ModShade);

            if (s.GetTexture("_ShadowStrengthMask") != null)
                c.Report.Drop("影", "影マスク (_ShadowStrengthMask) は NonToon の Shade にマスク入力が無いため破棄しました");
        }

        Color[]? BuildFromToonRamp(ConversionContext c, float strength, Color globalTint)
        {
            var s = c.Source;
            var rampTex = s.GetTexture("_ToonRamp");
            if (rampTex == null)
            {
                c.Report.Warn("影", "Texture Ramp モードですがランプテクスチャが未割り当てのため、フラット影として扱いました");
                return GradientSynth.Step(globalTint, Color.white, 0.5f, 0.02f);
            }

            float offset = s.GetFloat("_ShadowOffset", 0f);
            var raw = TextureBaker.ResampleRowToDisplay(rampTex, GradientSynth.Size);

            // Ramp Offset: サンプル位置を offset だけずらす (Poiyomi の挙動の近似)
            var ramp = new Color[GradientSynth.Size];
            for (int i = 0; i < GradientSynth.Size; i++)
            {
                float x = Mathf.Clamp01(i / (float)(GradientSynth.Size - 1) + offset);
                ramp[i] = raw[Mathf.Clamp(Mathf.RoundToInt(x * (GradientSynth.Size - 1)), 0, GradientSynth.Size - 1)];
            }

            // Shadow Tint は影側にのみ適用する (Poiyomi では明部に効かない)
            GradientSynth.ApplyShadowTint(ramp, globalTint);

            // 明部を白へ正規化: NonToon はグラデーションをアルベドへ乗算する設計のため、
            // 明部が暗いランプをそのまま使うとライト下でも全体が暗くなる
            float normalize = GradientSynth.NormalizeLitEnd(ramp);
            if (normalize > 1.01f)
                c.Report.Approx("影", $"ランプの明部が暗かったため {normalize:F2} 倍で白に正規化しました (影色の比率は維持)");

            GradientSynth.ApplyStrength(ramp, strength);

            if (s.GetInt("_ToonRampCount", 1) > 1)
                c.Report.Warn("影", "複数行ランプの 1 行目のみをベイクしました");
            if (Mathf.Abs(offset) > 0.001f)
                c.Report.Approx("影", $"Ramp Offset ({offset:F2}) をグラデーションのずらしとして近似しました");
            else
                c.Report.Info("影", "シャドウランプをグラデーションへベイクしました");
            return ramp;
        }

        static string ModeName(int mode) => mode switch
        {
            0 => "Texture Ramp", 1 => "Multilayer Math", 2 => "Wrapped", 3 => "Skin",
            4 => "ShadeMap", 5 => "Flat", 6 => "Realistic", 7 => "Cloth", 8 => "SDF",
            _ => mode.ToString(),
        };
    }
}
