#nullable enable
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// NonToon に受け皿が無い Poiyomi 機能の検出とレポート。
    /// 有効になっている機能を列挙してユーザーに知らせる (変換はしない)。
    /// </summary>
    public sealed class UnsupportedFeaturesModule : ConversionModule
    {
        public override int Order => 1000;
        public override string DisplayName => "未対応機能";

        // (有効トグルプロパティ, 表示名, 補足)
        static readonly (string prop, string label, string note)[] Features =
        {
            ("_EnableEmission", "エミッション 1", "jp.rroki.nontoon-extra-modules を導入すると引き継げます"),
            ("_EnableEmission1", "エミッション 2", ""),
            ("_EnableEmission2", "エミッション 3", ""),
            ("_EnableEmission3", "エミッション 4", ""),
            ("_EnableAudioLink", "AudioLink", ""),
            ("_LTCGIEnabled", "LTCGI", "NonToon に LTCGI のサンプリング実装はありません"),
            ("_SSAOEnabled", "SSAO", ""),
            ("_EnableDepthRimLighting", "深度リムライト", ""),
            ("_CubeMapEnabled", "キューブマップ", "MatCap での代用を検討してください"),
            ("_ClearCoatBRDF", "クリアコート", "jp.rroki.nontoon-extra-modules を導入すると引き継げます"),
            ("_PoiParallax", "視差マッピング (Height Mapping)", "jp.rroki.nontoon-extra-modules を導入すると引き継げます"),
            ("_MainColorAdjustToggle", "色調整 (彩度/色相など)", "jp.rroki.nontoon-extra-modules を導入すると引き継げます"),
            ("_MainHueShiftToggle", "色相シフト", "jp.rroki.nontoon-extra-modules を導入すると引き継げます"),
            ("_UseBump2ndMap", "ノーマルマップ 2", ""),
            ("_EnableBentNormal", "ベントノーマル", ""),
            ("_AlphaFresnel", "フレネルアルファ", ""),
            ("_AlphaAngular", "角度アルファ", ""),
            // 存在すれば検出する系 (バージョンによりプロパティ名が異なる可能性)
            ("_GlitterEnable", "グリッター", "jp.rroki.nontoon-extra-modules を導入すると引き継げます"),
            ("_GlitterEnabled", "グリッター", "jp.rroki.nontoon-extra-modules を導入すると引き継げます"),
            ("_EnableDissolve", "ディゾルブ", "jp.rroki.nontoon-extra-modules を導入すると引き継げます"),
            ("_DissolveEnabled", "ディゾルブ", "jp.rroki.nontoon-extra-modules を導入すると引き継げます"),
            ("_DecalEnabled", "デカール", "jp.rroki.nontoon-extra-modules を導入すると引き継げます"),
            ("_EnableDecal", "デカール", "jp.rroki.nontoon-extra-modules を導入すると引き継げます"),
            ("_FlipbookEnabled", "フリップブック", ""),
            ("_PoiInternalParallax", "内部パララックス (Internal Parallax)", "jp.rroki.nontoon-extra-modules を導入すると引き継げます"),
            ("_EnablePOM", "視差マッピング", ""),
            ("_EnableMirrorOptions", "ミラー設定", ""),
            ("_IridescenceEnabled", "イリデッセンス", ""),
            ("_EnableRefraction", "屈折", ""),
        };

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            var reported = new System.Collections.Generic.HashSet<string>();

            foreach (var (prop, label, note) in Features)
            {
                if (!s.HasFloat(prop) || !s.GetToggle(prop)) continue;
                if (c.IsSourceHandled(prop)) continue; // 拡張モジュールが変換済み
                if (!reported.Add(label)) continue;

                // エミッションは強度 0 なら実質無効なのでスキップ
                if (label.StartsWith("エミッション"))
                {
                    string strengthProp = prop switch
                    {
                        "_EnableEmission" => "_EmissionStrength",
                        "_EnableEmission1" => "_EmissionStrength1",
                        "_EnableEmission2" => "_EmissionStrength2",
                        "_EnableEmission3" => "_EmissionStrength3",
                        _ => "",
                    };
                    if (strengthProp != "" && s.GetFloat(strengthProp, 0f) <= 0.001f) continue;
                }

                c.Report.Drop(label, string.IsNullOrEmpty(note) ? "NonToon 非対応のため破棄しました" : note);
            }

            // UV スクロール検出 (Vector プロパティは m_Colors 側に保存される)
            foreach (var texProp in new[] { "_MainTex", "_BumpMap" })
            {
                var pan = s.GetColor(texProp + "Pan", Color.clear);
                if (Mathf.Abs(pan.r) > 0.0001f || Mathf.Abs(pan.g) > 0.0001f)
                {
                    c.Report.Drop("UV スクロール", $"{texProp} のスクロールアニメーションは NonToon 非対応です");
                }
            }
        }
    }
}
