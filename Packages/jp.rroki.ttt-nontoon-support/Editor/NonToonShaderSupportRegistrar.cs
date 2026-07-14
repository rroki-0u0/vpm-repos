#nullable enable
using System;
using net.rs64.TexTransTool.TextureAtlas;
using UnityEditor;
using UnityEngine;

namespace Rroki.TexTransToolNonToonSupport
{
    /// <summary>
    /// プロジェクト内の NonToon / NonToonFur シェーダーを
    /// TTT の <see cref="TTShaderTextureUsageInformationRegistry"/> へ登録する。
    ///
    /// 登録タイミング:
    /// - ドメインリロード直後
    /// - .scshader のインポート時 (Shader Core のモジュール有効化/無効化でシェーダーが
    ///   再生成されテクスチャプロパティ構成が変わるため、その都度分類を作り直して再登録する)
    /// </summary>
    internal static class NonToonShaderSupportRegistrar
    {
        [InitializeOnLoadMethod]
        static void RegisterExistingShaders()
        {
            foreach (var info in ShaderUtil.GetAllShaderInfo())
            {
                if (IsNonToonShaderName(info.name) is false) { continue; }
                var shader = Shader.Find(info.name);
                if (shader == null) { continue; }
                TryRegister(shader);
            }
            // 同名シェーダーが複数ファイルある場合に備え、アセット由来の Shader も個別に登録する
            foreach (var guid in AssetDatabase.FindAssets("NonToon t:Shader"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) { continue; }
                if (IsNonToonShaderName(shader.name) is false) { continue; }
                TryRegister(shader);
            }
        }

        internal static bool IsNonToonShaderName(string shaderName)
            => shaderName.StartsWith("NonToon", StringComparison.Ordinal);

        internal static void TryRegister(Shader shader)
        {
            // Shader Core (SCSample sd.uv) の規約を持たないものは対象外
            if (shader.FindPropertyIndex("_BaseTexture") < 0) { return; }
            TTShaderTextureUsageInformationRegistry.RegisterTTShaderTextureUsageInformation(
                shader, new NonToonShaderTextureUsageInformation(shader));
        }
    }

    /// <summary>
    /// モジュール構成の変更で再生成された NonToon シェーダーを追い掛けて再登録する。
    /// (登録済み情報はシェーダー単位の分類キャッシュを持つため、プロパティ構成が
    /// 変わったら作り直す必要がある)
    /// </summary>
    sealed class NonToonShaderImportWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (path.EndsWith(".scshader", StringComparison.OrdinalIgnoreCase) is false) { continue; }
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) { continue; }
                if (NonToonShaderSupportRegistrar.IsNonToonShaderName(shader.name) is false) { continue; }
                NonToonShaderSupportRegistrar.TryRegister(shader);
            }
        }
    }
}
