#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// Poiyomi → NonToon 変換のオーケストレーター。
    ///
    /// 変換の流れ:
    /// 1. ソースマテリアルの m_SavedProperties をスナップショット (ロック状態でも読める)
    /// 2. (複製モードなら) .mat を複製
    /// 3. シェーダーを NonToon へ差し替え、Poiyomi 由来のキーワードを全消去
    /// 4. 登録済みの全 ConversionModule を Order 順に実行
    /// 5. 生成物 (グラデーション配列 / パック済みマスク) をアセット化して割り当て
    /// 6. Shader Core モジュールの有効状態を検証
    /// </summary>
    public static class PoiyomiToNonToonConverter
    {
        /// <summary>TypeCache による変換モジュールの自動発見 (他パッケージからの拡張込み)。</summary>
        public static List<ConversionModule> CreateModules() =>
            TypeCache.GetTypesDerivedFrom<ConversionModule>()
                .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (ConversionModule)Activator.CreateInstance(t))
                .OrderBy(m => m.Order)
                .ToList();

        public static bool CanConvert(Material? material) => PoiyomiMaterialSnapshot.IsPoiyomi(material);

        /// <summary>複数マテリアルを変換する。</summary>
        public static List<ConversionReport> Convert(IEnumerable<Material> materials, ConversionOptions options)
        {
            var modules = CreateModules();
            var reports = new List<ConversionReport>();
            var list = materials.Distinct().ToList();
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var mat = list[i];
                    EditorUtility.DisplayProgressBar("Poiyomi → NonToon", mat.name, (float)i / list.Count);
                    reports.Add(ConvertSingle(mat, options, modules));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.SaveAssets();
            foreach (var report in reports) Debug.Log(report.ToConsoleString(), report.Result);
            return reports;
        }

        public static ConversionReport ConvertSingle(Material source, ConversionOptions options)
            => ConvertSingle(source, options, CreateModules());

        static ConversionReport ConvertSingle(Material source, ConversionOptions options, List<ConversionModule> modules)
        {
            var report = new ConversionReport { Source = source };

            if (!PoiyomiMaterialSnapshot.IsPoiyomi(source))
                return Fail(report, "Poiyomi マテリアルではありません");

            var nonToon = Shader.Find(NonToonProps.ShaderName);
            if (nonToon == null)
                return Fail(report, "NonToon シェーダーが見つかりません (jp.lilxyzw.nontoon がインストールされているか確認してください)");

            var sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath))
                return Fail(report, "アセット化されていないマテリアルは変換できません");

            var snapshot = PoiyomiMaterialSnapshot.Load(source);

            // Fur シェーダーはシェル法ファーを持つ NonToonFur へ変換する
            // (base NonToon シェーダーは geometry ステージを持たないためモジュールでは再現不可)
            var targetShader = nonToon;
            if (snapshot.IsFurShader)
            {
                var furShader = Shader.Find(NonToonProps.FurShaderName);
                if (furShader != null)
                {
                    targetShader = furShader;
                    report.Info("Fur", "Poiyomi Fur シェーダーを NonToonFur (シェル法ファー) へ変換します");
                }
                else
                {
                    report.Warn("Fur", "NonToonFur シェーダーが見つからないため通常の NonToon へ変換します (ファーは失われます)");
                }
            }
            if (snapshot.IsLocked)
                report.Info("ロック", "ロック済みマテリアルのため、保存済みプロパティ値から変換しました");

            // ---- ターゲットマテリアルの決定 ----
            Material target;
            if (options.CreateCopy)
            {
                var dir = Path.GetDirectoryName(sourcePath)!.Replace('\\', '/');
                var newPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{dir}/{Path.GetFileNameWithoutExtension(sourcePath)}{options.CopySuffix}.mat");
                if (!AssetDatabase.CopyAsset(sourcePath, newPath))
                    return Fail(report, "マテリアルの複製に失敗しました");
                target = AssetDatabase.LoadAssetAtPath<Material>(newPath);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(source, "Convert Poiyomi to NonToon");
                target = source;
            }
            report.Result = target;

            var targetPath = AssetDatabase.GetAssetPath(target);
            var assetBasePath = $"{Path.GetDirectoryName(targetPath)!.Replace('\\', '/')}/{Path.GetFileNameWithoutExtension(targetPath)}";

            try
            {
                // ---- シェーダー差し替え + Poiyomi キーワード全消去 ----
                target.shader = targetShader;
                target.shaderKeywords = Array.Empty<string>();
                target.renderQueue = -1; // 各モジュール (Base) が必要に応じて上書き

                var context = new ConversionContext(snapshot, target, options, report, assetBasePath);

                // ---- 必要モジュールの事前宣言 → 有効化 (シェーダー再生成) ----
                // 拡張モジュール (scmodule) 由来のプロパティは、モジュールが有効化されて
                // シェーダーが再生成されるまで存在しないため、Convert より前に済ませる。
                foreach (var module in modules)
                {
                    try
                    {
                        if (module.ShouldRun(context)) module.DeclareRequirements(context);
                    }
                    catch (Exception e)
                    {
                        report.Warn(module.DisplayName, $"モジュール要求の宣言中にエラー: {e.Message}");
                    }
                }
                ScModuleChecker.EnsureModules(targetShader, context.RequiredScModules.ToArray(), report);

                // ---- 変換モジュール実行 ----
                foreach (var module in modules)
                {
                    try
                    {
                        if (module.ShouldRun(context)) module.Convert(context);
                    }
                    catch (Exception e)
                    {
                        report.Warn(module.DisplayName, $"変換中にエラーが発生し、この機能をスキップしました: {e.Message}");
                        Debug.LogException(e);
                    }
                }

                FinalizeAssets(context);
                // Convert 中に追加宣言されたモジュールがあれば次回のために有効化しておく
                ScModuleChecker.EnsureModules(targetShader, context.RequiredScModules.ToArray(), report);

                // 旧 Poiyomi プロパティ (不可視のテクスチャ参照等) の掃除。
                // 残すとビルドの依存収集に旧テクスチャが含まれ、アバター容量が膨張する
                if (options.RemoveUnusedProperties)
                {
                    var cleaned = MaterialPropertyCleaner.RemoveUndeclaredProperties(target);
                    if (cleaned.Total > 0)
                        report.Info("クリーンアップ",
                            $"シェーダー未宣言の旧プロパティを削除しました (テクスチャ参照 {cleaned.Textures} / 数値 {cleaned.Floats + cleaned.Ints} / 色 {cleaned.Colors})");
                }

                EditorUtility.SetDirty(target);
                return report;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return Fail(report, e.Message);
            }
        }

        /// <summary>グラデーション配列・パック済みマスクのアセット化と割り当て。</summary>
        static void FinalizeAssets(ConversionContext context)
        {
            if (context.GradientLayers.Count > 0)
            {
                var path = $"{context.AssetBasePath}_gradients.asset";
                var array = TextureBaker.SaveGradientArray(context.GradientLayers.ToArray(), path);
                context.SetTexture(NonToonProps.SharedGradients, array);
                context.Report.Info("グラデーション", $"影/ハイライトを {context.GradientLayers.Count} 本のグラデーションにベイクしました: {Path.GetFileName(path)}");
            }

            if (context.MaskChannels.Any(c => c != null))
            {
                var path = $"{context.AssetBasePath}_mask.png";
                var size = context.MaskChannels
                    .Where(c => c?.Texture != null)
                    .Select(c => Mathf.Max(c!.Texture!.width, c.Texture.height))
                    .DefaultIfEmpty(256).Max();
                size = Mathf.Clamp(Mathf.NextPowerOfTwo(size), 64, 2048);

                var packed = TextureBaker.PackMask(context.MaskChannels, size, size, path);
                context.SetTexture(NonToonProps.SharedMask, packed);

                var labels = context.MaskChannels
                    .Select((c, i) => c == null ? null : $"{"RGBA"[i]}={c.Label}")
                    .Where(s => s != null);
                context.Report.Info("マスク", $"マスクを _SharedMask にパックしました ({string.Join(", ", labels)}): {Path.GetFileName(path)}");
            }
        }

        static ConversionReport Fail(ConversionReport report, string reason)
        {
            report.Succeeded = false;
            report.FailureReason = reason;
            return report;
        }
    }
}
