# Poiyomi → NonToon プロパティマッピング表

Poiyomi Pro/Toon 10.0.12 と NonToon 0.1.3 (Shader Core 0.1.5) の分析に基づく変換仕様。

## 前提知識

### NonToon / Shader Core 側の機構

- **モジュールプロパティの命名**: Shader Core は .scmodule 由来のプロパティを
  `_ + uniqueID(. → _) + 元名` にリネームする。
  例: Shade モジュールの `_ShadeGradientIndex` → `_jp_lilxyzw_nontoon_shade_ShadeGradientIndex`
  (本体 `NonToon_properties.hlsl` と .scshader 直書きのプロパティは素の名前)
- **`[SCConstValue]` プロパティ** (Details / MatCaps の `_Enable`) はコンパイル時定数で、
  値の設定だけでは無効。キーワード `<プロパティ名大文字>_<値>` を同時に有効化する必要がある。
  例: `_JP_LILXYZW_NONTOON_DETAILS_ENABLE_1`
- **影・リムシェード・ヘアハイライトはグラデーション方式**: `_SharedGradients`
  (Texture2DArray 128×1×N, RGBA32, sRGB, 横軸=パラメータ) をインデックスで参照。-1 = 無効
- **マスクは共有方式**: `_SharedMask` (RGBA) を全機能がチャンネル番号 (0=R..3=A, 既定 3) で参照。
  UV0・ST なし。未使用チャンネルは白で埋める必要がある (既定参照先が A のため)
- **モジュールの有効範囲**: `ProjectSettings/jp.lilxyzw.shadercore.asset` にシェーダー名単位で保存。
  未登録シェーダーは初回参照時に全モジュール有効で自動生成される
- **レンダリングモード** (`_RenderingMode`): 0=Opaque, 1=Cutout, 2=Transparent。
  切り替え時の副作用 (NTRenderingModeElement 相当):
  | mode | _SrcBlend/_DstBlend | _AlphaToMask | renderQueue |
  |---|---|---|---|
  | 0 | 1 / 0 | 0 | -1 (シェーダー既定 2000) |
  | 1 | 1 / 0 | ディザなし=1 / あり=0 | 2450 |
  | 2 | 5 / 10 | 0 | BIRP=2460, SRP=3000 |
  ZWrite は変更しない (常時 1)
- NonToon に **`_Color` ティントおよびメインテクスチャの ST は存在しない**

### Poiyomi 側の読み取り

- 値は `m_SavedProperties` から直接読む (ロック済みでも保持されている)
- **キーワードは判定に使わない** (Poiyomi はキーワード名を再利用しており、ロック時は消える)
- レンダリングモードは `_Mode` ではなく実効値 (`_SrcBlend`/`_DstBlend`/`_AlphaForceOpaque`/queue) から逆算
- ロック検出: シェーダー名 `Hidden/Locked/...` / `OriginalShader` タグ

## マッピング表

凡例: ✅ 直接 / 🔶 近似 / 🔥 ベイク / ❌ 破棄 (レポート)

### ベース (BaseConversionModule)

| Poiyomi | NonToon | 方式 |
|---|---|---|
| `_MainTex` | `_BaseTexture` | ✅ (ティント等なしの場合) |
| `_Color`, `_AlphaMask` (+mode/weights/invert) | `_BaseTexture` | 🔥 `{名前}_base.png` へベイク |
| `_MainTex` の ST / `_MainTexUV` / `_MainTexPan` | — | ❌ 警告 |
| `_BumpMap` / `_BumpScale` | `_NormalMap` / `_NormalScale` | ✅ |
| `_Cutoff` | `_Cutoff` | ✅ (Cutout/Transparent 時) |
| `_SrcBlend`/`_DstBlend`/`_AlphaForceOpaque`/queue | `_RenderingMode` + 副作用一式 | 🔶 逆算 |
| `_AlphaDithering` | `_NTDitherTex` (nt_bayer_4x4) | 🔶 |
| `_AlphaPremultiply` / 加算・乗算ブレンド | 通常半透明 | 🔶 警告 |
| `_StencilRef`/`_StencilCompareFunction`/`_StencilPassOp` | `_StencilRef`/`_StencilComp`/`_StencilPass` | ✅ |
| `_StencilWriteMask` 等 / 表裏別ステンシル | — | ❌ |
| `_Cull` | `_Cull` | ✅ |

