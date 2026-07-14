#nullable enable
using Rroki.PoiyomiToNonToon;
using UnityEngine;

// Poiyomi to NonToon Converter との連携ブリッジ。
// このアセンブリは jp.rroki.poiyomi-to-nontoon がインストールされている場合のみコンパイルされる
// (asmdef の versionDefines + defineConstraints)。
// ConversionModule 派生クラスは TypeCache で自動発見されるため、登録処理は不要。
namespace Rroki.NonToonExtraModules
{
    /// <summary>エミッション (Poiyomi: Emission 0 → Emission モジュール)。</summary>
    public sealed class EmissionConversionModule : ConversionModule
    {
        const string Mod = "jp.rroki.nontoon.emission";

        public override int Order => 200;
        public override string DisplayName => "エミッション";

        public override bool ShouldRun(ConversionContext c) =>
            c.Source.GetToggle("_EnableEmission") && c.Source.GetFloat("_EmissionStrength", 0f) > 0.001f;

        public override void DeclareRequirements(ConversionContext c) => c.RequireScModule(Mod);

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            c.MarkSourceHandled("_EnableEmission");
            c.SetInt(NonToonProps.Prop(Mod, "_Enable"), 1);

            c.SetColor(NonToonProps.Prop(Mod, "_EmissionColor"), s.GetColor("_EmissionColor", Color.white));
            c.SetFloat(NonToonProps.Prop(Mod, "_EmissionStrength"), Mathf.Clamp(s.GetFloat("_EmissionStrength", 1f), 0f, 20f));
            c.SetFloat(NonToonProps.Prop(Mod, "_EmissionMultiplyAlbedo"), s.GetToggle("_EmissionBaseColorAsMap") ? 1f : 0f);

            // ---- マップ + UV + スクロール ----
            var map = s.GetTexture("_EmissionMap");
            if (map != null)
            {
                c.SetTexture(NonToonProps.Prop(Mod, "_EmissionMap"), map);
                c.SetTextureST(NonToonProps.Prop(Mod, "_EmissionMap"), s.GetTextureScale("_EmissionMap"), s.GetTextureOffset("_EmissionMap"));
            }

            var scroll = Vector2.zero;
            var pan = s.GetColor("_EmissionMapPan", Color.clear);
            if (Mathf.Abs(pan.r) > 0.0001f || Mathf.Abs(pan.g) > 0.0001f)
            {
                // Poiyomi のパンは POI_TIME.x (= _Time.x = 秒/20) 係数、
                // NonToon 側モジュールは UV/秒 (_Time.y) なので 1/20 に換算する
                scroll = new Vector2(pan.r, pan.g) / 20f;
            }

            int uv = s.GetInt("_EmissionMapUV", 0);
            if (uv <= 3)
            {
                c.SetInt(NonToonProps.Prop(Mod, "_EmissionMapUV"), uv);
            }
            else
            {
                // Distorted UV / Panosphere 等の特殊 UV は UV0 + ゆるやかなスクロールで近似
                c.SetInt(NonToonProps.Prop(Mod, "_EmissionMapUV"), 0);
                if (scroll == Vector2.zero) scroll = new Vector2(0.05f, 0.02f);
                c.Report.Approx(DisplayName, $"特殊 UV モード ({uv}) は UV0 + スクロールで近似しました");
            }
            c.SetVector(NonToonProps.Prop(Mod, "_EmissionScroll"), new Vector4(scroll.x, scroll.y, 0f, 0f));

            // ---- マスク ----
            var mask = s.GetTexture("_EmissionMask");
            if (mask != null)
            {
                int ch = c.AllocateMaskChannel(new MaskChannelSource
                {
                    Texture = mask,
                    SourceChannel = Mathf.Clamp(s.GetInt("_EmissionMaskChannel", 0), 0, 3),
                    Label = "エミッション",
                });
                if (ch >= 0) c.SetInt(NonToonProps.Prop(Mod, "_EmissionMaskChannel"), ch);
            }

            // ---- 点滅 ----
            if (s.GetToggle("_EmissionBlinkingEnabled"))
            {
                float velocity = s.GetFloat("_EmissiveBlink_Velocity", 0f);
                if (velocity > 0.001f)
                {
                    c.SetVector(NonToonProps.Prop(Mod, "_EmissionBlink"), new Vector4(
                        Mathf.Clamp01(s.GetFloat("_EmissiveBlink_Min", 0f)),
                        Mathf.Clamp01(s.GetFloat("_EmissiveBlink_Max", 1f)),
                        velocity,
                        s.GetFloat("_EmissionBlinkingOffset", 0f)));
                    if (s.GetInt("_EmissiveBlink_Mode", 0) != 0)
                        c.Report.Approx(DisplayName, "点滅モード (矩形波など) は正弦波として近似しました");
                    else
                        c.Report.Info(DisplayName, "点滅設定を引き継ぎました");
                }
                // velocity 0 は実質静止なので既定値 (常時最大) のまま
            }

