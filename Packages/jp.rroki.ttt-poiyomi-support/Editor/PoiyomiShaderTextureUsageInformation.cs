#nullable enable
using System.Collections.Generic;
using net.rs64.TexTransTool.TextureAtlas;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rroki.TexTransToolPoiyomiSupport
{
    /// <summary>
    /// Poiyomi Toon / Pro 向けの <see cref="ITTShaderTextureUsageInformation"/> 実装。
    ///
    /// Poiyomi はテクスチャプロパティ _X ごとに UV チャンネルセレクタ _XUV
    /// (UV0=0, UV1=1, UV2=2, UV3=3, Panosphere=4, WorldPos=5, PolarUV=6, DistortedUV=7, LocalPos=8, Matcap=9)
    /// と _X_ST / _XPan を持つ。この規約に基づき、シェーダーの全テクスチャプロパティを
    /// 動的に列挙・分類して TTT に UV 使用情報を提供する。
    /// 規約に従わないスロット (UV0 固定サンプル・LUT・matcap 等) は例外テーブルで扱う。
    /// 例外テーブルは Poiyomi 9.3 / 10.0 (Toon / Pro) のシェーダーソース解析に基づく。
    ///
    /// 分類はシェーダーごとに 1 回だけ行い、マテリアルごとの UV セレクタ値は
    /// TTT から渡される writer 経由で評価ごとに読む。
    /// </summary>
    internal sealed class PoiyomiShaderTextureUsageInformation : ITTShaderTextureUsageInformation
    {
        enum Rule
        {
            /// <summary>_XUV セレクタの値でメッシュ UV チャンネルが決まる</summary>
            MeshUVSelector,
            /// <summary>セレクタを持たず常にメッシュ UV0 でサンプルされる</summary>
            FixedUV0,
            /// <summary>メッシュ UV 由来でない (view space / LUT / cubemap 等) → アトラス化対象外</summary>
            NonMesh,
        }

        readonly struct Entry
        {
            public readonly string PropertyName;
            public readonly Rule Rule;
            public readonly string? UVSelectorName;
            public readonly bool SelectorIsInteger;

            public Entry(string propertyName, Rule rule, string? uvSelectorName = null, bool selectorIsInteger = false)
            {
                PropertyName = propertyName;
                Rule = rule;
                UVSelectorName = uvSelectorName;
                SelectorIsInteger = selectorIsInteger;
            }
        }

        readonly Entry[] _entries;

        internal PoiyomiShaderTextureUsageInformation(Shader shader)
        {
            _entries = Classify(shader);
        }

        public void GetMaterialTextureUVUsage(ITTTextureUVUsageWriter writer)
        {
            foreach (var entry in _entries)
            {
                switch (entry.Rule)
                {
                    case Rule.FixedUV0:
                        writer.WriteTextureUVUsage(entry.PropertyName, UsageUVChannel.UV0);
                        break;
                    case Rule.MeshUVSelector:
                        // Poiyomi の UV セレクタは ShaderLab の legacy Int (float 保存) 宣言だが、
                        // 将来 Integer 宣言に変わっても読めるよう分類時の型で読み分ける。
                        var uvMode = entry.SelectorIsInteger
                            ? writer.GetInteger(entry.UVSelectorName!)
                            : Mathf.RoundToInt(writer.GetFloat(entry.UVSelectorName!));
                        writer.WriteTextureUVUsage(entry.PropertyName, ToUsageUVChannel(uvMode));
                        break;
                    case Rule.NonMesh:
                        writer.WriteTextureUVUsage(entry.PropertyName, UsageUVChannel.Unknown);
                        break;
                }
            }
        }

        static UsageUVChannel ToUsageUVChannel(int uvMode) => uvMode switch
        {
            0 => UsageUVChannel.UV0,
            1 => UsageUVChannel.UV1,
            2 => UsageUVChannel.UV2,
            3 => UsageUVChannel.UV3,
            // Panosphere(4) / World Pos(5) / Polar UV(6) / Distorted UV(7) / Local Pos(8) / Matcap(9)
            _ => UsageUVChannel.Unknown,
        };

        // ---------------- classification ----------------

        // セレクタ名が _XUV 規約に従わない、または Properties に宣言されないスロット
        static readonly Dictionary<string, string> AltUVSelectorNames = new()
        {
            ["_FlipbookMask1"] = "_FlipbookMaskUV1", // Flipbook モジュール 1 のみ逆順命名
            ["_PoiSkinDetailMap"] = "_PoiSkinFreckleMapUV", // freckle / tan UV でサンプル
            ["_VertexBasicsMask"] = "_VertexBasicsMaskUV", // Properties 未宣言のことがある (欠落時は UV0)
        };

        // セレクタを持たず常に poiMesh.uv[0] でサンプルされるスロット
        static readonly HashSet<string> FixedUV0Textures = new()
        {
            "_MetallicGlossMap",
            "_SmoothnessTex",
            "_ReflectionColorTex",
            "_ParallaxInternalMap",
        };

        // メッシュ UV でサンプルされない 2D スロット (LUT / ramp / view space / procedural / 外部入力)
        static readonly HashSet<string> NonMeshTextures = new()
        {
            "_ClothDFG",               // BRDF LUT (NoV × roughness)
            "_DissolveEdgeGradient",   // dissolve エッジ量でサンプル
            "_EmissionScrollingCurve", // 時間カーブ
            "_EmissionScrollingCurve1",
            "_EmissionScrollingCurve2",
            "_EmissionScrollingCurve3",
            "_GlitterTexture",         // グリッターセル空間
            "_MainGradationTex",       // 階調 LUT
            "_Matcap",                 // view space
            "_Matcap2",
            "_Matcap3",
            "_Matcap4",
            "_PoiSkinLUT",             // SSS LUT
            "_SkinLUT",
            "_SquishGradient",         // squish 量でサンプル
            "_TPS_BakedMesh",          // TPS データテクスチャ
            "_TextGlyphs",             // グリフアトラス (テキストグリッド空間)
            "_ToonRamp",               // NdotL ramp
            "_TruchetTex",             // truchet セル空間
            "_Udon_VideoTex",          // 外部ビデオ入力
            "_VertexGlitchMap",        // 時間ノイズ
            "_VideoGameboyRamp",       // LUT
        };

        static readonly HashSet<string> s_warnedUnknown = new();

        static Entry[] Classify(Shader shader)
        {
            var entries = new List<Entry>();
            var count = shader.GetPropertyCount();
            for (var i = 0; i < count; i++)
            {
                if (shader.GetPropertyType(i) != ShaderPropertyType.Texture) { continue; }
                var name = shader.GetPropertyName(i);

                // Cube / 3D / 2DArray はメッシュ UV でサンプルされない
                if (shader.GetPropertyTextureDimension(i) != TextureDimension.Tex2D)
                {
                    entries.Add(new Entry(name, Rule.NonMesh));
                    continue;
                }
                if (FixedUV0Textures.Contains(name))
                {
                    entries.Add(new Entry(name, Rule.FixedUV0));
                    continue;
                }
                if (NonMeshTextures.Contains(name))
                {
                    entries.Add(new Entry(name, Rule.NonMesh));
                    continue;
                }

                var hasAlt = AltUVSelectorNames.TryGetValue(name, out var altSelector);
                var selector = hasAlt ? altSelector! : name + "UV";
                var selectorIndex = shader.FindPropertyIndex(selector);
                if (selectorIndex >= 0)
                {
                    var isInteger = shader.GetPropertyType(selectorIndex) == ShaderPropertyType.Int;
                    entries.Add(new Entry(name, Rule.MeshUVSelector, selector, isInteger));
                    continue;
                }
                if (hasAlt)
                {
                    // セレクタが Properties にないスロット: writer 側の既定値 (0 = UV0) に任せる
                    entries.Add(new Entry(name, Rule.MeshUVSelector, selector));
                    continue;
                }

                // 規約で判定できない未知のスロット: 誤った位置に焼かれるのを避けるため対象外にする
                if (s_warnedUnknown.Add($"{shader.name}/{name}"))
                {
                    Debug.LogWarning(
                        $"[TTT Poiyomi Support] '{shader.name}' の '{name}' は UV 規約を判定できない未知のテクスチャプロパティのため、アトラス化対象から除外します。");
                }
                entries.Add(new Entry(name, Rule.NonMesh));
            }
            return entries.ToArray();
        }
    }
}
