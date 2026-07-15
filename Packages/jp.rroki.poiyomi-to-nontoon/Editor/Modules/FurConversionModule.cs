#nullable enable
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// ファー (Poiyomi Fur シェーダー → NonToonFur シェル法ファー)。
    /// ターゲットが NonToonFur のときのみ動作する (シェーダー選択は Converter が行う)。
    /// NonToonFur は NonToon 本体のシェル/フィンファーであり、モジュールではなくシェーダーごと
    /// 差し替えて再現する。length/subdivision は単位差があるため近似 (要手動調整)。
    /// </summary>
    public sealed class FurConversionModule : ConversionModule
    {
        public override int Order => 20;
        public override string DisplayName => "ファー";

        public override bool ShouldRun(ConversionContext c) =>
            c.Target.shader != null && c.Target.shader.name == NonToonProps.FurShaderName;

        public override void Convert(ConversionContext c)
        {
            var s = c.Source;

            // ---- 長さ (法線方向) + 重力 (下方向バイアス) ----
            // Poiyomi の _FurLength は独自単位のため、cm 想定で世界メートルへ概算する
            float length = Mathf.Clamp(s.GetFloat("_FurLength", 1f) * 0.01f, 0.001f, 0.1f);
            float gravity = Mathf.Clamp01(s.GetFloat("_FurGravity", 0f) * s.GetFloat("_FurGravityStrength", 1f));
            c.SetVector(NonToonProps.FurVector, new Vector4(0f, -gravity * length, length, 0f));

            // ---- ノイズマスク (毛の隙間) + タイリング ----
            var noise = s.GetTexture("_FurNoiseMask");
            if (noise != null) c.SetTexture(NonToonProps.FurNoiseMask, noise);
            float tiling = s.GetTextureScale("_FurNoiseMask").x;
            c.SetFloat(NonToonProps.FurNoiseTiling, tiling > 0.01f ? tiling : 64f);

            // ---- シェル数 → subdivision (1-3) ----
            int layers = s.GetInt("_FurLayers", s.GetInt("_FurLayerCount", 20));
            c.SetInt(NonToonProps.FurSubdivision, Mathf.Clamp(Mathf.RoundToInt(layers / 12f), 1, 3));

            if (s.GetColor("_FurColor", Color.white) != Color.white)
                c.Report.Approx(DisplayName, "ファーカラー (毛の色) はメインテクスチャ (base) 側の色で描画されます。必要ならベーステクスチャで調整してください");
            if (s.GetToggle("_FurWindEnabled"))
                c.Report.Drop(DisplayName, "ファーの風 (Wind) アニメーションは非対応です");
            if (s.GetTexture("_FurLengthMask") != null)
                c.Report.Drop(DisplayName, "長さマスク (部位ごとの毛の長さ) は非対応です (ノイズマスクで代用してください)");

            c.Report.Approx(DisplayName,
                $"NonToonFur (シェル法) へ変換しました (長さ {length:F3}m / subdivision {Mathf.Clamp(Mathf.RoundToInt(layers / 12f), 1, 3)})。" +
                "長さ・密度は見た目を見ながら NonToonFur マテリアルの Fur 設定で調整してください");
        }
    }
}
