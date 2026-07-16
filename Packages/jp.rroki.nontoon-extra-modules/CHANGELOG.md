# Changelog

このプロジェクトの注目すべき変更はこのファイルに記録されます。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に基づきます。

## [0.2.2] - 2026-07-16

### Added

- **バックフェースモジュール** (`jp.rroki.nontoon.backface`): 裏面 (`vertex.isFront == false`) のみアルベドの色/テクスチャを差し替える。二重構造の服の裏地・内側を別色にしたい場合向け。base フェーズでライティング前に置換するため裏面も表面と同じ光/影を受ける。Tint (元色×色) / Replace (置き換え) の 2 モード、専用テクスチャ (UV/ST 対応)、アルファ置換、色相シフト (共有 `RrokiNTHueRotate`)、エミッション強度。既定は無効のため既存マテリアルは不変
- Poiyomi 変換: **BackFace** (`_BackFaceEnabled` / `_BackFaceColor` / `_BackFaceTexture` / `_BackFaceHueShift` / `_BackFaceEmissionStrength` / `_BackFaceReplaceAlpha`) をバックフェースモジュールへ引き継ぎ (Replace モード)
- 内部パララックスの**色ソースに MatCap** (ビュー依存の球面マップ) を追加。`_InternalColorSource` (Texture / MatCap / Multiply) で切替、`_InternalMatCap` テクスチャ、`_InternalMatCapParallax` (法線ベース〜内部視差シフトへの連動量)、`_InternalMatCapHue` / `_InternalMatCapHueSpeed` (色相シフト、共有 `RrokiNTHueRotate` を流用) を追加。宝石/クリスタル/瞳の内部表現向け。内部マップのアルファ (形状/深度) はそのまま使い、色だけを MatCap に差し替える。base (Replace) / add 両フェーズ対応。既定は Texture のため既存マテリアルは不変
- エディタツール **エミッションマスク合成**: 既存の発光マスクへ追加マスクを合成して 1 枚の PNG に出力する (元テクスチャは非破壊)。2 つのモード:
  - **共有マスクのチャンネル**: `_SharedMask` の発光チャンネル (マテリアル指定で自動取得) へ追加マスクを Max / Add / Subtract 合成。ソースは R/G/B/A/輝度/最大RGB から選択。対象チャンネル以外はバイト単位で不変
  - **エミッションマップ (RGB)**: 発光色マップ同士を Add / Max / Screen 合成 (RGB そのまま / チャンネル×色)
  - マテリアル指定で結果の自動割り当て、エミッションモジュールの有効化ボタン ([SCConstValue] キーワード同期) 付き
  - PNG/JPG はファイルを直接デコードして合成する (インポーターの DXT 圧縮ノイズを焼き込まない)。スクリプト用 API `EmissionMaskCombiner.CombineMaskChannelSimple` あり

### Fixed

- ハイトマップ視差にマスクを追加。従来は視差がメッシュ全面に適用されていた (マスク未対応)。**専用マスクテクスチャ** `_ParallaxMask` + チャンネル選択 `_ParallaxMaskChannel` + 反転 `_ParallaxMaskInvert` を追加し、元 UV でのマスク値で視差量を減衰するようにした (マスク外=視差なし、マスク内=完全な視差)。既定 "white" のため未設定時は従来どおり全面に視差 (後方互換)。共有マスク (_SharedMask) のチャンネルではなく専用テクスチャにしたのは、masked 機能が多い材質では共有マスクの 4 チャンネルが枯渇して視差マスクが割り当てられない問題があったため
- Poiyomi 変換: これまで「非対応」として破棄していた Poiyomi のハイトマスク (`_Heightmask`) を専用マスク `_ParallaxMask` として引き継ぎ、`_HeightmaskChannel` / `_HeightmaskInvert` も反映するようになった (視差がマスク範囲に限定される)

## [0.2.1] - 2026-07-16

### Added

- 首元フェードマスク生成に **フェード基準の切替**を追加。首元は必ずしもメッシュの穴ではない (頭部が閉じている場合が多い) ため、開境界に依存しない基準を選べる:
  - **境界ループ (このメッシュ)**: 従来方式。開口があるメッシュ向け
  - **境界ループ (別メッシュ参照)**: 体側など首元が開口したメッシュを参照し、その首開口ループ (Neck ボーンに最も近いループを自動選択) からの距離で対象メッシュのマスクを作る。閉じた頭部でも体の首穴を基準にできる
  - **平面 (Transform/ボーン基準)**: Neck/Head ボーンやシーンビューのドラッグ可能ハンドルで決めた平面からの距離。既定では対象メッシュの最下部 (首の切れ目) に平面を置き、上方向へフェード。**開口が無い頭部メッシュでも首元中心のフェードマスクが作れる**
  - **点からの距離 (Transform)**: 参照位置からの放射距離
- 平面/点モードはシーンビューに位置ハンドル (と平面ディスク) を表示し、ドラッグで基準を調整できる。「Neck ボーンに合わせる」ボタンで Humanoid の Neck/Head へスナップ
- フェード開始/終了距離の既定値を対象メッシュの高さから自動設定 (スケールされたアバターでも初回から見えるグラデーションになる)

### Fixed

- 首元フェードマスク生成: ループ検出・中心判定・距離計算をメッシュのバインドポーズ ローカル座標から **posed ワールド座標**へ変更。SkinnedMeshRenderer に回転/スケールが付いたメッシュ (顔メッシュ等) で、傾いたローカル軸のせいで首以外の開口 (口など) が自動選択されていた不具合を修正。自動選択は「ワールド最下 Y のループ = 首元」で判定するようになった
- 首元フェードマスク生成: フェード開始/終了距離が実メートルにならず、SMR のスケール分ずれていた問題を修正 (ワールド座標で距離計算)
- 首元フェードマスク生成: シーンプレビュー/距離計算の位置ズレを修正。スケールされたアバター (VRChat では root スケールが一般的) で `SkinnedMeshRenderer.BakeMesh` のスケール未適用版を使っていたためメッシュが縮小・移動していた。`BakeMesh(mesh, useScale:true)` + `localToWorldMatrix` に修正し、実際の描画位置へ正確に重なるようにした
- ループ一覧の表示を「中心Y」から「ワールドY」に明確化

### Changed

- ツールメニューを `Tools/rroki/NonToon/...` から **`Tools/rroki_'s tools/NonToon/...`** へ移動 (他の rroki ツールと統一)

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
