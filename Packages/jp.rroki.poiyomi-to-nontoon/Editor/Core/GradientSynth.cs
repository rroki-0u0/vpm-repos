#nullable enable
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// NonToon の _SharedGradients 用グラデーション (128px, 表示色空間) の合成ヘルパー。
    /// x 軸の意味は用途による:
    ///   Shade        … ハーフランバート N・L (0=影側, 1=光側)
    ///   RimShade     … 1 - mask*(1-NdotV)
    ///   HairSpecular … 異方性項 aniso*0.5+0.5 (ハイライト中心 ≈ 0.5)
    /// </summary>
    public static class GradientSynth
    {
        public const int Size = 128;

        public static Color[] Solid(Color color)
        {
            var pixels = new Color[Size];
            for (int i = 0; i < Size; i++) pixels[i] = color;
            return pixels;
        }

        /// <summary>
        /// 影→光のステップ。border を中心に blur 幅で dark → lit へ遷移する。
        /// (Poiyomi の _ShadowBorder / _ShadowBlur と同じ解釈: x &lt; border が影側)
        /// </summary>
        public static Color[] Step(Color dark, Color lit, float border, float blur)
        {
            var pixels = new Color[Size];
            float halfBlur = Mathf.Max(blur * 0.5f, 1f / Size); // 最低 1px はなだらかにする (ランプサンプリングのエイリアス回避)
            for (int i = 0; i < Size; i++)
            {
                float x = i / (float)(Size - 1);
                float t = SmoothStep(border - halfBlur, border + halfBlur, x);
                pixels[i] = Color.Lerp(dark, lit, t);
            }
            return pixels;
        }

        /// <summary>多層影 (Poiyomi Multilayer Math 相当)。layer = (色, 境界, ぼかし, 不透明度)。</summary>
        public static Color[] MultiLayer(params (Color color, float border, float blur, float opacity)[] layers)
        {
            var pixels = Solid(Color.white);
            foreach (var layer in layers)
            {
                if (layer.opacity <= 0f) continue;
                float halfBlur = Mathf.Max(layer.blur * 0.5f, 1f / Size);
                for (int i = 0; i < Size; i++)
                {
                    float x = i / (float)(Size - 1);
                    float inShadow = 1f - SmoothStep(layer.border - halfBlur, layer.border + halfBlur, x);
                    var tint = Color.Lerp(Color.white, layer.color, inShadow * layer.opacity);
                    pixels[i] *= tint;
                    pixels[i].a = 1f;
                }
            }
            return pixels;
        }

        /// <summary>背景色の上にガウス風の帯を乗せる (ヘアハイライト用)。</summary>
        public static Color[] Band(Color background, Color band, float center, float width)
        {
            var pixels = new Color[Size];
            float w = Mathf.Max(width, 2f / Size);
            for (int i = 0; i < Size; i++)
            {
                float x = i / (float)(Size - 1);
                float d = (x - center) / w;
                float t = Mathf.Exp(-d * d * 4f);
                pixels[i] = Color.Lerp(background, band, t);
                pixels[i].a = 1f;
            }
            return pixels;
        }

        /// <summary>白へ向かって弱める (影の強度 strength: 1=そのまま, 0=影なし)。</summary>
        public static Color[] ApplyStrength(Color[] pixels, float strength)
        {
            if (strength >= 1f) return pixels;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.Lerp(Color.white, pixels[i], Mathf.Clamp01(strength));
                pixels[i].a = 1f;
            }
            return pixels;
        }

        /// <summary>全ピクセルにティントを乗算する。</summary>
        public static Color[] Multiply(Color[] pixels, Color tint)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] *= tint;
                pixels[i].a = 1f;
            }
            return pixels;
        }

        /// <summary>
        /// 影ティントを影側加重で乗算する (x=0 でフル適用、x=1 で無効)。
        /// Poiyomi の Shadow Tint は影側にのみ効くため、明部を暗くしない。
        /// </summary>
        public static Color[] ApplyShadowTint(Color[] pixels, Color tint)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                float t = i / (float)(pixels.Length - 1);
                pixels[i] *= Color.Lerp(tint, Color.white, t);
                pixels[i].a = 1f;
            }
            return pixels;
        }

        /// <summary>
        /// 明部 (x=1) が白になるよう全体を一様スケールする (色相は保持)。
        /// NonToon はグラデーションをアルベドに乗算する設計で、環境光側に
        /// 「明部≈白」前提の補正 (env×1.2) があるため、暗いランプをそのまま
        /// 持ち込むと全体が系統的に暗くなる。倍率は maxBoost でクランプ。
        /// 戻り値は適用した倍率 (1 なら無変換)。
        /// </summary>
        public static float NormalizeLitEnd(Color[] pixels, float maxBoost = 3f)
        {
            var lit = pixels[pixels.Length - 1];
            float peak = Mathf.Max(lit.r, Mathf.Max(lit.g, lit.b));
            if (peak >= 0.995f || peak <= 0.0001f) return 1f;
            float factor = Mathf.Min(1f / peak, maxBoost);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].r = Mathf.Min(pixels[i].r * factor, 1f);
                pixels[i].g = Mathf.Min(pixels[i].g * factor, 1f);
                pixels[i].b = Mathf.Min(pixels[i].b * factor, 1f);
                pixels[i].a = 1f;
            }
            return factor;
        }

        /// <summary>ほぼ白 (= 影効果なし) かどうか。</summary>
        public static bool IsNearlyWhite(Color[] pixels)
        {
            foreach (var p in pixels)
                if (p.r < 0.99f || p.g < 0.99f || p.b < 0.99f) return false;
            return true;
        }

        /// <summary>ほぼ黒 (= 加算効果なし) かどうか。</summary>
        public static bool IsNearlyBlack(Color[] pixels)
        {
            foreach (var p in pixels)
                if (p.r > 0.01f || p.g > 0.01f || p.b > 0.01f) return false;
            return true;
        }

        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(edge1 - edge0, 1e-5f));
            return t * t * (3f - 2f * t);
        }
    }
}
