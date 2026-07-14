#nullable enable
namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// 変換モジュールの基底クラス。Poiyomi の 1 機能グループを NonToon 側へ引き継ぐ責務を持つ。
    ///
    /// 非 abstract な派生クラスは TypeCache で自動発見・登録される。
    /// 将来、NonToon に存在しない Poiyomi 機能を Shader Core モジュール (.scmodule) として
    /// 追加実装する場合は、そのパッケージ内で本クラスを派生させるだけで
    /// 変換パイプラインに組み込まれる (本体パッケージの改変は不要):
    ///
    /// 1. シェーダー側: .scmodule + properties.hlsl + phase_*.hlsl を配布パッケージに含める
    /// 2. 変換側: ConversionModule を派生し、ctx.RequireScModule("your.module.id") を宣言した上で
    ///    NonToonProps.Prop("your.module.id", "_YourProp") へ値を書き込む
    /// </summary>
    public abstract class ConversionModule
    {
        /// <summary>実行順。小さいほど先に実行される (組み込みは 0-1000)。</summary>
        public abstract int Order { get; }

        /// <summary>レポート・UI 表示名。</summary>
        public abstract string DisplayName { get; }

        /// <summary>このモジュールが変換に関与するか (ソース側の機能が有効かどうか等)。</summary>
        public virtual bool ShouldRun(ConversionContext context) => true;

        /// <summary>
        /// 必要な Shader Core モジュールを事前宣言する (context.RequireScModule を呼ぶ)。
        /// Convert より前にまとめて実行され、不足モジュールの有効化とシェーダー再生成が
        /// 済んだ状態で Convert が呼ばれる。ここで宣言しないと、初回変換時にモジュール由来の
        /// プロパティがまだシェーダーに存在せず書き込みがスキップされる。
        /// </summary>
        public virtual void DeclareRequirements(ConversionContext context) { }

        /// <summary>変換を実行する。</summary>
        public abstract void Convert(ConversionContext context);
    }
}
