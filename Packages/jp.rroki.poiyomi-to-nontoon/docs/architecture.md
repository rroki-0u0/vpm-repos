# アーキテクチャ

## 全体構成

```
PoiyomiToNonToonConverter (オーケストレーター)
 ├─ PoiyomiMaterialSnapshot   … m_SavedProperties の読み取り (ロック非依存)
 ├─ ConversionContext         … モジュールへ渡す文脈
 │   ├─ グラデーションアロケーター (→ Texture2DArray 生成)
 │   ├─ マスクチャンネルアロケーター (→ RGBA パック PNG 生成)
 │   ├─ SetInt/SetFloat/... ([SCConstValue] キーワード自動同期)
 │   └─ RequireScModule(id)   … Shader Core モジュール要求の宣言
 ├─ ConversionModule[]        … TypeCache で自動発見、Order 順に実行
 ├─ TextureBaker / GradientSynth … RT 読み戻しベースのテクスチャ生成
 └─ ScModuleChecker           … ProjectSettings/jp.lilxyzw.shadercore.asset の検証 (リフレクション)
```

## 設計判断

### 1. Poiyomi / Thry へのコンパイル依存を持たない

ソース値は `SerializedObject` で `m_SavedProperties` から直接読む。

- ロック済みマテリアル (`Hidden/Locked/...`) でも全プロパティ値が残っている
- Thry の `ShaderOptimizer.UnlockMaterials` を呼ぶ必要がなく、asmdef 参照も versionDefines も不要
- Poiyomi 本体がアンインストールされたプロジェクトでも変換可能

判定材料もキーワードではなく float 値を使う (Poiyomi はキーワード名を再利用しており、
ロック時にはキーワード自体が消えるため)。

### 2. Shader Core へのコンパイル依存も持たない

- モジュールプロパティ名は命名規則 (`_ + uniqueID(.→_) + 元名`) から `NonToonProps` で組み立てる
- `[SCConstValue]` キーワードはターゲットシェーダーの `GetPropertyAttributes` から動的に解析して同期する
  (Shader Core の `CustomAttributes.SCConstValue` と同じ挙動)
- ProjectSettings の検証だけはリフレクションで `jp.lilxyzw.shadercore.ProjectSettings.GetShaderModules`
  を呼ぶ (失敗しても変換は続行し、レポートに残す)
- グラデーションは .scgradients を作らず素の Texture2DArray アセット (128×1×N, RGBA32, sRGB) を生成する。
  シェーダーは最終的なテクスチャしか見ないため互換

これにより NonToon / Shader Core の内部実装が変わっても、コンパイルエラーではなく
レポート警告 (プロパティ欠落) として現れる。

### 3. 拡張機構 — 将来のモジュール追加

ユーザー要件「NonToon に存在しない Poiyomi 機能をモジュールとして追加し、設定を引き継げる設計」への回答:

```
jp.rroki.nontoon-emission (将来の別パッケージの例)
 ├─ Shaders/Emission/
 │   ├─ jp.rroki.nontoon.emission.scmodule   … Shader Core モジュール定義
 │   ├─ properties.hlsl                       … SC_color(_EmissionColor, ...) など
 │   └─ phase_postemission.hlsl               … 発光の実装
 └─ Editor/
     └─ EmissionConversionModule.cs           … ConversionModule 派生
```

```csharp
public sealed class EmissionConversionModule : ConversionModule
{
    const string ModId = "jp.rroki.nontoon.emission";
    public override int Order => 200;
    public override string DisplayName => "エミッション";
    public override bool ShouldRun(ConversionContext c) => c.Source.GetToggle("_EnableEmission");
    public override void Convert(ConversionContext c)
    {
        c.RequireScModule(ModId);  // ProjectSettings での有効化を要求
        c.SetTexture(NonToonProps.Prop(ModId, "_EmissionMap"), c.Source.GetTexture("_EmissionMap"));
        c.SetColor(NonToonProps.Prop(ModId, "_EmissionColor"),
            c.Source.GetColor("_EmissionColor", Color.white) * c.Source.GetFloat("_EmissionStrength", 0f));
        // [SCConstValue] トグルがあれば c.SetInt(...) がキーワードまで自動同期する
    }
}
```

- `ConversionModule` の非 abstract 派生は **TypeCache で自動発見** されるため、本体パッケージの改変は不要
- `ctx.AllocateGradient` / `ctx.AllocateMaskChannel` も外部モジュールから利用可能
- Shader Core 側は「シェーダーと同じディレクトリ配下の .scmodule」以外も
  プロジェクト内の全 .scmodule を列挙して有効化できるため (ModuleSetter UI)、
  別パッケージからのモジュール追加が機構上サポートされている

### 4. 生成アセット

| 生成物 | 形式 | 命名 |
|---|---|---|
| ティント/アルファベイク済みメイン | PNG (sRGB) | `{mat}_base.png` |
| 影/ヘアハイライトのグラデーション | Texture2DArray .asset (128×1×N) | `{mat}_gradients.asset` |
| パック済みマスク | PNG (Linear, 未使用ch=白) | `{mat}_mask.png` |

すべて RenderTexture 読み戻しで生成するため、元テクスチャの Read/Write 設定に依存しない。
色空間: リニアで読み → リニアで演算 → sRGB 保存時のみガンマ変換 (マスクはリニアのまま +
importer の sRGB フラグを無効化)。

### 5. レンダリングモードの再現

NonToon の `NTRenderingModeElement` は internal のため呼ばず、同じ副作用
(ブレンド / AlphaToMask / renderQueue) を `BaseConversionModule` が書き込む。
NonToon 0.1.3 の実装と値を一致させてある (BIRP の Transparent は queue 2460 など)。