            // ---- 色相シフト ----
            if (s.GetToggle("_EmissionHueShiftEnabled"))
            {
                // Poiyomi: frac(_EmissionHueShift + _EmissionHueShiftSpeed * POI_TIME.x)
                // POI_TIME.x = _Time.x = 秒/20 のため、UV/秒基準のモジュールへは 1/20 換算
                c.SetFloat(NonToonProps.Prop(Mod, "_EmissionHueShift"), Mathf.Repeat(s.GetFloat("_EmissionHueShift", 0f), 1f));
                c.SetFloat(NonToonProps.Prop(Mod, "_EmissionHueShiftSpeed"), s.GetFloat("_EmissionHueShiftSpeed", 0f) / 20f);

                if (s.GetInt("_EmissionHueShiftColorSpace", 0) == 0)
                    c.Report.Approx(DisplayName, "色相シフトの色空間 (OKLab) は RGB 回転行列で近似しました");
                if (s.GetInt("_EmissionHueSelectOrShift", 1) == 0)
                    c.Report.Approx(DisplayName, "Hue Select (色相置き換え) はシフト (回転) として近似しました");
                // Thry のアニメーション可能フラグはプロパティではなくマテリアルタグに保存される
                bool hueAnimated = s.GetToggle("_EmissionHueShiftAnimated")
                    || s.Material.GetTag("_EmissionHueShiftAnimated", false, "") == "1";
                if (hueAnimated)
                    c.Report.Warn(DisplayName,
                        "色相シフトがアニメーション制御されています。アニメーションクリップのプロパティパスを " +
                        $"material.{NonToonProps.Prop(Mod, "_EmissionHueShift")} へ変更してください");
                else
                    c.Report.Info(DisplayName, "色相シフトを引き継ぎました");
            }

            if (s.GetToggle("_ScrollingEmission"))
                c.Report.Warn(DisplayName, "スクロールエミッション (波状の発光) は未対応です。必要ならスクロール速度で代用してください");

