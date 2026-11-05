using System;
using System.IO;
using Bartender;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Bakes textures and materials, prepares the scene and settings, and builds bin/Bartender3D.exe.</summary>
public static class BuildScript
{
    public const string Version = "1.0.0";
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string TexDir = "Assets/Textures";
    const string MatDir = "Assets/Resources/Materials";
    const string ResTexDir = "Assets/Resources/Textures";

    const string Lit = "Universal Render Pipeline/Lit";
    const string UnlitSh = "Universal Render Pipeline/Unlit";
    const string ParticleSh = "Universal Render Pipeline/Particles/Unlit";

    // ================================================================== textures

    static void SavePng(string path, int w, int h, Color[] px, bool alpha)
    {
        var t = new Texture2D(w, h, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false, true);
        t.SetPixels(px);
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
    }

    static void Import(string path, bool normal, bool srgb, bool mips = true, TextureWrapMode wrap = TextureWrapMode.Repeat, int maxSize = 2048, bool alphaTransparent = false)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        imp.sRGBTexture = srgb && !normal;
        imp.mipmapEnabled = mips;
        imp.wrapMode = wrap;
        imp.anisoLevel = 8;
        imp.maxTextureSize = maxSize;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.alphaIsTransparency = alphaTransparent;
        imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.SaveAndReimport();
    }

    /// <summary>Generates name_albedo.png (+ name_normal.png, name_emission.png) once.</summary>
    static void Bake(string name, Func<GenTex> make)
    {
        string a = $"{TexDir}/{name}_albedo.png";
        if (File.Exists(a)) return;
        var t = make();
        SavePng(a, t.W, t.H, ToSrgb(t.Albedo), true);
        Import(a, false, true);
        if (t.Height != null)
        {
            string n = $"{TexDir}/{name}_normal.png";
            SavePng(n, t.W, t.H, TexGen.NormalFromHeight(t), false);
            Import(n, true, false);
        }
        if (t.Emission != null)
        {
            string e = $"{TexDir}/{name}_emission.png";
            SavePng(e, t.W, t.H, ToSrgb(t.Emission), false);
            Import(e, false, true);
        }
    }

    /// <summary>Colours are authored as sRGB; PNG stores them as-is (alpha stays linear).</summary>
    static Color[] ToSrgb(Color[] c)
    {
        var o = new Color[c.Length];
        for (int i = 0; i < c.Length; i++) o[i] = new Color(Mathf.Clamp01(c[i].r), Mathf.Clamp01(c[i].g), Mathf.Clamp01(c[i].b), Mathf.Clamp01(c[i].a));
        return o;
    }

    static void BakeAll()
    {
        Directory.CreateDirectory(TexDir);
        Directory.CreateDirectory(ResTexDir);
        Bake("marble", () => TexGen.Marble(2048));
        Bake("walnut", () => TexGen.Walnut(2048));
        Bake("deck", () => TexGen.Deck(2048));
        Bake("terrazzo", () => TexGen.Terrazzo(2048));
        Bake("concrete", () => TexGen.Concrete(1024));
        Bake("brushed", () => TexGen.Brushed(512));
        Bake("hammered", () => TexGen.Hammered(512));
        Bake("leather", () => TexGen.Leather(512));
        Bake("fabric", () => TexGen.Fabric(512));
        Bake("rubber", () => TexGen.RubberMat(512));
        Bake("onyx", () => TexGen.Onyx(1024));
        Bake("windows1", () => TexGen.Windows(1024, 3));
        Bake("windows2", () => TexGen.Windows(1024, 9));
        // ice: soft frosted bumps
        string ice = $"{TexDir}/ice_normal.png";
        if (!File.Exists(ice))
        {
            var g = new GenTex { W = 256, H = 256, Height = new float[256 * 256], NormalStrength = 3f };
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                    g.Height[y * 256 + x] = Noise.Fbm(x / 256f, y / 256f, 4, 4, 5) * 0.4f;
            SavePng(ice, 256, 256, TexGen.NormalFromHeight(g), false);
            Import(ice, true, false);
        }
        string sky = $"{TexDir}/sky.png";
        if (!File.Exists(sky))
        {
            SavePng(sky, 4096, 2048, ToSrgb(TexGen.Sky(4096, 2048)), false);
            Import(sky, false, true, false, TextureWrapMode.Repeat, 4096);
            var imp = (TextureImporter)AssetImporter.GetAtPath(sky);
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
        string moon = $"{ResTexDir}/moon.png";
        if (!File.Exists(moon))
        {
            SavePng(moon, 256, 256, TexGen.Moon(256), true);
            Import(moon, false, true, true, TextureWrapMode.Clamp, 256, true);
        }
    }

    static Texture2D T(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{file}.png");

    // ================================================================== materials

    static Material Mat(string name, string shader)
    {
        string path = $"{MatDir}/{name}.mat";
        var sh = Shader.Find(shader);
        if (sh == null) throw new Exception("Shader not found: " + shader);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = sh;
        m.shaderKeywords = new string[0];
        m.renderQueue = -1;
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return m;
    }

    static void Opaque(Material m)
    {
        m.SetFloat("_Surface", 0);
        m.SetFloat("_Blend", 0);
        m.SetFloat("_AlphaClip", 0);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.Zero);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
        m.SetFloat("_ZWrite", 1);
        m.SetFloat("_Cull", 2);
        m.SetOverrideTag("RenderType", "Opaque");
        m.SetShaderPassEnabled("DepthOnly", true);
        m.SetShaderPassEnabled("ShadowCaster", true);
        m.SetShaderPassEnabled("DepthNormals", true);
        if (m.HasProperty("_WorkflowMode")) m.SetFloat("_WorkflowMode", 1);
        if (m.HasProperty("_ReceiveShadows")) m.SetFloat("_ReceiveShadows", 1);
        if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 1);
        if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1);
        m.renderQueue = (int)RenderQueue.Geometry;
    }

    /// <summary>mode: 0 alpha, 1 premultiplied, 2 additive.</summary>
    static void Transparent(Material m, int mode)
    {
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", mode);
        m.SetFloat("_AlphaClip", 0);
        if (mode == 1)
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        else if (mode == 2)
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
        }
        else
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", mode == 2 ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.SetFloat("_Cull", 2);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetShaderPassEnabled("DepthOnly", false);
        m.SetShaderPassEnabled("ShadowCaster", false);
        m.SetShaderPassEnabled("DepthNormals", false);
        if (m.HasProperty("_WorkflowMode")) m.SetFloat("_WorkflowMode", 1);
        if (m.HasProperty("_ReceiveShadows")) m.SetFloat("_ReceiveShadows", 1);
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    static void Textured(Material m, string tex, float meters, Color tint, float smooth, float metal, float bump = 1f, bool smoothFromAlpha = true)
    {
        Opaque(m);
        var a = T(tex + "_albedo");
        var n = T(tex + "_normal");
        m.SetTexture("_BaseMap", a);
        m.SetTextureScale("_BaseMap", Vector2.one / meters);
        m.SetColor("_BaseColor", tint);
        if (n != null)
        {
            m.SetTexture("_BumpMap", n);
            m.SetTextureScale("_BumpMap", Vector2.one / meters);
            m.SetFloat("_BumpScale", bump);
        }
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        if (smoothFromAlpha)
        {
            m.SetFloat("_SmoothnessTextureChannel", 1);
            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        }
    }

    static void MakeMaterials()
    {
        Directory.CreateDirectory(MatDir);
        // templates (one per shader variant we use at runtime)
        var o = Mat("T_Opaque", Lit); Opaque(o); o.SetColor("_BaseColor", Color.gray); o.SetFloat("_Smoothness", 0.5f);
        var e = Mat("T_Emissive", Lit); Opaque(e); e.EnableKeyword("_EMISSION"); e.SetColor("_EmissionColor", Color.white); e.SetColor("_BaseColor", Color.black);
        var g = Mat("T_Glass", Lit); Transparent(g, 1); g.SetColor("_BaseColor", new Color(0.9f, 0.95f, 1f, 0.09f)); g.SetFloat("_Smoothness", 0.96f);
        var ice = Mat("T_Ice", Lit); Transparent(ice, 1); ice.SetColor("_BaseColor", new Color(0.9f, 0.95f, 1f, 0.42f)); ice.SetFloat("_Smoothness", 0.9f);
        ice.SetTexture("_BumpMap", T("ice_normal")); ice.SetTextureScale("_BumpMap", Vector2.one * 0.35f); ice.SetFloat("_BumpScale", 0.8f); ice.EnableKeyword("_NORMALMAP");
        var liq = Mat("T_Liquid", Lit); Transparent(liq, 0); liq.SetColor("_BaseColor", new Color(1, 1, 1, 0.5f)); liq.SetFloat("_Smoothness", 0.93f);
        var u = Mat("T_Unlit", UnlitSh); Opaque(u); u.SetColor("_BaseColor", Color.white);
        var ut = Mat("T_UnlitTrans", UnlitSh); Transparent(ut, 0); ut.SetColor("_BaseColor", Color.white);
        var ua = Mat("T_UnlitAdd", UnlitSh); Transparent(ua, 2); ua.SetColor("_BaseColor", Color.white);
        var pa = Mat("T_Particle", ParticleSh); Transparent(pa, 0); pa.SetColor("_BaseColor", Color.white);
        Mat("T_UIDraw", "UI/Default");

        // textured surfaces (tile size in metres)
        Textured(Mat("M_Marble", Lit), "marble", 1.3f, Color.white, 0.95f, 0f, 0.6f);
        Textured(Mat("M_Walnut", Lit), "walnut", 1.0f, Color.white, 0.85f, 0f, 0.8f);
        Textured(Mat("M_Deck", Lit), "deck", 2.0f, Color.white, 0.8f, 0f, 1f);
        Textured(Mat("M_Terrazzo", Lit), "terrazzo", 1.5f, Color.white, 0.95f, 0f, 0.5f);
        Textured(Mat("M_Concrete", Lit), "concrete", 2.0f, Color.white, 0.8f, 0f, 0.7f);
        Textured(Mat("M_Steel", Lit), "brushed", 0.5f, new Color(0.82f, 0.83f, 0.85f), 0.95f, 1f, 0.35f);
        Textured(Mat("M_Brass", Lit), "brushed", 0.4f, new Color(1f, 0.76f, 0.42f), 1f, 1f, 0.3f);
        Textured(Mat("M_Copper", Lit), "hammered", 0.12f, new Color(0.98f, 0.55f, 0.36f), 1f, 1f, 0.7f);
        Textured(Mat("M_Leather", Lit), "leather", 0.5f, Color.white, 1f, 0f, 0.8f);
        Textured(Mat("M_Fabric", Lit), "fabric", 0.35f, Color.white, 1f, 0f, 0.9f);
        Textured(Mat("M_RubberMat", Lit), "rubber", 0.5f, Color.white, 1f, 0f, 1f);
        var onyx = Mat("M_Onyx", Lit); Opaque(onyx); onyx.EnableKeyword("_EMISSION");
        onyx.SetTexture("_BaseMap", T("onyx_albedo")); onyx.SetColor("_BaseColor", Color.white); onyx.SetFloat("_Smoothness", 0.85f);
        onyx.SetTexture("_EmissionMap", T("onyx_emission")); onyx.SetColor("_EmissionColor", new Color(0.95f, 0.72f, 0.52f));
        for (int i = 1; i <= 2; i++)
        {
            var f = Mat("M_Facade" + i, Lit); Opaque(f); f.EnableKeyword("_EMISSION");
            f.SetTexture("_BaseMap", T("windows" + i + "_albedo")); f.SetColor("_BaseColor", new Color(0.55f, 0.6f, 0.7f)); f.SetFloat("_Smoothness", 0.6f);
            f.SetTexture("_EmissionMap", T("windows" + i + "_emission")); f.SetColor("_EmissionColor", new Color(1.1f, 1.05f, 0.98f));
        }
        var sky = Mat("Sky", "Skybox/Panoramic");
        sky.SetTexture("_MainTex", T("sky"));
        sky.SetFloat("_Mapping", 1);
        sky.SetFloat("_ImageType", 0);
        sky.SetFloat("_Exposure", 1f);
        sky.SetFloat("_Rotation", 0);
        sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
            EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)));
        AssetDatabase.SaveAssets();
    }

    // ================================================================== pipeline, scene, player

    static void Pipeline()
    {
        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        if (rp == null) { Debug.LogWarning("PC_RPAsset not found"); return; }
        var so = new SerializedObject(rp);
        void Set(string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; }
        void SetF(string prop, float v) { var p = so.FindProperty(prop); if (p != null) p.floatValue = v; }
        void SetB(string prop, bool v) { var p = so.FindProperty(prop); if (p != null) p.boolValue = v; }
        Set("m_GPUResidentDrawerMode", 0);
        Set("m_MSAA", 4);
        SetB("m_SupportsHDR", true);
        SetF("m_ShadowDistance", 50f);
        Set("m_MainLightShadowmapResolution", 2048);
        Set("m_AdditionalLightsShadowmapResolution", 2048);
        SetB("m_AdditionalLightShadowsSupported", true);
        Set("m_AdditionalLightsPerObjectLimit", 8);
        SetB("m_SoftShadowsSupported", true);
        so.ApplyModifiedPropertiesWithoutUndo();
        GraphicsSettings.defaultRenderPipeline = rp;
        // stronger, finer ambient occlusion: contact shadows under bottles, glasses and in corners
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/PC_Renderer.asset"))
        {
            if (obj == null || obj.name != "ScreenSpaceAmbientOcclusion") continue;
            var fso = new SerializedObject(obj);
            void F(string prop, float v) { var q = fso.FindProperty(prop); if (q != null) q.floatValue = v; }
            void I(string prop, int v) { var q = fso.FindProperty(prop); if (q != null) q.intValue = v; }
            F("m_Settings.Intensity", 0.85f);
            F("m_Settings.Radius", 0.22f);
            F("m_Settings.DirectLightingStrength", 0.45f);
            F("m_Settings.Falloff", 60f);
            I("m_Settings.Samples", 0);
            fso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(obj);
        }
        // more room in the shadow atlas for the lamp shadows
        Set("m_AdditionalLightsShadowmapResolution", 4096);
        so.ApplyModifiedPropertiesWithoutUndo();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = rp;
        }
        QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, false);
        EditorUtility.SetDirty(rp);
    }

    static void MakeScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Sky.mat");
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0019f;
        RenderSettings.fogColor = new Color(0.075f, 0.065f, 0.1f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        Lightmapping.bakedGI = false;
        Lightmapping.realtimeGI = false;
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void MakeIcon()
    {
        const string path = "Assets/Icon/icon.png";
        if (!File.Exists(path))
        {
            Directory.CreateDirectory("Assets/Icon");
            const int N = 256;
            var px = new Color[N * N];
            var c0 = new Color(0.1f, 0.06f, 0.14f);
            var c1 = new Color(0.32f, 0.12f, 0.22f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, 44, N - 44), cy = Mathf.Clamp(y + 0.5f, 44, N - 44);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(44.5f - d);
                    var c = Color.Lerp(c0, c1, (float)y / N);
                    // martini glass: cone, stem, foot, olive
                    float u = (x - 128f) / 128f, v = (y - 128f) / 128f;
                    bool cone = v > 0.05f && v < 0.62f && Mathf.Abs(u) < (v - 0.05f) * 1.05f && Mathf.Abs(u) > (v - 0.05f) * 1.05f - 0.06f;
                    bool rim = v > 0.58f && v < 0.64f && Mathf.Abs(u) < 0.6f;
                    bool liquid = v > 0.12f && v < 0.5f && Mathf.Abs(u) < (v - 0.05f) * 1.05f - 0.05f;
                    bool stem = v > -0.48f && v <= 0.08f && Mathf.Abs(u) < 0.035f;
                    bool foot = v > -0.56f && v < -0.48f && Mathf.Abs(u) < 0.34f - (v + 0.48f) * -1f;
                    bool olive = Vector2.Distance(new Vector2(u, v), new Vector2(0.14f, 0.33f)) < 0.09f;
                    bool pick = Mathf.Abs((u - 0.14f) * 0.8f - (v - 0.33f) * -0.6f) < 0.018f && v > 0.25f && v < 0.8f && u > -0.1f && u < 0.4f;
                    if (liquid) c = new Color(0.95f, 0.42f, 0.55f);
                    if (olive) c = new Color(0.45f, 0.6f, 0.15f);
                    if (cone || rim || stem || foot) c = new Color(0.97f, 0.8f, 0.5f);
                    if (pick) c = new Color(0.9f, 0.9f, 0.9f);
                    c.a = a;
                    px[y * N + x] = c;
                }
            SavePng(path, N, N, px, true);
            Import(path, false, true, false, TextureWrapMode.Clamp, 256, true);
        }
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
    }

    static void PlayerSetup()
    {
        PlayerSettings.companyName = "TheProjects";
        PlayerSettings.productName = "Bartender 3D";
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.usePlayerLog = true;
        PlayerSettings.visibleInBackground = true;
        try { PlayerSettings.SplashScreen.show = false; } catch { }
        try { PlayerSettings.SplashScreen.showUnityLogo = false; } catch { }
    }

    [MenuItem("Bartender 3D/Prepare project")]
    public static void Setup()
    {
        BakeAll();
        AssetDatabase.Refresh();
        MakeMaterials();
        Pipeline();
        MakeScene();
        MakeIcon();
        PlayerSetup();
        AssetDatabase.SaveAssets();
        Debug.Log("[Setup] done");
    }

    [MenuItem("Bartender 3D/Rebuild textures")]
    public static void RebuildTextures()
    {
        if (Directory.Exists(TexDir)) foreach (var f in Directory.GetFiles(TexDir, "*.png")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
        Setup();
    }

    [MenuItem("Bartender 3D/Build game (Windows)")]
    public static void Build()
    {
        int code = 1;
        try
        {
            Setup();
            string output = Path.GetFullPath("bin/Bartender3D.exe");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[Build] {s.result}: {output} ({s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime})");
            code = s.result == BuildResult.Succeeded ? 0 : 1;
        }
        catch (Exception ex)
        {
            Debug.LogError("[Build] " + ex);
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }
}