### 影 (ShadeConversionModule → Shade モジュール)

x 軸 = ハーフランバート N・L (0=影, 1=光) の 128px グラデーションを合成し、
`_ShadeGradientIndex` にレイヤー番号を設定。`_ShadeGradientRange` = (0,1)。

| Poiyomi `_LightingMode` | グラデーション合成 |
|---|---|
| 0 Texture Ramp | `_ToonRamp` の 1 行目をリサンプル。`_ShadowOffset` はサンプル位置シフトで近似 🔥 |
| 1 Multilayer Math | 3 層 (`_ShadowColor`+`_ShadowBorder`+`_ShadowBlur`, 2nd, 3rd) の smoothstep 乗算合成 🔶 |
| 2 Wrapped | `_LightingGradientStart/End` 間のソフトステップ 🔶 |
| 3 Skin / 6 Realistic / 7 Cloth | ソフトステップで近似 🔶 |
| 4 ShadeMap | `_1st/_2nd_ShadeColor` + Step/Feather の 2 層合成 (マップは破棄) 🔶 |
| 5 Flat (既定) | `_LightingShadowColor` のハードステップ (白なら Shade 自体をスキップ) ✅ |
| 8 SDF | `_SDFShadingTexture` → `_SDFMap`, `_SDFType=1` + フラット影 🔶 |

共通: `_ShadowStrength` は白へのブレンドで適用。`_LightingShadowColor` を乗算。
`_ShadowStrengthMask` ❌ (Shade にマスク入力なし)。

### リムライト (RimLightConversionModule → RimLight モジュール)

NonToon のリムはフレネル項の min/max 範囲指定・加算・影で自動減衰。

| Poiyomi | NonToon | 方式 |
|---|---|---|
| `_RimLightColor` × `_RimBrightness` | `_RimLightColor` | 🔶 |
| `_RimWidth` / `_RimBlur` | `_RimLightRange` = (1-width, min+blur×(1-min)) | 🔶 |
| `_RimBaseColorMix` | `_RimLightMultiplyAlbedo` | ✅ |
| `_RimMask` (+channel/invert) | `_SharedMask` チャンネル | 🔥 パック |
| lilToon スタイル (`_RimBorder`/`_RimBlur`) | range = border±blur/2 | 🔶 |
| `_RimPower` / ブレンドモード / `_RimStrength` (エミッシブ) / Rim2 | — | ❌ 警告 |

### スペキュラ (SpecularConversionModule → Specular モジュール)

NonToon は F0=0.04 固定の異方性 GGX。

| Poiyomi | NonToon | 方式 |
|---|---|---|
| Mochie PBR: 1-`_MochieRoughnessMultiplier` | `_Roughness` | 🔶 |
| `_MochieSpecularTint` × strength | `_SpecularColor` | 🔶 |
| `_MochieMetallicMultiplier` | `_SpecularMultiplyAlbedo` (金属=アルベド色スペキュラの近似) | 🔶 |
| lilToon モード: 1-`_Smoothness` / `_Metallic` | `_Roughness` / `_SpecularMultiplyAlbedo` | 🔶 |
| UnityChan モード: `_HighColor` ×strength / `_HighColor_Power` | `_SpecularColor` / `_Roughness` | 🔶 |
| メタリックマップ / 環境リフレクション / キューブマップ | — | ❌ (MatCap 代用を提案) |

### ヘアスペキュラ (HairSpecularConversionModule → HairSpecular モジュール)

x 軸 = 異方性項 (ハイライト中心 ≈ 0.5) の帯グラデーションを合成。

| Poiyomi | NonToon | 方式 |
|---|---|---|
| `_Aniso0Tint` × `_Aniso0Strength` | 帯の色 | 🔶 |
| `_Aniso0Power` → 帯幅 / `_Aniso0Offset` → 中心 | 帯形状 | 🔶 (要手動調整) |
| 第 2 層 (`_Aniso1*`) | max 合成 | 🔶 |
| `_AnisoUseBaseColor` | `_HairSpecularMultiplyAlbedo` | ✅ |

