#nullable enable
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rroki.NonToonExtraModules.Tools
{
    /// <summary>首元フェードマスク関連ツール (生成ツール / SDF Baker) の共有ヘルパー。</summary>
    internal static class NeckFadeUtil
    {
        /// <summary>テクスチャを読み取り可能にする (インポーター設定を変更)。</summary>
        public static void EnsureReadable(Texture2D tex)
        {
            var path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return;
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        /// <summary>生バイト (色空間変換なし) で読む。読み取り不可なら readable 化する。</summary>
        public static Color32[] ReadRaw(Texture2D tex, out int width, out int height)
        {
            EnsureReadable(tex);
            width = tex.width;
            height = tex.height;
            return tex.GetPixels32();
        }

        /// <summary>テクスチャの sRGB フラグを返す (インポーター設定。既定 true)。</summary>
        public static bool IsSRGB(Texture2D tex)
        {
            var path = AssetDatabase.GetAssetPath(tex);
            return AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.sRGBTexture;
        }

        /// <summary>Color32 配列を PNG アセットとして保存し、読み込んだ Texture2D を返す。</summary>
        public static Texture2D SavePng(Color32[] pixels, int width, int height, string assetPath, bool sRGB)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, !sRGB);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath);

            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.sRGBTexture = sRGB;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.isReadable = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        /// <summary>マスク Color32[] を UV でバイリニアサンプル (0-1)。channel: 0=R,1=G,2=B,3=A。</summary>
        public static float SampleBilinear(Color32[] px, int w, int h, float u, float v, int channel)
        {
            float fx = Mathf.Repeat(u, 1f) * w - 0.5f;
            float fy = Mathf.Repeat(v, 1f) * h - 0.5f;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            float c00 = Channel(px, w, h, x0, y0, channel);
            float c10 = Channel(px, w, h, x0 + 1, y0, channel);
            float c01 = Channel(px, w, h, x0, y0 + 1, channel);
            float c11 = Channel(px, w, h, x0 + 1, y0 + 1, channel);
            return Mathf.Lerp(Mathf.Lerp(c00, c10, tx), Mathf.Lerp(c01, c11, tx), ty);
        }

        static float Channel(Color32[] px, int w, int h, int x, int y, int channel)
        {
            x = Mathf.Clamp(x, 0, w - 1);
            y = Mathf.Clamp(y, 0, h - 1);
            var c = px[y * w + x];
            byte b = channel == 0 ? c.r : channel == 1 ? c.g : channel == 2 ? c.b : c.a;
            return b / 255f;
        }
    }
}
