#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>Poiyomi → NonToon 変換ウィンドウ。</summary>
    internal sealed class ConverterWindow : EditorWindow
    {
        readonly List<Material> _materials = new();
        readonly ConversionOptions _options = new();
        List<ConversionReport>? _reports;
        Vector2 _scrollMaterials;
        Vector2 _scrollReports;

        [MenuItem("Tools/rroki_'s tool/Poiyomi to NonToon Converter")]
        public static void Open()
        {
            var window = GetWindow<ConverterWindow>("Poiyomi → NonToon");
            window.CollectFromSelection();
            window.Show();
        }

        public static void OpenWith(IEnumerable<Material> materials)
        {
            var window = GetWindow<ConverterWindow>("Poiyomi → NonToon");
            window._materials.Clear();
            window._materials.AddRange(materials.Where(PoiyomiToNonToonConverter.CanConvert).Distinct());
            window._reports = null;
            window.Show();
        }

        void CollectFromSelection()
        {
            _materials.Clear();
            _reports = null;
            foreach (var obj in Selection.objects)
            {
                switch (obj)
                {
                    case Material mat:
                        Add(mat);
                        break;
                    case GameObject go:
                        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                        foreach (var mat in renderer.sharedMaterials)
                            Add(mat);
                        break;
                }
            }

            void Add(Material? mat)
            {
                if (mat != null && PoiyomiToNonToonConverter.CanConvert(mat) && !_materials.Contains(mat))
                    _materials.Add(mat);
            }
        }

        void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("変換対象 (Poiyomi マテリアル)", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("選択から取得")) CollectFromSelection();
                if (GUILayout.Button("クリア")) { _materials.Clear(); _reports = null; }
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollMaterials, GUILayout.MaxHeight(150)))
            {
                _scrollMaterials = scroll.scrollPosition;
                if (_materials.Count == 0)
                    EditorGUILayout.HelpBox("マテリアルまたは GameObject を選択して「選択から取得」を押してください", MessageType.Info);
                for (int i = 0; i < _materials.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(_materials[i], typeof(Material), false);
                        if (GUILayout.Button("×", GUILayout.Width(22)))
                        {
                            _materials.RemoveAt(i);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            // ドラッグ & ドロップ
            var dropRect = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "ここにマテリアルをドロップ", EditorStyles.centeredGreyMiniLabel);
            HandleDragAndDrop(dropRect);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("オプション", EditorStyles.boldLabel);
            _options.CreateCopy = EditorGUILayout.ToggleLeft("複製を作成して変換 (元マテリアルを保持)", _options.CreateCopy);
            _options.BakeTint = EditorGUILayout.ToggleLeft("メインカラー/アルファマスクをテクスチャへベイク", _options.BakeTint);
            _options.BakeShadeGradient = EditorGUILayout.ToggleLeft("影設定をグラデーションへベイク", _options.BakeShadeGradient);
            _options.PackMasks = EditorGUILayout.ToggleLeft("機能マスクを _SharedMask へパック", _options.PackMasks);
            _options.RemoveUnusedProperties = EditorGUILayout.ToggleLeft("旧プロパティを削除 (残すと旧テクスチャがビルドに同梱され容量が膨張)", _options.RemoveUnusedProperties);

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(_materials.Count == 0))
            {
                if (GUILayout.Button($"変換 ({_materials.Count} 件)", GUILayout.Height(28)))
                {
                    _reports = PoiyomiToNonToonConverter.Convert(_materials, _options);
                }
            }

            if (_reports != null) DrawReports();
        }

        void HandleDragAndDrop(Rect rect)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;

            var mats = DragAndDrop.objectReferences.OfType<Material>()
                .Where(PoiyomiToNonToonConverter.CanConvert).ToList();
            DragAndDrop.visualMode = mats.Count > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

            if (e.type == EventType.DragPerform && mats.Count > 0)
            {
                DragAndDrop.AcceptDrag();
                foreach (var mat in mats)
                    if (!_materials.Contains(mat)) _materials.Add(mat);
                _reports = null;
                e.Use();
            }
        }

        void DrawReports()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("変換レポート", EditorStyles.boldLabel);

            using var scroll = new EditorGUILayout.ScrollViewScope(_scrollReports);
            _scrollReports = scroll.scrollPosition;

            foreach (var report in _reports!)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var title = report.Source != null ? report.Source.name : "(不明)";
                        EditorGUILayout.LabelField(
                            report.Succeeded ? $"✔ {title}" : $"✘ {title} — {report.FailureReason}",
                            EditorStyles.boldLabel);
                        if (report.Result != null && GUILayout.Button("選択", GUILayout.Width(50)))
                            Selection.activeObject = report.Result;
                    }

                    foreach (var entry in report.Entries)
                    {
                        var icon = entry.Severity switch
                        {
                            ConversionSeverity.Info => MessageType.None,
                            ConversionSeverity.Approximated => MessageType.Info,
                            ConversionSeverity.Warning => MessageType.Warning,
                            ConversionSeverity.Dropped => MessageType.Warning,
                            _ => MessageType.None,
                        };
                        var label = $"[{ConversionReport.Label(entry.Severity)}] {entry.Feature}: {entry.Message}";
                        if (icon == MessageType.None)
                            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                        else
                            EditorGUILayout.HelpBox(label, icon);
                    }
                }
            }
        }
    }
}
