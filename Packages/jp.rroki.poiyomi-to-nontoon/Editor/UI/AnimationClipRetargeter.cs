#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// アニメーションクリップ内の Poiyomi マテリアルプロパティパス
    /// (material._EmissionHueShift 等) を NonToon の対応プロパティへ変換するツール。
    ///
    /// NonToon (Shader Core) のインスペクターは録画モードのキー記録に対応していないため、
    /// 既存の Poiyomi 用クリップを変換して流用するのが現実的なワークフローになる。
    /// 時間係数が異なるプロパティ (色相シフト速度 / UV パン = Poiyomi は 秒/20 基準) は
    /// カーブの値を自動でスケーリングする。
    /// </summary>
    internal sealed class AnimationClipRetargeter : EditorWindow
    {
        /// <summary>置換エントリ。From/To はチャンネルサフィックス (.r 等) を除いたプロパティ名。</summary>
        readonly struct Map
        {
            public readonly string From;
            public readonly string To;
            public readonly float Scale;

            public Map(string from, string to, float scale = 1f)
            {
                From = from;
                To = to;
                Scale = scale;
            }
        }

        const string EmissionMod = "jp.rroki.nontoon.emission";
        const string ColorAdjustMod = "jp.rroki.nontoon.coloradjust";
        const string GlitterMod = "jp.rroki.nontoon.glitter";
        const string DecalMod = "jp.rroki.nontoon.decal";

        // Poiyomi の時間依存プロパティは POI_TIME.x (= _Time.x = 秒/20)、NonToon 側は 秒 (_Time.y) 基準
        const float TimeScale = 1f / 20f;

        static readonly Map[] Maps = BuildMaps();

        static Map[] BuildMaps()
        {
            var maps = new List<Map>
            {
                // エミッション
                new("_EmissionHueShift", NonToonProps.Prop(EmissionMod, "_EmissionHueShift")),
                new("_EmissionHueShiftSpeed", NonToonProps.Prop(EmissionMod, "_EmissionHueShiftSpeed"), TimeScale),
                new("_EmissionStrength", NonToonProps.Prop(EmissionMod, "_EmissionStrength")),
                new("_EmissionColor", NonToonProps.Prop(EmissionMod, "_EmissionColor")),
                new("_EmissionMapPan.x", NonToonProps.Prop(EmissionMod, "_EmissionScroll") + ".x", TimeScale),
                new("_EmissionMapPan.y", NonToonProps.Prop(EmissionMod, "_EmissionScroll") + ".y", TimeScale),
                new("_EmissiveBlink_Min", NonToonProps.Prop(EmissionMod, "_EmissionBlink") + ".x"),
                new("_EmissiveBlink_Max", NonToonProps.Prop(EmissionMod, "_EmissionBlink") + ".y"),
                new("_EmissiveBlink_Velocity", NonToonProps.Prop(EmissionMod, "_EmissionBlink") + ".z"),
                new("_EmissionBlinkingOffset", NonToonProps.Prop(EmissionMod, "_EmissionBlink") + ".w"),
                // 色調整
                new("_MainHueShift", NonToonProps.Prop(ColorAdjustMod, "_AdjustHue")),
                new("_Saturation", NonToonProps.Prop(ColorAdjustMod, "_AdjustSaturation")),
                new("_MainBrightness", NonToonProps.Prop(ColorAdjustMod, "_AdjustBrightness")),
                new("_MainGamma", NonToonProps.Prop(ColorAdjustMod, "_AdjustGamma")),
                // グリッター
                new("_GlitterColor", NonToonProps.Prop(GlitterMod, "_GlitterColor")),
                new("_GlitterBrightness", NonToonProps.Prop(GlitterMod, "_GlitterBrightness")),
                // アウトライン / 本体
                new("_LineWidth", NonToonProps.OutlineWidth),
                new("_LineColor", NonToonProps.OutlineColor),
            };

            // デカール 0-3 (位置 / 回転 / 色 / 不透明度)
            string[] poiSuffixes = { "", "1", "2", "3" };
            for (int slot = 0; slot < 4; slot++)
            {
                var p = poiSuffixes[slot];
                maps.Add(new Map($"_DecalPosition{p}.x", NonToonProps.Prop(DecalMod, $"_Decal{slot}Position") + ".x"));
                maps.Add(new Map($"_DecalPosition{p}.y", NonToonProps.Prop(DecalMod, $"_Decal{slot}Position") + ".y"));
                maps.Add(new Map($"_DecalRotation{p}", NonToonProps.Prop(DecalMod, $"_Decal{slot}Rotation")));
                maps.Add(new Map($"_DecalColor{p}", NonToonProps.Prop(DecalMod, $"_Decal{slot}Color")));
                maps.Add(new Map($"_DecalBlendAlpha{p}", NonToonProps.Prop(DecalMod, $"_Decal{slot}Alpha")));
                maps.Add(new Map($"_DecalEmissionStrength{p}", NonToonProps.Prop(DecalMod, $"_Decal{slot}Emission")));
            }
            return maps.ToArray();
        }

        // ---------------- リマップ本体 ----------------

        /// <summary>
        /// バインディングのプロパティ名 (material._X または material._X.ch) の変換を試みる。
        /// </summary>
        internal static bool TryMapPropertyName(string propertyName, out string mapped, out float scale)
        {
            mapped = propertyName;
            scale = 1f;
            const string prefix = "material.";
            if (propertyName.StartsWith(prefix, System.StringComparison.Ordinal) is false) { return false; }
            var body = propertyName.Substring(prefix.Length);

            foreach (var map in Maps)
            {
                if (body == map.From)
                {
                    mapped = prefix + map.To;
                    scale = map.Scale;
                    return true;
                }
                // チャンネルサフィックス付き (From がサフィックスなしで宣言されている場合)
                if (map.From.Contains('.') is false && body.StartsWith(map.From + ".", System.StringComparison.Ordinal))
                {
                    var channel = body.Substring(map.From.Length);
                    if (channel.Length == 2)
                    {
                        mapped = prefix + map.To + channel;
                        scale = map.Scale;
                        return true;
                    }
                }
            }
            return false;
        }

        internal static (int mapped, int unmapped) RetargetClip(AnimationClip clip)
        {
            int mappedCount = 0;
            int unmappedCount = 0;
            Undo.RegisterCompleteObjectUndo(clip, "Retarget Poiyomi Animation");

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.propertyName.StartsWith("material.", System.StringComparison.Ordinal) is false) { continue; }
                if (TryMapPropertyName(binding.propertyName, out var newName, out var scale) is false)
                {
                    unmappedCount++;
                    continue;
                }

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) { continue; }
                if (!Mathf.Approximately(scale, 1f))
                {
                    var keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        keys[i].value *= scale;
                        keys[i].inTangent *= scale;
                        keys[i].outTangent *= scale;
                    }
                    curve.keys = keys;
                }

                var newBinding = binding;
                newBinding.propertyName = newName;
                AnimationUtility.SetEditorCurve(clip, binding, null);      // 旧カーブを削除
                AnimationUtility.SetEditorCurve(clip, newBinding, curve);  // 新パスで追加
                mappedCount++;
            }

            if (mappedCount > 0) { EditorUtility.SetDirty(clip); }
            return (mappedCount, unmappedCount);
        }

        // ---------------- UI ----------------

        readonly List<AnimationClip> _clips = new();
        bool _createCopy = true;
        Vector2 _scroll;
        string _resultLog = "";

        [MenuItem("Tools/rroki_'s tool/Poiyomi → NonToon Animation Retarget")]
        static void Open() => GetWindow<AnimationClipRetargeter>("Poi→NT Anim");

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Poiyomi 用マテリアルアニメーション (material._EmissionHueShift 等) を NonToon のプロパティパスへ変換します。\n" +
                "時間係数が異なるプロパティ (色相シフト速度 / UV パン) は値を自動換算します。\n" +
                "AnimatorController をドロップすると含まれる全クリップを対象にします。",
                MessageType.Info);

            // ドラッグ & ドロップ
            var dropRect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "ここにクリップ / AnimatorController をドロップ", EditorStyles.centeredGreyMiniLabel);
            HandleDragAndDrop(dropRect);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.MaxHeight(160)))
            {
                _scroll = scroll.scrollPosition;
                for (int i = 0; i < _clips.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(_clips[i], typeof(AnimationClip), false);
                        if (GUILayout.Button("×", GUILayout.Width(22)))
                        {
                            _clips.RemoveAt(i);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            _createCopy = EditorGUILayout.ToggleLeft("複製を作成して変換 (元クリップを保持。コントローラーの差し替えは手動)", _createCopy);

            using (new EditorGUI.DisabledScope(_clips.Count == 0))
            {
                if (GUILayout.Button($"変換 ({_clips.Count} クリップ)", GUILayout.Height(26)))
                {
                    Run();
                }
            }

            if (string.IsNullOrEmpty(_resultLog) is false)
            {
                EditorGUILayout.HelpBox(_resultLog, MessageType.None);
            }
        }

        void HandleDragAndDrop(Rect rect)
        {
            var e = Event.current;
            if (rect.Contains(e.mousePosition) is false) { return; }
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) { return; }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    switch (obj)
                    {
                        case AnimationClip clip:
                            Add(clip);
                            break;
                        case AnimatorController controller:
                            foreach (var clip in controller.animationClips) { Add(clip); }
                            break;
                    }
                }
                _resultLog = "";
                e.Use();
            }

            void Add(AnimationClip clip)
            {
                if (clip != null && _clips.Contains(clip) is false) { _clips.Add(clip); }
            }
        }

        void Run()
        {
            var log = new System.Text.StringBuilder();
            int totalMapped = 0;
            foreach (var source in _clips.ToArray())
            {
                var clip = source;
                if (_createCopy)
                {
                    var path = AssetDatabase.GetAssetPath(source);
                    if (string.IsNullOrEmpty(path))
                    {
                        log.AppendLine($"{source.name}: アセット化されていないためスキップ");
                        continue;
                    }
                    var dir = Path.GetDirectoryName(path)!.Replace('\\', '/');
                    var newPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{source.name}_NT.anim");
                    if (AssetDatabase.CopyAsset(path, newPath) is false)
                    {
                        log.AppendLine($"{source.name}: 複製に失敗");
                        continue;
                    }
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newPath);
                }

                var (mapped, unmapped) = RetargetClip(clip);
                totalMapped += mapped;
                log.AppendLine($"{clip.name}: {mapped} 個のプロパティを変換" + (unmapped > 0 ? $" (未対応のマテリアルプロパティ {unmapped} 個は変換されず残っています)" : ""));
            }
            AssetDatabase.SaveAssets();
            _resultLog = log.ToString().TrimEnd();
            Debug.Log($"[Poiyomi→NonToon] アニメーション変換完了: {totalMapped} プロパティ\n{_resultLog}");
        }
    }
}
