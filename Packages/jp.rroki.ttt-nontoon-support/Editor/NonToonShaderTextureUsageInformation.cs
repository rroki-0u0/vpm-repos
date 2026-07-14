#nullable enable
using System.Collections.Generic;
using net.rs64.TexTransTool.TextureAtlas;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rroki.TexTransToolNonToonSupport
{
    /// <summary>
    /// NonToon / NonToonFur 向けの <see cref="ITTShaderTextureUsageInformation"/> 実装。
    ///
    /// NonToon (lilxyzw Shader Core) の規約:
    /// - 本体テクスチャ (_BaseTexture / _SharedMask / _NormalMap) は常にメッシュ UV0 (ST なし)
    /// - モジュール由来のプロパティは「_ + moduleID(.→_) + 元名」に改名されて埋め込まれるため、
    ///   末尾 (元名) のパターンで分類する
    /// - UV セレクタを持つスロット (Details / Emission / InternalParallax) は SC_uint (Integer 型) の
    ///   セレクタ値 (UV0=0..UV3=3) でチャンネルが決まる
    /// - マットキャップはビュー空間、ディザはスクリーン空間、デカールは位置/回転変換された
    ///   サブ UV 空間のため、いずれもアトラス化対象外 (Unknown)
    ///
    /// 分類はシェーダーごとに 1 回だけ行う。Shader Core はモジュール構成変更でシェーダーを
    /// 再生成するため、.scshader 再インポート時に Registrar が本クラスを作り直す。
    /// </summary>
    internal sealed class NonToonShaderTextureUsageInformation : ITTShaderTextureUsageInformation
    {
        enum Rule
        {
            /// <summary>常にメッシュ UV0 でサンプルされる</summary>
            FixedUV0,
            /// <summary>UV セレクタプロパティの値でメッシュ UV チャンネルが決まる</summary>
            MeshUVSelector,
            /// <summary>メッシュ UV 由来でない → アトラス化対象外</summary>
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

        internal NonToonShaderTextureUsageInformation(Shader shader)
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
            _ => UsageUVChannel.Unknown,
        };

        // ---------------- classification ----------------

        // 本体 (素の名前) の固定分類
        static readonly Dictionary<string, Rule> ExactRules = new()
        {
            ["_BaseTexture"] = Rule.FixedUV0,
            ["_SharedMask"] = Rule.FixedUV0,
            ["_NormalMap"] = Rule.FixedUV0,
            ["_NTDitherTex"] = Rule.NonMesh,   // スクリーン座標の 4x4 ディザ
            ["_FurNoiseMask"] = Rule.NonMesh,  // _FurNoiseTiling 乗算のリピートサンプル (アトラス化不可)
        };

        // モジュール由来プロパティの末尾 → 固定分類
        // (末尾一致。より具体的なパターンを先に評価する)
        static readonly (string suffix, Rule rule)[] SuffixRules =
        {
            ("_DetailMask", Rule.FixedUV0),        // Details: 共有 RGBA マスク (UV0)
            ("_SDFMap", Rule.FixedUV0),            // Shade: SDF 顔影 (UV0)
            ("_ParallaxHeightMap", Rule.FixedUV0), // HeightParallax: UV0 基準の視差
            ("_MatCapMultiply", Rule.NonMesh),     // MatCaps / MatCapsExtra: ビュー空間
            ("_MatCapAdd", Rule.NonMesh),
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

                // Cube / 3D / 2DArray (_SharedGradients 等) はメッシュ UV でサンプルされない
                if (shader.GetPropertyTextureDimension(i) != TextureDimension.Tex2D)
                {
                    entries.Add(new Entry(name, Rule.NonMesh));
                    continue;
                }

                if (ExactRules.TryGetValue(name, out var exactRule))
                {
                    entries.Add(new Entry(name, exactRule));
                    continue;
                }

                var suffixMatched = false;
                foreach (var (suffix, rule) in SuffixRules)
                {
                    if (name.EndsWith(suffix, System.StringComparison.Ordinal) is false) { continue; }
                    entries.Add(new Entry(name, rule));
                    suffixMatched = true;
                    break;
                }
                if (suffixMatched) { continue; }

                // デカールは位置/スケール/回転変換されたサブ UV 空間 → アトラス化すると位置が壊れるため対象外
                if (TryMatchDecal(name))
                {
                    entries.Add(new Entry(name, Rule.NonMesh));
                    continue;
                }

                // UV セレクタ付きスロット
                if (TryGetUVSelector(name, out var selector))
                {
                    var selectorIndex = shader.FindPropertyIndex(selector);
                    if (selectorIndex >= 0)
                    {
                        var isInteger = shader.GetPropertyType(selectorIndex) == ShaderPropertyType.Int;
                        entries.Add(new Entry(name, Rule.MeshUVSelector, selector, isInteger));
                        continue;
                    }
                }

                // 未知のスロット: 誤った位置に焼かれるのを避けるため対象外にする
                if (s_warnedUnknown.Add($"{shader.name}/{name}"))
                {
                    Debug.LogWarning(
                        $"[TTT NonToon Support] '{shader.name}' の '{name}' は UV 規約を判定できない未知のテクスチャプロパティのため、アトラス化対象から除外します。");
                }
                entries.Add(new Entry(name, Rule.NonMesh));
            }
            return entries.ToArray();
        }

        /// <summary>末尾が "_Decal{数字}Texture" (jp.rroki.nontoon.decal のスロット) かどうか。</summary>
        static bool TryMatchDecal(string name)
        {
            const string tail = "Texture";
            if (name.EndsWith(tail, System.StringComparison.Ordinal) is false) { return false; }
            var digitIndex = name.Length - tail.Length - 1;
            if (digitIndex < 0 || char.IsDigit(name[digitIndex]) is false) { return false; }
            return name.Substring(0, digitIndex).EndsWith("_Decal", System.StringComparison.Ordinal);
        }

        /// <summary>UV セレクタ規約: スロット末尾 → セレクタプロパティ名。</summary>
        static bool TryGetUVSelector(string name, out string selector)
        {
            // Details: _Detail{N}Texture / _Detail{N}NormalMap → _Detail{N}UV
            const string detailTexTail = "Texture";
            const string detailNormalTail = "NormalMap";
            if (name.EndsWith(detailTexTail, System.StringComparison.Ordinal))
            {
                var stem = name.Substring(0, name.Length - detailTexTail.Length);
                if (stem.Length > 0 && char.IsDigit(stem[stem.Length - 1]) && stem.Contains("_Detail"))
                {
                    selector = stem + "UV";
                    return true;
                }
            }
            if (name.EndsWith(detailNormalTail, System.StringComparison.Ordinal))
            {
                var stem = name.Substring(0, name.Length - detailNormalTail.Length);
                if (stem.Length > 0 && char.IsDigit(stem[stem.Length - 1]) && stem.Contains("_Detail"))
                {
                    selector = stem + "UV";
                    return true;
                }
            }
            // Emission: _EmissionMap → _EmissionMapUV
            if (name.EndsWith("_EmissionMap", System.StringComparison.Ordinal))
            {
                selector = name + "UV";
                return true;
            }
            // InternalParallax: _InternalMap → _InternalUV
            if (name.EndsWith("_InternalMap", System.StringComparison.Ordinal))
            {
                selector = name.Substring(0, name.Length - "Map".Length) + "UV";
                return true;
            }
            selector = string.Empty;
            return false;
        }
    }
}
