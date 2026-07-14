#nullable enable
namespace Rroki.PoiyomiToNonToon
{
    /// <summary>
    /// NonToon (jp.lilxyzw.nontoon 0.1.x) のマテリアルプロパティ名定義。
    /// Shader Core はモジュール由来のプロパティを「_ + uniqueID(.を_に置換) + 元プロパティ名」に
    /// リネームして最終シェーダーへ埋め込むため、ここで一元管理する。
    /// (本体 *_properties.hlsl / .scshader 直書きのプロパティは素の名前のまま)
    /// </summary>
    public static class NonToonProps
    {
        public const string ShaderName = "NonToon";
        public const string FurShaderName = "NonToonFur";

        // ---- モジュール uniqueID ----
        public const string ModDetails = "jp.lilxyzw.nontoon.details";
        public const string ModDistanceFade = "jp.lilxyzw.nontoon.distancefade";
        public const string ModHairSpecular = "jp.lilxyzw.nontoon.hairspecular";
        public const string ModLighten = "jp.lilxyzw.nontoon.lighten";
        public const string ModMatCaps = "jp.lilxyzw.nontoon.matcaps";
        public const string ModNearer = "jp.lilxyzw.nontoon.nearer";
        public const string ModRimLight = "jp.lilxyzw.nontoon.rimlight";
        public const string ModRimShade = "jp.lilxyzw.nontoon.rimshade";
        public const string ModShade = "jp.lilxyzw.nontoon.shade";
        public const string ModSpecular = "jp.lilxyzw.nontoon.specular";

        /// <summary>モジュールプロパティの最終名を組み立てる。prop は "_Enable" のように先頭アンダースコア込み。</summary>
        public static string Prop(string moduleId, string prop) => "_" + moduleId.Replace('.', '_') + prop;

        /// <summary>[SCConstValue] プロパティのキーワード名 (値 value のとき有効になるもの)。</summary>
        public static string ConstValueKeyword(string finalPropName, int value)
            => finalPropName.ToUpperInvariant() + "_" + value;

        // ---- 本体 (NonToon_properties.hlsl / NonToon.scshader) ----
        public const string RenderingMode = "_RenderingMode";       // Integer: 0=Opaque 1=Cutout 2=Transparent
        public const string BaseTexture = "_BaseTexture";           // ST なし
        public const string SharedMask = "_SharedMask";             // RGBA マスク (UV0)
        public const string SharedGradients = "_SharedGradients";   // Texture2DArray 128x1xN
        public const string NormalScale = "_NormalScale";
        public const string NormalMap = "_NormalMap";
        public const string NormalMapWithRoughness = "_NormalMapWithRoughness";
        public const string Roughness = "_Roughness";
        public const string Cutoff = "_Cutoff";
        public const string ShadowBias = "_NTShadowBias";
        public const string DitherTex = "_NTDitherTex";
        public const string OutlineColor = "_OutlineColor";
        public const string OutlineWidth = "_OutlineWidth";         // 値 x 0.01m (= cm 単位)
        public const string OutlineZOffset = "_OutlineZOffset";
        public const string OutlineFromVertexColor = "_OutlineFromVertexColor";
        public const string BacklightSharpness = "_BacklightSharpness";
        public const string BacklightRange = "_BacklightRange";
        public const string BacklightMaskChannel = "_BacklightMaskChannel";

        // .scshader 直書き (レガシー Int 型 = float ベース)
        public const string StencilRef = "_StencilRef";
        public const string StencilComp = "_StencilComp";
        public const string StencilPass = "_StencilPass";
        public const string Cull = "_Cull";
        public const string SrcBlend = "_SrcBlend";
        public const string DstBlend = "_DstBlend";
        public const string SrcBlendAlpha = "_SrcBlendAlpha";
        public const string DstBlendAlpha = "_DstBlendAlpha";
        public const string ZWrite = "_ZWrite";
        public const string AlphaToMask = "_AlphaToMask";

        // ---- Shade モジュール ----
        public static readonly string ShadeGradientIndex = Prop(ModShade, "_ShadeGradientIndex");
        public static readonly string ShadeGradientRange = Prop(ModShade, "_ShadeGradientRange");
        public static readonly string SdfType = Prop(ModShade, "_SDFType");
        public static readonly string SdfMap = Prop(ModShade, "_SDFMap");
        public static readonly string SdfBlendVertical = Prop(ModShade, "_SDFBlendVertical");

