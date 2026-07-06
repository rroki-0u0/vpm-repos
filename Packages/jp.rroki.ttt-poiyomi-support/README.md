# TexTransTool Poiyomi Support

[TexTransTool](https://github.com/ReinaS-64892/TexTransTool) (TTT) の AtlasTexture に、Poiyomi Toon / Pro シェーダーのテクスチャ UV 使用情報を提供する拡張パッケージです。

## 何が解決されるか

TTT の AtlasTexture は「どのテクスチャプロパティがどの UV チャンネルでサンプルされるか」というシェーダーごとの情報を使い、アトラス化するテクスチャを決定します。標準対応は lilToon / Standard / VRChat SDK シェーダーのみで、Poiyomi マテリアルはフォールバック (`_MainTex` のみ UV0 扱い) になります。その結果、アルファマップ・マットキャップマスク・ノーマルマップなどがアトラス化されず、UV 再配置後に元テクスチャの想定外の場所を参照する症状が発生します。

本パッケージは Poiyomi の命名規約 (`_X` / `_XUV` / `_X_ST`) に基づき、シェーダーの全テクスチャスロット (約 150) を動的に分類して TTT に登録します:

| 分類 | 例 | 扱い |
| --- | --- | --- |
| `_XUV` セレクタ持ち | `_MainTex`, `_AlphaMask`, `_MatcapMask`, `_EmissionMap` など大半 | セレクタ値 UV0〜UV3 に応じて登録。Panosphere / World Pos / Polar / Distorted / Local Pos / Matcap モードは対象外 |
| UV0 固定サンプル | `_MetallicGlossMap`, `_SmoothnessTex`, `_ReflectionColorTex`, `_ParallaxInternalMap` | 常に UV0 として登録 |
| 命名例外 | `_FlipbookMask1` (→ `_FlipbookMaskUV1`), `_PoiSkinDetailMap` (→ `_PoiSkinFreckleMapUV`) | 例外テーブルで対応 |
| メッシュ UV でないスロット | `_Matcap`〜`_Matcap4`, `_ToonRamp`, `_ClothDFG`, `_SkinLUT`, `_GlitterTexture` など | アトラス化対象外 (元テクスチャ維持) |
| Cube / 3D / 2DArray | `_CubeMap`, `_FlipbookTexArray` など | 次元判定で自動的に対象外 |
| 判定不能な未知スロット | (将来の Poiyomi の新機能など) | 安全側に倒して対象外 + 警告ログ |

テクスチャスロットはシェーダーのプロパティから動的に列挙するため、Poiyomi のバージョン (9.x / 10.x, Toon / Pro) に依存しません。Thry Optimizer でロックされたシェーダー (`Hidden/Locked/.poiyomi/*`) も Properties ブロックが保持されるためそのまま動作し、マテリアルのロックでセッション中に新しく生成されたシェーダーもインポート時に自動登録されます。

## 対応状況

- Poiyomi Toon / Pro 9.3 / 10.0 系のシェーダーソース解析に基づく (例外テーブルは両系列で同一)
- ロック済みシェーダー対応
- TexTransTool 1.0 系 (`TTShaderTextureUsageInformationRegistry` API)

## 既知の制限

- **Tiling / Offset (`_ST`)**: TTT の AtlasTexture 自体がテクスチャの Tiling / Offset を考慮しないため、1 以外のタイリングを持つスロットはアトラス化後にずれます (本パッケージでは解決できません)
- **UV 空間内の座標指定を持つ機能**: デカールの Position / Rotation / Scale、フリップブックの配置指定などは、アトラス化で UV レイアウトが変わると座標がずれます。デフォルト配置 (UV 全面) のみ正しく動作します
- **パンニング (`_XPan`)**: アトラス化後のアイランド再配置と干渉する場合があります
- Thry Optimizer の「アニメートされたプロパティのリネーム」で `_XUV` がリネームされた場合、そのスロットは既定 (UV0) と見なされます

## インストール

1. VCC (VRChat Creator Companion) または ALCOM に以下の VPM リポジトリを追加します:

   ```
   https://rroki-0u0.github.io/vpm-repos/index.json
   ```

   [Add to VCC](vcc://vpm/addRepo?url=https%3A%2F%2Frroki-0u0.github.io%2Fvpm-repos%2Findex.json)

2. 依存パッケージの TexTransTool のリポジトリ (`https://vpm.rs64.net/vpm.json`) も追加されていることを確認します。
3. プロジェクトに `TexTransTool Poiyomi Support` を追加します。

## ライセンス

[MIT](LICENSE.md)
