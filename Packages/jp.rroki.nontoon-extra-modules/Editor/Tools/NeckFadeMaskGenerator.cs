#nullable enable
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rroki.NonToonExtraModules.Tools
{
    /// <summary>
    /// 首元フェードマスク生成ツール。
    /// 首元は必ずしも開いた境界 (穴) ではない (頭部が閉じたメッシュの場合が多い)。
    /// そのためフェードの基準を複数方式から選べる:
    ///   1. 境界ループ (このメッシュ) … 開口があるメッシュ向け。開いた境界エッジからの距離。
    ///   2. 境界ループ (別メッシュ参照) … 体側メッシュの首開口ループを参照し、顔メッシュ側の距離を計算。
    ///   3. 平面 (Transform/ボーン基準) … Neck/Head ボーンやドラッグ可能ハンドルで決めた平面からの距離。
    ///   4. 点からの距離 (Transform) … 参照位置からの放射距離。
    /// いずれも posed ワールド座標 (実メートル) で計算し、UV 空間のグラデーションマスクを出力する。
    /// 生成したマスクは SDF Neck Fade Baker へそのまま渡せる。
    /// </summary>
    public sealed class NeckFadeMaskGenerator : EditorWindow
    {
        enum FadeSource { BoundaryLoopSelf, BoundaryLoopCrossMesh, Plane, Point }

        [SerializeField] Renderer? _target;
        [SerializeField] int _uvChannel;
        [SerializeField] int _resolution = 1024;
        [SerializeField] float _startDistance;
        [SerializeField] float _endDistance = 0.05f;
        [SerializeField] AnimationCurve _curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] bool _invert;
        [SerializeField] int _dilate = 4;
        [SerializeField] bool _flipV;
        [SerializeField] bool _livePreview = true;

        // ---- フェード基準 ----
        [SerializeField] FadeSource _fadeSource = FadeSource.Plane;
        [SerializeField] Renderer? _referenceRenderer;   // クロスメッシュ参照 (体側メッシュ)
        [SerializeField] Transform? _reference;           // 平面/点の参照 Transform (Neck ボーン等)
        [SerializeField] Vector3 _planeCenter;            // 平面/点の基準位置 (ワールド、シーンでドラッグ可)
        [SerializeField] int _planeAxisIndex;             // 平面の法線 (フェードが伸びる向き)
        [SerializeField] bool _planeInited;
        [SerializeField] bool _distInited;                // 距離既定をメッシュ寸法から自動設定済みか

        // ---- キャッシュ (非シリアライズ) ----
        Mesh? _mesh;
        Vector3[] _verts = System.Array.Empty<Vector3>();      // メッシュローカル (溶接/トポロジ用)
        Vector3[] _worldVerts = System.Array.Empty<Vector3>(); // posed ワールド座標 (重心/距離/プレビュー整合用)
        Vector2[] _uv = System.Array.Empty<Vector2>();
        int[] _tris = System.Array.Empty<int>();
        readonly List<List<int>> _loops = new();
        readonly List<Vector3> _loopCenter = new();
        bool[] _loopSelected = System.Array.Empty<bool>();
        Vector3[] _refPoints = System.Array.Empty<Vector3>();  // クロスメッシュ参照ループのワールド点
        string _refInfo = "";
        float[] _vertexMask = System.Array.Empty<float>();
        Mesh? _previewMesh;
        Material? _previewMat;
        bool _boundaryDirty = true;
        bool _refDirty = true;
        bool _maskDirty = true;
        bool _hasUV = true;
        Texture2D? _lastMask;
        Vector2 _scroll;

        static readonly string[] AxisLabels =
            { "ワールド +Y (上)", "ワールド -Y (下)", "ワールド +Z", "ワールド -Z", "ワールド +X", "ワールド -X", "参照Transform の上方向" };

        [MenuItem("Tools/rroki_'s tools/NonToon/首元フェードマスク生成")]
        public static void Open() => GetWindow<NeckFadeMaskGenerator>("首元フェードマスク生成");

        void OnEnable() => SceneView.duringSceneGui += OnScene;
        void OnDisable()
        {
            SceneView.duringSceneGui -= OnScene;
            if (_previewMesh != null) DestroyImmediate(_previewMesh);
            if (_previewMat != null) DestroyImmediate(_previewMat);
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "首元はメッシュの穴とは限りません (頭部が閉じている場合が多い)。フェードの基準を下の「フェード基準」で選び、" +
                "開始/終了距離とカーブを調整してください。シーンビューにヒートマップを表示します。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _target = (Renderer?)EditorGUILayout.ObjectField("対象メッシュ (Renderer)", _target, typeof(Renderer), true);
            _uvChannel = EditorGUILayout.IntPopup("UV チャンネル", _uvChannel,
                new[] { "UV0", "UV1", "UV2", "UV3" }, new[] { 0, 1, 2, 3 });
            if (EditorGUI.EndChangeCheck()) { _boundaryDirty = true; _planeInited = false; _distInited = false; }

            var mesh = ResolveMesh();
            if (mesh == null)
            {
                EditorGUILayout.HelpBox("SkinnedMeshRenderer または MeshFilter を持つ Renderer を指定してください。", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }
            if (!mesh.isReadable)
            {
                EditorGUILayout.HelpBox("メッシュの Read/Write が無効です。有効化が必要です。", MessageType.Warning);
                if (GUILayout.Button("メッシュの Read/Write を有効化"))
                {
                    EnableMeshReadWrite(mesh);
                    _boundaryDirty = true;
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            if (_boundaryDirty) ComputeBoundary();

            // 平面/点モードで基準位置が未初期化なら Neck ボーン等へ初期化
            if ((_fadeSource == FadeSource.Plane || _fadeSource == FadeSource.Point) && !_planeInited)
            {
                InitPlaneCenter();
                _maskDirty = true;
            }

            if (!_hasUV)
                EditorGUILayout.HelpBox($"UV{_uvChannel} が見つかりません。別の UV チャンネルを選ぶか、UV のあるメッシュを指定してください。", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            _fadeSource = (FadeSource)EditorGUILayout.Popup("フェード基準",
                (int)_fadeSource,
                new[] { "境界ループ (このメッシュ)", "境界ループ (別メッシュ参照)", "平面 (Transform/ボーン)", "点からの距離 (Transform)" });
            if (EditorGUI.EndChangeCheck())
            {
                if ((_fadeSource == FadeSource.Plane || _fadeSource == FadeSource.Point) && !_planeInited)
                    InitPlaneCenter();
                _refDirty = true;
                _maskDirty = true;
            }

            switch (_fadeSource)
            {
                case FadeSource.BoundaryLoopSelf: DrawSelfLoopUI(); break;
                case FadeSource.BoundaryLoopCrossMesh: DrawCrossMeshUI(); break;
                case FadeSource.Plane: DrawPlaneUI(pointMode: false); break;
                case FadeSource.Point: DrawPlaneUI(pointMode: true); break;
            }

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            _startDistance = EditorGUILayout.FloatField("フェード開始距離 (m)", _startDistance);
            _endDistance = EditorGUILayout.FloatField("フェード終了距離 (m)", _endDistance);
            _curve = EditorGUILayout.CurveField("フェードカーブ", _curve, Color.cyan, new Rect(0, 0, 1, 1));
            _invert = EditorGUILayout.Toggle("反転", _invert);
            if (EditorGUI.EndChangeCheck()) _maskDirty = true;

            _resolution = EditorGUILayout.IntPopup("解像度", _resolution,
                new[] { "512", "1024", "2048", "4096" }, new[] { 512, 1024, 2048, 4096 });
            _dilate = EditorGUILayout.IntSlider("UV ふちの拡張 (px)", _dilate, 0, 16);
            _flipV = EditorGUILayout.Toggle("V 反転 (上下が逆なら)", _flipV);
            _livePreview = EditorGUILayout.Toggle("シーンにプレビュー", _livePreview);

            if (_refDirty && _fadeSource == FadeSource.BoundaryLoopCrossMesh) ComputeReferenceLoop();
            if (_maskDirty) ComputeMask();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("首元=暖色(1) / 遠方=寒色(0) でプレビューされます", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(_vertexMask.Length == 0))
            {
                if (GUILayout.Button("マスクを生成して保存", GUILayout.Height(28)))
                    GenerateAndSave(openBaker: false);
                if (GUILayout.Button("生成して SDF Neck Fade Baker へ送る", GUILayout.Height(24)))
                    GenerateAndSave(openBaker: true);
            }

            if (_lastMask != null)
            {
                EditorGUILayout.ObjectField("最後に生成したマスク", _lastMask, typeof(Texture2D), false);
                if (GUILayout.Button("このマスクで SDF Baker を開く"))
                    SdfNeckFadeBaker.OpenWithMask(_lastMask);
            }

            EditorGUILayout.EndScrollView();
            if (_livePreview) SceneView.RepaintAll();
        }

        // ---- モード別 UI ----

        void DrawSelfLoopUI()
        {
            if (_loops.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "このメッシュに開いた境界エッジが見つかりませんでした (首元が閉じている頭部メッシュなど)。" +
                    "「別メッシュ参照」「平面」「点からの距離」いずれかの基準を使ってください。",
                    MessageType.Warning);
                return;
            }
            EditorGUILayout.LabelField($"境界ループ ({_loops.Count} 個) — 首元のループにチェック", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < _loops.Count; i++)
            {
                var c = _loopCenter[i];
                _loopSelected[i] = EditorGUILayout.ToggleLeft(
                    $"ループ {i}: ワールドY={c.y:F3}  頂点数={_loops[i].Count}", _loopSelected[i]);
            }
            if (EditorGUI.EndChangeCheck()) _maskDirty = true;
        }

        void DrawCrossMeshUI()
        {
            EditorGUILayout.HelpBox(
                "体側など、首元が開口しているメッシュを参照に指定します。その首開口ループ (最も低いループ) からの距離で" +
                "対象メッシュのマスクを作ります。閉じた頭部メッシュでも、体の首穴を基準にできます。",
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            _referenceRenderer = (Renderer?)EditorGUILayout.ObjectField("参照メッシュ (体側など)", _referenceRenderer, typeof(Renderer), true);
            if (EditorGUI.EndChangeCheck()) { _refDirty = true; _maskDirty = true; }

            if (_referenceRenderer == null)
                EditorGUILayout.HelpBox("首元が開口している参照メッシュを指定してください。", MessageType.Warning);
            else if (!string.IsNullOrEmpty(_refInfo))
                EditorGUILayout.LabelField(_refInfo, EditorStyles.miniLabel);
        }

        void DrawPlaneUI(bool pointMode)
        {
            EditorGUILayout.HelpBox(
                pointMode
                    ? "参照位置からの放射距離でフェードします。位置はシーンビューのハンドルでドラッグできます。"
                    : "基準の平面からの距離でフェードします。平面の位置はシーンビューのハンドルでドラッグ、向きは下の軸で指定します。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _reference = (Transform?)EditorGUILayout.ObjectField("参照 Transform (任意)", _reference, typeof(Transform), true);
            if (EditorGUI.EndChangeCheck() && _reference != null) { _planeCenter = _reference.position; _planeInited = true; _maskDirty = true; }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Neck ボーンに合わせる"))
                {
                    var nb = FindNeckReference();
                    if (nb != null) { _reference = nb; _planeCenter = nb.position; _planeInited = true; _maskDirty = true; }
                    else Debug.LogWarning("[首元フェードマスク生成] Humanoid の Neck/Head ボーンが見つかりませんでした。");
                }
                if (_reference != null && GUILayout.Button("参照 Transform の位置へ"))
                {
                    _planeCenter = _reference.position; _planeInited = true; _maskDirty = true;
                }
            }

            EditorGUI.BeginChangeCheck();
            _planeCenter = EditorGUILayout.Vector3Field(pointMode ? "基準位置 (ワールド)" : "平面の位置 (ワールド)", _planeCenter);
            if (!pointMode)
                _planeAxisIndex = EditorGUILayout.Popup("平面の向き (フェード方向)", _planeAxisIndex, AxisLabels);
            if (EditorGUI.EndChangeCheck()) { _planeInited = true; _maskDirty = true; }

            EditorGUILayout.LabelField("シーンビューのハンドルで位置を調整できます。", EditorStyles.miniLabel);
        }

        // ================= 境界検出 =================

        Mesh? ResolveMesh() => _target != null ? MeshOf(_target) : null;

        static Mesh? MeshOf(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        void ComputeBoundary()
        {
            _boundaryDirty = false;
            _loops.Clear();
            _loopCenter.Clear();

            var mesh = ResolveMesh();
            if (mesh == null || !mesh.isReadable || _target == null) return;
            _mesh = mesh;
            _verts = mesh.vertices;
            _tris = mesh.triangles;
            _worldVerts = WorldVertsOf(_target, mesh);

            var uvList = new List<Vector2>();
            mesh.GetUVs(_uvChannel, uvList);
            _hasUV = uvList.Count == _verts.Length;
            _uv = _hasUV ? uvList.ToArray() : new Vector2[_verts.Length];

            // 開境界ループ検出 (溶接はローカル座標、重心はワールド座標)
            var loops = DetectLoops(_verts, _tris);
            _loops.AddRange(loops);

            _loopSelected = new bool[_loops.Count];
            int lowest = -1;
            float lowestY = float.MaxValue;
            for (int i = 0; i < _loops.Count; i++)
            {
                Vector3 sum = Vector3.zero;
                foreach (int r in _loops[i]) sum += _worldVerts[r];
                Vector3 center = sum / Mathf.Max(1, _loops[i].Count);
                _loopCenter.Add(center);
                if (center.y < lowestY) { lowestY = center.y; lowest = i; }
            }
            if (lowest >= 0) _loopSelected[lowest] = true;

            // 距離の既定値をメッシュの高さから自動設定 (アバターのスケールに追従)。
            // 0.19 倍などに縮小されたアバターでも初回から見えるグラデーションになる。
            if (!_distInited)
            {
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (var v in _worldVerts) { if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y; }
                float h = Mathf.Max(1e-4f, maxY - minY);
                _startDistance = 0f;
                _endDistance = h * 0.6f;   // 首の切れ目付近から高さの 6 割でフェードしきる
                _distInited = true;
            }
            _maskDirty = true;
        }

        // 開いた境界エッジ (1 三角形にしか属さないエッジ) の連結成分をループとして返す。
        // weldVerts は位置溶接用 (UV/法線シームで割れた頂点を同一視)。返す添字は weldVerts の添字。
        static List<List<int>> DetectLoops(Vector3[] weldVerts, int[] tris)
        {
            var result = new List<List<int>>();
            if (weldVerts.Length == 0 || tris.Length == 0) return result;

            var repOf = new int[weldVerts.Length];
            var posToRep = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < weldVerts.Length; i++)
            {
                var p = weldVerts[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));
                if (!posToRep.TryGetValue(key, out int rep)) { rep = i; posToRep[key] = i; }
                repOf[i] = rep;
            }

            var edgeCount = new Dictionary<long, int>();
            void AddEdge(int a, int b)
            {
                int ra = repOf[a], rb = repOf[b];
                long k = EdgeKey(ra, rb);
                edgeCount.TryGetValue(k, out int c);
                edgeCount[k] = c + 1;
            }
            for (int t = 0; t < tris.Length; t += 3)
            {
                AddEdge(tris[t], tris[t + 1]);
                AddEdge(tris[t + 1], tris[t + 2]);
                AddEdge(tris[t + 2], tris[t]);
            }

            var adj = new Dictionary<int, List<int>>();
            void Link(int a, int b)
            {
                if (!adj.TryGetValue(a, out var la)) { la = new List<int>(); adj[a] = la; }
                la.Add(b);
            }
            foreach (var kv in edgeCount)
            {
                if (kv.Value != 1) continue;
                UnpackEdge(kv.Key, out int a, out int b);
                Link(a, b);
                Link(b, a);
            }

            var visited = new HashSet<int>();
            foreach (var startVert in adj.Keys)
            {
                if (visited.Contains(startVert)) continue;
                var comp = new List<int>();
                var stack = new Stack<int>();
                stack.Push(startVert);
                visited.Add(startVert);
                while (stack.Count > 0)
                {
                    int v = stack.Pop();
                    comp.Add(v);
                    foreach (int n in adj[v])
                        if (visited.Add(n)) stack.Push(n);
                }
                result.Add(comp);
            }
            return result;
        }

        // 参照メッシュの首開口ループ (最も低いワールド Y のループ) をワールド点として取得。
        void ComputeReferenceLoop()
        {
            _refDirty = false;
            _refPoints = System.Array.Empty<Vector3>();
            _refInfo = "";
            if (_referenceRenderer == null) return;
            var rm = MeshOf(_referenceRenderer);
            if (rm == null || !rm.isReadable) { _refInfo = "参照メッシュが読み取り不可 (Read/Write を有効化してください)"; return; }

            var rworld = WorldVertsOf(_referenceRenderer, rm);
            var loops = DetectLoops(rworld, rm.triangles);
            if (loops.Count == 0) { _refInfo = "参照メッシュに開境界がありません"; return; }

            // 首開口を選ぶ: Neck ボーン位置 (無ければ対象メッシュの中心) に最も近いループ。
            // フルボディは腰/手首などにも開口があるため「最下Y」では首を外すことがある。
            var nb = FindNeckReference();
            Vector3 anchor = nb != null ? nb.position
                : (_target != null ? _target.bounds.center : Vector3.zero);

            int pick = -1; float bestD = float.MaxValue; Vector3 pc = default;
            for (int i = 0; i < loops.Count; i++)
            {
                Vector3 sum = Vector3.zero;
                foreach (int r in loops[i]) sum += rworld[r];
                Vector3 c = sum / Mathf.Max(1, loops[i].Count);
                float d = (c - anchor).sqrMagnitude;
                if (d < bestD) { bestD = d; pick = i; pc = c; }
            }
            var pts = new List<Vector3>();
            foreach (int r in loops[pick]) pts.Add(rworld[r]);
            _refPoints = pts.ToArray();
            _refInfo = $"検出: {loops.Count} ループ / 首基準に最も近いループを使用 中心=({pc.x:F3},{pc.y:F3},{pc.z:F3}) 頂点{loops[pick].Count}";
        }

        // posed ワールド頂点。SkinnedMesh は BakeMesh(useScale:true) + localToWorldMatrix。
        // アバター root がスケールされている場合 (VRChat では一般的) に、これが実描画位置と一致する
        // (useScale:false だと縮んで潰れる)。静的メッシュは matrix * 頂点。
        static Vector3[] WorldVertsOf(Renderer r, Mesh mesh)
        {
            var mtx = r.transform.localToWorldMatrix;
            Vector3[] local;
            if (r is SkinnedMeshRenderer smr)
            {
                var baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                smr.BakeMesh(baked, true);
                local = baked.vertices;
                DestroyImmediate(baked);
            }
            else
            {
                local = mesh.vertices;
            }
            var wv = new Vector3[local.Length];
            for (int i = 0; i < local.Length; i++) wv[i] = mtx.MultiplyPoint3x4(local[i]);
            return wv;
        }

        Transform? FindNeckReference()
        {
            if (_target == null) return null;
            var anim = _target.GetComponentInParent<Animator>();
            if (anim != null && anim.isHuman)
            {
                var neck = anim.GetBoneTransform(HumanBodyBones.Neck);
                if (neck != null) return neck;
                return anim.GetBoneTransform(HumanBodyBones.Head);
            }
            return null;
        }

        // 平面の既定位置 = 対象メッシュの最下部・中心 (首の切れ目付近)。
        // ここを基準に上方向へフェードすれば、開口が無い頭部でも首元中心のマスクになる。
        // Neck ボーンや任意 Transform に合わせたい場合は UI のボタン/ハンドルで変更可能。
        void InitPlaneCenter()
        {
            _planeInited = true;
            if (_worldVerts.Length > 0)
            {
                Vector3 sum = Vector3.zero; float minY = float.MaxValue;
                foreach (var v in _worldVerts) { sum += v; if (v.y < minY) minY = v.y; }
                Vector3 c = sum / _worldVerts.Length;
                _planeCenter = new Vector3(c.x, minY, c.z);
                _planeAxisIndex = 0;   // 上方向へフェード
                return;
            }
            var nb = FindNeckReference();
            if (nb != null) { if (_reference == null) _reference = nb; _planeCenter = nb.position; return; }
            if (_target != null) _planeCenter = _target.bounds.center;
        }

        Vector3 PlaneNormal() => _planeAxisIndex switch
        {
            0 => Vector3.up,
            1 => Vector3.down,
            2 => Vector3.forward,
            3 => Vector3.back,
            4 => Vector3.right,
            5 => Vector3.left,
            6 => _reference != null ? _reference.up : Vector3.up,
            _ => Vector3.up,
        };

        static long EdgeKey(int a, int b)
        {
            int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            return ((long)lo << 32) | (uint)hi;
        }
        static void UnpackEdge(long key, out int a, out int b)
        {
            a = (int)(key >> 32);
            b = (int)(key & 0xffffffff);
        }

        // ================= マスク計算 =================

        void ComputeMask()
        {
            _maskDirty = false;
            if (_worldVerts.Length == 0) { _vertexMask = System.Array.Empty<float>(); return; }
            _vertexMask = new float[_worldVerts.Length];

            // 参照点集合 (距離基準)。平面モードのみ pts を使わず符号付き距離。
            Vector3[]? pts = null;
            switch (_fadeSource)
            {
                case FadeSource.BoundaryLoopSelf:
                {
                    var l = new List<Vector3>();
                    for (int i = 0; i < _loops.Count; i++)
                        if (i < _loopSelected.Length && _loopSelected[i])
                            foreach (int r in _loops[i]) l.Add(_worldVerts[r]);
                    pts = l.ToArray();
                    break;
                }
                case FadeSource.BoundaryLoopCrossMesh:
                    pts = _refPoints;
                    break;
                case FadeSource.Point:
                    pts = new[] { _reference != null ? _reference.position : _planeCenter };
                    break;
                case FadeSource.Plane:
                    pts = null;
                    break;
            }

            bool plane = _fadeSource == FadeSource.Plane;
            Vector3 pc = _planeCenter;
            Vector3 nrm = PlaneNormal().normalized;

            if (!plane && (pts == null || pts.Length == 0)) { UpdatePreviewMesh(); return; }

            float start = _startDistance;
            float end = Mathf.Max(_endDistance, start + 1e-4f);
            for (int i = 0; i < _worldVerts.Length; i++)
            {
                var p = _worldVerts[i];
                float dist;
                if (plane)
                {
                    // 法線側 (フェード方向) の符号付き距離。基準面より手前 (体側) は 0 → マスク1。
                    dist = Mathf.Max(0f, Vector3.Dot(p - pc, nrm));
                }
                else
                {
                    float best = float.MaxValue;
                    for (int j = 0; j < pts!.Length; j++)
                    {
                        float d = (p - pts[j]).sqrMagnitude;
                        if (d < best) best = d;
                    }
                    dist = Mathf.Sqrt(best);
                }
                float t = Mathf.InverseLerp(start, end, dist);
                float fade = Mathf.Clamp01(_curve.Evaluate(Mathf.Clamp01(t)));
                float v = 1f - fade;                 // 首元(dist小)=1, 遠方=0
                if (_invert) v = 1f - v;
                _vertexMask[i] = Mathf.Clamp01(v);
            }
            UpdatePreviewMesh();
        }

        void UpdatePreviewMesh()
        {
            if (_mesh == null || _vertexMask.Length == 0 || _target == null) return;
            if (_previewMesh == null)
                _previewMesh = new Mesh { name = "__NeckFadePreview", hideFlags = HideFlags.HideAndDontSave };

            _previewMesh.Clear();
            if (_target is SkinnedMeshRenderer smr)
            {
                // useScale:true + OnScene の localToWorldMatrix で実描画位置に一致させる。
                // useScale:false だとスケールされたアバターでプレビューが縮小・移動してしまう。
                smr.BakeMesh(_previewMesh, true);
            }
            else
            {
                _previewMesh.vertices = _mesh.vertices;
                _previewMesh.triangles = _mesh.triangles;
                _previewMesh.normals = _mesh.normals;
            }

            if (_previewMesh.vertexCount == _vertexMask.Length)
            {
                var cols = new Color[_vertexMask.Length];
                for (int i = 0; i < cols.Length; i++)
                {
                    float m = _vertexMask[i];
                    cols[i] = new Color(m, m, m, 1f);
                }
                _previewMesh.colors = cols;
            }
        }

        // ================= プレビュー / シーンハンドル =================

        void OnScene(SceneView sv)
        {
            if (_target == null) return;

            // 平面/点モードは基準位置をドラッグ可能なハンドルで表示・編集
            if (_fadeSource == FadeSource.Plane || _fadeSource == FadeSource.Point)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 np = Handles.PositionHandle(_planeCenter, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    _planeCenter = np;
                    _planeInited = true;
                    _maskDirty = true;
                    Repaint();
                }
                if (_fadeSource == FadeSource.Plane)
                {
                    var n = PlaneNormal().normalized;
                    float r = Mathf.Max(0.05f, _target.bounds.size.magnitude * 0.25f);
                    Handles.color = new Color(0.2f, 0.8f, 1f, 0.7f);
                    Handles.DrawWireDisc(_planeCenter, n, r);
                    Handles.DrawLine(_planeCenter, _planeCenter + n * (r * 0.5f));
                }
                else
                {
                    Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
                    Handles.SphereHandleCap(0, _planeCenter, Quaternion.identity,
                        HandleUtility.GetHandleSize(_planeCenter) * 0.08f, EventType.Repaint);
                }
            }

            if (!_livePreview || _previewMesh == null || _vertexMask.Length == 0) return;
            if (_previewMat == null)
            {
                var sh = Shader.Find("Hidden/Rroki/NeckFadePreview");
                if (sh == null) return;
                _previewMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
            var mtx = _target.transform.localToWorldMatrix;
            _previewMat.SetPass(0);
            Graphics.DrawMeshNow(_previewMesh, mtx);
        }

        // ================= ベイク =================

        void GenerateAndSave(bool openBaker)
        {
            var tex = BakeMaskTexture();
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            DestroyImmediate(tex);
            var path = OutputPath();
            var saved = NeckFadeUtil.SavePng(px, w, h, path, sRGB: false);
            _lastMask = saved;
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
            Debug.Log($"[首元フェードマスク生成] 保存しました: {path}", saved);
            if (openBaker) SdfNeckFadeBaker.OpenWithMask(saved);
        }

        string OutputPath()
        {
            string dir = "Assets";
            string name = "NeckFadeMask";
            var meshPath = _mesh != null ? AssetDatabase.GetAssetPath(_mesh) : null;
            if (!string.IsNullOrEmpty(meshPath))
            {
                dir = Path.GetDirectoryName(meshPath)!.Replace('\\', '/');
                name = Path.GetFileNameWithoutExtension(meshPath) + "_NeckFade";
            }
            return AssetDatabase.GenerateUniqueAssetPath($"{dir}/{name}.png");
        }

        Texture2D BakeMaskTexture()
        {
            int res = _resolution;
            var rt = RenderTexture.GetTemporary(res, res, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            GL.PushMatrix();
            GL.LoadOrtho();

            var mat = GLMaterial();
            mat.SetPass(0);
            GL.Begin(GL.TRIANGLES);
            for (int t = 0; t < _tris.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int idx = _tris[t + k];
                    float m = _vertexMask[idx];
                    GL.Color(new Color(m, m, m, 1f));
                    var uv = _uv[idx];
                    GL.Vertex3(uv.x, _flipV ? 1f - uv.y : uv.y, 0f);
                }
            }
            GL.End();
            GL.PopMatrix();

            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            Dilate(tex, _dilate);

            // R=G=B=マスク, A=255 (グレースケール化)
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                byte v = px[i].r;
                px[i] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static Material? _glMat;
        static Material GLMaterial()
        {
            if (_glMat == null)
            {
                _glMat = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                _glMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                _glMat.SetInt("_ZWrite", 0);
                _glMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _glMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                _glMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            }
            return _glMat;
        }

        // UV ふちを拡張してシーム跡を埋める (covered = alpha>0)
        static void Dilate(Texture2D tex, int iterations)
        {
            if (iterations <= 0) return;
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            for (int it = 0; it < iterations; it++)
            {
                var src = (Color32[])px.Clone();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (src[i].a != 0) continue;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                                var n = src[ny * w + nx];
                                if (n.a != 0) { px[i] = n; dy = 2; break; }
                            }
                        }
                    }
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
        }

        static void EnableMeshReadWrite(Mesh mesh)
        {
            var path = AssetDatabase.GetAssetPath(mesh);
            if (AssetImporter.GetAtPath(path) is ModelImporter mi)
            {
                mi.isReadable = true;
                mi.SaveAndReimport();
            }
        }
    }
}