### マットキャップ (MatCapConversionModule → MatCaps モジュール)

Poiyomi スロット 0/1 → NonToon 乗算/加算スロットへ、支配的なブレンド量で振り分け。

| Poiyomi | NonToon | 方式 |
|---|---|---|
| `_Matcap(2)` + `_Matcap(2)Color` × intensity | `_MatCapMultiply`/`_MatCapAdd` + 色 | ✅ |
| `_Matcap(2)Add`+Screen+AddToLight 優勢 | 加算スロット (量は色に乗算) | 🔶 |
| `_Matcap(2)Multiply` 優勢 | 乗算スロット (量は定数マスクチャンネル) | 🔶 |
| `_Matcap(2)Replace` 優勢 (poi 既定) | 乗算スロットで近似 | 🔶 ⚠️ |
| `_Matcap(2)Mask` | `_SharedMask` チャンネル | 🔥 パック |
| スロット 3/4 / 回転 / UV モード | — | ❌ |

`_Enable` は [SCConstValue] のため値+キーワードを同時設定。

### ディテール (DetailsConversionModule → Details モジュール)

Poiyomi `_DetailMask` (R=テクスチャ/G=ノーマル) と NonToon のスロット別チャンネル (R=slot0/G=slot1...) を
一致させるため、slot0=テクスチャ / slot1=ノーマルに配置。

| Poiyomi | NonToon | 方式 |
|---|---|---|
| `_DetailTex` (+ST/UV) | `_Detail0Texture` (+ST/UV), Boost=2 (グレー×2 基準の等価値) | ✅ |
| `_DetailNormalMap` (+scale/UV) | `_Detail1NormalMap` 等 | ✅ |
| `_DetailMask` | `_DetailMask` (チャンネル意味が一致) | ✅ |
| `_DetailTexIntensity` ≠ 1 / `_DetailTint` | — | ❌ 警告 |

### その他

| Poiyomi | NonToon | 方式 |
|---|---|---|
| Backlight border/blur | 本体 `_BacklightRange`/`_BacklightSharpness` | 🔶 (色指定は ❌) |
| `_AlphaDistanceFade` min/max/minAlpha | DistanceFade モジュール (0-1m, 黒フェード) | 🔶 ⚠️ 意味が異なる |
| `_LightingMinLightBrightness` | Lighten モジュール (AsEmission=1) | ✅ |
| `_LightingCap` ≠ 1 / グレースケールライティング | — | ❌ |
| `_LineWidth` (cm) | `_OutlineWidth` (cm) — **単位一致** | ✅ |
| `_LineColor` / `_Offset_Z` | `_OutlineColor` / `_OutlineZOffset` | ✅ (NonToon は常にアルベド×色 ⚠️) |
| アウトライン Fixed Size / マスク / テクスチャ / エミッション | — | ❌ |
| AudioLink / LTCGI / SSAO / 深度リム / ノーマル2 / UV スクロール等 | — | ❌ レポート |
| `_EnableEnvironmentalRim` (`_RimEnviroWidth`/`Blur`/`Intensity`) | RimLight モジュール (白×強度、ライト色=環境色が乗る) | 🔶 通常リム未使用時のみ (競合時は ❌) |
| エミッション (スロット1) / 色調整 / グリッター / 環境リフレクション / クリアコート / ハイトマップ視差 / 内部パララックス / ディゾルブ / デカール / 追加マットキャップ / オクルージョン (AO) | 各拡張モジュール | ✅ **jp.rroki.nontoon-extra-modules** 導入時のみ (未導入なら ❌ レポート) |

### 拡張モジュール (jp.rroki.nontoon-extra-modules) のマッピング

