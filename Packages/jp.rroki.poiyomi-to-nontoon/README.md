# Poiyomi to NonToon Converter

Poiyomi Toon / Pro のマテリアルを [lilxyzw NonToon](https://github.com/lilxyzw/NonToon) マテリアルへ変換する Unity エディタ拡張です。

NonToon は [Shader Core](https://github.com/lilxyzw/ShaderCore) ベースのモジュール式シェーダーで、Poiyomi とは思想も実装も大きく異なります。本ツールは **NonToon 側に受け皿が存在する機能のみ** を引き継ぎ、変換できなかった機能はレポートとして明示します。

## 何が解決されるか

| Poiyomi | NonToon | 変換方式 |
|---|---|---|
| メインテクスチャ / ノーマルマップ / カットアウト | `_BaseTexture` / `_NormalMap` / `_Cutoff` | 直接引き継ぎ |
| メインカラー (`_Color`) / アルファマスク | — (NonToon にティントなし) | **テクスチャへベイク** |
| レンダリングモード (Opaque/Cutout/Transparent/ディザ) | `_RenderingMode` + ブレンド/キュー | 実効ブレンド状態から自動判定 |
| 影 (Texture Ramp / Multilayer / Flat / ShadeMap / SDF など) | Shade モジュール + `_SharedGradients` | **グラデーションへベイク** |
| リムライト | RimLight モジュール | 範囲へ近似 |
| スペキュラ (PBR / スタイライズド) | Specular モジュール (GGX) | 近似 |
| 異方性ハイライト | HairSpecular モジュール | 帯グラデーションへ近似 |
| マットキャップ ×2 | MatCaps モジュール (乗算/加算) | ブレンド量で振り分け |
| ディテール (テクスチャ/ノーマル) | Details モジュール スロット 0/1 | 直接引き継ぎ |
| 距離フェード / 最低輝度 / アウトライン / ステンシル | DistanceFade / Lighten / 本体設定 | 近似または直接 |
| 各機能のマスク | `_SharedMask` (RGBA) | **チャンネルへパック** |

- **ロック済みマテリアル対応**: シリアライズ済みプロパティ (`m_SavedProperties`) を直接読むため、Thry のアンロックは不要です。**Poiyomi 本体が削除済みのプロジェクトでも変換できます。**
- モジュールの有効化 (`[SCConstValue]` キーワード同期・Shader Core プロジェクト設定の検証) は自動で行われます。

## 使い方

1. マテリアル (または GameObject) を選択
2. 右クリック → `rroki_'s tool/Poiyomi → NonToon 変換...`
   (または `Tools/rroki_'s tool/Poiyomi to NonToon Converter`)
3. オプションを確認して「変換」
4. レポートで「近似」「要確認」「未対応」の項目を確認し、必要に応じて手動調整

### アニメーションクリップの変換

Poiyomi のマテリアルプロパティをアニメーションしているクリップ (エミッションの色相シフトによるカラーチェンジ等) は、
`Tools/rroki_'s tool/Poiyomi → NonToon Animation Retarget` で NonToon のプロパティパスへ変換できます
(時間係数が異なる色相シフト速度 / UV パンは値を自動換算)。
NonToon のインスペクターは録画モードのキー記録に対応していないため、新規にキーを打つ場合は
Animation ウィンドウの Add Property から `Material._jp_rroki_nontoon_emission_EmissionHueShift` 等を直接追加してください。

既定では複製 (`*_NT.mat`) を作成します。生成物 (ベイク済みテクスチャ / グラデーション配列 / パック済みマスク) はマテリアルと同じフォルダに保存されます。

## 対応状況

- Poiyomi 10.0 (Pro / Toon) で確認。9.x はプロパティ名が概ね共通のため多くは動作しますが未検証です
- NonToon 0.1.3 / Shader Core 0.1.5 が対象です (どちらも開発途上のため、将来のバージョンでプロパティ構成が変わる可能性があります)
- Poiyomi Fur → NonToonFur は未対応 (将来対応予定)

## 既知の制限

- AudioLink / ディゾルブ / デカール / UV スクロールなど、NonToon に存在しない機能は破棄されます (レポートに記録)
- **エミッション / 色調整 (色相・彩度・明度・ガンマ) / グリッター**は、拡張パッケージ [NonToon Extra Modules](https://github.com/rroki-0u0/vpm-repos) (`jp.rroki.nontoon-extra-modules`) を導入すると自動で引き継がれます
- NonToon はメインテクスチャのタイリング/オフセットに非対応です
- 影のベイクは「ハーフランバートを x 軸とするグラデーション乗算」への写像であり、Poiyomi の全ライティングモードを完全再現するものではありません

## 拡張 (将来の構想)

変換パイプラインは `ConversionModule` の派生クラスを TypeCache で自動発見します。NonToon に無い機能 (例: エミッション) を Shader Core モジュール (.scmodule) として別パッケージで追加し、同じパッケージ内で `ConversionModule` を実装すれば、本体を改変せずに変換対応を拡張できます。詳細は `docs/architecture.md` を参照してください。

## インストール

VCC / ALCOM にリポジトリを追加してください:

```
https://rroki-0u0.github.io/vpm-repos/index.json
```

[Add to VCC](vcc://vpm/addRepo?url=https://rroki-0u0.github.io/vpm-repos/index.json)

## ライセンス

[MIT](LICENSE.md)
