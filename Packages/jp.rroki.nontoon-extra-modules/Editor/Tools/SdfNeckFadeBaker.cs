#nullable enable
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rroki.NonToonExtraModules.Tools
{
    /// <summary>
    /// SDF Neck Fade Baker: NonToon の Shade (SDF) が使う SDF マップの B チャンネル
    /// (SDF ↔ 幾何 NdotL のブレンド量) へ、首元フェードマスクを焼き込む。
    /// B を首に向けて 1 (= 幾何 NdotL) へ寄せることで、顔 SDF と体 NdotL の境界を連続化する。
    /// シェーダー変更不要・ランタイムコスト 0 で NonToon 標準機構をそのまま使う。
    /// </summary>
    public sealed class SdfNeckFadeBaker : EditorWindow
    {
        enum BlendMode { Max, LerpToNdotL, Add }

        [SerializeField] Texture2D? _sdfMap;
        [SerializeField] Texture2D? _fadeMask;
        [SerializeField] int _maskChannel;          // 0=R,1=G,2=B,3=A
        [SerializeField] BlendMode _blend = BlendMode.Max;
        [SerializeField, Range(0f, 1f)] float _strength = 1f;

        static readonly string[] ChannelLabels = { "R", "G", "B", "A" };

        [MenuItem("Tools/rroki/NonToon/SDF Neck Fade Baker")]
        public static void Open() => GetWindow<SdfNeckFadeBaker>("SDF Neck Fade Baker");

        /// <summary>マスク生成ツールから、生成したマスクを渡して開く導線。</summary>
        public static void OpenWithMask(Texture2D mask)
        {
            var window = GetWindow<SdfNeckFadeBaker>("SDF Neck Fade Baker");
            window._fadeMask = mask;
            window.Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "顔の SDF マップの B チャンネル (SDF↔幾何NdotL ブレンド) へ、首元フェードマスクを焼き込みます。" +
                "B を首へ向けて 1 に寄せると、体側の NdotL と滑らかに繋がります。",
                MessageType.Info);

            _sdfMap = (Texture2D?)EditorGUILayout.ObjectField("SDF マップ", _sdfMap, typeof(Texture2D), false);
            _fadeMask = (Texture2D?)EditorGUILayout.ObjectField("フェードマスク", _fadeMask, typeof(Texture2D), false);
            _maskChannel = EditorGUILayout.Popup("マスクのチャンネル", _maskChannel, ChannelLabels);
            _blend = (BlendMode)EditorGUILayout.EnumPopup("ブレンド", _blend);
            EditorGUILayout.LabelField(BlendHint(_blend), EditorStyles.miniLabel);
            _strength = EditorGUILayout.Slider("強度", _strength, 0f, 1f);

            using (new EditorGUI.DisabledScope(_sdfMap == null || _fadeMask == null))
            {
                if (GUILayout.Button("B チャンネルへ焼き込んで保存"))
                    Bake();
            }
        }

        static string BlendHint(BlendMode b) => b switch
        {
            BlendMode.Max => "Max: max(元のB, フェード)。既存の SDF 領域を保ちつつ首だけ NdotL へ (推奨)",
            BlendMode.LerpToNdotL => "Lerp: 元のB を フェード量ぶん 1 (幾何NdotL) へ補間",
            _ => "Add: 元のB + フェード (加算)",
        };

        void Bake()
        {
            var sdf = _sdfMap!;
            var mask = _fadeMask!;

            var sdfPx = NeckFadeUtil.ReadRaw(sdf, out int sw, out int sh);
            var maskPx = NeckFadeUtil.ReadRaw(mask, out int mw, out int mh);
            var outPx = new Color32[sdfPx.Length];

            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    int i = y * sw + x;
                    var c = sdfPx[i];
                    float u = (x + 0.5f) / sw;
                    float v = (y + 0.5f) / sh;
                    float fade = NeckFadeUtil.SampleBilinear(maskPx, mw, mh, u, v, _maskChannel) * _strength;
                    float b = c.b / 255f;
                    float nb = _blend switch
                    {
                        BlendMode.Max => Mathf.Max(b, fade),
                        BlendMode.Add => b + fade,
                        _ => Mathf.Lerp(b, 1f, fade),
                    };
                    c.b = (byte)Mathf.RoundToInt(Mathf.Clamp01(nb) * 255f);
                    outPx[i] = c;
                }
            }

            var sdfPath = AssetDatabase.GetAssetPath(sdf);
            var outPath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(Path.GetDirectoryName(sdfPath)!, Path.GetFileNameWithoutExtension(sdfPath) + "_NeckFade.png")
                    .Replace('\\', '/'));

            var outTex = NeckFadeUtil.SavePng(outPx, sw, sh, outPath, NeckFadeUtil.IsSRGB(sdf));
            Selection.activeObject = outTex;
            EditorGUIUtility.PingObject(outTex);
            Debug.Log($"[SDF Neck Fade Baker] 焼き込んだ SDF マップを保存しました: {outPath}", outTex);
        }
    }
}
