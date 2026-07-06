#nullable enable
using System;
using net.rs64.TexTransTool.TextureAtlas;
using UnityEditor;
using UnityEngine;

namespace Rroki.TexTransToolPoiyomiSupport
{
    /// <summary>
    /// プロジェクト内の Poiyomi シェーダー (Thry Optimizer でロックされた Hidden/Locked/.poiyomi/* を含む) を
    /// TTT の <see cref="TTShaderTextureUsageInformationRegistry"/> へ登録する。
    ///
    /// 登録タイミング:
    /// - ドメインリロード直後 (遅延なし。TTT のレジストリは静的辞書のため初期化順に依存しない)
    /// - シェーダーアセットのインポート時 (マテリアルロックでセッション中に生成される最適化シェーダー対応)
    /// </summary>
    internal static class PoiyomiShaderSupportRegistrar
    {
        [InitializeOnLoadMethod]
        static void RegisterExistingShaders()
        {
            // 1. 名前で解決できるシェーダー (ファイル名に poiyomi を含まないフォーク等もカバー)
            foreach (var info in ShaderUtil.GetAllShaderInfo())
            {
                if (IsPoiyomiShaderName(info.name) is false) { continue; }
                var shader = Shader.Find(info.name);
                if (shader == null) { continue; }
                TryRegister(shader);
            }
            // 2. 同名シェーダーが複数ファイルある場合 (例: 9.3 と 10.0 の ".poiyomi/Poiyomi Toon")、
            //    Shader.Find は 1 つしか返さない。レジストリは Shader 参照がキーのため、
            //    アセット由来の Shader オブジェクトも個別に登録する。
            foreach (var guid in AssetDatabase.FindAssets("Poiyomi t:Shader"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) { continue; }
                if (IsPoiyomiShaderName(shader.name) is false) { continue; }
                TryRegister(shader);
            }
        }

        internal static bool IsPoiyomiShaderName(string shaderName)
            => shaderName.IndexOf("poiyomi", StringComparison.OrdinalIgnoreCase) >= 0;

        internal static void TryRegister(Shader shader)
        {
            // poiUV 規約 (_MainTexUV) を持たないもの (Extras の FakeShadow 等) は対象外。
            // それらは TTT 標準のフォールバック (_MainTex = UV0) に任せる。
            if (shader.FindPropertyIndex("_MainTexUV") < 0) { return; }
            TTShaderTextureUsageInformationRegistry.RegisterTTShaderTextureUsageInformation(
                shader, new PoiyomiShaderTextureUsageInformation(shader));
        }
    }

    /// <summary>セッション中に生成・再インポートされた Poiyomi シェーダーを追加登録する。</summary>
    sealed class PoiyomiShaderImportWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) is false) { continue; }
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) { continue; }
                if (PoiyomiShaderSupportRegistrar.IsPoiyomiShaderName(shader.name) is false) { continue; }
                PoiyomiShaderSupportRegistrar.TryRegister(shader);
            }
        }
    }
}
