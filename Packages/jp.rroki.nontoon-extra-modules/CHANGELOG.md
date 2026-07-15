# Changelog

このプロジェクトの注目すべき変更はこのファイルに記録されます。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に基づきます。

## [0.2.0] - 2026-07-15

### Added

- ディゾルブモジュール (`jp.rroki.nontoon.dissolve`): ノイズ閾値でピクセルを消去 (clip) + 境界の HDR カラー発光。base フェーズで動作するため影/深度パスでも欠落し、不透明マテリアルでも動作 (レンダリングモード非依存)
- オクルージョン (AO) モジュール (`jp.rroki.nontoon.occlusion`): くぼみを常に暗くする静的 AO。指向性のトゥーン影とは独立。Ramp (ランプ連動、`sd.shadow` を下げて Shade より前に実行) / Multiply / Light の 3 モード、専用マップ or 共有マスク、Strength / 常時最小の暗さ (Floor) / Invert
- 色調整・エミッション・追加マットキャップの色相にシームレスループ (0=1) と色相シフト速度を追加 (色調整 `_AdjustHue` を 0..1 + `frac`、`_AdjustHueSpeed` 追加。追加マットキャップに `_MatCapHueShift` / `_MatCapHueShiftSpeed` を追加)
- エディタツール **首元フェードマスク生成**: メッシュの開いた境界エッジ (首の切れ目) からの距離で UV グラデーションマスクを自動生成 (境界ループ選択 / フェード距離 / カーブ / シーンビューのヒートマップ プレビュー / UV ふち拡張)。コンバーター非依存の独立 asmdef
- エディタツール **SDF Neck Fade Baker**: SDF マップの B チャンネル (SDF↔幾何NdotL ブレンド) へフェードマスクを合成 (Max / Lerp / Add)。顔 SDF と体 NdotL の首元境界をシェーダー変更なしで連続化。生成ツールからの導線付き
- エディタツール **Animation Composer**: アバター配下の NonToon マテリアルを走査し、検索可能なプロパティ一覧から複数メッシュ×複数パラメータを選んでクリップ生成 (色相ループ 0→1 / A→B / A→B→A プリセット)。対象モジュールの `_Enable` ([SCConstValue] キーワード) を自動有効化し「無効モジュールでアニメが効かない」問題を回避
- Poiyomi 変換: ディゾルブ / オクルージョン (AO Map) / メインカラー色相シフト (速度含む) / マットキャップ色相の引き継ぎを追加

### Changed

- ハイトマップ視差を本格 POM 化 (視線角に応じた動的ステップ数 Steps Min/Max + 交点の線形補間リファインメントで階段状アーティファクトを解消)
- 内部パララックス (Heightmap / Replace) を固定点反復から線形探索 + 交点補間の正確なレイマーチへ変更 (沈み込み位置の精度向上、マスク外は自然に効果が消える挙動を維持)
- グリッターに視線/光源角依存の煌めき (セルごとのファセット法線) とセル内の丸い輝点形状 (Point Size) を追加 (View Dependent / Sparkle Sharpness で調整)
- Poiyomi 変換のグリッター (Density/Size/視線依存) とハイトマップ視差 (Min/Max ステップ) の引き継ぎを更新

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
