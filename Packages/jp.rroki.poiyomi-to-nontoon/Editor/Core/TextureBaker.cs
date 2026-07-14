#nullable enable
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// テクスチャ生成ユーティリティ。
    /// 元テクスチャが Read/Write 無効でも動くよう、すべて RenderTexture 経由で読み戻す。
    /// (拡張パッケージの ConversionModule からも利用できるよう public)
    /// </summary>
    public static class TextureBaker
    {
        /// <summary>
        /// テクスチャをリニア空間の Color 配列として読み戻す (sRGB テクスチャはリニア化される)。
        /// </summary>
        public static Color[] ReadbackLinear(Texture source, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                var tex = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply(false, false);
                var pixels = tex.GetPixels();
                UnityEngine.Object.DestroyImmediate(tex);
                return pixels;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>
        /// メインテクスチャに色 (リニア) とアルファ値を乗算した PNG を生成して保存する。
        /// alphaMultiplier: ピクセルごとのアルファ乗算値 (null なら乗算しない)。
        /// 戻り値は importer 設定済みの Texture2D アセット。
        /// </summary>
        public static Texture2D BakeTintedTexture(
            Texture source, Color linearTint, Func<int, int, float>? alphaMultiplier,
            int width, int height, string assetPath)
        {
            var pixels = ReadbackLinear(source, width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    var p = pixels[i];
                    p.r *= linearTint.r;
                    p.g *= linearTint.g;
                    p.b *= linearTint.b;
                    p.a *= linearTint.a;
                    if (alphaMultiplier != null) p.a *= alphaMultiplier(x, y);
                    pixels[i] = p;
                }
            }
            return SavePng(pixels, width, height, assetPath, sRGB: true);
        }

        /// <summary>
        /// リニア空間の Color 配列を PNG として保存し、TextureImporter を設定して読み込む。
        /// </summary>
        public static Texture2D SavePng(Color[] linearPixels, int width, int height, string assetPath, bool sRGB)
        {
            // Texture2D(linear:false) の SetPixels は渡した値をそのまま格納する。
            // sRGB として保存したい場合はガンマ空間へ変換してから書き込む。
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            if (sRGB)
            {
                var display = new Color[linearPixels.Length];
                for (int i = 0; i < linearPixels.Length; i++)
                {
                    var g = linearPixels[i].gamma;
                    g.a = linearPixels[i].a; // アルファは色空間変換しない
                    display[i] = g;
                }
                tex.SetPixels(display);
            }
            else
            {
                tex.SetPixels(linearPixels);
            }
            tex.Apply(false, false);
            var png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            File.WriteAllBytes(assetPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.sRGBTexture = sRGB;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        /// <summary>
        /// グラデーション列 (各 128px・表示色空間) から NonToon の _SharedGradients 互換
        /// Texture2DArray (128x1xN, RGBA32, sRGB) を生成して .asset 保存する。
        /// jp.lilxyzw.shadercore の GradientsImporter が生成するものと同じレイアウト。
        /// </summary>
        public static Texture2DArray SaveGradientArray(Color[][] layers, string assetPath)
        {
            const int Size = 128;
            var array = new Texture2DArray(Size, 1, layers.Length, TextureFormat.RGBA32, true, false);
            array.name = Path.GetFileNameWithoutExtension(assetPath);
            for (int layer = 0; layer < layers.Length; layer++)
            {
                var pixels = layers[layer];
                if (pixels.Length != Size) throw new ArgumentException($"gradient layer {layer} must be {Size}px");
                array.SetPixels(pixels, layer, 0);
            }
            array.Apply(true, false);
            array.wrapMode = TextureWrapMode.Clamp;

            var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(array, existing);
                UnityEngine.Object.DestroyImmediate(array);
                AssetDatabase.SaveAssets();
                return existing;
            }
            AssetDatabase.CreateAsset(array, assetPath);
            return array;
        }

        /// <summary>
        /// テクスチャの 1 行を横 width ピクセルにリサンプルして表示色空間で返す (ランプ読み取り用)。
        /// </summary>
        public static Color[] ResampleRowToDisplay(Texture source, int width, float rowV = 0.5f)
        {
            int srcW = Mathf.Max(source.width, width);
            var linear = ReadbackLinear(source, srcW, Mathf.Max(source.height, 1));
            int y = Mathf.Clamp(Mathf.RoundToInt(rowV * (Mathf.Max(source.height, 1) - 1)), 0, Mathf.Max(source.height, 1) - 1);
            var result = new Color[width];
            for (int x = 0; x < width; x++)
            {
                int sx = Mathf.Clamp(Mathf.RoundToInt((float)x / (width - 1) * (srcW - 1)), 0, srcW - 1);
                var c = linear[y * srcW + sx].gamma;
                c.a = 1f;
                result[x] = c;
            }
            return result;
        }

        /// <summary>
        /// マスクパック: 最大 4 枚のソース (テクスチャのチャンネル or 定数) を RGBA へ合成した
        /// リニア PNG を生成する。未使用チャンネルは白 (1) で埋める — NonToon は全機能が
        /// 既定で A チャンネルを読むため、余計なマスクがかからないようにする。
        /// </summary>
        public static Texture2D PackMask(MaskChannelSource?[] channels, int width, int height, string assetPath)
        {
            if (channels.Length != 4) throw new ArgumentException("channels must be RGBA (4)");
            var result = new Color[width * height];
            for (int i = 0; i < result.Length; i++) result[i] = Color.white;

            for (int ch = 0; ch < 4; ch++)
            {
                var src = channels[ch];
                if (src == null) continue;
                if (src.Texture == null)
                {
                    float v = Mathf.Clamp01(src.Constant);
                    for (int i = 0; i < result.Length; i++) SetChannel(ref result[i], ch, v);
                }
                else
                {
                    var pixels = ReadbackLinear(src.Texture, width, height);
                    for (int i = 0; i < result.Length; i++)
                    {
                        float v = GetChannel(pixels[i], src.SourceChannel);
                        if (src.Invert) v = 1f - v;
                        SetChannel(ref result[i], ch, Mathf.Clamp01(v));
                    }
                }
            }
            return SavePng(result, width, height, assetPath, sRGB: false);

            static float GetChannel(Color c, int ch) => ch switch { 0 => c.r, 1 => c.g, 2 => c.b, 3 => c.a, _ => 1f };
            static void SetChannel(ref Color c, int ch, float v)
            {
                switch (ch) { case 0: c.r = v; break; case 1: c.g = v; break; case 2: c.b = v; break; case 3: c.a = v; break; }
            }
        }
    }

    /// <summary>パック対象 1 チャンネル分の入力。Texture が null なら Constant で塗り潰し。</summary>
    public sealed class MaskChannelSource
    {
        public Texture? Texture;
        /// <summary>ソーステクスチャのどのチャンネルを読むか (0=R..3=A)。</summary>
        public int SourceChannel;
        public bool Invert;
        public float Constant = 1f;
        /// <summary>レポート表示用の機能名。</summary>
        public string Label = "";
    }
}
