# Changelog

このプロジェクトの注目すべき変更はこのファイルに記録されます。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に基づきます。

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
