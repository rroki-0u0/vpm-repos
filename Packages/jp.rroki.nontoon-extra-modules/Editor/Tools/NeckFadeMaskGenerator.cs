#nullable enable
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rroki.NonToonExtraModules.Tools
{
    /// <summary>
    /// 首元フェードマスク生成ツール。
    /// メッシュの「開いた境界エッジ」(首の切れ目など、1 つの三角形にしか属さないエッジ) からの
    /// 距離をもとに、UV 空間のグラデーションマスクを自動生成する。シーンビューでモデル上に
    /// ヒートマップ表示しながら、フェード開始/終了距離とカーブを調整できる。
    /// 生成したマスクは SDF Neck Fade Baker へそのまま渡せる。
    /// </summary>
    public sealed class NeckFadeMaskGenerator : EditorWindow
    {
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

        // ---- キャッシュ (非シリアライズ) ----
        Mesh? _mesh;
        Vector3[] _verts = System.Array.Empty<Vector3>();
        Vector2[] _uv = System.Array.Empty<Vector2>();
        int[] _tris = System.Array.Empty<int>();
        readonly List<List<int>> _loops = new();
        readonly List<Vector3> _loopCenter = new();
        bool[] _loopSelected = System.Array.Empty<bool>();
        float[] _vertexMask = System.Array.Empty<float>();
        Mesh? _previewMesh;
        Material? _previewMat;
        bool _boundaryDirty = true;
        bool _maskDirty = true;
        bool _hasUV = true;
        Texture2D? _lastMask;
        Vector2 _scroll;

        [MenuItem("Tools/rroki/NonToon/首元フェードマスク生成")]
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
                "首の切れ目 (開いた境界エッジ) からの距離でフェードマスクを作ります。" +
                "顔メッシュを指定し、境界ループを選んで、開始/終了距離とカーブを調整してください。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _target = (Renderer?)EditorGUILayout.ObjectField("対象メッシュ (Renderer)", _target, typeof(Renderer), true);
            _uvChannel = EditorGUILayout.IntPopup("UV チャンネル", _uvChannel,
                new[] { "UV0", "UV1", "UV2", "UV3" }, new[] { 0, 1, 2, 3 });
            if (EditorGUI.EndChangeCheck()) _boundaryDirty = true;

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

            if (!_hasUV)
                EditorGUILayout.HelpBox($"UV{_uvChannel} が見つかりません。別の UV チャンネルを選ぶか、UV のあるメッシュを指定してください。", MessageType.Warning);

            // ---- 境界ループの選択 ----
            if (_loops.Count == 0)
            {
                EditorGUILayout.HelpBox("開いた境界エッジが見つかりませんでした (閉じたメッシュの可能性)。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField($"境界ループ ({_loops.Count} 個) — 首元のループにチェック", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                for (int i = 0; i < _loops.Count; i++)
                {
                    var c = _loopCenter[i];
                    _loopSelected[i] = EditorGUILayout.ToggleLeft(
                        $"ループ {i}: 中心Y={c.y:F3}  頂点数={_loops[i].Count}", _loopSelected[i]);
                }
                if (EditorGUI.EndChangeCheck()) _maskDirty = true;
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

        // ================= 境界検出 =================

        Mesh? ResolveMesh()
        {
            if (_target is SkinnedMeshRenderer smr) return smr.sharedMesh;
            if (_target != null)
            {
                var mf = _target.GetComponent<MeshFilter>();
                return mf != null ? mf.sharedMesh : null;
            }
            return null;
        }

        void ComputeBoundary()
        {
            _boundaryDirty = false;
            _loops.Clear();
            _loopCenter.Clear();

            var mesh = ResolveMesh();
            if (mesh == null || !mesh.isReadable) return;
            _mesh = mesh;
            _verts = mesh.vertices;
            _tris = mesh.triangles;

            var uvList = new List<Vector2>();
            mesh.GetUVs(_uvChannel, uvList);
            _hasUV = uvList.Count == _verts.Length;
            _uv = _hasUV ? uvList.ToArray() : new Vector2[_verts.Length];

            // 位置で溶接 (UV/法線シームで割れた頂点を同一視 → 真の開境界のみ検出)
            var repOf = new int[_verts.Length];
            var posToRep = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < _verts.Length; i++)
            {
                var p = _verts[i];
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
            for (int t = 0; t < _tris.Length; t += 3)
            {
                AddEdge(_tris[t], _tris[t + 1]);
                AddEdge(_tris[t + 1], _tris[t + 2]);
                AddEdge(_tris[t + 2], _tris[t]);
            }

            // 1 つの三角形にしか属さないエッジ = 開境界。境界頂点間の隣接を作る
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

            // 連結成分 = 境界ループ
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
                _loops.Add(comp);
            }

            // 中心を計算し、既定で最も低い (首元とみなす) ループを選択
            _loopSelected = new bool[_loops.Count];
            int lowest = -1;
            float lowestY = float.MaxValue;
            for (int i = 0; i < _loops.Count; i++)
            {
                Vector3 sum = Vector3.zero;
                foreach (int r in _loops[i]) sum += _verts[r];
                Vector3 center = sum / Mathf.Max(1, _loops[i].Count);
                _loopCenter.Add(center);
                if (center.y < lowestY) { lowestY = center.y; lowest = i; }
            }
            if (lowest >= 0) _loopSelected[lowest] = true;
            _maskDirty = true;
        }

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
            if (_verts.Length == 0) { _vertexMask = System.Array.Empty<float>(); return; }

            var pts = new List<Vector3>();
            for (int i = 0; i < _loops.Count; i++)
                if (i < _loopSelected.Length && _loopSelected[i])
                    foreach (int r in _loops[i]) pts.Add(_verts[r]);

            _vertexMask = new float[_verts.Length];
            if (pts.Count == 0) { UpdatePreviewMesh(); return; }

            var arr = pts.ToArray();
            float start = _startDistance;
            float end = Mathf.Max(_endDistance, start + 1e-4f);
            for (int i = 0; i < _verts.Length; i++)
            {
                var p = _verts[i];
                float best = float.MaxValue;
                for (int j = 0; j < arr.Length; j++)
                {
                    float d = (p - arr[j]).sqrMagnitude;
                    if (d < best) best = d;
                }
                float dist = Mathf.Sqrt(best);
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
            if (_mesh == null || _vertexMask.Length == 0) return;
            if (_previewMesh == null)
                _previewMesh = new Mesh { name = "__NeckFadePreview", hideFlags = HideFlags.HideAndDontSave };

            _previewMesh.Clear();
            if (_target is SkinnedMeshRenderer smr)
            {
                smr.BakeMesh(_previewMesh);
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

        // ================= プレビュー =================

        void OnScene(SceneView sv)
        {
            if (!_livePreview || _previewMesh == null || _target == null || _vertexMask.Length == 0) return;
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
