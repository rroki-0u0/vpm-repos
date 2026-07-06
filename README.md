# vpm-repos

rroki-0u0 の VRChat 向けパッケージを配布する VPM リポジトリです。

## 利用者向け

VCC (VRChat Creator Companion) または ALCOM に以下のリポジトリ URL を追加してください:

```
https://rroki-0u0.github.io/vpm-repos/index.json
```

[Add to VCC](vcc://vpm/addRepo?url=https%3A%2F%2Frroki-0u0.github.io%2Fvpm-repos%2Findex.json)

### 収録パッケージ

| パッケージ | 説明 | 依存 |
| --- | --- | --- |
| [TexTransTool Poiyomi Support](Packages/jp.rroki.ttt-poiyomi-support/) (`jp.rroki.ttt-poiyomi-support`) | TexTransTool の AtlasTexture に Poiyomi Toon/Pro の全テクスチャスロットの UV 使用情報を提供 | [TexTransTool](https://vpm.rs64.net/) |
| [Submesh Mask Baker](Packages/jp.rroki.submesh-mask-baker/) (`jp.rroki.submesh-mask-baker`) | 指定サブメッシュ (マテリアルスロット) の UV 領域を白黒マスク PNG として焼き出すエディタ拡張 | なし |

## リポジトリ構成

- `Packages/<id>/` — パッケージ本体 (Unity プロジェクトの embedded package と同じ内容、.meta 込み)
- `.github/workflows/release.yml` — 手動実行でパッケージを zip 化し GitHub Release を作成
- `.github/workflows/build-listing.yml` — Release 一覧から `index.json` を生成して GitHub Pages に公開
- `tools/build-listing.mjs` — リスティング生成スクリプト
- `site/` — 公開ページ (index.html)
- `listing.config.json` — リスティングのメタデータ (名前・ID・URL)

## リリース手順 (開発者向け)

1. Unity プロジェクト側で開発し、`tools/sync-from-unity.ps1` でこのリポジトリに同期する
2. `Packages/<id>/package.json` の `version` と `CHANGELOG.md` を更新してコミット & push
3. GitHub Actions の **Release Package** ワークフローを対象パッケージ ID で実行する
   - パッケージの zip + SHA256 + package.json が添付された Release (`<id>-v<version>`) が作成される
   - 続けて **Build VPM Listing** が自動実行され、Pages 上の `index.json` が更新される

パッケージを追加する場合は `Packages/<新しい id>/` を置いて同じ手順を踏むだけです (リスティングは Release から自動生成されます)。

## ライセンス

このリポジトリのツール類は [MIT](LICENSE)。各パッケージのライセンスはパッケージ内の表記に従います。
