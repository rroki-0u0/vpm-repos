# Changelog

このプロジェクトの注目すべき変更はこのファイルに記録されます。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に基づきます。

## [0.2.2] - 2026-07-16

### Fixed

- Shader Core モジュールの自動有効化 (`ScModuleChecker.EnsureModules`) が動作していなかった不具合を修正。shadercore の `GetShaderModules` は 3 引数 (out) 版のみだが 1 引数で呼んでおり、例外が握り潰されて**新規モジュールが登録されずシェーダーに反映されない**状態だった (既に登録済みのモジュールは影響なし)。これによりバックフェース等、変換時に新規追加されるモジュールが正しく有効化されるようになった

## [0.2.1] - 2026-07-16

### Changed

- ツールメニューを **`Tools/rroki_'s tools/...`** / 右クリック **`Assets/rroki_'s tools/...`** へ統一 (従来の `rroki_'s tool` から複数形に、他の rroki ツールと統一)

## [0.2.0] - 2026-07-15

### Added

- ファー変換: Poiyomi Fur シェーダーを NonToonFur (シェル法ファー) へ変換 (長さ / 重力 / ノイズマスク / シェル数 → subdivision)。長さ・密度は単位差があるため近似 (要手動調整)
- ディゾルブ変換 (`jp.rroki.nontoon-extra-modules` 導入時): ノイズテクスチャ / 溶解量 / UV / スクロール / 反転 / 境界発光 / マスクの引き継ぎ
- オクルージョン (AO) 変換 (`jp.rroki.nontoon-extra-modules` 導入時): `_OcclusionMap` / チャンネル / 強度の引き継ぎ

### Changed

- メインカラー色相シフトを 0..1 (シームレスループ) へ変換し、色相シフト速度 (`_MainHueShiftSpeed`) も引き継ぐようになった (従来は速度をドロップ)
- 追加マットキャップの色相シフト (スロット 3/4) を引き継ぐようになった
- Animation Retarget に色相シフト速度・追加マットキャップ色相のマッピングを追加
- Fur シェーダー検出時、これまでの「未対応」警告に代えて NonToonFur を自動選択するようになった
- ハイトマップ視差の引き継ぎを Min/Max ステップ (視線角で可変) に対応、グリッターの引き継ぎを Density/Size/視線依存パラメータに対応

## [0.1.0] - 2026-07-14

### Added

- Poiyomi Toon/Pro → NonToon マテリアル変換の初回リリース
- レンダリングモード自動判定 (実効ブレンド状態からの逆算、ディザ透過対応)
- メインカラー / アルファマスクのテクスチャベイク
- 影設定 (Texture Ramp / Multilayer Math / Flat / ShadeMap / Wrapped / SDF) のグラデーションベイク
- リムライト / スペキュラ / 異方性ハイライト / マットキャップ / ディテール / 距離フェード / 最低輝度 / アウトライン / ステンシルの変換
- 機能マスクの `_SharedMask` RGBA チャンネルパック
- `[SCConstValue]` キーワードの自動同期と Shader Core モジュール有効状態の検証
- 変換レポート (引継 / 近似 / 要確認 / 未対応)
- `ConversionModule` 派生クラスの自動発見による拡張機構

### Notes

- ロック済み Poiyomi マテリアルはアンロック不要で変換できます (Poiyomi 本体が無いプロジェクトでも可)
- Poiyomi Fur → NonToonFur は未対応です
