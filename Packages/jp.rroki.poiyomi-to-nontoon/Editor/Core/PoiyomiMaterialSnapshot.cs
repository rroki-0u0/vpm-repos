#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// Poiyomi マテリアルのシリアライズ済みプロパティ (m_SavedProperties) のスナップショット。
    ///
    /// Material.GetFloat 等は現在のシェーダーに存在するプロパティしか信頼できないが、
    /// m_SavedProperties にはロック時 (Hidden/Locked/...) やシェーダー欠落時でも
    /// 全プロパティ値が残っているため、SerializedObject 経由で直接読み取る。
    /// これにより Thry のアンロック API に依存せず、Poiyomi 本体が削除された
    /// プロジェクトのマテリアルすら変換できる。
    /// </summary>
    public sealed class PoiyomiMaterialSnapshot
    {
        public Material Material { get; }
        public string ShaderName { get; }
        /// <summary>ロック時は OriginalShader タグ、非ロック時はシェーダー名。</summary>
        public string OriginalShaderName { get; }
        /// <summary>m_CustomRenderQueue の生値 (-1 = シェーダー既定)。</summary>
        public int RawRenderQueue { get; }
        public bool IsLocked { get; }

        readonly Dictionary<string, float> _floats = new();
        readonly Dictionary<string, int> _ints = new();
        readonly Dictionary<string, Color> _colors = new();
        readonly Dictionary<string, (Texture? tex, Vector2 scale, Vector2 offset)> _texEnvs = new();

        PoiyomiMaterialSnapshot(Material material)
        {
            Material = material;
            ShaderName = material.shader != null ? material.shader.name : "";
            var tag = material.GetTag("OriginalShader", false, "");
            OriginalShaderName = string.IsNullOrEmpty(tag) ? ShaderName : tag;
            IsLocked = ShaderName.StartsWith("Hidden/Locked/", StringComparison.Ordinal);

            using var so = new SerializedObject(material);
            RawRenderQueue = so.FindProperty("m_CustomRenderQueue")?.intValue ?? -1;

            ReadPairs(so, "m_SavedProperties.m_Floats", p => _floats[Name(p)] = p.FindPropertyRelative("second").floatValue);
            ReadPairs(so, "m_SavedProperties.m_Ints", p => _ints[Name(p)] = p.FindPropertyRelative("second").intValue);
            ReadPairs(so, "m_SavedProperties.m_Colors", p => _colors[Name(p)] = p.FindPropertyRelative("second").colorValue);
            ReadPairs(so, "m_SavedProperties.m_TexEnvs", p =>
            {
                var second = p.FindPropertyRelative("second");
                _texEnvs[Name(p)] = (
                    second.FindPropertyRelative("m_Texture").objectReferenceValue as Texture,
                    second.FindPropertyRelative("m_Scale").vector2Value,
                    second.FindPropertyRelative("m_Offset").vector2Value);
            });

            static string Name(SerializedProperty pair) => pair.FindPropertyRelative("first").stringValue;
        }

        static void ReadPairs(SerializedObject so, string path, Action<SerializedProperty> read)
        {
            var array = so.FindProperty(path);
            if (array == null || !array.isArray) return;
            for (int i = 0; i < array.arraySize; i++) read(array.GetArrayElementAtIndex(i));
        }

        public static PoiyomiMaterialSnapshot Load(Material material) => new(material);

        // ---- 判定 ----

        /// <summary>Poiyomi 系マテリアルか (ロック済み・シェーダー欠落込み)。</summary>
        public static bool IsPoiyomi(Material? material)
        {
            if (material == null) return false;
            var name = material.shader != null ? material.shader.name : "";
            if (ContainsPoiyomi(name)) return true;
            return ContainsPoiyomi(material.GetTag("OriginalShader", false, ""));

            static bool ContainsPoiyomi(string s) =>
                s.IndexOf("poiyomi", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool IsFurShader =>
            OriginalShaderName.IndexOf("fur", StringComparison.OrdinalIgnoreCase) >= 0;

        // ---- 値アクセス ----
        // Poiyomi のプロパティはほぼ全てレガシー Int / Float (= m_Floats)。m_Ints もフォールバックで見る。

        public bool HasFloat(string name) => _floats.ContainsKey(name) || _ints.ContainsKey(name);
        public bool HasColor(string name) => _colors.ContainsKey(name);
        public bool HasTexture(string name) => _texEnvs.ContainsKey(name);

        public float GetFloat(string name, float defaultValue = 0f)
        {
            if (_floats.TryGetValue(name, out var f)) return f;
            if (_ints.TryGetValue(name, out var i)) return i;
            return defaultValue;
        }

        public int GetInt(string name, int defaultValue = 0) =>
            Mathf.RoundToInt(GetFloat(name, defaultValue));

        public bool GetToggle(string name, bool defaultValue = false) =>
            GetFloat(name, defaultValue ? 1f : 0f) >= 0.5f;

        public Color GetColor(string name, Color defaultValue) =>
            _colors.TryGetValue(name, out var c) ? c : defaultValue;

        public Texture? GetTexture(string name) =>
            _texEnvs.TryGetValue(name, out var t) ? t.tex : null;

        public Vector2 GetTextureScale(string name) =>
            _texEnvs.TryGetValue(name, out var t) ? t.scale : Vector2.one;

        public Vector2 GetTextureOffset(string name) =>
            _texEnvs.TryGetValue(name, out var t) ? t.offset : Vector2.zero;

        public bool HasNonDefaultST(string name) =>
            GetTextureScale(name) != Vector2.one || GetTextureOffset(name) != Vector2.zero;
    }
}
