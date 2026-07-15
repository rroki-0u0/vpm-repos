# Submesh Mask Baker

SkinnedMeshRenderer の指定サブメッシュ(Materials 要素)の三角形を UV 空間に焼き込み、白黒マスク PNG を出力する Unity エディタ拡張です。

ファーマスク・マテリアル領域マスク・アトラス用マスクなど、「特定のマテリアルスロットが占める UV 領域」のマスクが欲しい場面で使えます。

## 使い方

1. メニュー `Tools > rroki_'s tools > Submesh Mask Baker` を開く
2. パラメータを設定して **Bake**:

| パラメータ | 説明 |
| --- | --- |
| Renderer | 対象の SkinnedMeshRenderer |
| Submesh (Material要素) | マスク化したいマテリアルスロット番号 (0 始まり) |
| Resolution | 出力解像度 (px) |
| Padding (px) | UV シームのにじみ対策の膨張幅 |
| UV Channel | 参照する UV チャンネル (通常 0) |

3. 保存先を指定すると、対象サブメッシュの領域が白・それ以外が黒の PNG が出力されます

## インストール

1. VCC (VRChat Creator Companion) または ALCOM に以下の VPM リポジトリを追加します:

   ```
   https://rroki-0u0.github.io/vpm-repos/index.json
   ```

   [Add to VCC](vcc://vpm/addRepo?url=https%3A%2F%2Frroki-0u0.github.io%2Fvpm-repos%2Findex.json)

2. プロジェクトに `Submesh Mask Baker` を追加します。

依存パッケージはありません (Unity 2022.3 以降)。

## ライセンス

[MIT](LICENSE.md)
