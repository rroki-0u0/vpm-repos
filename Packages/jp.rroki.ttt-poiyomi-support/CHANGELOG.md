# Changelog

## [0.1.0] - 2026-07-07

### Added

- 初版
- Poiyomi Toon / Pro (9.x / 10.x) の全テクスチャスロットをシェーダープロパティから動的に分類し、TTT AtlasTexture へ UV 使用情報を提供
- `_XUV` セレクタ規約 + 例外テーブル (UV0 固定スロット / 命名例外 / 非メッシュ UV スロット) による分類
- Cube / 3D / 2DArray の次元判定による自動除外
- ロック済みシェーダー (`Hidden/Locked/.poiyomi/*`) の起動時一括登録と、セッション中に生成されたシェーダーのインポート時自動登録
- 判定不能な未知スロットの安全側除外と警告ログ

### Notes

- 旧実装 (`Assets/rroki-0u0/Kaihen/0_Common/Runtime/PoiyomiShaderInformation.cs`) を置き換え。旧実装の既知の問題: `_AlphaMask` 等のリスト漏れ、UV0 固定スロット非対応、`_FlipbookMask1` の命名例外非対応、起動時のみの登録によるロック直後の未登録、`EditorApplication.delayCall` による登録遅延
