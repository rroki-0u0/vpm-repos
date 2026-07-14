#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// 変換 1 回分のコンテキスト。変換モジュール (ConversionModule) はこれを通じて
    /// ソース値の読み取り・ターゲットへの書き込み・グラデーション/マスクの割り当て・
    /// レポート記録を行う。
    /// </summary>
    public sealed class ConversionContext
    {
        public PoiyomiMaterialSnapshot Source { get; }
        public Material Target { get; }
        public ConversionOptions Options { get; }
        public ConversionReport Report { get; }

        /// <summary>生成アセットの保存先ベースパス (拡張子なし)。例: Assets/.../m_Hair_NT</summary>
        public string AssetBasePath { get; }

        readonly List<Color[]> _gradientLayers = new();
        readonly MaskChannelSource?[] _maskChannels = new MaskChannelSource?[4];
        readonly HashSet<string> _requiredScModules = new();
        readonly HashSet<string> _handledSourceProps = new();

        internal IReadOnlyList<Color[]> GradientLayers => _gradientLayers;
        internal MaskChannelSource?[] MaskChannels => _maskChannels;
        internal IReadOnlyCollection<string> RequiredScModules => _requiredScModules;

        internal ConversionContext(
            PoiyomiMaterialSnapshot source, Material target,
            ConversionOptions options, ConversionReport report, string assetBasePath)
        {
            Source = source;
            Target = target;
            Options = options;
            Report = report;
            AssetBasePath = assetBasePath;
        }

        // ---- ターゲット書き込み (存在チェック + Integer/レガシー Int の自動判別) ----

        public bool HasTargetProperty(string name) =>
            Target.shader != null && Target.shader.FindPropertyIndex(name) >= 0;

        /// <summary>
        /// int 系プロパティを設定する。ShaderLab の Integer 型 (SC_uint/SC_int 由来) は SetInteger、
        /// レガシー Int 型 (.scshader 直書きのステンシル/ブレンド等) は float ベースなので SetFloat を使う。
        /// [SCConstValue] 属性が付いたプロパティはキーワード (NAME_値) も同期する。
        /// </summary>
        public bool SetInt(string name, int value)
        {
            var shader = Target.shader;
            int index = shader.FindPropertyIndex(name);
            if (index < 0) return Missing(name);

            if (shader.GetPropertyType(index) == UnityEngine.Rendering.ShaderPropertyType.Int)
                Target.SetInteger(name, value);
            else
                Target.SetFloat(name, value);

            SyncConstValueKeywords(shader, index, name, value);
            return true;
        }

        public bool SetFloat(string name, float value)
        {
            if (!HasTargetProperty(name)) return Missing(name);
            Target.SetFloat(name, value);
            return true;
        }

        public bool SetColor(string name, Color value)
        {
            if (!HasTargetProperty(name)) return Missing(name);
            Target.SetColor(name, value);
            return true;
        }

        public bool SetVector(string name, Vector4 value)
        {
            if (!HasTargetProperty(name)) return Missing(name);
            Target.SetVector(name, value);
            return true;
        }

        public bool SetTexture(string name, Texture? value)
        {
            if (!HasTargetProperty(name)) return Missing(name);
            Target.SetTexture(name, value);
            return true;
        }

        public bool SetTextureST(string name, Vector2 scale, Vector2 offset)
        {
            if (!HasTargetProperty(name)) return Missing(name);
            Target.SetTextureScale(name, scale);
            Target.SetTextureOffset(name, offset);
            return true;
        }

        bool Missing(string name)
        {
            Report.Warn("プロパティ", $"NonToon 側にプロパティ {name} が見つかりません (NonToon のバージョン差異の可能性)");
            return false;
        }

        static readonly Regex RegConstValue = new(@"^SCConstValue\((\d+)", RegexOptions.Compiled);

        /// <summary>
        /// [SCConstValue(max,...)] プロパティは値がシェーダーキーワード
        /// (プロパティ名大文字 + _値) にコンパイルされるため、値と一緒に同期する。
        /// Shader Core の CustomAttributes.SCConstValue と同じ挙動。
        /// </summary>
        void SyncConstValueKeywords(Shader shader, int index, string name, int value)
        {
            foreach (var attr in shader.GetPropertyAttributes(index))
            {
                var m = RegConstValue.Match(attr);
                if (!m.Success) continue;
                int max = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                for (int i = 0; i <= max; i++)
                {
                    var keyword = NonToonProps.ConstValueKeyword(name, i);
                    if (i == value) Target.EnableKeyword(keyword);
                    else Target.DisableKeyword(keyword);
                }
                return;
            }
        }

        // ---- グラデーション割り当て ----

        /// <summary>
        /// 128px のグラデーション (表示色空間) を登録し、_SharedGradients のレイヤー番号を返す。
        /// 変換の最後に Texture2DArray へまとめて書き出される。
        /// </summary>
        public int AllocateGradient(Color[] pixels128)
        {
            if (pixels128.Length != 128)
                throw new ArgumentException("gradient must be 128px", nameof(pixels128));
            _gradientLayers.Add(pixels128);
            return _gradientLayers.Count - 1;
        }

        // ---- マスクチャンネル割り当て ----

        /// <summary>
        /// マスクを _SharedMask の空きチャンネルへ割り当て、チャンネル番号 (0=R..3=A) を返す。
        /// 空きが無い場合は -1 を返しレポートに記録する。
        /// A チャンネルは全機能の既定参照先なので最後に使う (R→G→B→A の順で割り当て)。
        /// </summary>
        public int AllocateMaskChannel(MaskChannelSource source)
        {
            if (!Options.PackMasks)
            {
                Report.Warn(source.Label, "マスクのパックが無効のため、マスクを引き継ぎませんでした");
                return -1;
            }
            for (int ch = 0; ch < 4; ch++)
            {
                if (_maskChannels[ch] != null) continue;
                _maskChannels[ch] = source;
                if (ch == 3)
                    Report.Warn(source.Label,
                        "A チャンネルへマスクを割り当てました。A は各機能の既定参照先のため、" +
                        "マスク未設定の機能 (バックライト等) にもこのマスクがかかります。問題があればチャンネル設定を確認してください");
                return ch;
            }
            Report.Warn(source.Label, "_SharedMask の空きチャンネルが無いため、マスクを破棄しました (最大4)");
            return -1;
        }

        // ---- Shader Core モジュール要求 ----

        /// <summary>
        /// 変換結果が依存する Shader Core モジュール (scmodule) を宣言する。
        /// 変換の最後にプロジェクト設定 (ProjectSettings/jp.lilxyzw.shadercore.asset) で
        /// 有効かどうか検証される。
        /// </summary>
        public void RequireScModule(string uniqueId) => _requiredScModules.Add(uniqueId);

        // ---- 変換済み機能の宣言 ----

        /// <summary>
        /// Poiyomi 側の機能 (有効トグルプロパティ名) を変換済みとして宣言する。
        /// 拡張モジュールが処理した機能に対して UnsupportedFeaturesModule が
        /// 重複して「未対応」を報告しないようにするための仕組み。
        /// </summary>
        public void MarkSourceHandled(string enableProp) => _handledSourceProps.Add(enableProp);

        public bool IsSourceHandled(string enableProp) => _handledSourceProps.Contains(enableProp);
    }
}
