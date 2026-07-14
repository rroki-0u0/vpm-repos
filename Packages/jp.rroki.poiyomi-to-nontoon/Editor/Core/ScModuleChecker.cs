#nullable enable
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// Shader Core のモジュール有効状態 (ProjectSettings/jp.lilxyzw.shadercore.asset) の検証。
    ///
    /// jp.lilxyzw.shadercore.ProjectSettings は internal のためリフレクションで触る。
    /// 仕様 (shadercore 0.1.5): シェーダー "名" 単位で有効モジュール ID のリストを保持し、
    /// 未登録シェーダーの初回参照時はシェーダーと同じディレクトリ配下の全 .scmodule を
    /// 有効にしたエントリが自動生成される (= 既定で NonToon の全モジュールが有効)。
    /// 他パッケージ由来のモジュール (jp.rroki.* 等) は自動有効化されないため、ここで追加する。
    /// </summary>
    internal static class ScModuleChecker
    {
        /// <summary>
        /// 必要なモジュールがシェーダーで有効かつ実際にコンパイル済みかを確認し、
        /// 不足があれば有効化 + シェーダー再生成を行う。
        /// 「設定リストに載っているがシェーダーに反映されていない」状態も検出する
        /// (モジュールの後入れや過去の失敗で起こり得るため、リストではなく
        /// 生成済みシェーダーのプロパティ存在で判定する)。
        /// 失敗してもレポートに残すだけで変換自体は続行する。
        /// </summary>
        public static void EnsureModules(Shader shader, string[] requiredIds, ConversionReport report)
        {
            if (requiredIds.Length == 0) return;
            try
            {
                var shaderPath = AssetDatabase.GetAssetPath(shader);
                if (string.IsNullOrEmpty(shaderPath))
                {
                    report.Warn("モジュール", "NonToon シェーダーのアセットパスを取得できず、モジュール有効状態を確認できませんでした");
                    return;
                }

                // シェーダーに反映済みのモジュールはプロパティの名前空間プレフィックスで判定できる
                // (プロパティを 1 つも持たないモジュールは判定不能だが、変換対象は必ず持つ)
                var notCompiled = requiredIds.Where(id => !ShaderHasModuleProps(shader, id)).ToArray();
                if (notCompiled.Length == 0) return;

                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "jp.lilxyzw.shadercore")
                    ?.GetType("jp.lilxyzw.shadercore.ProjectSettings");
                var getModules = type?.GetMethod("GetShaderModules",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (getModules == null)
                {
                    report.Warn("モジュール", "Shader Core の ProjectSettings API が見つからず、モジュールを有効化できませんでした");
                    return;
                }

                // GetShaderModules はライブな List<string> を返す (未登録なら全モジュール有効で自動生成)
                if (getModules.Invoke(null, new object[] { shaderPath }) is not IList modules)
                {
                    report.Warn("モジュール", "モジュールリストを取得できませんでした");
                    return;
                }

                bool listChanged = false;
                foreach (var id in notCompiled)
                {
                    if (modules.Cast<object>().Any(m => Equals(m, id))) continue;
                    modules.Add(id);
                    listChanged = true;
                }

                if (listChanged) SaveProjectSettings(type!);
                AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceSynchronousImport);

                var stillMissing = notCompiled.Where(id => !ShaderHasModuleProps(shader, id)).ToArray();
                if (stillMissing.Length > 0)
                    report.Warn("モジュール",
                        $"モジュールをシェーダーに反映できませんでした: {string.Join(", ", stillMissing)}。" +
                        "対応する .scmodule パッケージがインストールされているか確認してください");
                else
                    report.Info("モジュール", $"Shader Core モジュールを有効化してシェーダーを再生成しました: {string.Join(", ", notCompiled)}");
            }
            catch (Exception e)
            {
                report.Warn("モジュール",
                    $"モジュール有効状態の確認中にエラー: {e.Message}。" +
                    "Project Settings > Shader Core で必要モジュールが有効か確認してください");
            }
        }

        /// <summary>モジュール由来のプロパティ (プレフィックス "_&lt;id&gt;_") がシェーダーに存在するか。</summary>
        static bool ShaderHasModuleProps(Shader shader, string moduleId)
        {
            var prefix = "_" + moduleId.Replace('.', '_') + "_";
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
                if (shader.GetPropertyName(i).StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// ProjectSettings.instance.Save() を呼ぶ。instance は ScriptableSingleton&lt;T&gt; 基底と
        /// 派生の両方に見えるため PropertyType で一意に絞る (GetProperty 単発だと Ambiguous match)。
        /// </summary>
        static void SaveProjectSettings(Type type)
        {
            var instance = type
                .GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
                .FirstOrDefault(p => p.Name == "instance" && p.PropertyType == type)
                ?.GetValue(null);
            if (instance == null) return;

            var save = instance.GetType().GetMethod("Save",
                           BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                           null, Type.EmptyTypes, null)
                       ?? instance.GetType().GetMethod("Save",
                           BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                           null, new[] { typeof(bool) }, null);
            save?.Invoke(instance, save.GetParameters().Length == 0 ? null : new object[] { true });
        }
    }
}
