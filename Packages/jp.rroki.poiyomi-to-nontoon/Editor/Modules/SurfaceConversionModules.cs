#nullable enable
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>マットキャップ (Poiyomi: Material Capture 0/1 → NonToon: MatCaps モジュールの乗算/加算スロット)。</summary>
    public sealed class MatCapConversionModule : ConversionModule
    {
        public override int Order => 50;
        public override string DisplayName => "マットキャップ";

        public override bool ShouldRun(ConversionContext c) =>
            c.Source.GetToggle("_MatcapEnable") || c.Source.GetToggle("_Matcap2Enable");

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            bool multiplySlotUsed = false;
            bool addSlotUsed = false;

            // Poiyomi スロット定義: (enable, tex, color, intensity, replace, multiply, add, screen, addToLight, mask, maskCh, maskInv, label)
            var slots = new[]
            {
                ("_MatcapEnable", "_Matcap", "_MatcapColor", "_MatcapIntensity", "_MatcapReplace", "_MatcapMultiply",
                 "_MatcapAdd", "_MatcapScreen", "_MatcapAddToLight", "_MatcapMask", "_MatcapMaskChannel", "_MatcapMaskInvert", "MatCap 1"),
                ("_Matcap2Enable", "_Matcap2", "_Matcap2Color", "_Matcap2Intensity", "_Matcap2Replace", "_Matcap2Multiply",
                 "_Matcap2Add", "_Matcap2Screen", "_Matcap2AddToLight", "_Matcap2Mask", "_Matcap2MaskChannel", "_Matcap2MaskInvert", "MatCap 2"),
            };

            foreach (var (enable, texName, colorName, intensityName, replaceName, multiplyName,
                     addName, screenName, addToLightName, maskName, maskChName, maskInvName, label) in slots)
            {
                if (!s.GetToggle(enable)) continue;
                var tex = s.GetTexture(texName);
                if (tex == null) continue;

                float intensity = Mathf.Clamp(s.GetFloat(intensityName, 1f), 0f, 5f);
                var color = s.GetColor(colorName, Color.white) * intensity;
                color.a = 1f;

                float replace = Mathf.Clamp01(s.GetFloat(replaceName, 1f));
                float multiply = Mathf.Clamp01(s.GetFloat(multiplyName, 0f));
                float add = Mathf.Clamp01(s.GetFloat(addName, 0f))
                          + Mathf.Clamp01(s.GetFloat(screenName, 0f))
                          + Mathf.Clamp01(s.GetFloat(addToLightName, 0f));
                add = Mathf.Clamp01(add);

                bool preferAdd = add >= multiply && add >= replace && add > 0.001f;
                bool preferMultiply = !preferAdd && (multiply >= replace && multiply > 0.001f);
                bool isReplaceFallback = !preferAdd && !preferMultiply;

                if (preferAdd && !addSlotUsed)
                {
                    addSlotUsed = true;
                    c.SetTexture(NonToonProps.MatCapAdd, tex);
                    c.SetColor(NonToonProps.MatCapAddColor, color * add);
                    ApplyMask(c, s, maskName, maskChName, maskInvName, label, NonToonProps.MatCapAddMaskChannel, 1f);
                    c.Report.Info(label, "加算マットキャップとして変換しました");
                }
                else if (!multiplySlotUsed)
                {
                    multiplySlotUsed = true;
                    c.SetTexture(NonToonProps.MatCapMultiply, tex);
                    c.SetColor(NonToonProps.MatCapMultiplyColor, color);
                    // 乗算スロットはマスク値がブレンド量を兼ねる。ブレンド量 < 1 でマスクが無い場合は定数チャンネルで近似。
                    float blend = isReplaceFallback ? replace : multiply;
                    ApplyMask(c, s, maskName, maskChName, maskInvName, label, NonToonProps.MatCapMultiplyMaskChannel, blend);
                    if (isReplaceFallback)
                        c.Report.Approx(label, "Replace ブレンドは NonToon 非対応のため乗算マットキャップとして近似しました (明るい部分が暗くなる場合があります)");
                    else
                        c.Report.Info(label, "乗算マットキャップとして変換しました");
                }
                else
                {
                    c.Report.Drop(label, "NonToon の MatCap スロット (乗算/加算 各1) が埋まっているため破棄しました");
                    continue;
                }

                if (s.GetInt(texName + "UVMode", 1) != 1 && s.HasFloat(texName + "UVMode"))
                    c.Report.Approx(label, "UV モードは標準のビュー空間マットキャップとして変換しました");
                if (Mathf.Abs(s.GetFloat(texName + "Rotation", 0f)) > 0.001f)
                    c.Report.Drop(label, "マットキャップの回転は NonToon 非対応です");
            }

            if (multiplySlotUsed || addSlotUsed)
            {
                c.SetInt(NonToonProps.MatCapsEnable, 1); // [SCConstValue] のためキーワードも自動同期される
                c.SetFloat(NonToonProps.MatCapMultiplyDetail, 0f);
                c.SetFloat(NonToonProps.MatCapAddDetail, 0f);
                c.RequireScModule(NonToonProps.ModMatCaps);
            }

            if ((s.GetToggle("_Matcap3Enable") || s.GetToggle("_Matcap4Enable")) && !c.IsSourceHandled("_Matcap3Enable"))
                c.Report.Drop("MatCap 3/4", "3, 4 スロット目のマットキャップは jp.rroki.nontoon-extra-modules を導入すると引き継げます");
        }

        static void ApplyMask(ConversionContext c, PoiyomiMaterialSnapshot s,
            string maskName, string maskChName, string maskInvName, string label,
            string targetChannelProp, float constantBlend)
        {
            var mask = s.GetTexture(maskName);
            if (mask != null)
            {
                int ch = c.AllocateMaskChannel(new MaskChannelSource
                {
                    Texture = mask,
                    SourceChannel = Mathf.Clamp(s.GetInt(maskChName, 0), 0, 3),
                    Invert = s.GetToggle(maskInvName),
                    Label = label,
                });
                if (ch >= 0) c.SetInt(targetChannelProp, ch);
            }
            else if (constantBlend < 0.999f)
            {
                int ch = c.AllocateMaskChannel(new MaskChannelSource
                {
                    Constant = constantBlend,
                    Label = $"{label} ブレンド量",
                });
                if (ch >= 0)
                {
                    c.SetInt(targetChannelProp, ch);
                    c.Report.Approx(label, $"ブレンド量 ({constantBlend:F2}) を定数マスクチャンネルとして近似しました");
                }
            }
        }
    }

    /// <summary>ディテール (Poiyomi: Details → NonToon: Details モジュール スロット0=テクスチャ, スロット1=ノーマル)。</summary>
    public sealed class DetailsConversionModule : ConversionModule
    {
        public override int Order => 60;
        public override string DisplayName => "ディテール";

        public override bool ShouldRun(ConversionContext c) => c.Source.GetToggle("_DetailEnabled");

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            bool any = false;

            // Poiyomi の _DetailMask は R=テクスチャ / G=ノーマル のマスク。
            // NonToon はスロットごとに RGBA チャンネルを割り当てるため、
            // スロット0 (R) にディテールテクスチャ、スロット1 (G) にディテールノーマルを配置すると
            // マスクチャンネルの意味がそのまま一致する。
            var detailMask = s.GetTexture("_DetailMask");
            if (detailMask != null)
                c.SetTexture(NonToonProps.DetailMask, detailMask);

            var detailTex = s.GetTexture("_DetailTex");
            if (detailTex != null)
            {
                any = true;
                c.SetTexture(NonToonProps.DetailTexture(0), detailTex);
                c.SetTextureST(NonToonProps.DetailTexture(0), s.GetTextureScale("_DetailTex"), s.GetTextureOffset("_DetailTex"));
                // Poiyomi のディテールはグレー基準 (0.5=中立, x2 オーバーレイ) なので Boost=2 が等価
                c.SetFloat(NonToonProps.DetailBoost(0), 2f);
                int uv = s.GetInt("_DetailTexUV", 0);
                c.SetInt(NonToonProps.DetailUV(0), Mathf.Clamp(uv, 0, 3));
                if (uv > 3) c.Report.Warn("ディテール", "UV3 を超える UV 設定は UV0-3 に丸めました");

                float intensity = s.GetFloat("_DetailTexIntensity", 1f);
                if (Mathf.Abs(intensity - 1f) > 0.01f)
                    c.Report.Approx("ディテール", $"ディテール強度 ({intensity:F2}) は NonToon 非対応のため等倍で変換しました");
                var tintColor = s.GetColor("_DetailTint", Color.white);
                if (tintColor != Color.white)
                    c.Report.Drop("ディテール", "ディテールティントは NonToon 非対応です");
                c.Report.Info("ディテール", "ディテールテクスチャをスロット 0 (マスク R) へ変換しました");
            }

            var detailNormal = s.GetTexture("_DetailNormalMap");
            if (detailNormal != null)
            {
                any = true;
                c.SetTexture(NonToonProps.DetailNormalMap(1), detailNormal);
                c.SetFloat(NonToonProps.DetailNormalScale(1), Mathf.Clamp(s.GetFloat("_DetailNormalMapScale", 1f), -10f, 10f));
                c.SetInt(NonToonProps.DetailUV(1), Mathf.Clamp(s.GetInt("_DetailNormalMapUV", 0), 0, 3));
                c.SetFloat(NonToonProps.DetailBoost(1), 1f);
                if (s.HasNonDefaultST("_DetailNormalMap"))
                    c.SetTextureST(NonToonProps.DetailTexture(1), s.GetTextureScale("_DetailNormalMap"), s.GetTextureOffset("_DetailNormalMap"));
                c.Report.Info("ディテール", "ディテールノーマルをスロット 1 (マスク G) へ変換しました");
            }

            if (!any) return;
            c.SetInt(NonToonProps.DetailsEnable, 1); // [SCConstValue] のためキーワードも自動同期される
            c.RequireScModule(NonToonProps.ModDetails);
        }
    }

    /// <summary>距離フェード (Poiyomi: Alpha Distance Fade → NonToon: DistanceFade モジュール)。</summary>
    public sealed class DistanceFadeConversionModule : ConversionModule
    {
        public override int Order => 80;
        public override string DisplayName => "距離フェード";

        public override bool ShouldRun(ConversionContext c) => c.Source.GetToggle("_AlphaDistanceFade");

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            float min = s.GetFloat("_AlphaDistanceFadeMin", 0f);
            float max = s.GetFloat("_AlphaDistanceFadeMax", 0f);
            if (max <= min)
            {
                c.Report.Warn("距離フェード", "距離範囲が不正 (max <= min) のためスキップしました");
                return;
            }

            // NonToon の距離フェードは 0-1m の範囲でカメラ近接時に黒へフェードする
            float clampedMin = Mathf.Clamp01(min);
            float clampedMax = Mathf.Clamp01(max);
            if (min > 1f || max > 1f)
                c.Report.Warn("距離フェード", $"距離 ({min:F2}〜{max:F2}m) は NonToon の上限 1m にクランプしました");

            float minAlpha = Mathf.Clamp01(s.GetFloat("_AlphaDistanceFadeMinAlpha", 0f));
            c.SetVector(NonToonProps.DistanceFade, new Vector4(clampedMin, Mathf.Max(clampedMax, clampedMin + 0.01f), 0f, 0f));
            c.SetFloat(NonToonProps.DistanceFadeStrength, 1f - minAlpha);
            c.RequireScModule(NonToonProps.ModDistanceFade);
            c.Report.Approx("距離フェード", "Poiyomi はアルファフェード、NonToon は黒へのフェードのため見た目が異なります (特に不透明マテリアル)");
        }
    }

    /// <summary>アウトライン (Poiyomi: Outlines → NonToon: 本体のアウトライン設定)。</summary>
    public sealed class OutlineConversionModule : ConversionModule
    {
        public override int Order => 100;
        public override string DisplayName => "アウトライン";

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            if (!s.GetToggle("_EnableOutlines"))
            {
                // NonToon はアウトライン幅 0 でアウトラインパスが完全に除去される
                c.SetFloat(NonToonProps.OutlineWidth, 0f);
                return;
            }

            // 単位: Poiyomi _LineWidth も NonToon _OutlineWidth も「値 × 0.01m (= cm)」なのでそのままコピーできる
            float width = s.GetFloat("_LineWidth", 1f);
            c.SetFloat(NonToonProps.OutlineWidth, width);

            var lineColor = s.GetColor("_LineColor", Color.white);
            lineColor.a = 1f;
            c.SetColor(NonToonProps.OutlineColor, lineColor);
            c.SetFloat(NonToonProps.OutlineZOffset, s.GetFloat("_Offset_Z", 0f));
            c.SetInt(NonToonProps.OutlineFromVertexColor, 0);
            if (width <= 0f)
                c.Report.Info("アウトライン", "アウトライン幅が 0 のため実質無効です (幅と色はそのまま引き継ぎ)");
            else
                c.Report.Info("アウトライン", $"幅 {width:F2} (cm) と色を引き継ぎました");

            float tintMix = s.GetFloat("_OutlineTintMix", 0f);
            if (tintMix < 0.5f && lineColor.maxColorComponent > 0.5f)
                c.Report.Approx("アウトライン", "NonToon のアウトラインは常にアルベド×色になります。Poiyomi の単色アウトラインとは見た目が異なる場合があります");
            if (s.GetToggle("_OutlineFixedSize", true))
                c.Report.Approx("アウトライン", "画面固定幅 (Fixed Size) は NonToon 非対応のため、距離で太さが変わります");
            if (s.GetTexture("_OutlineMask") != null)
                c.Report.Drop("アウトライン", "アウトラインマスクは NonToon 非対応です (頂点カラー方式のみ)");
            if (s.GetTexture("_OutlineTexture") != null)
                c.Report.Drop("アウトライン", "アウトラインテクスチャは NonToon 非対応です");
            if (s.GetFloat("_OutlineEmission", 0f) > 0f)
                c.Report.Drop("アウトライン", "アウトラインエミッションは NonToon 非対応です");
            int expansionMode = s.GetInt("_OutlineExpansionMode", 1);
            if (expansionMode >= 3)
                c.Report.Drop("アウトライン", "Directional / DropShadow モードは NonToon 非対応です (通常の押し出しになります)");
        }
    }
}
