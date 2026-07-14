# Changelog

このプロジェクトの注目すべき変更はこのファイルに記録されます。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に基づきます。

## [0.1.0] - 2026-07-14

### Added

- エミッションモジュール (`jp.rroki.nontoon.emission`): HDR カラー / マップ + タイリング + UV 選択 + スクロール / アルベド乗算 / 共有マスクチャンネル / 正弦波点滅
- 色調整モジュール (`jp.rroki.nontoon.coloradjust`): 色相 / 彩度 / 明度 / ガンマ
- グリッターモジュール (`jp.rroki.nontoon.glitter`): ハッシュベースのスパークル
- 環境リフレクションモジュール (`jp.rroki.nontoon.envreflection`): リフレクションプローブ + フレネル
- クリアコートモジュール (`jp.rroki.nontoon.clearcoat`): コート専用滑らかさの第2ローブ (直接光ハイライト + シャープな映り込み)
- ハイトマップ視差モジュール (`jp.rroki.nontoon.heightparallax`): 簡易 POM
- 内部パララックスモジュール (`jp.rroki.nontoon.internalparallax`): 内部深度表現 (Heightmap 置き換え / LayerAlpha 多層合成)
- デカールモジュール (`jp.rroki.nontoon.decal`): 位置/スケール/回転指定のテクスチャ合成 ×4 スロット
- マットキャップ追加モジュール (`jp.rroki.nontoon.matcapsextra`): 乗算/加算スロットをもう 1 組追加 (Poiyomi スロット 3/4 の受け皿)
- Poiyomi to NonToon Converter 連携 (Emission / Color Adjust / Glitter / 環境リフレクション / Clear Coat / Parallax Height Mapping の設定引き継ぎ、コンバーター不在時は自動的に無効)

### Notes

- シェーダーコードはすべて Poiyomi 非流用のクリーンルーム実装です
- 実装の参考として 1111 (Flare) アバター構成の Poiyomi マテリアル 36 個の機能使用状況を調査しました
