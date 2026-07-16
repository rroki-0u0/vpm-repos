#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rroki.NonToonExtraModules.Tools
{
    /// <summary>
    /// エミッションマスク合成: 既存の発光マスクへ追加のマスクを合成して 1 枚にまとめる。
    /// NonToon のエミッションは「共有マスク (_SharedMask) の 1 チャンネル」で発光領域を
    /// ゲートするため、新しい発光領域 (例: 変換したタトゥーの EMI マスク) を追加するには
    /// 既存マスクの該当チャンネルへ焼き込む必要がある。本ツールはそれを非破壊
    /// (新規 PNG 出力) で行う。エミッションマップ (RGB 発光色) 同士の合成にも対応。
    /// </summary>
    public sealed class EmissionMaskCombiner : EditorWindow
    {
        enum CombineTarget { SharedMaskChannel, EmissionMap }
        enum SourceChannel { R, G, B, A, Luminance, MaxRGB }
        enum MaskBlend { Max, Add, Subtract }
        enum ColorBlend { Add, Max, Screen }

        [Serializable]
        sealed class Layer
        {
            public Texture2D? texture;
            public SourceChannel source = SourceChannel.MaxRGB;
            public MaskBlend maskBlend = MaskBlend.Max;
            public ColorBlend colorBlend = ColorBlend.Add;
            public bool useSourceRgb = true;           // EmissionMap モード: RGB をそのまま使う
            public Color tint = Color.white;           // EmissionMap モード: チャンネル×色
            [Range(0f, 1f)] public float strength = 1f;
        }

        const string SharedMaskProp = "_SharedMask";
        const string EmissionPrefix = "_jp_rroki_nontoon_emission_";
        const string EmissionMapProp = EmissionPrefix + "EmissionMap";
        const string EmissionMaskChannelProp = EmissionPrefix + "EmissionMaskChannel";
        const string EmissionEnableProp = EmissionPrefix + "Enable";
        const string EmissionEnableKeyword = "_JP_RROKI_NONTOON_EMISSION_ENABLE_1";

        static readonly string[] ChannelLabels = { "R", "G", "B", "A" };
        static readonly string[] SourceLabels = { "R", "G", "B", "A", "輝度", "最大RGB" };

        [SerializeField] CombineTarget _target = CombineTarget.SharedMaskChannel;
        [SerializeField] Material? _material;
        [SerializeField] Texture2D? _baseTexture;
        [SerializeField] int _channel = 2; // B (NonToon Emission の既定マスクチャンネルに合わせる)
        [SerializeField] List<Layer> _layers = new List<Layer> { new Layer() };
        [SerializeField] bool _assignToMaterial = true;
        [SerializeField] Vector2 _scroll;

        Material? _lastMaterial;

        [MenuItem("Tools/rroki_'s tools/NonToon/エミッションマスク合成")]
        public static void Open() => GetWindow<EmissionMaskCombiner>("エミッションマスク合成");

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "既存の発光マスクへ追加マスクを合成して 1 枚の PNG に出力します (元テクスチャは変更しません)。\n" +
                "共有マスク: _SharedMask の発光チャンネルへ白黒マスクを追加 (発光色は既存のエミッション設定のまま)\n" +
                "エミッションマップ: RGB 発光色マップ同士を合成 (色付きの発光を追加したい場合)",
                MessageType.Info);

            _target = (CombineTarget)EditorGUILayout.Popup("合成先",
                (int)_target, new[] { "共有マスクのチャンネル", "エミッションマップ (RGB)" });

            _material = (Material?)EditorGUILayout.ObjectField(
                new GUIContent("マテリアル (任意)", "NonToon マテリアルを指定すると合成元テクスチャとチャンネルを自動取得し、保存後に差し戻せます"),
                _material, typeof(Material), false);
            if (_material != _lastMaterial)
            {
                _lastMaterial = _material;
                PullFromMaterial();
            }
            DrawMaterialInfo();

            EditorGUILayout.Space();
            string baseLabel = _target == CombineTarget.SharedMaskChannel ? "合成元 (共有マスク)" : "合成元 (エミッションマップ)";
            _baseTexture = (Texture2D?)EditorGUILayout.ObjectField(baseLabel, _baseTexture, typeof(Texture2D), false);
            if (_target == CombineTarget.SharedMaskChannel)
            {
                _channel = EditorGUILayout.Popup("対象チャンネル", _channel, ChannelLabels);
                if (_baseTexture == null)
                    EditorGUILayout.HelpBox("合成元が未指定です。黒 (全チャンネル 0) から開始します。", MessageType.None);
            }
            else if (_baseTexture == null)
            {
                EditorGUILayout.HelpBox(
                    "エミッションマップ未設定のマテリアルは「白いマップ×マスクチャンネル」で発光しています。" +
                    "ここでマップを新規作成して割り当てると、既存の発光領域はマップの黒に潰されます。" +
                    "既存の発光を維持したい場合は「共有マスクのチャンネル」モードを使ってください。",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("追加するマスク", EditorStyles.boldLabel);
            int remove = -1;
            for (int i = 0; i < _layers.Count; i++)
            {
                var l = _layers[i];
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();
                l.texture = (Texture2D?)EditorGUILayout.ObjectField(l.texture, typeof(Texture2D), false);
                if (GUILayout.Button("×", GUILayout.Width(22))) remove = i;
                EditorGUILayout.EndHorizontal();
                if (_target == CombineTarget.SharedMaskChannel)
                {
                    l.source = (SourceChannel)EditorGUILayout.Popup("ソース", (int)l.source, SourceLabels);
                    l.maskBlend = (MaskBlend)EditorGUILayout.EnumPopup("合成", l.maskBlend);
                    EditorGUILayout.LabelField(MaskBlendHint(l.maskBlend), EditorStyles.miniLabel);
                }
                else
                {
                    l.useSourceRgb = EditorGUILayout.Toggle(new GUIContent("RGB をそのまま使う", "オフにするとソースチャンネル×色で加える"), l.useSourceRgb);
                    if (!l.useSourceRgb)
                    {
                        l.source = (SourceChannel)EditorGUILayout.Popup("ソース", (int)l.source, SourceLabels);
                        l.tint = EditorGUILayout.ColorField("色", l.tint);
                    }
                    l.colorBlend = (ColorBlend)EditorGUILayout.EnumPopup("合成", l.colorBlend);
                }
                l.strength = EditorGUILayout.Slider("強度", l.strength, 0f, 1f);
                EditorGUILayout.EndVertical();
            }
            if (remove >= 0) _layers.RemoveAt(remove);
            if (GUILayout.Button("+ マスクを追加")) _layers.Add(new Layer());

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_material == null))
            {
                _assignToMaterial = EditorGUILayout.Toggle(
                    new GUIContent("マテリアルへ割り当て", "保存した PNG をマテリアルの該当スロットへ差し戻す"), _assignToMaterial);
            }

            bool hasLayer = _layers.Exists(l => l.texture != null);
            using (new EditorGUI.DisabledScope(!hasLayer))
            {
                if (GUILayout.Button("合成して保存", GUILayout.Height(28)))
                    Combine();
            }

            EditorGUILayout.EndScrollView();
        }

        static string MaskBlendHint(MaskBlend b) => b switch
        {
            MaskBlend.Max => "Max: max(既存, 追加)。既存の発光領域を保ちつつ追加 (推奨)",
            MaskBlend.Add => "Add: 既存 + 追加 (重なりは飽和)",
            _ => "Subtract: 既存 − 追加 (発光を削る)",
        };

        void DrawMaterialInfo()
        {
            if (_material == null) return;
            var m = _material;
            if (m.HasProperty(EmissionEnableProp) && m.GetInteger(EmissionEnableProp) == 0)
            {
                EditorGUILayout.HelpBox("このマテリアルのエミッションモジュールは無効です。", MessageType.Warning);
                if (GUILayout.Button("エミッションモジュールを有効化"))
                {
                    Undo.RecordObject(m, "Enable NonToon Emission");
                    m.SetInteger(EmissionEnableProp, 1);
                    m.EnableKeyword(EmissionEnableKeyword);
                    EditorUtility.SetDirty(m);
                }
            }
            if (_target == CombineTarget.SharedMaskChannel && m.HasProperty(EmissionMaskChannelProp))
            {
                int ch = m.GetInteger(EmissionMaskChannelProp);
                if (ch != _channel)
                    EditorGUILayout.HelpBox(
                        $"マテリアルの発光マスクチャンネルは {ChannelLabels[Mathf.Clamp(ch, 0, 3)]} です (現在の対象: {ChannelLabels[_channel]})。",
                        MessageType.Warning);
            }
        }

        void PullFromMaterial()
        {
            if (_material == null) return;
            var m = _material;
            if (_target == CombineTarget.SharedMaskChannel)
            {
                if (m.HasProperty(SharedMaskProp))
                    _baseTexture = m.GetTexture(SharedMaskProp) as Texture2D;
                if (m.HasProperty(EmissionMaskChannelProp))
                    _channel = Mathf.Clamp(m.GetInteger(EmissionMaskChannelProp), 0, 3);
            }
            else if (m.HasProperty(EmissionMapProp))
            {
                _baseTexture = m.GetTexture(EmissionMapProp) as Texture2D;
            }
            Repaint();
        }

        void Combine()
        {
            var layers = _layers.FindAll(l => l.texture != null);
            if (layers.Count == 0) return;

            Texture2D? result = _target == CombineTarget.SharedMaskChannel
                ? CombineIntoChannel(_baseTexture, _channel, layers)
                : CombineEmissionMaps(_baseTexture, layers);
            if (result == null) return;

            if (_assignToMaterial && _material != null)
            {
                string prop = _target == CombineTarget.SharedMaskChannel ? SharedMaskProp : EmissionMapProp;
                if (_material.HasProperty(prop))
                {
                    Undo.RecordObject(_material, "Assign Combined Emission Mask");
                    _material.SetTexture(prop, result);
                    EditorUtility.SetDirty(_material);
                    Debug.Log($"[エミッションマスク合成] {_material.name} の {prop} へ割り当てました", _material);
                }
            }
            _baseTexture = result;
            Selection.activeObject = result;
            EditorGUIUtility.PingObject(result);
        }

        // -----------------------------------------------------------------
        // 合成本体 (スクリプトからも呼べる static API)
        // -----------------------------------------------------------------

        /// <summary>
        /// スクリプト用: 追加マスク 1 枚を共有マスクの指定チャンネルへ Max 合成する。
        /// sourceChannel: 0-3 = R/G/B/A, 4 = 輝度, 5 = 最大RGB
        /// </summary>
        public static Texture2D? CombineMaskChannelSimple(Texture2D? baseTex, int channel, Texture2D add, int sourceChannel, float strength = 1f)
        {
            var layer = new Layer { texture = add, source = (SourceChannel)sourceChannel, maskBlend = MaskBlend.Max, strength = strength };
            return CombineIntoChannel(baseTex, channel, new List<Layer> { layer });
        }

        /// <summary>共有マスクの 1 チャンネルへマスク群を合成し、PNG アセットとして保存する。</summary>
        static Texture2D? CombineIntoChannel(Texture2D? baseTex, int channel, List<Layer> layers)
        {
            int w, h;
            Color32[] px;
            bool srgb;
            if (baseTex != null)
            {
                px = ReadPixelsBest(baseTex, out w, out h);
                srgb = NeckFadeUtil.IsSRGB(baseTex);
            }
            else
            {
                var first = layers[0].texture!;
                ReadPixelsBest(first, out w, out h);
                px = new Color32[w * h];
                srgb = false;
            }

            var samplers = PrepareLayers(layers);
            long changed = 0;
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    int i = y * w + x;
                    float val = GetChannel(px[i], channel) / 255f;
                    float before = val;
                    foreach (var s in samplers)
                    {
                        float add = s.SampleScalar(u, v) * s.layer.strength;
                        val = s.layer.maskBlend switch
                        {
                            MaskBlend.Max => Mathf.Max(val, add),
                            MaskBlend.Add => val + add,
                            _ => val - add,
                        };
                    }
                    val = Mathf.Clamp01(val);
                    if (!Mathf.Approximately(val, before))
                    {
                        px[i] = SetChannel(px[i], channel, (byte)Mathf.RoundToInt(val * 255f));
                        changed++;
                    }
                }
            }

            string outPath = MakeOutputPath(baseTex, layers[0].texture!, "_Combined");
            var result = SavePngLike(px, w, h, outPath, baseTex, srgb);
            Debug.Log($"[エミッションマスク合成] {ChannelLabels[channel]} チャンネルへ合成しました: {outPath} (変更 {(double)changed * 100 / (w * h):F1}% のテクセル)", result);
            return result;
        }

        /// <summary>エミッションマップ (RGB) 同士を合成し、PNG アセットとして保存する。</summary>
        static Texture2D? CombineEmissionMaps(Texture2D? baseTex, List<Layer> layers)
        {
            int w, h;
            Color32[] px;
            bool srgb;
            if (baseTex != null)
            {
                px = ReadPixelsBest(baseTex, out w, out h);
                srgb = NeckFadeUtil.IsSRGB(baseTex);
            }
            else
            {
                var first = layers[0].texture!;
                ReadPixelsBest(first, out w, out h);
                px = new Color32[w * h]; // 黒 (A=0)
                srgb = NeckFadeUtil.IsSRGB(first);
            }

            var samplers = PrepareLayers(layers);
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    int i = y * w + x;
                    Vector3 c = new Vector3(px[i].r, px[i].g, px[i].b) / 255f;
                    foreach (var s in samplers)
                    {
                        Vector3 src;
                        if (s.layer.useSourceRgb)
                        {
                            var rgb = s.SampleColor(u, v);
                            src = new Vector3(rgb.r, rgb.g, rgb.b);
                        }
                        else
                        {
                            float scalar = s.SampleScalar(u, v);
                            src = new Vector3(s.layer.tint.r, s.layer.tint.g, s.layer.tint.b) * scalar;
                        }
                        src *= s.layer.strength;
                        c = s.layer.colorBlend switch
                        {
                            ColorBlend.Max => Vector3.Max(c, src),
                            ColorBlend.Screen => Vector3.one - Vector3.Scale(Vector3.one - c, Vector3.one - src),
                            _ => c + src,
                        };
                    }
                    px[i].r = (byte)Mathf.RoundToInt(Mathf.Clamp01(c.x) * 255f);
                    px[i].g = (byte)Mathf.RoundToInt(Mathf.Clamp01(c.y) * 255f);
                    px[i].b = (byte)Mathf.RoundToInt(Mathf.Clamp01(c.z) * 255f);
                    if (baseTex == null) px[i].a = 255;
                }
            }

            string outPath = MakeOutputPath(baseTex, layers[0].texture!, "_Combined");
            var result = SavePngLike(px, w, h, outPath, baseTex ?? layers[0].texture, srgb);
            Debug.Log($"[エミッションマスク合成] エミッションマップを合成しました: {outPath}", result);
            return result;
        }

        // -----------------------------------------------------------------
        // ヘルパー
        // -----------------------------------------------------------------

        /// <summary>
        /// テクセルを劣化なしで読む。アセットが PNG/JPG ならファイルを直接デコードする
        /// (インポーターの圧縮 (DXT 等) を経由すると圧縮ノイズが焼き込まれるため)。
        /// それ以外は NeckFadeUtil.ReadRaw (readable 化) にフォールバック。
        /// </summary>
        static Color32[] ReadPixelsBest(Texture2D tex, out int width, out int height)
        {
            var path = AssetDatabase.GetAssetPath(tex);
            if (!string.IsNullOrEmpty(path))
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                    if (tmp.LoadImage(File.ReadAllBytes(path)))
                    {
                        width = tmp.width;
                        height = tmp.height;
                        var px = tmp.GetPixels32();
                        UnityEngine.Object.DestroyImmediate(tmp);
                        return px;
                    }
                    UnityEngine.Object.DestroyImmediate(tmp);
                }
            }
            return NeckFadeUtil.ReadRaw(tex, out width, out height);
        }

        sealed class LayerSampler
        {
            public Layer layer = null!;
            public Color32[] px = null!;
            public int w, h;

            public float SampleScalar(float u, float v)
            {
                if (layer.source <= SourceChannel.A)
                    return NeckFadeUtil.SampleBilinear(px, w, h, u, v, (int)layer.source);
                var c = SampleColor(u, v);
                return layer.source == SourceChannel.Luminance
                    ? c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f
                    : Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            }

            public Color SampleColor(float u, float v)
            {
                float r = NeckFadeUtil.SampleBilinear(px, w, h, u, v, 0);
                float g = NeckFadeUtil.SampleBilinear(px, w, h, u, v, 1);
                float b = NeckFadeUtil.SampleBilinear(px, w, h, u, v, 2);
                return new Color(r, g, b, 1f);
            }
        }

        static List<LayerSampler> PrepareLayers(List<Layer> layers)
        {
            var list = new List<LayerSampler>();
            foreach (var l in layers)
            {
                var s = new LayerSampler { layer = l };
                s.px = ReadPixelsBest(l.texture!, out s.w, out s.h);
                list.Add(s);
            }
            return list;
        }

        static float GetChannel(Color32 c, int ch) => ch == 0 ? c.r : ch == 1 ? c.g : ch == 2 ? c.b : c.a;

        static Color32 SetChannel(Color32 c, int ch, byte v)
        {
            if (ch == 0) c.r = v; else if (ch == 1) c.g = v; else if (ch == 2) c.b = v; else c.a = v;
            return c;
        }

        static string MakeOutputPath(Texture2D? baseTex, Texture2D fallbackSibling, string suffix)
        {
            var refTex = baseTex != null ? baseTex : fallbackSibling;
            var refPath = AssetDatabase.GetAssetPath(refTex);
            var dir = Path.GetDirectoryName(refPath)!;
            var name = Path.GetFileNameWithoutExtension(refPath);
            return AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, name + suffix + ".png").Replace('\\', '/'));
        }

        /// <summary>PNG を保存し、インポーター設定を元テクスチャからコピーする (無ければ既定)。</summary>
        static Texture2D SavePngLike(Color32[] px, int w, int h, string assetPath, Texture2D? settingsSource, bool srgb)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath);

            var dst = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            var src = settingsSource != null
                ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(settingsSource)) as TextureImporter
                : null;
            if (dst != null)
            {
                if (src != null)
                {
                    dst.sRGBTexture = src.sRGBTexture;
                    dst.alphaIsTransparency = src.alphaIsTransparency;
                    dst.alphaSource = src.alphaSource;
                    dst.mipmapEnabled = src.mipmapEnabled;
                    dst.streamingMipmaps = src.streamingMipmaps;
                    dst.wrapMode = src.wrapMode;
                    dst.filterMode = src.filterMode;
                    dst.maxTextureSize = src.maxTextureSize;
                    dst.textureCompression = src.textureCompression;
                    dst.crunchedCompression = src.crunchedCompression;
                }
                else
                {
                    dst.sRGBTexture = srgb;
                    dst.textureCompression = TextureImporterCompression.Compressed;
                }
                dst.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }
    }
}
