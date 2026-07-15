using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rroki.Tools
{
    // Tools/rroki_'s tools/Submesh Mask Baker
    // 指定サブメッシュ(Materials要素)の三角形をUV空間に焼き込み、白黒マスクPNGを出力する
    internal sealed class SubmeshMaskBaker : EditorWindow
    {
        SkinnedMeshRenderer _smr;
        int _submesh = 1;       // マスク化したいMaterials要素 = サブメッシュ番号
        int _resolution = 2048;
        int _padding = 4;       // UVシームのにじみ対策(ピクセル)
        int _uvChannel = 0;     // 0 = UV0(通常)

        [MenuItem("Tools/rroki_'s tools/Submesh Mask Baker")]
        static void Open() => GetWindow<SubmeshMaskBaker>("Submesh Mask Baker");

        void OnGUI()
        {
            _smr        = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Renderer", _smr, typeof(SkinnedMeshRenderer), true);
            _submesh    = EditorGUILayout.IntField("Submesh (Material要素)", _submesh);
            _resolution = EditorGUILayout.IntField("Resolution", _resolution);
            _padding    = EditorGUILayout.IntField("Padding (px)", _padding);
            _uvChannel  = EditorGUILayout.IntField("UV Channel", _uvChannel);

            using (new EditorGUI.DisabledScope(_smr == null || _smr.sharedMesh == null))
                if (GUILayout.Button("Bake"))
                    Bake();
        }

        void Bake()
        {
            Mesh mesh = _smr.sharedMesh;
            var uv = new List<Vector2>();
            mesh.GetUVs(_uvChannel, uv);
            int[] tris = mesh.GetTriangles(_submesh);

            var mask = new bool[_resolution * _resolution];
            for (int i = 0; i < tris.Length; i += 3)
                Fill(uv[tris[i]], uv[tris[i + 1]], uv[tris[i + 2]], mask);

            for (int i = 0; i < _padding; i++) Dilate(mask);

            var px = new Color32[mask.Length];
            Color32 w = new Color32(255, 255, 255, 255), b = new Color32(0, 0, 0, 255);
            for (int i = 0; i < mask.Length; i++) px[i] = mask[i] ? w : b;

            var tex = new Texture2D(_resolution, _resolution, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();

            string path = EditorUtility.SaveFilePanelInProject("Save Mask", "FurMask", "png", "");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.Refresh();
            Debug.Log($"Baked submesh {_submesh} mask -> {path}");
        }

        // UV(0..1)→ピクセル座標。SetPixels32は下→上の行順なのでV反転は不要
        void Fill(Vector2 a, Vector2 b, Vector2 c, bool[] mask)
        {
            int r = _resolution;
            a *= r; b *= r; c *= r;

            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x)), 0, r - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt (Mathf.Max(a.x, b.x, c.x)), 0, r - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y)), 0, r - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt (Mathf.Max(a.y, b.y, c.y)), 0, r - 1);

            float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(d) < 1e-7f) return;   // 退化三角形を除外

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float qx = x + 0.5f, qy = y + 0.5f;
                float w0 = ((b.y - c.y) * (qx - c.x) + (c.x - b.x) * (qy - c.y)) / d;
                float w1 = ((c.y - a.y) * (qx - c.x) + (a.x - c.x) * (qy - c.y)) / d;
                if (w0 >= 0 && w1 >= 0 && w0 + w1 <= 1)   // 重心座標で内外判定(巻き順非依存)
                    mask[y * r + x] = true;
            }
        }

        void Dilate(bool[] src)
        {
            int r = _resolution;
            var prev = (bool[])src.Clone();
            for (int y = 0; y < r; y++)
            for (int x = 0; x < r; x++)
            {
                int i = y * r + x;
                if (prev[i]) continue;
                if ((x > 0 && prev[i - 1]) || (x < r - 1 && prev[i + 1]) ||
                    (y > 0 && prev[i - r]) || (y < r - 1 && prev[i + r]))
                    src[i] = true;
            }
        }
    }
}
