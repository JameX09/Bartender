using System.Collections.Generic;
using UnityEngine;

namespace Bartender
{
    /// <summary>
    /// Materials. Every runtime material is a copy of a template in Resources/Materials so the
    /// build keeps exactly the shader variants we use (templates are created by BuildScript).
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> templates = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Smooth = Shader.PropertyToID("_Smoothness");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        static readonly int BumpScale = Shader.PropertyToID("_BumpScale");

        public static Material Template(string name)
        {
            if (templates.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                Debug.LogWarning("Missing material template " + name + " (run Bartender 3D > Prepare project)");
                var sh = Shader.Find(name.StartsWith("T_Unlit") ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                m = new Material(sh);
            }
            templates[name] = m;
            return m;
        }

        static string Key(string kind, Color c, float a, float b) =>
            kind + ((Color32)c).GetHashCode() + "_" + c.a.ToString("0.000") + "_" + a.ToString("0.00") + "_" + b.ToString("0.00");

        /// <summary>Opaque PBR colour.</summary>
        public static Material Lit(Color c, float smooth = 0.5f, float metal = 0f)
        {
            string k = Key("lit", c, smooth, metal);
            if (cache.TryGetValue(k, out var m)) return m;
            m = new Material(Template("T_Opaque")) { name = "lit" };
            m.SetColor(BaseColor, c);
            m.SetFloat(Smooth, smooth);
            m.SetFloat(MetallicId, metal);
            return cache[k] = m;
        }

        public static Material Metal(Color c, float smooth = 0.85f) => Lit(c, smooth, 1f);

        public static Material Chrome => Metal(new Color(0.85f, 0.86f, 0.88f), 0.93f);
        public static Material Steel => Tex("M_Steel");
        public static Material Brass => Tex("M_Brass");
        public static Material Gold => Metal(new Color(1f, 0.78f, 0.42f), 0.9f);
        public static Material BlackMetal => Metal(new Color(0.06f, 0.06f, 0.065f), 0.7f);
        public static Material Rubber => Lit(new Color(0.04f, 0.04f, 0.045f), 0.25f);

        /// <summary>Self-lit surface (lamps, neon, LED). Emission is HDR so it blooms.</summary>
        public static Material Emissive(Color baseC, Color emission, float intensity)
        {
            string k = Key("emi", baseC, intensity, emission.r * 7 + emission.g * 3 + emission.b);
            if (cache.TryGetValue(k, out var m)) return m;
            m = new Material(Template("T_Emissive")) { name = "emissive" };
            m.SetColor(BaseColor, baseC);
            m.SetColor(Emission, emission * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return cache[k] = m;
        }

        public static Material EmissiveMap(Texture tex, Color tint, float intensity, Vector2 tiling)
        {
            var m = new Material(Template("T_Emissive")) { name = "emissiveMap" };
            m.SetColor(BaseColor, Color.black);
            m.SetTexture(EmissionMap, tex);
            m.SetTextureScale(EmissionMap, tiling);
            m.SetTextureScale(BaseMap, tiling);
            m.SetColor(Emission, tint * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        /// <summary>Clear or tinted glass (premultiplied, keeps full reflections).</summary>
        public static Material Glass(Color tint, float smooth = 0.96f)
        {
            string k = Key("glass", tint, smooth, 0);
            if (cache.TryGetValue(k, out var m)) return m;
            m = new Material(Template("T_Glass")) { name = "glass" };
            m.SetColor(BaseColor, tint);
            m.SetFloat(Smooth, smooth);
            m.renderQueue = 3010;
            return cache[k] = m;
        }

        public static Material ClearGlass => Glass(new Color(0.9f, 0.95f, 1f, 0.09f));

        /// <summary>Ice: frosted glass with a bumpy normal map.</summary>
        public static Material Ice
        {
            get
            {
                if (cache.TryGetValue("ice", out var m)) return m;
                m = new Material(Template("T_Ice")) { name = "ice" };
                m.renderQueue = 2980;
                return cache["ice"] = m;
            }
        }

        /// <summary>A new liquid material (each container owns one and recolours it).</summary>
        public static Material Liquid(Color c)
        {
            var m = new Material(Template("T_Liquid")) { name = "liquid" };
            m.SetColor(BaseColor, c);
            m.renderQueue = 2990;
            return m;
        }

        public static void SetColor(Material m, Color c) => m.SetColor(BaseColor, c);
        public static void SetEmission(Material m, Color c) => m.SetColor(Emission, c);

        public static Material Unlit(Color c)
        {
            string k = Key("unlit", c, 0, 0);
            if (cache.TryGetValue(k, out var m)) return m;
            m = new Material(Template("T_Unlit")) { name = "unlit" };
            m.SetColor(BaseColor, c);
            return cache[k] = m;
        }

        public static Material UnlitTex(Texture tex, Color tint, bool transparent)
        {
            var m = new Material(Template(transparent ? "T_UnlitTrans" : "T_Unlit")) { name = "unlitTex" };
            m.SetTexture(BaseMap, tex);
            m.SetColor(BaseColor, tint);
            return m;
        }

        public static Material Additive(Color c, Texture tex = null)
        {
            var m = new Material(Template("T_UnlitAdd")) { name = "additive" };
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
            return m;
        }

        public static Material Particle(Color c, Texture tex)
        {
            var m = new Material(Template("T_Particle")) { name = "particle" };
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
            return m;
        }

        /// <summary>A textured material asset (marble, wood ...).</summary>
        public static Material Tex(string name)
        {
            if (cache.TryGetValue("tex_" + name, out var m)) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null) { Debug.LogWarning("Missing material " + name); m = Lit(Color.gray); }
            return cache["tex_" + name] = m;
        }

        /// <summary>Copy of a textured material with a tint, smoothness and tiling (in metres per texture).</summary>
        public static Material TexTint(string name, Color tint, float smooth = -1f, float tileMeters = 0f, float metal = -1f)
        {
            string k = "tt_" + name + Key("", tint, smooth, tileMeters + metal * 10f);
            if (cache.TryGetValue(k, out var m)) return m;
            m = new Material(Tex(name)) { name = name + "_tint" };
            m.SetColor(BaseColor, tint);
            if (smooth >= 0) m.SetFloat(Smooth, smooth);
            if (metal >= 0) m.SetFloat(MetallicId, metal);
            if (tileMeters > 0)
            {
                m.SetTextureScale(BaseMap, Vector2.one / tileMeters);
                m.SetTextureScale("_BumpMap", Vector2.one / tileMeters);
            }
            return cache[k] = m;
        }

        /// <summary>Opaque material using a runtime texture (bottle labels).</summary>
        public static Material Label(Texture tex, float smooth = 0.35f)
        {
            var m = new Material(Template("T_Opaque")) { name = "label" };
            m.SetTexture(BaseMap, tex);
            m.SetColor(BaseColor, Color.white);
            m.SetFloat(Smooth, smooth);
            m.SetFloat(MetallicId, 0f);
            return m;
        }

        static Texture2D soft;
        /// <summary>Soft round dot, used for particles and glows.</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (soft != null) return soft;
                const int n = 64;
                soft = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "softdot" };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1 - d);
                        a = a * a * (3 - 2 * a);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                soft.SetPixels32(px);
                soft.Apply(true);
                return soft;
            }
        }
    }
}
