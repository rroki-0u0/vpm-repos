#nullable enable
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>右クリックメニュー登録。</summary>
    internal static class ConverterMenus
    {
        const string AssetsMenu = "Assets/rroki_'s tools/Poiyomi → NonToon 変換...";
        const string ContextMenu = "CONTEXT/Material/Poiyomi → NonToon 変換...";

        [MenuItem(AssetsMenu, false, 2000)]
        static void ConvertSelected()
        {
            ConverterWindow.OpenWith(Selection.objects.OfType<Material>());
        }

        [MenuItem(AssetsMenu, true)]
        static bool ValidateConvertSelected() =>
            Selection.objects.OfType<Material>().Any(PoiyomiToNonToonConverter.CanConvert);

        [MenuItem(ContextMenu, false, 2000)]
        static void ConvertFromInspector(MenuCommand command)
        {
            if (command.context is Material mat)
                ConverterWindow.OpenWith(new[] { mat });
        }

        [MenuItem(ContextMenu, true)]
        static bool ValidateConvertFromInspector(MenuCommand command) =>
            command.context is Material mat && PoiyomiToNonToonConverter.CanConvert(mat);

        // ---- 未使用プロパティの掃除 (変換済みマテリアル向け) ----

        const string CleanMenu = "Assets/rroki_'s tools/マテリアルの未使用プロパティを削除";

        [MenuItem(CleanMenu, false, 2001)]
        static void CleanSelected()
        {
            int materials = 0;
            int removed = 0;
            foreach (var mat in Selection.objects.OfType<Material>())
            {
                var result = MaterialPropertyCleaner.RemoveUndeclaredProperties(mat);
                if (result.Total <= 0) continue;
                materials++;
                removed += result.Total;
                Debug.Log($"[Poiyomi→NonToon] {mat.name}: 未宣言プロパティを {result.Total} 個削除 (テクスチャ参照 {result.Textures})", mat);
            }
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"[Poiyomi→NonToon] クリーンアップ完了: {materials} マテリアル / {removed} プロパティ削除");
        }

        [MenuItem(CleanMenu, true)]
        static bool ValidateCleanSelected() => Selection.objects.OfType<Material>().Any();
    }
}
