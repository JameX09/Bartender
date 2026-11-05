using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bartender
{
    /// <summary>Builds the bar gear: glasses, bottles, shaker, strainer, jigger, spoon, garnish ...</summary>
    public static class Props
    {
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, LiquidSolid> solidCache = new Dictionary<string, LiquidSolid>();
        static readonly Dictionary<Ingredient, Texture2D> labelCache = new Dictionary<Ingredient, Texture2D>();

        // ------------------------------------------------------------------ helpers

        public static GameObject Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos = default, Quaternion? rot = null, Vector3? scale = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public static GameObject PartMulti(string name, Transform parent, Mesh mesh, Material[] mats, Vector3 pos = default, Quaternion? rot = null)
        {
            var go = Part(name, parent, mesh, mats[0], pos, rot);
            go.GetComponent<MeshRenderer>().sharedMaterials = mats;
            return go;
        }

        static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (meshCache.TryGetValue(key, out var m) && m != null) return m;
            m = make();
            m.name = key;
            return meshCache[key] = m;
        }

        static BoxCollider Box(GameObject go, Vector3 center, Vector3 size)
        {
            var b = go.AddComponent<BoxCollider>();
            b.center = center;
            b.size = size;
            return b;
        }

        /// <summary>Offsets a polyline (r, y) sideways by t along its 2D normal (right-hand side when walking upward).</summary>
        static List<Vector2> Offset(List<Vector2> pts, float t)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 d = i == 0 ? pts[1] - pts[0] : i == pts.Count - 1 ? pts[i] - pts[i - 1] : pts[i + 1] - pts[i - 1];
                d.Normalize();
                var n = new Vector2(d.y, -d.x);
                var p = pts[i] + n * t;
                if (i == 0) p.x = 0;
                o.Add(p);
            }
            return o;
        }

        // ------------------------------------------------------------------ glass shapes

        /// <summary>Inner bowl/wall curve from the bottom centre to the rim for each vessel type.</summary>
        static List<Vector2> InnerCurve(string type)
        {
            var l = new List<Vector2>();
            switch (type)
            {
                case "highball": Tumbler(l, 0.0285f, 0.0275f, 0.012f, 0.148f); break;
                case "rocks": Tumbler(l, 0.0395f, 0.036f, 0.019f, 0.092f); break;
                case "mug": Tumbler(l, 0.0395f, 0.0395f, 0.004f, 0.098f); break;
                case "tin": Tumbler(l, 0.0452f, 0.041f, 0.0015f, 0.175f); break;
                case "captin": Tumbler(l, 0.0405f, 0.032f, 0.0015f, 0.14f); break;
                case "mixing": Tumbler(l, 0.0425f, 0.04f, 0.026f, 0.142f); break;
                case "cup": Tumbler(l, 0.0295f, 0.0185f, 0.007f, 0.057f); break;
                case "jigger":
                    l.Add(new Vector2(0, 0.049f)); l.Add(new Vector2(0.0098f, 0.049f)); l.Add(new Vector2(0.0106f, 0.0505f));
                    l.Add(new Vector2(0.0225f, 0.099f));
                    break;
                case "coupe":
                    for (int i = 0; i <= 14; i++)
                    {
                        float a = Mathf.PI * 0.5f * i / 14f;
                        l.Add(new Vector2(0.05f * Mathf.Sin(a), 0.122f + 0.043f * (1f - Mathf.Cos(a))));
                    }
                    l.Add(new Vector2(0.0505f, 0.168f));
                    break;
                case "martini":
                    l.Add(new Vector2(0, 0.112f)); l.Add(new Vector2(0.003f, 0.1125f));
                    for (int i = 1; i <= 6; i++) l.Add(new Vector2(0.056f * i / 6f, 0.112f + 0.074f * i / 6f));
                    break;
                case "wine":
                    for (int i = 0; i <= 16; i++)
                    {
                        float s = i / 16f;
                        float r = s <= 0.45f ? 0.045f * Mathf.Sqrt(Mathf.Max(0, 1f - Mathf.Pow(1f - s / 0.45f, 2f))) : Mathf.Lerp(0.045f, 0.037f, Mathf.Pow((s - 0.45f) / 0.55f, 1.6f));
                        l.Add(new Vector2(r, 0.092f + 0.112f * s));
                    }
                    break;
            }
            return l;
        }

        static void Tumbler(List<Vector2> l, float rTop, float rBottom, float baseY, float topY)
        {
            l.Add(new Vector2(0, baseY));
            l.Add(new Vector2(rBottom - 0.004f, baseY));
            l.Add(new Vector2(rBottom - 0.0012f, baseY + 0.0008f));
            l.Add(new Vector2(rBottom, baseY + 0.004f));
            l.Add(new Vector2(Mathf.Lerp(rBottom, rTop, 0.5f), Mathf.Lerp(baseY, topY, 0.5f)));
            l.Add(new Vector2(rTop, topY));
        }

        /// <summary>Full outer+inner profile for a tumbler-like vessel with a rounded rim.</summary>
        static List<LP> TumblerProfile(List<Vector2> inner, float wall, float outerBottomR)
        {
            var p = new List<LP>();
            var top = inner[inner.Count - 1];
            float bevel = 0.003f;
            p.Add(new LP(0, 0));
            p.Add(new LP(outerBottomR - bevel, 0));
            p.Add(new LP(outerBottomR - bevel * 0.3f, bevel * 0.3f));
            p.Add(new LP(outerBottomR, bevel));
            p.Add(new LP(Mathf.Lerp(outerBottomR, top.x + wall, 0.5f), top.y * 0.5f));
            p.Add(new LP(top.x + wall, top.y - wall * 0.5f));
            // rounded rim
            for (int i = 1; i < 6; i++)
            {
                float a = Mathf.PI * i / 6f;
                p.Add(new LP(top.x + wall * 0.5f + Mathf.Cos(a) * wall * 0.5f, top.y - wall * 0.5f + Mathf.Sin(a) * wall * 0.6f));
            }
            p.Add(new LP(top.x, top.y - wall * 0.5f));
            for (int i = inner.Count - 2; i >= 0; i--) p.Add(new LP(inner[i].x, inner[i].y));
            return p;
        }

        /// <summary>Stemmed glass profile: foot, stem, bowl (outer offset from inner), rim, inner bowl.</summary>
        static List<LP> StemProfile(List<Vector2> inner, float wall, float foot, float stemR)
        {
            var p = new List<LP>();
            float stemTop = inner[0].y - wall * 1.2f;
            p.Add(new LP(0, 0));
            p.Add(new LP(foot - 0.003f, 0));
            p.Add(new LP(foot, 0.0012f, false));
            p.Add(new LP(foot - 0.002f, 0.0028f));
            p.Add(new LP(foot * 0.55f, 0.0045f));
            p.Add(new LP(stemR * 2.4f, 0.009f));
            p.Add(new LP(stemR * 1.2f, 0.018f));
            p.Add(new LP(stemR, 0.03f));
            p.Add(new LP(stemR, stemTop - 0.012f));
            p.Add(new LP(stemR * 1.3f, stemTop - 0.004f));
            var outer = Offset(inner, wall);
            bool started = false;
            for (int i = 0; i < outer.Count; i++)
            {
                if (!started && outer[i].x < stemR * 1.8f) continue;
                started = true;
                p.Add(new LP(outer[i].x, outer[i].y));
            }
            var top = inner[inner.Count - 1];
            var otop = outer[outer.Count - 1];
            for (int i = 1; i < 5; i++)
            {
                float a = Mathf.PI * i / 5f;
                var c = (top + otop) * 0.5f;
                float rr = Vector2.Distance(top, otop) * 0.5f;
                p.Add(new LP(c.x + Mathf.Cos(a) * rr, c.y + Mathf.Sin(a) * rr * 1.2f));
            }
            for (int i = inner.Count - 1; i >= 0; i--) p.Add(new LP(inner[i].x, inner[i].y));
            return p;
        }

        static LiquidSolid Solid(string type)
        {
            if (solidCache.TryGetValue(type, out var s)) return s;
            var inner = InnerCurve(type);
            var shrunk = new List<Vector2>();
            foreach (var v in inner) shrunk.Add(new Vector2(Mathf.Max(0, v.x - 0.0007f), v.y + 0.0006f));
            s = new LiquidSolid(shrunk, 36);
            return solidCache[type] = s;
        }

        // ------------------------------------------------------------------ vessels

        static Vessel MakeVessel(string name, VesselKind kind, string type, Mesh mesh, Material mat, float height, float radius, int maxIce, float iceSize)
        {
            var go = new GameObject(name);
            var v = go.AddComponent<Vessel>();
            v.VKind = kind;
            v.Height = height;
            v.Radius = radius;
            v.MaxIce = maxIce;
            v.IceSize = iceSize;
            Part("body", go.transform, mesh, mat);
            var solid = Solid(type);
            v.InitVisuals(solid);
            Box(go, new Vector3(0, height * 0.5f, 0), new Vector3(radius * 2f, height, radius * 2f));
            var m = new GameObject("mouth");
            m.transform.SetParent(go.transform, false);
            m.transform.localPosition = new Vector3(0, solid.RimY + 0.012f, 0);
            var mc = m.AddComponent<BoxCollider>();
            mc.isTrigger = true;
            mc.size = new Vector3(solid.RimR * 1.9f, 0.028f, solid.RimR * 1.9f);
            m.AddComponent<Mouth>().Owner = v;
            v.Mouth = m.transform;
            return v;
        }

        public static Vessel Glass(GlassType t)
        {
            Vessel v;
            switch (t)
            {
                case GlassType.Highball:
                    v = MakeVessel("Highball", VesselKind.Glass, "highball",
                        Cached("g_highball", () => MeshGen.Lathe(TumblerProfile(InnerCurve("highball"), 0.0028f, 0.0305f), 48)),
                        Mats.ClearGlass, 0.15f, 0.032f, 7, 0.026f);
                    break;
                case GlassType.Rocks:
                    v = MakeVessel("Rocks", VesselKind.Glass, "rocks",
                        Cached("g_rocks", () => MeshGen.Lathe(TumblerProfile(InnerCurve("rocks"), 0.0035f, 0.0395f), 56)),
                        Mats.ClearGlass, 0.094f, 0.043f, 5, 0.03f);
                    break;
                case GlassType.Coupe:
                    v = MakeVessel("Coupe", VesselKind.Glass, "coupe",
                        Cached("g_coupe", () => MeshGen.Lathe(StemProfile(InnerCurve("coupe"), 0.0022f, 0.036f, 0.0042f), 56)),
                        Mats.ClearGlass, 0.17f, 0.052f, 2, 0.024f);
                    break;
                case GlassType.Martini:
                    v = MakeVessel("Martini", VesselKind.Glass, "martini",
                        Cached("g_martini", () => MeshGen.Lathe(StemProfile(InnerCurve("martini"), 0.0022f, 0.038f, 0.004f), 56)),
                        Mats.ClearGlass, 0.188f, 0.057f, 2, 0.024f);
                    break;
                case GlassType.Wine:
                    v = MakeVessel("Wine", VesselKind.Glass, "wine",
                        Cached("g_wine", () => MeshGen.Lathe(StemProfile(InnerCurve("wine"), 0.0022f, 0.038f, 0.0042f), 56)),
                        Mats.ClearGlass, 0.206f, 0.046f, 6, 0.026f);
                    break;
                default:
                    {
                        v = MakeVessel("Copper mug", VesselKind.Glass, "mug",
                            Cached("g_mug", () => MeshGen.Lathe(TumblerProfile(InnerCurve("mug"), 0.0016f, 0.0405f), 56, 0.12f)),
                            Mats.TexTint("M_Copper", Color.white), 0.1f, 0.042f, 6, 0.028f);
                        var handle = Cached("g_mughandle", () =>
                        {
                            var path = new List<Vector3>();
                            for (int i = 0; i <= 16; i++)
                            {
                                float a = Mathf.PI * (i / 16f) - Mathf.PI / 2f;
                                path.Add(new Vector3(0.041f + Mathf.Cos(a) * 0.026f, 0.05f + Mathf.Sin(a) * 0.033f, 0));
                            }
                            return MeshGen.Tube(path, 0.0055f, 12, true, 0.55f);
                        });
                        Part("handle", v.transform, handle, Mats.TexTint("M_Copper", Color.white), Vector3.zero, Quaternion.Euler(0, 180, 0));
                        break;
                    }
            }
            v.GType = t;
            return v;
        }

        public static Vessel ShakerTin()
        {
            var v = MakeVessel("Shaker tin", VesselKind.Tin, "tin",
                Cached("tin", () => MeshGen.Lathe(TumblerProfile(InnerCurve("tin"), 0.0012f, 0.042f), 56, 0.1f)),
                Mats.Steel, 0.176f, 0.047f, 10, 0.026f);
            return v;
        }

        public static ShakerCap ShakerCap()
        {
            var go = new GameObject("Shaker cap");
            var c = go.AddComponent<ShakerCap>();
            c.Height = 0.141f; c.Radius = 0.042f;
            var mesh = Cached("captin", () => MeshGen.Lathe(TumblerProfile(InnerCurve("captin"), 0.0012f, 0.0335f), 48, 0.1f));
            Part("body", go.transform, mesh, Mats.Steel);
            Box(go, new Vector3(0, 0.07f, 0), new Vector3(0.084f, 0.14f, 0.084f));
            return c;
        }

        public static Vessel MixingGlass()
        {
            var v = MakeVessel("Mixing glass", VesselKind.MixingGlass, "mixing",
                Cached("mixing", () => MeshGen.Lathe(TumblerProfile(InnerCurve("mixing"), 0.0042f, 0.044f), 64)),
                Mats.Glass(new Color(0.92f, 0.96f, 1f, 0.12f)), 0.143f, 0.047f, 9, 0.027f);
            // faceted look: thin rings near the base
            var ring = Cached("mixring", () => MeshGen.Torus(0.0445f, 0.0016f, 64, 8));
            for (int i = 0; i < 3; i++) Part("ring", v.transform, ring, Mats.Glass(new Color(0.92f, 0.96f, 1f, 0.12f)), new Vector3(0, 0.012f + i * 0.006f, 0), null, null, false);
            return v;
        }

        /// <summary>
        /// Double-sided jigger: big cup up by default, press R to turn it over to the small cup.
        /// rb/hb = big cup inner rim radius and depth, rs/hs = small cup.
        /// </summary>
        public static Vessel Jigger(float bigMl, float smallMl, float rb, float hb, float rs, float hs, Material mat)
        {
            const float wi = 0.0105f, wall = 0.0011f, plate = 0.004f;
            float H = hs + plate + hb;
            string key = $"jig{bigMl}_{smallMl}";
            var mesh = Cached(key, () =>
            {
                var p = new List<LP>
                {
                    new LP(0, hs), new LP(wi - 0.0015f, hs), new LP(wi, hs - 0.0015f, true),
                    new LP(rs, 0.0012f), new LP(rs + 0.0003f, 0.0002f), new LP(rs + wall * 0.6f, -0.0002f), new LP(rs + wall, 0.0006f, true),
                    new LP(Mathf.Lerp(rs, wi, 0.5f) + wall * 1.3f, hs * 0.55f), new LP(wi + wall * 1.4f, hs + plate * 0.5f),
                    new LP(Mathf.Lerp(wi, rb, 0.45f) + wall * 1.3f, hs + plate + hb * 0.45f), new LP(rb + wall, H - 0.0006f, true),
                    new LP(rb + wall * 0.6f, H + 0.0002f), new LP(rb + 0.0003f, H - 0.0002f), new LP(rb, H - 0.0012f, true),
                    new LP(wi, hs + plate + 0.0015f, true), new LP(wi - 0.0015f, hs + plate), new LP(0, hs + plate),
                };
                return MeshGen.Lathe(p, 56, 0.05f);
            });
            var go = new GameObject("Jigger");
            var v = go.AddComponent<Vessel>();
            v.VKind = VesselKind.Jigger;
            v.Height = H;
            v.Radius = rb + 0.002f;
            v.MaxIce = 0;
            var body = Part("body", go.transform, mesh, mat);
            // measuring lines halfway down each cup
            var lineMat = Mats.Metal(new Color(0.35f, 0.3f, 0.25f), 0.5f);
            float yb = hs + plate + hb * 0.62f, rbl = Mathf.Lerp(wi, rb, 0.62f) + 0.0002f;
            Part("line", body.transform, Cached(key + "lb", () => MeshGen.Torus(rbl, 0.00032f, 48, 4)), lineMat, new Vector3(0, yb, 0), null, null, false);
            v.Body = body.transform;
            // liquid solids for each side (in the vessel frame)
            var a = new List<Vector2> { new Vector2(0, hs + plate + 0.0006f), new Vector2(wi - 0.0007f, hs + plate + 0.0006f), new Vector2(rb - 0.0007f, H) };
            var b = new List<Vector2> { new Vector2(0, H - hs + 0.0006f), new Vector2(wi - 0.0007f, H - hs + 0.0006f), new Vector2(rs - 0.0007f, H) };
            v.SolidA = solidCache.TryGetValue(key + "A", out var sa) ? sa : solidCache[key + "A"] = new LiquidSolid(a, 32);
            v.SolidB = solidCache.TryGetValue(key + "B", out var sb) ? sb : solidCache[key + "B"] = new LiquidSolid(b, 32);
            v.NominalA = bigMl; v.NominalB = smallMl; v.JiggerHeight = H;
            v.WaistA = hs + plate * 0.5f; v.WaistB = H - hs - plate * 0.5f;
            v.InitVisuals(v.SolidA);
            Box(go, new Vector3(0, H * 0.5f, 0), new Vector3((rb + 0.002f) * 2f, H, (rb + 0.002f) * 2f));
            var m = new GameObject("mouth");
            m.transform.SetParent(go.transform, false);
            var mc = m.AddComponent<BoxCollider>();
            mc.isTrigger = true;
            m.AddComponent<Mouth>().Owner = v;
            v.Mouth = m.transform;
            v.SetSide(false);
            return v;
        }

        /// <summary>Small porcelain cup for espresso shots.</summary>
        public static Vessel EspressoCup()
        {
            var mesh = Cached("cup", () => TumblerMesh());
            var v = MakeVessel("Espresso cup", VesselKind.Cup, "cup", mesh, Mats.Lit(new Color(0.95f, 0.94f, 0.9f), 0.88f), 0.058f, 0.033f, 0, 0.02f);
            var handle = Cached("cuphandle", () =>
            {
                var path = new List<Vector3>();
                for (int i = 0; i <= 12; i++)
                {
                    float an = Mathf.PI * (i / 12f) - Mathf.PI / 2f;
                    path.Add(new Vector3(0.031f + Mathf.Cos(an) * 0.013f, 0.032f + Mathf.Sin(an) * 0.014f, 0));
                }
                return MeshGen.Tube(path, 0.0035f, 8, true, 0.6f);
            });
            Part("handle", v.transform, handle, Mats.Lit(new Color(0.95f, 0.94f, 0.9f), 0.88f));
            return v;
        }

        static Mesh TumblerMesh() => MeshGen.Lathe(TumblerProfile(InnerCurve("cup"), 0.0035f, 0.021f), 40);

        // ------------------------------------------------------------------ tools

        public static StrainerItem Strainer()
        {
            var go = new GameObject("Strainer");
            var s = go.AddComponent<StrainerItem>();
            s.Height = 0.012f; s.Radius = 0.05f;
            var plate = Cached("strplate", () => MeshGen.Lathe(new List<LP>
            {
                new LP(0, 0.006f), new LP(0.046f, 0.006f), new LP(0.049f, 0.0068f), new LP(0.0505f, 0.0078f), new LP(0.049f, 0.0088f),
                new LP(0.046f, 0.0078f), new LP(0, 0.0078f),
            }, 56, 0.1f));
            Part("plate", go.transform, plate, Mats.Steel);
            // perforation
            var hole = Cached("strhole", () => MeshGen.Disc(0.0022f, 0, 10));
            var dark = Mats.Lit(new Color(0.02f, 0.02f, 0.02f), 0.2f);
            int n = 0;
            for (int ring = 1; ring <= 3; ring++)
            {
                int count = ring * 8;
                for (int i = 0; i < count; i++)
                {
                    float a = Mathf.PI * 2 * i / count + ring;
                    Part("hole", go.transform, hole, dark, new Vector3(Mathf.Cos(a) * ring * 0.011f, 0.0081f, Mathf.Sin(a) * ring * 0.011f), null, null, false);
                    n++;
                }
            }
            // the iconic spring underneath
            var spring = Cached("strspring", () =>
            {
                var path = new List<Vector3>();
                const int turns = 34, steps = 34 * 12;
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float a = t * Mathf.PI * 2f;
                    float c = t * turns * Mathf.PI * 2f;
                    var center = new Vector3(Mathf.Cos(a) * 0.036f, 0, Mathf.Sin(a) * 0.036f);
                    var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    path.Add(center + radial * Mathf.Cos(c) * 0.0055f + Vector3.up * Mathf.Sin(c) * 0.0055f);
                }
                return MeshGen.Tube(path, 0.0007f, 5, false);
            });
            Part("spring", go.transform, spring, Mats.Chrome, new Vector3(0, 0.0005f, 0), null, null, false);
            // handle (toward -Z) and two small tabs
            var handle = Cached("strhandle", () =>
            {
                var path = new List<Vector3> { new Vector3(0, 0.007f, -0.045f), new Vector3(0, 0.0075f, -0.08f), new Vector3(0, 0.009f, -0.12f), new Vector3(0, 0.012f, -0.14f) };
                return MeshGen.Tube(path, 0.007f, 10, true, 0.2f);
            });
            Part("handle", go.transform, handle, Mats.Steel);
            var tab = Cached("strtab", () => MeshGen.RoundedBox(new Vector3(0.012f, 0.0015f, 0.016f), 0.0006f, 2));
            Part("tab", go.transform, tab, Mats.Steel, new Vector3(0.04f, 0.007f, 0.03f), Quaternion.Euler(0, -45, 0));
            Part("tab", go.transform, tab, Mats.Steel, new Vector3(-0.04f, 0.007f, 0.03f), Quaternion.Euler(0, 45, 0));
            Box(go, new Vector3(0, 0.006f, -0.035f), new Vector3(0.1f, 0.02f, 0.18f));
            return s;
        }

        public static BarSpoon BarSpoon()
        {
            var go = new GameObject("Bar spoon");
            var s = go.AddComponent<BarSpoon>();
            s.Height = 0.3f; s.Radius = 0.012f;
            var mesh = Cached("spoon", () =>
            {
                var mb = new MeshBuilder();
                // shaft with twisted strands
                var core = new List<Vector3> { new Vector3(0, 0.03f, 0), new Vector3(0, 0.29f, 0) };
                MeshGen.TubeInto(mb, core, 0.0017f, 8, false);
                for (int k = 0; k < 2; k++)
                {
                    var path = new List<Vector3>();
                    for (int i = 0; i <= 240; i++)
                    {
                        float t = i / 240f;
                        float a = t * 38f * Mathf.PI * 2f + k * Mathf.PI;
                        path.Add(new Vector3(Mathf.Cos(a) * 0.0013f, 0.035f + t * 0.24f, Mathf.Sin(a) * 0.0013f));
                    }
                    MeshGen.TubeInto(mb, path, 0.0011f, 5, false);
                }
                // bowl
                var bowl = new List<LP>();
                for (int i = 0; i <= 8; i++) { float a = Mathf.PI * 0.5f * i / 8f; bowl.Add(new LP(Mathf.Sin(a) * 0.011f, 0.012f - Mathf.Cos(a) * 0.006f)); }
                for (int i = 8; i >= 0; i--) { float a = Mathf.PI * 0.5f * i / 8f; bowl.Add(new LP(Mathf.Sin(a) * 0.0102f, 0.0128f - Mathf.Cos(a) * 0.0052f)); }
                MeshGen.LatheInto(mb, bowl, 24, 0, 0, 360, Matrix4x4.TRS(new Vector3(0, 0.006f, 0), Quaternion.identity, new Vector3(0.7f, 1f, 1.1f)));
                MeshGen.TubeInto(mb, new List<Vector3> { new Vector3(0, 0.014f, 0), new Vector3(0, 0.035f, 0) }, 0.0019f, 8, false);
                // weight on top
                var tear = new List<LP>();
                for (int i = 0; i <= 10; i++) { float a = -Mathf.PI / 2 + Mathf.PI * i / 10f; tear.Add(new LP(Mathf.Cos(a) * 0.0045f, Mathf.Sin(a) * 0.0065f)); }
                MeshGen.LatheInto(mb, tear, 16, 0, 0, 360, Matrix4x4.Translate(new Vector3(0, 0.295f, 0)));
                return mb.ToMesh();
            });
            Part("spoon", go.transform, mesh, Mats.Chrome);
            Box(go, new Vector3(0, 0.15f, 0), new Vector3(0.03f, 0.3f, 0.03f));
            return s;
        }

        public static Muddler Muddler()
        {
            var go = new GameObject("Muddler");
            var m = go.AddComponent<Muddler>();
            m.Height = 0.22f; m.Radius = 0.017f;
            var mesh = Cached("muddler", () => MeshGen.Lathe(new List<LP>
            {
                new LP(0, 0), new LP(0.014f, 0), new LP(0.0165f, 0.003f), new LP(0.0165f, 0.03f), new LP(0.0125f, 0.05f),
                new LP(0.0105f, 0.12f), new LP(0.0118f, 0.19f), new LP(0.013f, 0.212f), new LP(0.009f, 0.22f), new LP(0, 0.221f),
            }, 32, 0.15f));
            Part("wood", go.transform, mesh, Mats.TexTint("M_Walnut", new Color(1.5f, 1.35f, 1.2f), 0.4f, 0.3f));
            Box(go, new Vector3(0, 0.11f, 0), new Vector3(0.035f, 0.22f, 0.035f));
            return m;
        }

        public static IceScoop IceScoop()
        {
            var go = new GameObject("Ice scoop");
            var s = go.AddComponent<IceScoop>();
            s.Height = 0.075f; s.Radius = 0.04f;
            var body = new GameObject("scoop").transform;
            body.SetParent(go.transform, false);
            body.localPosition = new Vector3(0, 0.037f, -0.02f);
            var cup = Cached("scoopcup", () =>
            {
                var p = new List<LP> { new LP(0, 0), new LP(0.035f, 0.004f), new LP(0.0365f, 0.012f), new LP(0.0365f, 0.1f), new LP(0.0355f, 0.1005f), new LP(0.035f, 0.012f), new LP(0.034f, 0.0055f), new LP(0, 0.0015f) };
                return MeshGen.Lathe(p, 40, 0.1f);
            });
            Part("cup", body, cup, Mats.Steel, Vector3.zero, Quaternion.Euler(90, 0, 0));
            var handle = Cached("scoophandle", () => MeshGen.Tube(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(0, 0, -0.05f), new Vector3(0, 0.01f, -0.1f) }, 0.009f, 12, true));
            Part("handle", body, handle, Mats.Rubber);
            s.IceRoot = new GameObject("ice").transform;
            s.IceRoot.SetParent(body, false);
            s.IceRoot.localPosition = new Vector3(0, -0.02f, 0.04f);
            Box(go, new Vector3(0, 0.037f, 0.0f), new Vector3(0.075f, 0.075f, 0.2f));
            return s;
        }

        static Mesh iceMesh;
        public static GameObject IceCube(float size)
        {
            string key = "ice" + Mathf.RoundToInt(size * 1000);
            var mesh = Cached(key, () => MeshGen.RoundedBox(new Vector3(size, size * 0.92f, size), size * 0.24f, 2, size));
            var go = new GameObject("ice");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mats.Ice;
            r.shadowCastingMode = ShadowCastingMode.Off;
            _ = iceMesh;
            return go;
        }

        // ------------------------------------------------------------------ garnish

        public static GameObject GarnishMesh(Garnish g)
        {
            var go = new GameObject(g.ToString());
            switch (g)
            {
                case Garnish.LimeWedge:
                    {
                        var m = Cached("wedge", () => MeshGen.CitrusWedge(0.021f, 0.05f, 55f));
                        PartMulti("wedge", go.transform, m, new[] { Mats.Lit(new Color(0.22f, 0.5f, 0.1f), 0.65f), Mats.Lit(new Color(0.72f, 0.88f, 0.42f), 0.75f) });
                        break;
                    }
                case Garnish.LemonTwist:
                case Garnish.OrangePeel:
                    {
                        bool lemon = g == Garnish.LemonTwist;
                        var m = Cached(lemon ? "twist" : "peel", () =>
                        {
                            var path = MeshGen.Helix(Vector3.zero, lemon ? 0.008f : 0.011f, 0.055f, lemon ? 1.6f : 1.1f, 40);
                            return MeshGen.Tube(path, lemon ? 0.0055f : 0.008f, 10, true, 0.22f);
                        });
                        Part("peel", go.transform, m, Mats.Lit(lemon ? new Color(0.98f, 0.82f, 0.15f) : new Color(1f, 0.48f, 0.06f), 0.55f), Vector3.zero, Quaternion.Euler(90, 0, 0));
                        break;
                    }
                case Garnish.Cherry:
                    {
                        var m = Cached("cherry", () => MeshGen.Sphere(0.0105f, 20, 12));
                        Part("cherry", go.transform, m, Mats.Lit(new Color(0.42f, 0.01f, 0.05f), 0.92f), Vector3.zero, null, new Vector3(1f, 0.92f, 1f));
                        var stem = Cached("cherrystem", () => MeshGen.Tube(MeshGen.Catenary(new Vector3(0, 0.009f, 0), new Vector3(0.012f, 0.04f, 0.004f), -0.006f, 8), 0.0008f, 5));
                        Part("stem", go.transform, stem, Mats.Lit(new Color(0.3f, 0.22f, 0.08f), 0.3f));
                        break;
                    }
                case Garnish.Mint:
                    {
                        var m = Cached("mint", () =>
                        {
                            var mb = new MeshBuilder();
                            MeshGen.TubeInto(mb, new List<Vector3> { Vector3.zero, new Vector3(0, 0.05f, 0.002f), new Vector3(0.002f, 0.075f, 0) }, 0.0012f, 5);
                            for (int i = 0; i < 9; i++)
                            {
                                float y = 0.035f + (i / 9f) * 0.045f;
                                float a = i * 137.5f;
                                float s = 1.15f - i * 0.06f;
                                var tr = Matrix4x4.TRS(new Vector3(0, y, 0), Quaternion.Euler(-35f + i * 3f, a, 0), Vector3.one * s);
                                MeshGen.LeafInto(mb, tr, 0.028f, 0.018f, -0.25f, 0.004f, 6, 4);
                            }
                            return mb.ToMesh();
                        });
                        Part("mint", go.transform, m, Mats.Lit(new Color(0.12f, 0.42f, 0.12f), 0.45f));
                        break;
                    }
                case Garnish.Olive:
                    {
                        var pick = Cached("pick", () => MeshGen.Tube(new List<Vector3> { Vector3.zero, new Vector3(0, 0, 0.085f) }, 0.0012f, 6));
                        Part("pick", go.transform, pick, Mats.Lit(new Color(0.8f, 0.62f, 0.35f), 0.4f));
                        var m = Cached("olive", () => MeshGen.Sphere(0.0085f, 18, 10));
                        Part("olive", go.transform, m, Mats.Lit(new Color(0.3f, 0.4f, 0.1f), 0.8f), new Vector3(0, 0, 0.06f), Quaternion.Euler(90, 0, 0), new Vector3(1f, 1.3f, 1f));
                        var dot = Cached("pimento", () => MeshGen.Sphere(0.003f, 10, 6));
                        Part("pimento", go.transform, dot, Mats.Lit(new Color(0.75f, 0.08f, 0.05f), 0.7f), new Vector3(0, 0, 0.071f));
                        break;
                    }
            }
            return go;
        }

        // ------------------------------------------------------------------ bottles

        struct BottleShape
        {
            public List<LP> Outer;
            public float BodyR, LabelY0, LabelY1, Top, NeckR, Fill;
            public bool Square, Flat;
        }

        static float Shoulder(float t) => (1f - Mathf.Cos(Mathf.PI * t)) * 0.5f;

        static BottleShape Shape(BottleStyle st)
        {
            float R, body, sh, neckR, neck, lip = 0.002f;
            var s = new BottleShape();
            switch (st)
            {
                case BottleStyle.Shoulder: R = 0.045f; body = 0.15f; sh = 0.035f; neckR = 0.0145f; neck = 0.06f; s.Fill = 0.8f; break;
                case BottleStyle.Slim: R = 0.032f; body = 0.21f; sh = 0.05f; neckR = 0.012f; neck = 0.07f; s.Fill = 0.75f; break;
                case BottleStyle.Flask: R = 0.045f; body = 0.15f; sh = 0.05f; neckR = 0.0135f; neck = 0.05f; s.Fill = 0.8f; s.Flat = true; break;
                case BottleStyle.Wine: R = 0.036f; body = 0.17f; sh = 0.07f; neckR = 0.0135f; neck = 0.075f; s.Fill = 0.8f; break;
                case BottleStyle.Bitters: R = 0.027f; body = 0.085f; sh = 0.025f; neckR = 0.0105f; neck = 0.025f; s.Fill = 0.85f; break;
                case BottleStyle.Syrup: R = 0.034f; body = 0.19f; sh = 0.035f; neckR = 0.0125f; neck = 0.05f; s.Fill = 0.85f; break;
                case BottleStyle.Carafe: R = 0.038f; body = 0.15f; sh = 0.03f; neckR = 0.022f; neck = 0.03f; s.Fill = 0.82f; lip = 0.0025f; break;
                case BottleStyle.Soda: R = 0.028f; body = 0.1f; sh = 0.06f; neckR = 0.0115f; neck = 0.035f; s.Fill = 0.86f; break;
                case BottleStyle.Square: R = 0.034f; body = 0.19f; sh = 0.035f; neckR = 0.0135f; neck = 0.06f; s.Fill = 0.8f; s.Square = true; break;
                default: R = 0.037f; body = 0.2f; sh = 0.05f; neckR = 0.0135f; neck = 0.065f; s.Fill = 0.78f; break;
            }
            var p = new List<LP>();
            if (!s.Square)
            {
                p.Add(new LP(0, 0.004f));
                p.Add(new LP(R * 0.75f, 0.0015f));
                p.Add(new LP(R - 0.004f, 0f));
                p.Add(new LP(R - 0.0008f, 0.0025f));
                p.Add(new LP(R, 0.008f));
            }
            else p.Add(new LP(0, body - 0.004f));
            p.Add(new LP(R, body));
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                p.Add(new LP(Mathf.Lerp(R, neckR, Shoulder(t)), body + sh * t));
            }
            float top = body + sh + neck;
            p.Add(new LP(neckR, top - 0.012f));
            p.Add(new LP(neckR + lip, top - 0.009f));
            p.Add(new LP(neckR + lip, top - 0.002f));
            p.Add(new LP(neckR * 0.9f, top));
            p.Add(new LP(0, top));
            s.Outer = p;
            s.BodyR = R; s.Top = top; s.NeckR = neckR;
            s.LabelY0 = body * 0.22f; s.LabelY1 = body * (st == BottleStyle.Bitters ? 0.98f : 0.8f);
            if (st == BottleStyle.Soda) { s.LabelY0 = body * 0.3f; s.LabelY1 = body * 0.85f; }
            return s;
        }

        static Mesh LiquidLathe(BottleShape s, float fill)
        {
            var p = new List<LP>();
            float level = s.Outer[s.Outer.Count - 1].y * fill;
            float prevY = 0;
            p.Add(new LP(0, 0.006f));
            foreach (var lp in s.Outer)
            {
                if (lp.r <= 0) continue;
                float y = Mathf.Max(lp.y, 0.006f);
                if (y >= level)
                {
                    p.Add(new LP(Mathf.Max(0, lp.r - 0.0025f), level));
                    break;
                }
                p.Add(new LP(Mathf.Max(0, lp.r - 0.0025f), y));
                prevY = y;
            }
            p.Add(new LP(0, level));
            _ = prevY;
            return MeshGen.Lathe(p, 32);
        }

        public static Texture2D LabelTex(Ingredient ing)
        {
            if (labelCache.TryGetValue(ing, out var t) && t != null) return t;
            t = TextTex.Label(ing);
            return labelCache[ing] = t;
        }

        /// <summary>Builds the visual parts of a bottle into parent; returns the pourer tip height.</summary>
        public static float BottleVisual(Transform parent, BottleStyle style, Color glass, Color liquid, float liquidAlpha, Material label, Material capMat, bool pourer, bool decorative, Material liquidOverride = null)
        {
            var s = Shape(style);
            var root = new GameObject("shape").transform;
            root.SetParent(parent, false);
            if (s.Flat) root.localScale = new Vector3(1f, 1f, 0.58f);
            string k = style.ToString();
            var glassMat = Mats.Glass(glass, 0.97f);
            if (s.Square)
            {
                var bodyBox = Cached("sq_body", () => MeshGen.RoundedBox(new Vector3(0.074f, 0.19f, 0.074f), 0.011f, 4));
                Part("glassBody", root, bodyBox, glassMat, new Vector3(0, 0.095f, 0), null, null, !decorative);
                var liqBox = Cached("sq_liq", () => MeshGen.RoundedBox(new Vector3(0.066f, 0.15f, 0.066f), 0.008f, 3));
                var lm = liquidOverride ?? Mats.Liquid(new Color(liquid.r, liquid.g, liquid.b, Mathf.Lerp(0.04f, 0.95f, liquidAlpha)));
                Part("liquid", root, liqBox, lm, new Vector3(0, 0.081f, 0), null, null, false);
            }
            var glassMesh = Cached("b_" + k, () => MeshGen.Lathe(s.Outer, 40));
            Part("glass", root, glassMesh, glassMat, Vector3.zero, null, null, !decorative);
            if (!s.Square)
            {
                var liq = Cached("bl_" + k, () => LiquidLathe(s, s.Fill));
                var lm = liquidOverride ?? Mats.Liquid(new Color(liquid.r, liquid.g, liquid.b, Mathf.Lerp(0.04f, 0.95f, liquidAlpha)));
                Part("liquid", root, liq, lm, Vector3.zero, null, null, false);
            }
            if (label != null)
            {
                if (s.Square)
                {
                    var q = Cached("sq_label", () => MeshGen.Quad(0.062f, 0.1f));
                    Part("label", root, q, label, new Vector3(0, 0.1f, 0.0372f), Quaternion.Euler(0, 180, 0), null, false);
                }
                else
                {
                    var band = Cached("lb_" + k, () =>
                    {
                        var p = new List<LP> { new LP(s.BodyR + 0.0006f, s.LabelY0), new LP(s.BodyR + 0.0006f, s.LabelY1) };
                        return MeshGen.Lathe(p, 24, 0, 20f, 160f);
                    });
                    Part("label", root, band, label, Vector3.zero, null, null, false);
                }
            }
            // neck cap / foil / pourer
            float tip = s.Top;
            if (style == BottleStyle.Wine)
            {
                var foil = Cached("foil_" + k, () => MeshGen.Lathe(new List<LP> { new LP(s.NeckR + 0.0012f, s.Top - 0.045f), new LP(s.NeckR + 0.0024f, s.Top - 0.004f), new LP(s.NeckR, s.Top + 0.001f), new LP(0, s.Top + 0.001f) }, 24));
                Part("foil", root, foil, capMat, Vector3.zero, null, null, false);
            }
            else if (style == BottleStyle.Bitters)
            {
                var cap = Cached("bittercap", () => MeshGen.Lathe(new List<LP> { new LP(0.0125f, s.Top - 0.012f), new LP(0.0125f, s.Top + 0.006f), new LP(0.006f, s.Top + 0.012f), new LP(0.0025f, s.Top + 0.018f), new LP(0, s.Top + 0.018f) }, 24));
                Part("dasher", root, cap, capMat, Vector3.zero, null, null, false);
                tip = s.Top + 0.018f;
            }
            if (pourer && style != BottleStyle.Soda && style != BottleStyle.Bitters)
            {
                var collar = Cached("collar_" + Mathf.RoundToInt(s.NeckR * 10000), () => MeshGen.Cylinder(s.NeckR + 0.0018f, 0.014f, 24, 0.0015f));
                Part("collar", root, collar, Mats.Rubber, new Vector3(0, s.Top - 0.006f, 0), null, null, false);
                var spout = Cached("spout", () => { var mb = new MeshBuilder(); MeshGen.TubeInto(mb, new List<Vector3> { Vector3.zero, new Vector3(0, 0.018f, 0.002f), new Vector3(0, 0.032f, 0.008f), new Vector3(0, 0.04f, 0.014f) }, 0.0032f, 12, true, 1f, 0.1f, x => Mathf.Lerp(1f, 0.62f, x)); return mb.ToMesh(); });
                Part("spout", root, spout, Mats.Chrome, new Vector3(0, s.Top + 0.006f, 0), null, null, false);
                tip = s.Top + 0.046f;
            }
            else if (!pourer && style != BottleStyle.Soda && style != BottleStyle.Wine && style != BottleStyle.Bitters)
            {
                var cap = Cached("cap_" + Mathf.RoundToInt(s.NeckR * 10000), () => MeshGen.Cylinder(s.NeckR + 0.003f, 0.02f, 24, 0.002f));
                Part("cap", root, cap, capMat, new Vector3(0, s.Top - 0.012f, 0), null, null, false);
            }
            return tip;
        }

        public static Bottle Bottle(Ingredient ing)
        {
            var go = new GameObject(ing.Key);
            var b = go.AddComponent<Bottle>();
            b.Ing = ing;
            var s = Shape(ing.Style);
            Material label = null;
            if (ing.Style != BottleStyle.Carafe) label = Mats.Label(LabelTex(ing));
            var capMat = ing.Style == BottleStyle.Wine ? (ing.Key == "prosecco" ? Mats.Gold : Mats.Metal(new Color(0.5f, 0.06f, 0.08f), 0.7f)) : Mats.Chrome;
            float tip = BottleVisual(go.transform, ing.Style, ing.GlassTint, ing.Color, ing.Alpha, label, capMat, true, false);
            b.Height = tip;
            b.Radius = s.BodyR;
            b.SpoutLocal = ing.Style == BottleStyle.Soda || ing.Style == BottleStyle.Bitters
                ? new Vector3(0, tip, 0) : new Vector3(0, tip - 0.002f, 0.014f);
            float depth = s.Flat ? s.BodyR * 1.2f : s.BodyR * 2f;
            Box(go, new Vector3(0, tip * 0.5f, 0), new Vector3(s.BodyR * 2f, tip, depth));
            return b;
        }
    }
}