            c.Report.Info(DisplayName, "エミッションを Emission モジュールへ引き継ぎました");
        }
    }

    /// <summary>色調整 (Poiyomi: Color Adjust → ColorAdjust モジュール)。</summary>
    public sealed class ColorAdjustConversionModule : ConversionModule
    {
        const string Mod = "jp.rroki.nontoon.coloradjust";

        public override int Order => 210;
        public override string DisplayName => "色調整";

        public override bool ShouldRun(ConversionContext c) =>
            c.Source.GetToggle("_MainColorAdjustToggle") || c.Source.GetToggle("_MainHueShiftToggle");

        public override void DeclareRequirements(ConversionContext c) => c.RequireScModule(Mod);

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;

            float hue = 0f;
            if (s.GetToggle("_MainHueShiftToggle"))
            {
                hue = Mathf.Repeat(s.GetFloat("_MainHueShift", 0f), 1f);
                if (hue > 0.5f) hue -= 1f; // 0..1 → -0.5..0.5 (同じ回転角)
                if (s.GetFloat("_MainHueShiftSpeed", 0f) > 0.0001f)
                    c.Report.Drop(DisplayName, "色相シフトのアニメーションは未対応です");
            }

            float saturation = s.GetToggle("_MainColorAdjustToggle") ? s.GetFloat("_Saturation", 0f) : 0f;
            float brightness = s.GetToggle("_MainColorAdjustToggle") ? s.GetFloat("_MainBrightness", 0f) : 0f;
            float gamma = s.GetToggle("_MainColorAdjustToggle") ? s.GetFloat("_MainGamma", 1f) : 1f;

            bool neutral = Mathf.Abs(hue) < 0.001f && Mathf.Abs(saturation) < 0.001f
                        && Mathf.Abs(brightness) < 0.001f && Mathf.Abs(gamma - 1f) < 0.001f;
            if (neutral)
            {
                c.MarkSourceHandled("_MainColorAdjustToggle");
                c.MarkSourceHandled("_MainHueShiftToggle");
                c.Report.Info(DisplayName, "色調整は有効でしたが実質無補正のためスキップしました");
                return;
            }

            c.MarkSourceHandled("_MainColorAdjustToggle");
            c.MarkSourceHandled("_MainHueShiftToggle");
            c.SetInt(NonToonProps.Prop(Mod, "_Enable"), 1);
            c.SetFloat(NonToonProps.Prop(Mod, "_AdjustHue"), hue);

            if (saturation > 2f)
            {
                c.Report.Approx(DisplayName, $"彩度 ({saturation:F2}) は上限 2 にクランプしました");
                saturation = 2f;
            }
            c.SetFloat(NonToonProps.Prop(Mod, "_AdjustSaturation"), Mathf.Clamp(saturation, -1f, 2f));
            c.SetFloat(NonToonProps.Prop(Mod, "_AdjustBrightness"), Mathf.Clamp(brightness, -1f, 2f));
            c.SetFloat(NonToonProps.Prop(Mod, "_AdjustGamma"), Mathf.Clamp(gamma, 0.01f, 5f));

            if (s.GetFloat("_MainChromatize", 0f) > 0.001f)
                c.Report.Drop(DisplayName, "Chromatize は未対応です");
            var tint = s.GetColor("_MainTintColor", new Color(1f, 1f, 1f, 0f));
            if (tint.a > 0.001f)
                c.Report.Drop(DisplayName, "ティントカラー (アルファ加重) は未対応です");

            c.Report.Approx(DisplayName, "色相/彩度/明度/ガンマを ColorAdjust モジュールへ引き継ぎました (明度の数式は Poiyomi と異なるため要確認)");
        }
    }

    /// <summary>
    /// 環境リフレクション (Poiyomi: Mochie PBR / lilToon スタイル反射 → EnvReflection モジュール)。
    /// クリアコートは専用の ClearCoatConversionModule が担当する。
    /// </summary>
    public sealed class EnvReflectionConversionModule : ConversionModule
    {
        const string Mod = "jp.rroki.nontoon.envreflection";

        public override int Order => 230;
        public override string DisplayName => "環境リフレクション";

        static bool UsesMochieReflection(ConversionContext c) =>
            c.Source.GetToggle("_MochieBRDF") && c.Source.GetFloat("_MochieReflectionStrength", 1f) > 0.001f;

        static bool UsesLilReflection(ConversionContext c) =>
            c.Source.GetToggle("_StylizedSpecular") && c.Source.GetInt("_StylizedReflectionMode", 0) == 1
            && c.Source.GetToggle("_ApplyReflection");

        public override bool ShouldRun(ConversionContext c) =>
            UsesMochieReflection(c) || UsesLilReflection(c);

        public override void DeclareRequirements(ConversionContext c)
        {
            c.RequireScModule(Mod);
            // ベースパッケージ側の「環境リフレクション未対応」Drop を抑制する
            c.MarkSourceHandled("_MochieReflectionStrength");
            c.MarkSourceHandled("_ApplyReflection");
        }

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;

            float strength = 0f;
            var tint = Color.white;
            float multiplyAlbedo = 0f;
            float fresnel = 1f;

            if (UsesMochieReflection(c))
            {
                strength = Mathf.Clamp01(s.GetFloat("_MochieReflectionStrength", 1f));
                tint = s.GetColor("_MochieReflectionTint", Color.white);
                float metallic = Mathf.Clamp01(s.GetFloat("_MochieMetallicMultiplier", 0f));
                multiplyAlbedo = metallic;       // 金属はアルベド色の反射
                fresnel = 1f - metallic;         // 金属は全面反射 (フレネル減衰なし)
                c.Report.Approx(DisplayName, "PBR (Mochie) の環境リフレクションを EnvReflection モジュールへ引き継ぎました");
            }
            else if (UsesLilReflection(c))
            {
                // lilToon 系は _ReflectionColor のアルファがブレンド強度
                tint = s.GetColor("_ReflectionColor", Color.white);
                strength = Mathf.Clamp01(tint.a);
                float metallic = Mathf.Clamp01(s.GetFloat("_Metallic", 0f));
                multiplyAlbedo = metallic;
                fresnel = 1f - metallic;
                c.Report.Approx(DisplayName, $"lilToon スタイルの環境リフレクション (強度 {strength:F2} = 反射色のアルファ) を EnvReflection モジュールへ引き継ぎました");
            }

            if (strength <= 0.001f)
            {
                c.Report.Info(DisplayName, "反射強度が 0 のためスキップしました");
                return;
            }

            c.SetInt(NonToonProps.Prop(Mod, "_Enable"), 1);
            c.SetColor(NonToonProps.Prop(Mod, "_ReflectionTint"), tint);
            c.SetFloat(NonToonProps.Prop(Mod, "_ReflectionStrength"), strength);
            c.SetFloat(NonToonProps.Prop(Mod, "_ReflectionFresnel"), fresnel);
            c.SetFloat(NonToonProps.Prop(Mod, "_ReflectionMultiplyAlbedo"), multiplyAlbedo);

            // Mochie のパックドマップ (B = リフレクションマスク) があれば共有マスクへ
            var metallicMaps = s.GetTexture("_MochieMetallicMaps");
            if (UsesMochieReflection(c) && metallicMaps != null)
            {
                int ch = c.AllocateMaskChannel(new MaskChannelSource
                {
                    Texture = metallicMaps,
                    SourceChannel = Mathf.Clamp(s.GetInt("_MochieMetallicMapsReflectionMaskChannel", 2), 0, 3),
                    Label = "環境リフレクション",
                });
                if (ch >= 0) c.SetInt(NonToonProps.Prop(Mod, "_ReflectionMaskChannel"), ch);
            }

            c.Report.Warn(DisplayName, "反射の明るさはワールドのリフレクションプローブ設置状況に依存します (プローブ無しワールドでは反射が出ません)");
        }
    }

    /// <summary>
    /// クリアコート (Poiyomi: Clear Coat → ClearCoat モジュール)。
    /// コート専用の滑らかさを持つ第2ローブ (直接光ハイライト + シャープな映り込み) として引き継ぐ。
    /// ベースの環境リフレクション (EnvReflection) とは独立して調整できる。
    /// </summary>
    public sealed class ClearCoatConversionModule : ConversionModule
    {
        const string Mod = "jp.rroki.nontoon.clearcoat";

        public override int Order => 235;
        public override string DisplayName => "クリアコート";

        public override bool ShouldRun(ConversionContext c) =>
            c.Source.GetToggle("_ClearCoatBRDF") && c.Source.GetFloat("_ClearCoatStrength", 1f) > 0.001f;

        public override void DeclareRequirements(ConversionContext c)
        {
            c.RequireScModule(Mod);
            c.MarkSourceHandled("_ClearCoatBRDF");
        }

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            float strength = Mathf.Clamp01(s.GetFloat("_ClearCoatStrength", 1f));

            c.SetInt(NonToonProps.Prop(Mod, "_Enable"), 1);
            c.SetFloat(NonToonProps.Prop(Mod, "_CoatSmoothness"), Mathf.Clamp01(s.GetFloat("_ClearCoatSmoothness", 1f)));
            c.SetFloat(NonToonProps.Prop(Mod, "_CoatSpecular"),
                Mathf.Clamp(s.GetFloat("_ClearCoatSpecularStrength", 1f) * strength, 0f, 2f));
            c.SetFloat(NonToonProps.Prop(Mod, "_CoatReflection"),
                Mathf.Clamp01(s.GetFloat("_ClearCoatReflectionStrength", 1f) * strength));

            // _ClearCoatMaps: R=コートマスク (既定) のパックドマップ
            var coatMaps = s.GetTexture("_ClearCoatMaps");
            if (coatMaps != null)
            {
                int ch = c.AllocateMaskChannel(new MaskChannelSource
                {
                    Texture = coatMaps,
                    SourceChannel = Mathf.Clamp(s.GetInt("_ClearCoatMapsClearCoatMaskChannel", 0), 0, 3),
                    Label = "クリアコート",
                });
                if (ch >= 0) c.SetInt(NonToonProps.Prop(Mod, "_CoatMaskChannel"), ch);
                c.Report.Approx(DisplayName, "コートマスクを共有マスクへパックしました (ラフネス/反射マスクの個別チャンネルは非対応)");
            }

            c.Report.Approx(DisplayName,
                "クリアコートを専用モジュールへ引き継ぎました (ハイライトと映り込みは NonToon マテリアルの「クリアコート」で個別調整できます)");
        }
    }

    /// <summary>
    /// 内部パララックス (Poiyomi: Internal Parallax → InternalParallax モジュール)。
    /// 専用マスクは共有マスクチャンネルを消費しないよう、内部マップのアルファへベイクして合成する。
    /// </summary>
    public sealed class InternalParallaxConversionModule : ConversionModule
    {
        const string Mod = "jp.rroki.nontoon.internalparallax";

        public override int Order => 245;
        public override string DisplayName => "内部パララックス";

        public override bool ShouldRun(ConversionContext c) =>
            c.Source.GetToggle("_PoiInternalParallax") && c.Source.GetTexture("_ParallaxInternalMap") != null;

        public override void DeclareRequirements(ConversionContext c)
        {
            c.RequireScModule(Mod);
            c.MarkSourceHandled("_PoiInternalParallax");
        }

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            var map = s.GetTexture("_ParallaxInternalMap")!;
            bool heightmapMode = s.GetInt("_ParallaxInternalHeightmapMode", 0) == 1;

            c.SetInt(NonToonProps.Prop(Mod, "_Enable"), 1);

            // ---- マスクをマップの A へベイク ----
            // Heightmap モード: マスク外 = 表面レベル (A=1) にする → 沈み込みが無くなり効果が消える。
            //   RGB (柄の色) は元のまま保持されるため、Replace でも正しい色が出る。
            // LayerAlpha モード: A は不透明度なのでマスクを乗算する。
            var mask = s.GetTexture("_ParallaxInternalMapMask");
            Texture assignedMap = map;
            if (mask != null)
            {
                int w = Mathf.Clamp(map.width, 4, 4096);
                int h = Mathf.Clamp(map.height, 4, 4096);
                var mapPixels = TextureBaker.ReadbackLinear(map, w, h);
                var maskPixels = TextureBaker.ReadbackLinear(mask, w, h);
                int maskCh = Mathf.Clamp(s.GetInt("_ParallaxInternalMapMaskChannel", 0), 0, 3);
                bool invert = s.GetToggle("_ParallaxInternalMapMaskInvert");
                for (int i = 0; i < mapPixels.Length; i++)
                {
                    var m = maskPixels[i];
                    float v = maskCh switch { 0 => m.r, 1 => m.g, 2 => m.b, _ => m.a };
                    if (invert) v = 1f - v;
                    v = Mathf.Clamp01(v);
                    mapPixels[i].a = heightmapMode
                        ? Mathf.Lerp(1f, mapPixels[i].a, v)  // マスク外は表面レベルへ
                        : mapPixels[i].a * v;                 // 不透明度へ乗算
                }
                assignedMap = TextureBaker.SavePng(mapPixels, w, h, $"{c.AssetBasePath}_internal.png", sRGB: true);
                c.Report.Info(DisplayName, "専用マスクを内部マップの高さ (A) へベイクしました (マスク外は効果なし、柄の色は保持)");
            }

            c.SetTexture(NonToonProps.Prop(Mod, "_InternalMap"), assignedMap);
            c.SetTextureST(NonToonProps.Prop(Mod, "_InternalMap"),
                s.GetTextureScale("_ParallaxInternalMap"), s.GetTextureOffset("_ParallaxInternalMap"));

            float minDepth = Mathf.Clamp(s.GetFloat("_ParallaxInternalMinDepth", 0f), 0f, 0.1f);
            float maxDepth = Mathf.Clamp(s.GetFloat("_ParallaxInternalMaxDepth", 0.01f), 0f, 0.1f);
            if (maxDepth <= minDepth) maxDepth = minDepth + 0.005f;
            c.SetVector(NonToonProps.Prop(Mod, "_InternalDepth"), new Vector4(minDepth, maxDepth, 0f, 0f));

            // Poiyomi の内部マップは RGB=色 / A=高さ (ThryRGBAPacker(RGB Color, A Height))
            c.SetInt(NonToonProps.Prop(Mod, "_InternalMode"), heightmapMode ? 0 : 1);
            if (heightmapMode)
            {
                // Replace (置き換え) では Poiyomi の深度色/明るさ (加算蓄積用の値) を掛けると
                // 真っ黒になるため、ニュートラル (テクスチャの色そのまま) で引き継ぐ
                c.SetColor(NonToonProps.Prop(Mod, "_InternalColorNear"), Color.white);
                c.SetColor(NonToonProps.Prop(Mod, "_InternalColorFar"), Color.white);
                c.SetFloat(NonToonProps.Prop(Mod, "_InternalFadeNear"), 1f);
                c.SetFloat(NonToonProps.Prop(Mod, "_InternalFadeFar"), 1f);
                c.Report.Info(DisplayName,
                    "ハイトマップモード: 沈み込み箇所は内部テクスチャの色そのまま (深度による色/明るさの減衰はニュートラル)。奥を暗くしたい場合は「色/明るさ (Far)」を下げてください");
            }
            else
            {
                // 加算蓄積では Poiyomi の深度色/明るさをそのまま引き継ぐ
                c.SetColor(NonToonProps.Prop(Mod, "_InternalColorNear"), s.GetColor("_ParallaxInternalMinColor", Color.white));
                c.SetColor(NonToonProps.Prop(Mod, "_InternalColorFar"), s.GetColor("_ParallaxInternalMaxColor", Color.white));
                c.SetFloat(NonToonProps.Prop(Mod, "_InternalFadeNear"), Mathf.Clamp(s.GetFloat("_ParallaxInternalMinFade", 1f), 0f, 5f));
                c.SetFloat(NonToonProps.Prop(Mod, "_InternalFadeFar"), Mathf.Clamp(s.GetFloat("_ParallaxInternalMaxFade", 0.1f), 0f, 5f));
                c.Report.Approx(DisplayName, "多層サンプルの前後合成として近似しました");
            }
            c.SetInt(NonToonProps.Prop(Mod, "_InternalIterations"),
                Mathf.Clamp(s.GetInt("_ParallaxInternalIterations", 8), 1, 32));
            c.SetFloat(NonToonProps.Prop(Mod, "_InternalStrength"), 1f);

            // 表面ブレンド: 沈み込み箇所はアルベドを内部テクスチャ色で置き換える (Replace) のが
            // 見た目の期待に合うため、ハイトマップモードでは Replace を既定にする。
            // (Poiyomi の Surface Blend Mode が Replace(0) の場合も同様)
            int surfaceBlend = s.GetInt("_ParallaxInternalSurfaceBlendMode", 8);
            bool replace = heightmapMode || surfaceBlend == 0;
            c.SetInt(NonToonProps.Prop(Mod, "_InternalSurfaceBlend"), replace ? 1 : 0);
            c.SetInt(NonToonProps.Prop(Mod, "_InternalLit"), 1);
            if (replace)
                c.Report.Info(DisplayName, "沈み込み箇所のアルベドを内部テクスチャ色で置き換えます (加算に戻す場合は Surface Blend を Add に)");
            else if (surfaceBlend != 8)
                c.Report.Approx(DisplayName, $"サーフェスブレンドモード ({surfaceBlend}) はライティングを受ける加算として近似しました");

            if (s.GetToggle("_ParallaxInternalHueShiftEnabled"))
                c.Report.Drop(DisplayName, "レイヤーごとの色相シフトは非対応です");
        }
    }

    /// <summary>
    /// 追加マットキャップ (Poiyomi: MatCap スロット 3/4 → MatCapsExtra モジュール)。
    /// スロット 1/2 はベースパッケージが NonToon 標準 MatCaps へ変換するため、
    /// ここでは 3/4 を追加の乗算/加算スロットへ振り分ける。
    /// </summary>
    public sealed class ExtraMatCapConversionModule : ConversionModule
    {
        const string Mod = "jp.rroki.nontoon.matcapsextra";

        public override int Order => 55;
        public override string DisplayName => "マットキャップ (追加)";

        static bool SlotEnabled(ConversionContext c, string prefix) =>
            c.Source.GetToggle(prefix + "Enable") && c.Source.GetTexture(prefix) != null;

        public override bool ShouldRun(ConversionContext c) =>
            SlotEnabled(c, "_Matcap3") || SlotEnabled(c, "_Matcap4");

        public override void DeclareRequirements(ConversionContext c)
        {
            c.RequireScModule(Mod);
            c.MarkSourceHandled("_Matcap3Enable");
            c.MarkSourceHandled("_Matcap4Enable");
        }

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            bool multiplyUsed = false;
            bool addUsed = false;

            foreach (var prefix in new[] { "_Matcap3", "_Matcap4" })
            {
                if (!SlotEnabled(c, prefix)) continue;
                var tex = s.GetTexture(prefix)!;
                string label = "MatCap " + prefix.Substring(7);

                float intensity = Mathf.Clamp(s.GetFloat(prefix + "Intensity", 1f), 0f, 5f);
                var color = s.GetColor(prefix + "Color", Color.white) * intensity;
                color.a = 1f;

                float replace = Mathf.Clamp01(s.GetFloat(prefix + "Replace", 1f));
                float multiply = Mathf.Clamp01(s.GetFloat(prefix + "Multiply", 0f));
                float add = Mathf.Clamp01(
                    Mathf.Clamp01(s.GetFloat(prefix + "Add", 0f))
                    + Mathf.Clamp01(s.GetFloat(prefix + "Screen", 0f))
                    + Mathf.Clamp01(s.GetFloat(prefix + "AddToLight", 0f)));

                bool preferAdd = add >= multiply && add >= replace && add > 0.001f;
                bool preferMultiply = !preferAdd && (multiply >= replace && multiply > 0.001f);
                bool isReplaceFallback = !preferAdd && !preferMultiply;

                if (preferAdd && !addUsed)
                {
                    addUsed = true;
                    c.SetTexture(NonToonProps.Prop(Mod, "_MatCapAdd"), tex);
                    c.SetColor(NonToonProps.Prop(Mod, "_MatCapAddColor"), color * add);
                    ApplyMask(c, s, prefix, label, NonToonProps.Prop(Mod, "_MatCapAddMaskChannel"), 1f);
                    c.Report.Info(label, "追加モジュールの加算マットキャップとして変換しました");
                }
                else if (!multiplyUsed)
                {
                    multiplyUsed = true;
                    c.SetTexture(NonToonProps.Prop(Mod, "_MatCapMultiply"), tex);
                    c.SetColor(NonToonProps.Prop(Mod, "_MatCapMultiplyColor"), color);
                    float blend = isReplaceFallback ? replace : multiply;
                    ApplyMask(c, s, prefix, label, NonToonProps.Prop(Mod, "_MatCapMultiplyMaskChannel"), blend);
                    if (isReplaceFallback)
                        c.Report.Approx(label, "Replace ブレンドは乗算マットキャップとして近似しました");
                    else
                        c.Report.Info(label, "追加モジュールの乗算マットキャップとして変換しました");
                }
                else
                {
                    c.Report.Drop(label, "追加マットキャップスロット (乗算/加算 各1) も埋まっているため破棄しました");
                    continue;
                }
            }

            if (multiplyUsed || addUsed)
            {
                c.SetInt(NonToonProps.Prop(Mod, "_Enable"), 1);
                c.SetFloat(NonToonProps.Prop(Mod, "_MatCapMultiplyDetail"), 0f);
                c.SetFloat(NonToonProps.Prop(Mod, "_MatCapAddDetail"), 0f);
            }
        }

        static void ApplyMask(ConversionContext c, PoiyomiMaterialSnapshot s,
            string prefix, string label, string targetChannelProp, float constantBlend)
        {
            var mask = s.GetTexture(prefix + "Mask");
            if (mask != null)
            {
                int ch = c.AllocateMaskChannel(new MaskChannelSource
                {
                    Texture = mask,
                    SourceChannel = Mathf.Clamp(s.GetInt(prefix + "MaskChannel", 0), 0, 3),
                    Invert = s.GetToggle(prefix + "MaskInvert"),
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

    /// <summary>
    /// デカール (Poiyomi: Decal 0-3 → Decal モジュール)。
    /// 位置/スケール/回転/色/ブレンド/タイリング/エミッションを引き継ぐ。
    /// 専用マスク (_DecalMask) はデカールテクスチャのアルファへベイクする。
    /// </summary>
    public sealed class DecalConversionModule : ConversionModule
    {
        const string Mod = "jp.rroki.nontoon.decal";

        static readonly string[] PoiSuffixes = { "", "1", "2", "3" };

        public override int Order => 250;
        public override string DisplayName => "デカール";

        static bool SlotEnabled(ConversionContext c, int slot) =>
            c.Source.GetToggle(slot == 0 ? "_DecalEnabled" : $"_DecalEnabled{slot}")
            && c.Source.GetTexture("_DecalTexture" + PoiSuffixes[slot]) != null;

        public override bool ShouldRun(ConversionContext c) =>
            SlotEnabled(c, 0) || SlotEnabled(c, 1) || SlotEnabled(c, 2) || SlotEnabled(c, 3);

        public override void DeclareRequirements(ConversionContext c)
        {
            c.RequireScModule(Mod);
            c.MarkSourceHandled("_DecalEnabled");
            c.MarkSourceHandled("_EnableDecal");
        }

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            c.SetInt(NonToonProps.Prop(Mod, "_Enable"), 1);

            var decalMask = s.GetTexture("_DecalMask");
            int converted = 0;

            for (int slot = 0; slot < 4; slot++)
            {
                if (!SlotEnabled(c, slot)) continue;
                string p = PoiSuffixes[slot];
                var tex = s.GetTexture("_DecalTexture" + p)!;
                string label = $"デカール {slot + 1}";

                // 専用マスクをテクスチャのアルファへベイク
                Texture assigned = tex;
                if (decalMask != null)
                {
                    int w = Mathf.Clamp(tex.width, 4, 4096);
                    int h = Mathf.Clamp(tex.height, 4, 4096);
                    var texPixels = TextureBaker.ReadbackLinear(tex, w, h);
                    var maskPixels = TextureBaker.ReadbackLinear(decalMask, w, h);
                    int maskCh = Mathf.Clamp(s.GetInt($"_Decal{slot}MaskChannel", 0), 0, 3);
                    for (int i = 0; i < texPixels.Length; i++)
                    {
                        var m = maskPixels[i];
                        float v = maskCh switch { 0 => m.r, 1 => m.g, 2 => m.b, _ => m.a };
                        texPixels[i].a *= Mathf.Clamp01(v);
                    }
                    assigned = TextureBaker.SavePng(texPixels, w, h, $"{c.AssetBasePath}_decal{slot}.png", sRGB: true);
                    c.Report.Info(label, "デカールマスクをテクスチャのアルファへベイクしました");
                }

                c.SetTexture(NonToonProps.Prop(Mod, $"_Decal{slot}Texture"), assigned);

                var position = s.GetColor("_DecalPosition" + p, new Color(0.5f, 0.5f, 0f, 0f));
                var scale = s.GetColor("_DecalScale" + p, new Color(1f, 1f, 1f, 0f));
                c.SetVector(NonToonProps.Prop(Mod, $"_Decal{slot}Position"), new Vector4(position.r, position.g, 0f, 0f));
                c.SetVector(NonToonProps.Prop(Mod, $"_Decal{slot}Scale"), new Vector4(scale.r, scale.g, 0f, 0f));
                c.SetFloat(NonToonProps.Prop(Mod, $"_Decal{slot}Rotation"), Mathf.Repeat(s.GetFloat("_DecalRotation" + p, 0f), 360f));
                c.SetColor(NonToonProps.Prop(Mod, $"_Decal{slot}Color"), s.GetColor("_DecalColor" + p, Color.white));
                c.SetFloat(NonToonProps.Prop(Mod, $"_Decal{slot}Alpha"), Mathf.Clamp01(s.GetFloat("_DecalBlendAlpha" + p, 1f)));
                c.SetInt(NonToonProps.Prop(Mod, $"_Decal{slot}Tiled"), s.GetToggle("_DecalTiled" + p) ? 1 : 0);
                c.SetFloat(NonToonProps.Prop(Mod, $"_Decal{slot}Emission"), Mathf.Clamp(s.GetFloat("_DecalEmissionStrength" + p, 0f), 0f, 20f));

                int blendType = s.GetInt("_DecalBlendType" + p, 0);
                int blend = blendType switch { 0 => 0, 2 => 1, 8 => 2, _ => 0 };
                c.SetInt(NonToonProps.Prop(Mod, $"_Decal{slot}Blend"), blend);
                if (blendType != 0 && blendType != 2 && blendType != 8)
                    c.Report.Approx(label, $"ブレンドモード ({blendType}) は Replace として近似しました");

                int uv = s.GetInt("_DecalTextureUV" + p, 0);
                if (uv <= 3)
                {
                    c.SetInt(NonToonProps.Prop(Mod, $"_Decal{slot}UV"), uv);
                }
                else
                {
                    c.SetInt(NonToonProps.Prop(Mod, $"_Decal{slot}UV"), 0);
                    c.Report.Approx(label, $"特殊 UV モード ({uv}) は UV0 として近似しました");
                }

                if (s.GetToggle("_DecalHueShiftEnabled" + p))
                    c.Report.Drop(label, "デカールの色相シフトは非対応です");
                if (s.GetInt("_DecalOverrideAlpha" + p, 0) != 0)
                    c.Report.Drop(label, "アルファ上書きモードは非対応です");

                converted++;
            }

            c.Report.Info(DisplayName, $"{converted} 個のデカールを引き継ぎました");
        }
    }

    /// <summary>
    /// ハイトマップ視差 (Poiyomi: Parallax Height Mapping → HeightParallax モジュール)。
    /// </summary>
    public sealed class HeightParallaxConversionModule : ConversionModule
    {
        const string Mod = "jp.rroki.nontoon.heightparallax";

        public override int Order => 240;
        public override string DisplayName => "ハイトマップ視差";

        public override bool ShouldRun(ConversionContext c) =>
            c.Source.GetToggle("_PoiParallax") && c.Source.GetTexture("_HeightMap") != null;

        public override void DeclareRequirements(ConversionContext c)
        {
            c.RequireScModule(Mod);
            c.MarkSourceHandled("_PoiParallax");
        }

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;

            c.SetInt(NonToonProps.Prop(Mod, "_Enable"), 1);
            c.SetTexture(NonToonProps.Prop(Mod, "_ParallaxHeightMap"), s.GetTexture("_HeightMap"));
            c.SetFloat(NonToonProps.Prop(Mod, "_ParallaxStrength"),
                Mathf.Clamp(s.GetFloat("_HeightStrength", 0.01f), -0.1f, 0.1f));
            c.SetFloat(NonToonProps.Prop(Mod, "_ParallaxHeightOffset"),
                Mathf.Clamp(s.GetFloat("_HeightOffset", 0f), -1f, 1f));

            // Poiyomi の可変ステップ (min-max) を固定ステップへ丸める
            int steps = Mathf.Clamp(Mathf.RoundToInt(s.GetFloat("_HeightStepsMax", 128f) / 4f), 4, 32);
            c.SetInt(NonToonProps.Prop(Mod, "_ParallaxSteps"), steps);

            if (s.GetInt("_HeightMapUV", 0) != 0)
                c.Report.Warn(DisplayName, "UV0 以外のハイトマップ UV は非対応です");
            if (s.GetTexture("_Heightmask") != null)
                c.Report.Drop(DisplayName, "ハイトマスクは非対応です");

            c.Report.Approx(DisplayName,
                $"簡易 POM ({steps} ステップ) へ変換しました。視差が逆に見える場合は強度の符号を反転してください。Details 等の後続レイヤーには視差がかかりません");
        }
    }

    /// <summary>グリッター (Poiyomi: Glitter → Glitter モジュール)。</summary>
    public sealed class GlitterConversionModule : ConversionModule
    {
        const string Mod = "jp.rroki.nontoon.glitter";

        public override int Order => 220;
        public override string DisplayName => "グリッター";

        public override bool ShouldRun(ConversionContext c) =>
            c.Source.GetToggle("_GlitterEnable") || c.Source.GetToggle("_GlitterEnabled");

        public override void DeclareRequirements(ConversionContext c) => c.RequireScModule(Mod);

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;
            c.MarkSourceHandled("_GlitterEnable");
            c.MarkSourceHandled("_GlitterEnabled");
            c.SetInt(NonToonProps.Prop(Mod, "_Enable"), 1);

            c.SetColor(NonToonProps.Prop(Mod, "_GlitterColor"), s.GetColor("_GlitterColor", Color.white));
            c.SetFloat(NonToonProps.Prop(Mod, "_GlitterBrightness"), Mathf.Clamp(s.GetFloat("_GlitterBrightness", 1f), 0f, 10f));
            c.SetFloat(NonToonProps.Prop(Mod, "_GlitterFrequency"), Mathf.Clamp(s.GetFloat("_GlitterFrequency", 256f), 4f, 2048f));
            c.SetFloat(NonToonProps.Prop(Mod, "_GlitterSpeed"), Mathf.Clamp(s.GetFloat("_GlitterSpeed", 5f), 0f, 30f));
            c.SetFloat(NonToonProps.Prop(Mod, "_GlitterMultiplyAlbedo"), Mathf.Clamp01(s.GetFloat("_GlitterUseSurfaceColor", 0f)));

            // Poiyomi の Size (セル内の輝点半径) を輝点セル割合へ大雑把に写像
            float size = s.GetFloat("_GlitterSize", 0.01f);
            float density = Mathf.Clamp(size * 2f, 0.005f, 0.5f);
            c.SetFloat(NonToonProps.Prop(Mod, "_GlitterDensity"), density);

            var mask = s.GetTexture("_GlitterMask");
            if (mask != null)
            {
                int ch = c.AllocateMaskChannel(new MaskChannelSource
                {
                    Texture = mask,
                    SourceChannel = 0,
                    Label = "グリッター",
                });
                if (ch >= 0) c.SetInt(NonToonProps.Prop(Mod, "_GlitterMaskChannel"), ch);
            }

            c.Report.Approx(DisplayName, "グリッターを独自実装のスパークルへ近似しました (輝点サイズ/密度は要調整。視線角度による煌めきは未対応)");
        }
    }
}
