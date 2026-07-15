#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Rroki.NonToonExtraModules.Tools
{
    /// <summary>
    /// NonToon マテリアルアニメーション コンポーザー。
    /// アバター配下の NonToon / NonToonFur マテリアルを走査し、検索可能なプロパティ一覧から
    /// 複数メッシュ × 複数パラメータをまとめて選んでアニメーションクリップを生成する。
    ///
    /// 重要: NonToon の各モジュールは _Enable が [SCConstValue] (シェーダーキーワード) で、
    /// モジュールが無効だと機能コードごとコンパイルから除外される。そのため無効モジュールの
    /// パラメータをアニメーションしても VRChat 上で「動かない」。本ツールは対象モジュールの
    /// _Enable を自動で有効化してこの問題を回避する。
    /// </summary>
    public sealed class NonToonAnimationComposer : EditorWindow
    {
        enum Mode { HueLoop, LinearAB, PingAB }

        [SerializeField] GameObject? _root;
        [SerializeField] string _search = "";
        [SerializeField] Mode _mode = Mode.HueLoop;
        [SerializeField] float _duration = 2f;
        [SerializeField] bool _loopTime = true;
        [SerializeField] bool _ensureEnable = true;
        [SerializeField] float _floatA;
        [SerializeField] float _floatB = 1f;
        [SerializeField] Color _colorA = Color.black;
        [SerializeField] Color _colorB = Color.white;
        [SerializeField] string _clipName = "NonToonAnim";

        sealed class PropEntry
        {
            public string Name = "";
            public string Display = "";
            public ShaderUtil.ShaderPropertyType Type;
            public readonly List<Renderer> Renderers = new();
        }

        readonly List<PropEntry> _props = new();
        readonly HashSet<string> _selected = new();
        Vector2 _scroll;
        string _log = "";

        [MenuItem("Tools/rroki_'s tools/NonToon/Animation Composer")]
        public static void Open() => GetWindow<NonToonAnimationComposer>("NonToon Anim Composer");

        static bool IsNonToon(Shader? s) => s != null && (s.name == "NonToon" || s.name == "NonToonFur");

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "アバターを指定してスキャン → 動かしたいプロパティを検索して複数選択 → プリセットでクリップ生成。\n" +
                "対象モジュールの _Enable を自動有効化するため、変換直後で無効だった機能もアニメーションできます。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _root = (GameObject?)EditorGUILayout.ObjectField("アバター (ルート)", _root, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) { _props.Clear(); _selected.Clear(); }

            using (new EditorGUI.DisabledScope(_root == null))
                if (GUILayout.Button("スキャン (NonToon マテリアルのプロパティを収集)"))
                    Scan();

            if (_props.Count == 0)
            {
                EditorGUILayout.HelpBox("スキャンすると、配下の NonToon マテリアルのアニメーション可能プロパティが一覧されます。", MessageType.None);
                DrawLog();
                return;
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField("検索", _search);
                if (GUILayout.Button("色相", GUILayout.Width(48))) QuickSelect("Hue");
                if (GUILayout.Button("発光", GUILayout.Width(48))) QuickSelect("Emission");
                if (GUILayout.Button("解除", GUILayout.Width(48))) _selected.Clear();
            }

            EditorGUILayout.LabelField($"プロパティ ({_props.Count} 種、選択 {_selected.Count})", EditorStyles.boldLabel);
            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.MaxHeight(240)))
            {
                _scroll = sv.scrollPosition;
                foreach (var p in _props)
                {
                    if (!Match(p)) continue;
                    bool sel = _selected.Contains(p.Name);
                    bool now = EditorGUILayout.ToggleLeft(
                        $"{p.Display}   [{TypeLabel(p.Type)}]  ({p.Renderers.Count} メッシュ)  {p.Name}", sel);
                    if (now != sel)
                    {
                        if (now) _selected.Add(p.Name); else _selected.Remove(p.Name);
                    }
                }
            }

            EditorGUILayout.Space();
            _mode = (Mode)EditorGUILayout.EnumPopup("プリセット", _mode);
            EditorGUILayout.LabelField(ModeHint(_mode), EditorStyles.miniLabel);
            _duration = Mathf.Max(0.01f, EditorGUILayout.FloatField("長さ (秒)", _duration));
            _loopTime = EditorGUILayout.Toggle("ループ (Loop Time)", _loopTime);

            if (_mode != Mode.HueLoop)
            {
                bool anyColor = _props.Any(p => _selected.Contains(p.Name) && p.Type == ShaderUtil.ShaderPropertyType.Color);
                bool anyScalar = _props.Any(p => _selected.Contains(p.Name) && p.Type != ShaderUtil.ShaderPropertyType.Color);
                if (anyScalar)
                {
                    _floatA = EditorGUILayout.FloatField("値 A (開始)", _floatA);
                    _floatB = EditorGUILayout.FloatField("値 B (終了)", _floatB);
                }
                if (anyColor)
                {
                    _colorA = EditorGUILayout.ColorField(new GUIContent("色 A (開始)"), _colorA, true, true, true);
                    _colorB = EditorGUILayout.ColorField(new GUIContent("色 B (終了)"), _colorB, true, true, true);
                }
            }

            _ensureEnable = EditorGUILayout.Toggle("対象モジュールを自動有効化 (推奨)", _ensureEnable);
            _clipName = EditorGUILayout.TextField("クリップ名", _clipName);

            using (new EditorGUI.DisabledScope(_selected.Count == 0 || _root == null))
                if (GUILayout.Button($"クリップを生成 ({_selected.Count} プロパティ)", GUILayout.Height(28)))
                    Generate();

            DrawLog();
        }

        void DrawLog()
        {
            if (!string.IsNullOrEmpty(_log)) EditorGUILayout.HelpBox(_log, MessageType.None);
        }

        static string TypeLabel(ShaderUtil.ShaderPropertyType t) => t switch
        {
            ShaderUtil.ShaderPropertyType.Color => "色",
            ShaderUtil.ShaderPropertyType.Vector => "Vector",
            _ => "数値",
        };

        static string ModeHint(Mode m) => m switch
        {
            Mode.HueLoop => "色相ループ: 0→1 を等速でキー。色相回転は 0=1 なのでシームレスにループします (数値プロパティ用)",
            Mode.LinearAB => "A→B: 開始値から終了値へ等速。エミッション強度のフェード等",
            _ => "A→B→A: 往復。点滅やパルスに",
        };

        bool Match(PropEntry p)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            return p.Display.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                || p.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void QuickSelect(string keyword)
        {
            foreach (var p in _props)
                if (p.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    _selected.Add(p.Name);
        }

        void Scan()
        {
            _props.Clear();
            _selected.Clear();
            _log = "";
            if (_root == null) return;

            var byName = new Dictionary<string, PropEntry>();
            foreach (var r in _root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null || !IsNonToon(mat.shader)) continue;
                    var shader = mat.shader;
                    int cnt = ShaderUtil.GetPropertyCount(shader);
                    for (int i = 0; i < cnt; i++)
                    {
                        var t = ShaderUtil.GetPropertyType(shader, i);
                        // アニメーションに向くスカラー/色のみ (テクスチャ / enum(Int) は除外)
                        if (t != ShaderUtil.ShaderPropertyType.Float
                            && t != ShaderUtil.ShaderPropertyType.Range
                            && t != ShaderUtil.ShaderPropertyType.Color
                            && t != ShaderUtil.ShaderPropertyType.Vector) continue;

                        var name = ShaderUtil.GetPropertyName(shader, i);
                        if (!byName.TryGetValue(name, out var e))
                        {
                            var desc = ShaderUtil.GetPropertyDescription(shader, i);
                            e = new PropEntry { Name = name, Type = t, Display = string.IsNullOrEmpty(desc) ? name : desc };
                            byName[name] = e;
                        }
                        if (!e.Renderers.Contains(r)) e.Renderers.Add(r);
                    }
                }
            }
            _props.AddRange(byName.Values.OrderBy(e => e.Display, StringComparer.OrdinalIgnoreCase));
            _log = _props.Count == 0
                ? "NonToon / NonToonFur マテリアルが見つかりませんでした。"
                : $"{_props.Count} 個のプロパティを収集しました。";
        }

        void Generate()
        {
            var clip = new AnimationClip { name = _clipName };
            var scope = _props.Where(p => _selected.Contains(p.Name)).ToList();
            int curves = 0, enabled = 0;

            foreach (var p in scope)
            {
                foreach (var r in p.Renderers)
                {
                    string path = AnimationUtility.CalculateTransformPath(r.transform, _root!.transform);
                    var type = r.GetType();

                    if (_ensureEnable)
                    {
                        foreach (var mat in r.sharedMaterials)
                        {
                            if (mat == null || !IsNonToon(mat.shader)) continue;
                            if (mat.shader.FindPropertyIndex(p.Name) < 0) continue;
                            if (EnsureModuleEnabled(mat, p.Name)) enabled++;
                        }
                    }
                    curves += AddCurves(clip, path, type, p);
                }
            }

            var dir = "Assets";
            var rootPath = _root != null ? AssetDatabase.GetAssetPath(_root) : null;
            if (!string.IsNullOrEmpty(rootPath)) dir = System.IO.Path.GetDirectoryName(rootPath)!.Replace('\\', '/');
            var outPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{_clipName}.anim");
            AssetDatabase.CreateAsset(clip, outPath);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = _loopTime;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
            _log = $"クリップを生成しました: {outPath}\nカーブ {curves} 本 / 有効化したモジュール {enabled} 個";
            Debug.Log($"[NonToon Anim Composer] {_log}", clip);
        }

        int AddCurves(AnimationClip clip, string path, Type type, PropEntry p)
        {
            if (p.Type == ShaderUtil.ShaderPropertyType.Color)
            {
                string[] ch = { ".r", ".g", ".b", ".a" };
                float[] a = { _colorA.r, _colorA.g, _colorA.b, _colorA.a };
                float[] b = { _colorB.r, _colorB.g, _colorB.b, _colorB.a };
                for (int i = 0; i < 4; i++)
                    SetCurve(clip, path, type, "material." + p.Name + ch[i], a[i], b[i]);
                return 4;
            }
            if (p.Type == ShaderUtil.ShaderPropertyType.Vector)
            {
                SetCurve(clip, path, type, "material." + p.Name + ".x", _floatA, _floatB);
                return 1;
            }
            float fa = _floatA, fb = _floatB;
            if (_mode == Mode.HueLoop) { fa = 0f; fb = 1f; }
            SetCurve(clip, path, type, "material." + p.Name, fa, fb);
            return 1;
        }

        void SetCurve(AnimationClip clip, string path, Type type, string propertyName, float a, float b)
        {
            AnimationCurve curve = _mode == Mode.PingAB
                ? new AnimationCurve(new Keyframe(0f, a), new Keyframe(_duration * 0.5f, b), new Keyframe(_duration, a))
                : AnimationCurve.Linear(0f, a, _duration, b);
            var binding = EditorCurveBinding.FloatCurve(path, type, propertyName);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        static readonly Regex ConstValueRegex = new(@"SCConstValue\((\d+)", RegexOptions.Compiled);

        /// <summary>プロパティが属するモジュールの _Enable ([SCConstValue] キーワード) を有効化する。</summary>
        static bool EnsureModuleEnabled(Material mat, string propName)
        {
            var shader = mat.shader;
            int cnt = ShaderUtil.GetPropertyCount(shader);
            string? bestEnable = null;
            int bestLen = -1;
            for (int i = 0; i < cnt; i++)
            {
                var n = ShaderUtil.GetPropertyName(shader, i);
                if (!n.EndsWith("_Enable", StringComparison.Ordinal)) continue;
                string prefix = n.Substring(0, n.Length - "Enable".Length);
                if (propName.StartsWith(prefix, StringComparison.Ordinal) && prefix.Length > bestLen)
                {
                    bestEnable = n;
                    bestLen = prefix.Length;
                }
            }
            if (bestEnable == null) return false;

            int idx = shader.FindPropertyIndex(bestEnable);
            if (idx < 0) return false;
            if (shader.GetPropertyType(idx) == UnityEngine.Rendering.ShaderPropertyType.Int) mat.SetInteger(bestEnable, 1);
            else mat.SetFloat(bestEnable, 1);

            // [SCConstValue] のキーワード同期 (<大文字名>_<値>)
            foreach (var attr in shader.GetPropertyAttributes(idx))
            {
                var m = ConstValueRegex.Match(attr);
                if (!m.Success) continue;
                int max = int.Parse(m.Groups[1].Value);
                string upper = bestEnable.ToUpperInvariant();
                for (int v = 0; v <= max; v++)
                {
                    string kw = upper + "_" + v;
                    if (v == 1) mat.EnableKeyword(kw); else mat.DisableKeyword(kw);
                }
                break;
            }
            EditorUtility.SetDirty(mat);
            return true;
        }
    }
}
