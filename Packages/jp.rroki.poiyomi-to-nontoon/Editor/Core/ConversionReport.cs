#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    public enum ConversionSeverity
    {
        /// <summary>そのまま/自動で引き継いだ。</summary>
        Info,
        /// <summary>数式が異なるため近似変換した。見た目の確認推奨。</summary>
        Approximated,
        /// <summary>手動対応が必要な可能性がある。</summary>
        Warning,
        /// <summary>NonToon 側に受け皿が無く破棄した。</summary>
        Dropped,
    }

    /// <summary>1 マテリアル分の変換結果レポート。</summary>
    public sealed class ConversionReport
    {
        public readonly struct Entry
        {
            public ConversionSeverity Severity { get; }
            public string Feature { get; }
            public string Message { get; }

            public Entry(ConversionSeverity severity, string feature, string message)
            {
                Severity = severity;
                Feature = feature;
                Message = message;
            }
        }

        public Material? Source { get; internal set; }
        public Material? Result { get; internal set; }
        public bool Succeeded { get; internal set; } = true;
        public string? FailureReason { get; internal set; }

        readonly List<Entry> _entries = new();
        public IReadOnlyList<Entry> Entries => _entries;

        public void Info(string feature, string message) => Add(ConversionSeverity.Info, feature, message);
        public void Approx(string feature, string message) => Add(ConversionSeverity.Approximated, feature, message);
        public void Warn(string feature, string message) => Add(ConversionSeverity.Warning, feature, message);
        public void Drop(string feature, string message) => Add(ConversionSeverity.Dropped, feature, message);

        public void Add(ConversionSeverity severity, string feature, string message) =>
            _entries.Add(new Entry(severity, feature, message));

        public int CountOf(ConversionSeverity severity) => _entries.Count(e => e.Severity == severity);

        public string ToConsoleString()
        {
            var sb = new StringBuilder();
            var name = Source != null ? Source.name : "(不明)";
            sb.AppendLine($"[Poiyomi→NonToon] {name}: " +
                          (Succeeded ? "変換完了" : $"失敗 ({FailureReason})") +
                          $" — 引継 {CountOf(ConversionSeverity.Info)} / 近似 {CountOf(ConversionSeverity.Approximated)}" +
                          $" / 要確認 {CountOf(ConversionSeverity.Warning)} / 未対応 {CountOf(ConversionSeverity.Dropped)}");
            foreach (var e in _entries)
                sb.AppendLine($"  [{Label(e.Severity)}] {e.Feature}: {e.Message}");
            return sb.ToString();
        }

        public static string Label(ConversionSeverity s) => s switch
        {
            ConversionSeverity.Info => "引継",
            ConversionSeverity.Approximated => "近似",
            ConversionSeverity.Warning => "要確認",
            ConversionSeverity.Dropped => "未対応",
            _ => s.ToString(),
        };
    }

    /// <summary>変換オプション。</summary>
    public sealed class ConversionOptions
    {
        /// <summary>true: 複製した .mat を変換 / false: 元マテリアルを直接変換 (Undo 可)。</summary>
        public bool CreateCopy = true;

        /// <summary>_Color / アルファマスクをメインテクスチャへベイクする。</summary>
        public bool BakeTint = true;

        /// <summary>Poiyomi の影設定をグラデーション (Texture2DArray) へベイクする。</summary>
        public bool BakeShadeGradient = true;

        /// <summary>各機能のマスクを _SharedMask の RGBA チャンネルへパックする。</summary>
        public bool PackMasks = true;

        /// <summary>
        /// 変換後、NonToon シェーダーが宣言していない旧プロパティ (Poiyomi のテクスチャ参照等) を
        /// マテリアルから削除する。無効にするとビルドに旧テクスチャが同梱され容量が膨張する。
        /// </summary>
        public bool RemoveUnusedProperties = true;

        /// <summary>複製時のファイル名サフィックス。</summary>
        public string CopySuffix = "_NT";
    }
}
