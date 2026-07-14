# TexTransTool NonToon Support

[lilxyzw NonToon](https://github.com/lilxyzw/NonToon) / NonToonFur のテクスチャ UV 使用情報を [TexTransTool](https://github.com/ReinaS-64892/TexTransTool) の AtlasTexture へ提供するエディタ拡張です。[TexTransTool Poiyomi Support](https://github.com/rroki-0u0/vpm-repos) の NonToon 版です。

## 何が解決されるか

NonToon は TTT にとって未知のシェーダーのため、そのままでは AtlasTexture が `_MainTex` (存在しない) しか見ず、テクスチャがアトラス化されません。本パッケージは NonToon の全テクスチャプロパティを分類して UV 使用情報を登録します:

| 分類 | 対象 | 扱い |
|---|---|---|
| UV0 固定 | `_BaseTexture` / `_SharedMask` / `_NormalMap` / ディテールマスク / SDF マップ / ハイトマップ | UV0 としてアトラス化 |
| UV セレクタ連動 | ディテール 0-3 / エミッションマップ / 内部パララックスマップ | マテリアルの UV 設定 (UV0-3) を参照 |
| 対象外 | マットキャップ (ビュー空間) / ディザ (スクリーン空間) / グラデーション配列 / デカール (サブ UV 空間) / ファーノイズ (タイリング) | アトラス化しない |

- [jp.rroki.nontoon-extra-modules](https://github.com/rroki-0u0/vpm-repos) のモジュール (エミッション / 内部パララックス / デカール等) にも対応しています
- **Shader Core のモジュール構成変更に追従**: モジュールの有効化/無効化でシェーダーが再生成されると、プロパティ分類を自動で作り直します

## 既知の制限

- デカールはアトラス化対象外です (位置/スケール/回転で変換されたサブ UV 空間のため、アトラス化すると位置が壊れます)。アトラス化後はデカールの位置設定を手動で調整してください
- エミッションのスクロールや内部パララックスの視差など「UV をずらすサンプル」は、アトラス端で隣のテクスチャがにじむ可能性があります (ずらし量が小さいため通常は目立ちません)

## インストール

VCC / ALCOM にリポジトリを追加してください:

```
https://rroki-0u0.github.io/vpm-repos/index.json
```

## ライセンス

[MIT](LICENSE.md)
