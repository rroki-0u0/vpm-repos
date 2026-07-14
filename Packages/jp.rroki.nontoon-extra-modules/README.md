# NonToon Extra Modules

[lilxyzw NonToon](https://github.com/lilxyzw/NonToon) に、NonToon 本体に存在しない機能を [Shader Core](https://github.com/lilxyzw/ShaderCore) モジュールとして追加するパッケージです。

すべてのシェーダーコードは **Poiyomi のコードを流用しない独自実装 (クリーンルーム)** です。数式は一般に知られた公開手法 (グレー軸まわりの色相回転行列、fract-sin ハッシュ等) のみを使用しています。

## 収録モジュール

| モジュール | uniqueID | 内容 |
|---|---|---|
| **エミッション** | `jp.rroki.nontoon.emission` | 発光。HDR カラー / 強度 / マップ (タイリング・UV0-3・スクロール) / アルベド乗算 / 共有マスクチャンネル / 正弦波点滅 |
| **色調整** | `jp.rroki.nontoon.coloradjust` | メインカラーの色相 / 彩度 / 明度 / ガンマ補正 (Details より前に適用) |
| **グリッター** | `jp.rroki.nontoon.glitter` | ハッシュベースのスパークル。グリッド密度 / 輝点割合 / 明滅速度 / HDR カラー |
| **環境リフレクション** | `jp.rroki.nontoon.envreflection` | リフレクションプローブのサンプリング (ラフネス連動ミップ + Schlick フレネル)。本体の `_Roughness` を共有。金属向けのアルベド色反射に対応 |
| **クリアコート** | `jp.rroki.nontoon.clearcoat` | コート専用の滑らかさを持つ第2ローブ。直接光ハイライト (GGX + Kelemen 可視項) + シャープな環境映り込み。ベース反射と独立して調整可能 |
| **ハイトマップ視差** | `jp.rroki.nontoon.heightparallax` | 簡易 POM。接空間視線に沿った UV 後退でベース/マスク/ノーマルを再サンプル (base フェーズ先頭) |
| **内部パララックス** | `jp.rroki.nontoon.internalparallax` | 模様が表面の下に沈んで見える表現。Heightmap (RGB=色 / A=高さ、置き換え or ライト加算) と LayerAlpha (多層合成) の 2 モード、最大 32 レイヤー |
| **デカール** | `jp.rroki.nontoon.decal` | 位置 / スケール / 回転指定のテクスチャ合成 ×4 スロット。Replace/Multiply/Add ブレンド + エミッションオプション |
| **マットキャップ (追加)** | `jp.rroki.nontoon.matcapsextra` | NonToon 標準 MatCaps (乗算/加算 各1) に加えて、もう 1 組の乗算/加算スロットを追加 |

- エミッション / グリッターは `postpixel` フェーズ (ライティング合成後の加算) で DistanceFade より前に、環境リフレクションは `reflection` フェーズで動作します。いずれもアウトラインパスでは無効です
- 各モジュールはマテリアルごとの ON/OFF トグル ([SCConstValue] キーワード) を持ち、OFF のマテリアルにはコストがかかりません

## 使い方

### モジュールの有効化

NonToon の `.scshader` を選択し、インスペクターのモジュール一覧から各モジュールにチェックを入れて Apply してください (Shader Core のプロジェクト設定に保存されます)。

### Poiyomi からの変換

[Poiyomi to NonToon Converter](https://github.com/rroki-0u0/vpm-repos) (`jp.rroki.poiyomi-to-nontoon`) がインストールされていると、変換時に以下が自動で引き継がれます:

| Poiyomi | 引き継ぎ先 |
|---|---|
| Emission 0 (色 / 強度 / マップ / マスク / 点滅 / UV パン) | エミッションモジュール |
| Color Adjust (彩度 / 明度 / ガンマ) + Hue Shift | 色調整モジュール |
| Glitter (色 / 明るさ / グリッド / 速度 / サーフェスカラー) | グリッターモジュール |
| Mochie PBR / lilToon スタイルの環境リフレクション | 環境リフレクションモジュール |
| Clear Coat (滑らかさ / ハイライト / 映り込み / コートマスク) | クリアコートモジュール |
| Parallax Height Mapping (`_HeightMap` / 強度 / ステップ数) | ハイトマップ視差モジュール |
| Internal Parallax (マップ / 深度範囲 / レイヤー数、専用マスクは高さ (A) へベイク) | 内部パララックスモジュール |
| Decal 0-3 (位置 / スケール / 回転 / 色 / ブレンド / タイリング / エミッション、マスクはアルファへベイク) | デカールモジュール |
| MatCap スロット 3/4 | マットキャップ (追加) モジュール |

必要なモジュールの有効化 (Shader Core プロジェクト設定) も変換時に自動で行われます。
コンバーターが無い環境ではシェーダーモジュールのみが機能します (連携コードはコンパイルされません)。

## 既知の制限

- Poiyomi の Distorted UV / スクロールエミッション (波状) / 点滅の矩形波モードは近似またはスキップされます
- グリッターは視線角度による煌めき (view-dependent sparkle) に非対応です (時間ベースの明滅のみ)
- 明度補正の数式は Poiyomi と異なります (乗算方式)。変換後に見た目を確認してください
- 環境リフレクション / クリアコートの映り込みはワールドのリフレクションプローブに依存します (プローブ無しワールドでは反射が出ません。フォールバックキューブマップは未対応)
- ハイトマップ視差は「上書き再サンプル」方式のため、Details など後続レイヤーには視差がかかりません。視差が逆に見える場合は強度の符号を反転してください
- 内部パララックスは加算発光扱いの近似です (Poiyomi のレイヤー合成モード / 高さマップモード / レイヤー色相シフトは再現されません)
- URP でのリフレクションは未検証です (BIRP / VRChat で検証済み)
- NonToon / Shader Core 0.1.x (開発版) 対象です。将来のバージョンで動作しなくなる可能性があります

## インストール

VCC / ALCOM にリポジトリを追加してください:

```
https://rroki-0u0.github.io/vpm-repos/index.json
```

## ライセンス

[MIT](LICENSE.md)

### サードパーティ表記

- 本パッケージのシェーダーコードは Poiyomi のコードを一切流用していません (プロパティ名・数値挙動の観察に基づく独自実装)
- マットキャップ (追加) モジュールは [NonToon](https://github.com/lilxyzw/NonToon) (MIT License, Copyright (c) lilxyzw) 標準の MatCaps モジュールと同じ投影挙動を再現しています
- 色相回転 / ハッシュ / GGX / Kelemen 可視項 / POM 等は一般に公開されている標準的な手法です
