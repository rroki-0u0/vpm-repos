#nullable enable
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// マテリアルの m_SavedProperties から、現在のシェーダーが宣言していない
    /// プロパティを削除する。
    ///
    /// Unity はシェーダー切り替え後も旧プロパティ (テクスチャ参照を含む) を保持し続け、
    /// ビルド時の依存収集はそれらを全て含めるため、Poiyomi から変換したマテリアルは
    /// 掃除しないと旧テクスチャ参照でアバター容量が膨張する。
    /// </summary>
    public static class MaterialPropertyCleaner
    {
        public readonly struct CleanResult
        {
            public readonly int Textures;
            public readonly int Floats;
            public readonly int Ints;
            public readonly int Colors;

            public CleanResult(int textures, int floats, int ints, int colors)
            {
                Textures = textures;
                Floats = floats;
                Ints = ints;
                Colors = colors;
            }

            public int Total => Textures + Floats + Ints + Colors;
        }

        /// <summary>
        /// シェーダーに宣言されていない保存済みプロパティを削除する。
        /// 戻り値は削除数 (種類別)。
        /// </summary>
        public static CleanResult RemoveUndeclaredProperties(Material material)
        {
            var shader = material.shader;
            if (shader == null) { return new CleanResult(); }

            var declared = new HashSet<string>();
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++) { declared.Add(shader.GetPropertyName(i)); }

            Undo.RegisterCompleteObjectUndo(material, "Remove Undeclared Material Properties");
            using var so = new SerializedObject(material);
            int textures = StripArray(so, "m_SavedProperties.m_TexEnvs", declared);
            int floats = StripArray(so, "m_SavedProperties.m_Floats", declared);
            int ints = StripArray(so, "m_SavedProperties.m_Ints", declared);
            int colors = StripArray(so, "m_SavedProperties.m_Colors", declared);
            so.ApplyModifiedProperties();

            if (textures + floats + ints + colors > 0) { EditorUtility.SetDirty(material); }
            return new CleanResult(textures, floats, ints, colors);
        }

        static int StripArray(SerializedObject so, string arrayPath, HashSet<string> declared)
        {
            var array = so.FindProperty(arrayPath);
            if (array == null || array.isArray is false) { return 0; }

            int removed = 0;
            for (int i = array.arraySize - 1; i >= 0; i--)
            {
                var name = array.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                if (declared.Contains(name)) { continue; }
                array.DeleteArrayElementAtIndex(i);
                removed++;
            }
            return removed;
        }
    }
}
