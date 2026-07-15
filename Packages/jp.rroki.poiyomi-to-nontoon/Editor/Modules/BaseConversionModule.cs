#nullable enable
using UnityEditor;
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// ベース: レンダリングモード / メインテクスチャ (+ティント・アルファマスクのベイク) /
    /// ノーマルマップ / カットアウト / ステンシル / カリング。
    /// </summary>
    public sealed class BaseConversionModule : ConversionModule
    {
        public override int Order => 0;
        public override string DisplayName => "ベース";

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;

            ConvertRenderingMode(c);
            ConvertBaseTexture(c);

            // ---- ノーマルマップ ----
            var bump = s.GetTexture("_BumpMap");
            if (bump != null)
            {
                c.SetTexture(NonToonProps.NormalMap, bump);
                c.SetFloat(NonToonProps.NormalScale, Mathf.Clamp(s.GetFloat("_BumpScale", 1f), -10f, 10f));
                if (s.HasNonDefaultST("_BumpMap"))
                    c.Report.Warn("ノーマルマップ", "タイリング/オフセットは NonToon 非対応のため無視されました");
            }
            c.SetInt(NonToonProps.NormalMapWithRoughness, 0);
            c.SetFloat(NonToonProps.Roughness, 0.5f); // Specular モジュールが必要に応じて上書き

            // ---- ステンシル ----
            int stencilRef = s.GetInt("_StencilRef", 0);
            int stencilComp = s.GetInt("_StencilCompareFunction", 8);
            int stencilPass = s.GetInt("_StencilPassOp", 0);
            if (stencilRef != 0 || stencilComp != 8 || stencilPass != 0)
            {
                c.SetInt(NonToonProps.StencilRef, stencilRef);
                c.SetInt(NonToonProps.StencilComp, stencilComp);
                c.SetInt(NonToonProps.StencilPass, stencilPass);
                c.Report.Info("ステンシル", $"Ref={stencilRef}, Comp={stencilComp}, Pass={stencilPass} を引き継ぎました");

                if (s.GetInt("_StencilWriteMask", 255) != 255 || s.GetInt("_StencilReadMask", 255) != 255)
                    c.Report.Drop("ステンシル", "Read/WriteMask は NonToon 非対応です");
                if (s.GetInt("_StencilZFailOp", 0) != 0)
                    c.Report.Drop("ステンシル", "ZFailOp は NonToon 非対応です");
                if (s.GetInt("_StencilType", 0) != 0)
                    c.Report.Drop("ステンシル", "表裏別ステンシルは NonToon 非対応です");
            }

            // ---- カリング ---- (NonToonFur は Cull Off 固定でプロパティ無し)
            if (s.HasFloat("_Cull") && c.HasTargetProperty(NonToonProps.Cull))
                c.SetInt(NonToonProps.Cull, Mathf.Clamp(s.GetInt("_Cull", 2), 0, 2));
        }

        /// <summary>
        /// Poiyomi の実効レンダリング状態 (_SrcBlend/_DstBlend/_AlphaForceOpaque/queue) から
        /// NonToon の 3 モード (0=Opaque 1=Cutout 2=Transparent) を決定して適用する。
        /// _Mode (レンダリングプリセット) はロック後に信頼できないため参照しない。
        /// NonToon 側の副作用 (ブレンド/キュー/AlphaToMask) は NTRenderingModeElement と同じ値を書く。
        /// </summary>
        void ConvertRenderingMode(ConversionContext c)
        {
            // NonToonFur は常に不透明 + AlphaToMask 固定でレンダリングモード系プロパティを持たない
            if (!c.HasTargetProperty(NonToonProps.RenderingMode))
            {
                if (c.HasTargetProperty(NonToonProps.Cutoff))
                    c.SetFloat(NonToonProps.Cutoff, c.Source.GetFloat("_Cutoff", 0.5f));
                return;
            }

            var s = c.Source;
            float src = s.GetFloat("_SrcBlend", 1f);
            float dst = s.GetFloat("_DstBlend", 0f);
            bool blended = !(Mathf.Approximately(src, 1f) && Mathf.Approximately(dst, 0f));
            bool forceOpaque = s.GetToggle("_AlphaForceOpaque", true);

            int mode;
            if (blended) mode = 2;
            else if (forceOpaque && (s.RawRenderQueue < 0 || s.RawRenderQueue < 2450)) mode = 0;
            else mode = 1;

            // ディザ透過を使っていた場合は NonToon のディザテクスチャを割り当てる
            bool useDither = s.GetToggle("_AlphaDithering");
            if (useDither)
            {
                var dither = AssetDatabase.LoadAssetAtPath<Texture2D>(NonToonProps.DitherTexturePath);
                if (dither != null)
                {
                    c.SetTexture(NonToonProps.DitherTex, dither);
                    c.Report.Info("ディザ", "ディザ透過を NonToon のディザテクスチャに置き換えました");
                }
            }

            switch (mode)
            {
                case 0:
                    c.SetInt(NonToonProps.RenderingMode, 0);
                    c.SetInt(NonToonProps.SrcBlend, 1);
                    c.SetInt(NonToonProps.DstBlend, 0);
                    c.SetInt(NonToonProps.AlphaToMask, 0);
                    c.Target.renderQueue = -1;
                    c.Report.Info("レンダリング", "不透明 (Opaque) として変換しました");
                    break;
                case 1:
                    c.SetInt(NonToonProps.RenderingMode, 1);
                    c.SetInt(NonToonProps.SrcBlend, 1);
                    c.SetInt(NonToonProps.DstBlend, 0);
                    c.SetInt(NonToonProps.AlphaToMask, useDither ? 0 : 1);
                    c.Target.renderQueue = 2450;
                    c.SetFloat(NonToonProps.Cutoff, s.GetFloat("_Cutoff", 0.5f));
                    c.Report.Info("レンダリング", "カットアウト (Cutout) として変換しました");
                    break;
                case 2:
                    c.SetInt(NonToonProps.RenderingMode, 2);
                    c.SetInt(NonToonProps.SrcBlend, 5);   // SrcAlpha
                    c.SetInt(NonToonProps.DstBlend, 10);  // OneMinusSrcAlpha
                    c.SetInt(NonToonProps.AlphaToMask, 0);
                    c.Target.renderQueue = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? 3000 : 2460;
                    c.SetFloat(NonToonProps.Cutoff, s.GetFloat("_Cutoff", 0f));
                    c.Report.Info("レンダリング", "半透明 (Transparent) として変換しました");

                    if (!Mathf.Approximately(src, 5f) || !Mathf.Approximately(dst, 10f))
                    {
                        if (Mathf.Approximately(src, 1f) && Mathf.Approximately(dst, 10f) && s.GetToggle("_AlphaPremultiply"))
                            c.Report.Approx("レンダリング", "乗算済みアルファ (Premultiply) をストレートアルファ合成に置き換えました");
                        else
                            c.Report.Approx("レンダリング", $"特殊ブレンド (Src={src}, Dst={dst}: 加算/乗算等) は通常の半透明合成に置き換えました");
                    }
                    if (s.GetFloat("_ZWrite", 1f) < 0.5f)
                        c.Report.Info("レンダリング", "Poiyomi では ZWrite Off でしたが、NonToon の標準に合わせ ZWrite On のままにしています (queue 2460)");
                    break;
            }

            if (s.RawRenderQueue >= 0 && mode == 2 && s.RawRenderQueue != 3000 && s.RawRenderQueue != 2460)
                c.Report.Warn("レンダリング", $"カスタムレンダーキュー ({s.RawRenderQueue}) は NonToon 標準値に置き換えました。描画順が変わる場合は手動調整してください");
        }

        /// <summary>
        /// メインテクスチャ。_Color ティントやアルファマスクが使われている場合は
        /// オプションに応じて新規テクスチャへベイクする (NonToon にはティント/ST が無い)。
        /// </summary>
        void ConvertBaseTexture(ConversionContext c)
        {
            var s = c.Source;
            var mainTex = s.GetTexture("_MainTex");
            var tint = s.GetColor("_Color", Color.white);

            // アルファマスク: Replace(1) / Multiply(2) のみ対応
            int alphaMaskMode = s.GetInt("_MainAlphaMaskMode", 0);
            var alphaMask = s.GetTexture("_AlphaMask");
            bool useAlphaMask = alphaMaskMode > 0 && alphaMask != null;

            bool needsBake = tint != Color.white || useAlphaMask;

            if (s.HasNonDefaultST("_MainTex"))
                c.Report.Warn("メインテクスチャ", $"タイリング/オフセット ({s.GetTextureScale("_MainTex")}, {s.GetTextureOffset("_MainTex")}) は NonToon 非対応のため無視されました");
            if (s.GetInt("_MainTexUV", 0) != 0)
                c.Report.Warn("メインテクスチャ", "UV0 以外の UV 設定は NonToon 非対応です");

            if (mainTex == null)
            {
                if (tint != Color.white)
                    c.Report.Warn("メインカラー", "テクスチャ未割り当てのため、_Color の引き継ぎには単色テクスチャの作成が必要です (未対応)");
                return;
            }

            if (!needsBake || !c.Options.BakeTint)
            {
                c.SetTexture(NonToonProps.BaseTexture, mainTex);
                if (needsBake)
                    c.Report.Warn("メインカラー", "ティント/アルファマスクが設定されていますが、ベイクオプションが無効のため色が変わります");
                return;
            }

            // ---- ベイク実行 ----
            int w = Mathf.Clamp(mainTex.width, 4, 4096);
            int h = Mathf.Clamp(mainTex.height, 4, 4096);

            System.Func<int, int, float>? alphaFunc = null;
            if (useAlphaMask)
            {
                var weights = new Vector4(
                    s.GetFloat("_AlphaMaskR", 1f), s.GetFloat("_AlphaMaskG", 0f),
                    s.GetFloat("_AlphaMaskB", 0f), s.GetFloat("_AlphaMaskA", 0f));
                bool invert = s.GetToggle("_AlphaMaskInvert");
                var maskPixels = TextureBaker.ReadbackLinear(alphaMask!, w, h);
                bool replace = alphaMaskMode == 1;
                var basePixels = replace ? TextureBaker.ReadbackLinear(mainTex, w, h) : null;

                alphaFunc = (x, y) =>
                {
                    var m = maskPixels[y * w + x];
                    float v = Mathf.Clamp01(m.r * weights.x + m.g * weights.y + m.b * weights.z + m.a * weights.w);
                    if (invert) v = 1f - v;
                    if (replace)
                    {
                        // Replace: 元のアルファを打ち消してマスク値へ置き換える
                        float baseA = basePixels![y * w + x].a;
                        return baseA > 1e-4f ? v / baseA : v;
                    }
                    return v; // Multiply
                };

                if (alphaMaskMode > 2)
                    c.Report.Approx("アルファマスク", $"マスクモード {alphaMaskMode} (Add/Subtract) は乗算として近似しました");
                else
                    c.Report.Info("アルファマスク", "アルファマスクをメインテクスチャへベイクしました");
            }

            var tintLinear = tint.linear;
            var baked = TextureBaker.BakeTintedTexture(
                mainTex, tintLinear, alphaFunc, w, h, $"{c.AssetBasePath}_base.png");
            c.SetTexture(NonToonProps.BaseTexture, baked);

            if (tint != Color.white)
                c.Report.Info("メインカラー", $"_Color ({ColorUtility.ToHtmlStringRGBA(tint)}) をテクスチャへベイクしました");
        }
    }
}