| Poiyomi | NonToon (拡張) | 方式 |
|---|---|---|
| `_EmissionColor`/`_EmissionStrength`/`_EmissionMap` (+ST/`_EmissionMapPan`) | `emission` モジュール | ✅ |
| `_EmissionMapUV` 4+ (Distorted 等) | UV0 + スクロール | 🔶 |
| `_EmissionMask` (+channel) / `_EmissionBaseColorAsMap` | 共有マスク ch / MultiplyAlbedo | 🔥 / ✅ |
| `_EmissiveBlink_Min/Max/Velocity` + `_EmissionBlinkingOffset` | 点滅 (正弦波近似) | 🔶 |
| `_Saturation`/`_MainBrightness`/`_MainGamma`/`_MainHueShift` | `coloradjust` モジュール | 🔶 (明度の数式は相違) |
| `_GlitterColor`/`_GlitterFrequency`/`_GlitterDensity`/`_GlitterSize`/`_GlitterContrast`/`_GlitterSpeed`/`_GlitterUseSurfaceColor` | `glitter` モジュール (視線角依存 + 丸い輝点) | 🔶 (サイズ/鋭さは要調整) |
| `_MochieReflectionStrength`/`_MochieReflectionTint`/メタリック、lil `_ApplyReflection`/`_ReflectionColor` | `envreflection` モジュール (プローブ+フレネル、金属=アルベド色反射) | 🔶 |
| `_ClearCoatStrength` × `_ClearCoatReflectionStrength` | `envreflection` の強度へ合算 | 🔶 (コート専用の滑らかさ/第2ローブは非対応) |
| `_HeightMap`/`_HeightStrength`/`_HeightStepsMin`/`_HeightStepsMax`/`_Heightmask`(+チャンネル/反転) | `heightparallax` モジュール (視線角で可変な POM + 交点補間、マスクは専用テクスチャで引き継ぎ) | 🔶 (上書き再サンプル方式) |
| `_ParallaxInternalMap`/深度/レイヤー数/色 (+専用マスクは高さへベイク) | `internalparallax` モジュール (Heightmap は正確な POM 交差) | 🔶 |
| `_DissolveNoiseTexture`/`_DissolveAmount`/UV/スクロール/反転/`_DissolveEdge*`/`_DissolveMask` | `dissolve` モジュール (ノイズ閾値 + 境界発光) | 🔶 (Point-to-Point / 頂点高さ / AudioLink は ❌) |
| `_OcclusionMap`/`_OcclusionMapChannel`/`_OcclusionStrength` | `occlusion` モジュール (静的 AO、既定 Ramp = トゥーンランプ連動) | 🔶 (首元の境目対策は両メッシュへ載せる。Shade 未使用は Mode=Multiply) |
| `_Decal*` (位置/スケール/回転/色/ブレンド/タイリング/エミッション) | `decal` モジュール ×4 | 🔶 |

### ファー (シェーダーごと差し替え)

Poiyomi Fur シェーダーは base NonToon にモジュールとして注入できない (geometry ステージが必要) ため、
**シェーダーごと NonToonFur (シェル法ファー) へ差し替え**て変換する。

| Poiyomi | NonToonFur | 方式 |
|---|---|---|
| `_FurLength` (+`_FurGravity`/`_FurGravityStrength`) | `_FurVector` (法線方向の長さ + 下方向バイアス) | 🔶 (単位差により要調整) |
| `_FurNoiseMask` (+ST) | `_FurNoiseMask` / `_FurNoiseTiling` | ✅ |
| `_FurLayers`/`_FurLayerCount` | `_FurSubdivision` (1-3) | 🔶 (粗い写像) |
| `_FurColor` / `_FurLengthMask` / `_FurWind*` | — | ❌ (毛色はベーステクスチャ側) |

> NonToonFur の Shader Core 設定は基本 (lilxyzw) モジュールのみの最小構成。ファーマテリアルが emission 等の
> 拡張機能を使う場合は、ScModuleChecker が変換時に該当モジュールだけを自動追加する。

## 変換対象外 (将来課題)

- エミッションスロット 2-4 (拡張モジュールはスロット 1 のみ対応)
- メインテクスチャ未割り当て時の `_Color` 引き継ぎ (単色テクスチャの生成)
- RimShade モジュール (Poiyomi 側に 1:1 対応機能なし。深度リム/影リムの写像は保留)