        // ---- RimLight モジュール ----
        public static readonly string RimLightColor = Prop(ModRimLight, "_RimLightColor");
        public static readonly string RimLightMultiplyAlbedo = Prop(ModRimLight, "_RimLightMultiplyAlbedo");
        public static readonly string RimLightRange = Prop(ModRimLight, "_RimLightRange");
        public static readonly string RimLightMaskChannel = Prop(ModRimLight, "_RimLightMaskChannel");

        // ---- RimShade モジュール ----
        public static readonly string RimShadeGradientIndex = Prop(ModRimShade, "_RimShadeGradientIndex");
        public static readonly string RimShadeMaskChannel = Prop(ModRimShade, "_RimShadeMaskChannel");

        // ---- Specular モジュール ----
        public static readonly string SpecularColor = Prop(ModSpecular, "_SpecularColor");
        public static readonly string SpecularMultiplyAlbedo = Prop(ModSpecular, "_SpecularMultiplyAlbedo");
        public static readonly string SpecularMaskChannel = Prop(ModSpecular, "_SpecularMaskChannel");

        // ---- HairSpecular モジュール ----
        public static readonly string HairSpecularGradientIndex = Prop(ModHairSpecular, "_HairSpecularGradientIndex");
        public static readonly string HairSpecularMultiplyAlbedo = Prop(ModHairSpecular, "_HairSpecularMultiplyAlbedo");
        public static readonly string HairSpecularMaskChannel = Prop(ModHairSpecular, "_HairSpecularMaskChannel");

        // ---- MatCaps モジュール ----
        public static readonly string MatCapsEnable = Prop(ModMatCaps, "_Enable"); // [SCConstValue] → 要キーワード
        public static readonly string MatCapMultiplyColor = Prop(ModMatCaps, "_MatCapMultiplyColor");
        public static readonly string MatCapMultiply = Prop(ModMatCaps, "_MatCapMultiply");
        public static readonly string MatCapMultiplyDetail = Prop(ModMatCaps, "_MatCapMultiplyDetail");
        public static readonly string MatCapMultiplyMaskChannel = Prop(ModMatCaps, "_MatCapMultiplyMaskChannel");
        public static readonly string MatCapAddColor = Prop(ModMatCaps, "_MatCapAddColor");
        public static readonly string MatCapAdd = Prop(ModMatCaps, "_MatCapAdd");
        public static readonly string MatCapAddDetail = Prop(ModMatCaps, "_MatCapAddDetail");
        public static readonly string MatCapAddMaskChannel = Prop(ModMatCaps, "_MatCapAddMaskChannel");

        // ---- Details モジュール ----
        public static readonly string DetailsEnable = Prop(ModDetails, "_Enable"); // [SCConstValue] → 要キーワード
        public static readonly string DetailMask = Prop(ModDetails, "_DetailMask");
        public static string DetailBoost(int slot) => Prop(ModDetails, $"_Detail{slot}Boost");
        public static string DetailTexture(int slot) => Prop(ModDetails, $"_Detail{slot}Texture"); // ST あり
        public static string DetailNormalScale(int slot) => Prop(ModDetails, $"_Detail{slot}NormalScale");
        public static string DetailNormalMap(int slot) => Prop(ModDetails, $"_Detail{slot}NormalMap");
        public static string DetailUV(int slot) => Prop(ModDetails, $"_Detail{slot}UV");

        // ---- DistanceFade モジュール ----
        public static readonly string DistanceFade = Prop(ModDistanceFade, "_DistanceFade");
        public static readonly string DistanceFadeStrength = Prop(ModDistanceFade, "_DistanceFadeStrength");

        // ---- Lighten モジュール ----
        public static readonly string LightBoost = Prop(ModLighten, "_LightBoost");
        public static readonly string LightBoostMaskChannel = Prop(ModLighten, "_LightBoostMaskChannel");
        public static readonly string LightBoostAsEmission = Prop(ModLighten, "_LightBoostAsEmission");

        /// <summary>nontoon パッケージ同梱のディザテクスチャ。</summary>
        public const string DitherTexturePath = "Packages/jp.lilxyzw.nontoon/Textures/nt_bayer_4x4.png";
    }
}
