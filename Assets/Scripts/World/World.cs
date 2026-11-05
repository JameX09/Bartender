using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bartender
{
    /// <summary>Key positions shared by the world, guests and the game.</summary>
    public static class Layout
    {
        public const float BarTopY = 1.12f;
        public const float CoasterZ = 0.33f;
        public const float WorkY = 0.92f;
        public const float BackY = 0.96f;
        public const float StoolZ = 1.02f;
        public static readonly float[] SeatX = { -2.4f, -1.2f, 0f, 1.2f, 2.4f };
        public static readonly Vector3 Entrance = new Vector3(-10.2f, 0, 2.3f);
        public const float AisleZ = 2.3f;
    }

    /// <summary>Builds the rooftop lounge: bar, back bar, work station, furniture, lights, city.</summary>
    public sealed class World : MonoBehaviour
    {
        public static World I;
        public readonly List<ServeSpot> Coasters = new List<ServeSpot>();
        public ServeSpot TastingMat;
        public Transform Root;
        public ReflectionProbe BarProbe, LoungeProbe;
        public Volume PostFx;
        public DepthOfField Dof;
        public readonly List<Light> Flicker = new List<Light>();
        public readonly List<Light> ShadowLights = new List<Light>();
        readonly List<Transform> cones = new List<Transform>();
        float probeTimer;
        static Texture2D coneTex;
        readonly List<float> flickerBase = new List<float>();
        Material beaconMat;
        readonly List<Transform> flames = new List<Transform>();

        static readonly Dictionary<string, Mesh> boxCache = new Dictionary<string, Mesh>();

        // ------------------------------------------------------------------ helpers

        static Mesh BoxMesh(Vector3 size, float r, int k = 3)
        {
            string key = size.x.ToString("0.000") + size.y.ToString("0.000") + size.z.ToString("0.000") + r.ToString("0.000") + k;
            if (boxCache.TryGetValue(key, out var m)) return m;
            m = MeshGen.RoundedBox(size, r, k, 1f);
            return boxCache[key] = m;
        }

        GameObject Slab(string name, Vector3 center, Vector3 size, Material mat, float radius = 0.004f, bool collider = true, bool surface = false, Transform parent = null, bool shadows = true)
        {
            var go = Props.Part(name, parent ?? Root, BoxMesh(size, radius, radius > 0.02f ? 4 : 2), mat, center, null, null, shadows);
            go.isStatic = true;
            if (collider)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.size = size;
            }
            if (surface) go.AddComponent<Surface>();
            return go;
        }

        Light PointLight(Vector3 p, Color c, float intensity, float range, bool shadows = false, Transform parent = null)
        {
            var go = new GameObject("light");
            go.transform.SetParent(parent ?? Root, false);
            go.transform.position = p;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.85f;
            return l;
        }

        Light SpotLight(Vector3 p, Vector3 dir, Color c, float intensity, float range, float angle, bool shadows)
        {
            var go = new GameObject("spot");
            go.transform.SetParent(Root, false);
            go.transform.position = p;
            go.transform.rotation = Quaternion.LookRotation(dir);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.55f;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.8f;
            return l;
        }

        static Color Kelvin(float k)
        {
            // rough black-body tint
            if (k <= 2000) return new Color(1f, 0.54f, 0.16f);
            if (k <= 2700) return new Color(1f, 0.66f, 0.34f);
            if (k <= 3200) return new Color(1f, 0.75f, 0.5f);
            if (k <= 4000) return new Color(1f, 0.84f, 0.68f);
            return new Color(1f, 0.95f, 0.9f);
        }

        // ------------------------------------------------------------------ build

        public void Build()
        {
            I = this;
            Root = new GameObject("World").transform;
            Floors();
            BarCounter();
            WorkStation();
            BackBar();
            Canopy();
            Stools();
            Lounge();
            Balustrade();
            Entrance();
            City.Build(Root);
            Sky();
            Lighting();
            PostProcessing();
        }

        void Floors()
        {
            var deck = Mats.TexTint("M_Deck", Color.white);
            // main deck
            var d = Props.Part("deck", Root, MeshGen.Plane(30f, 20f, 1f), deck, new Vector3(0, 0, 5f));
            d.isStatic = true;
            var col = d.AddComponent<BoxCollider>(); col.size = new Vector3(30f, 0.02f, 20f); col.center = new Vector3(0, -0.01f, 0);
            // terrazzo behind the bar, slightly raised
            Slab("bar floor", new Vector3(0, 0.01f, -1.2f), new Vector3(7.3f, 0.04f, 2.3f), Mats.TexTint("M_Terrazzo", Color.white), 0.006f);
            // the tower below the roof edge
            var conc = Mats.TexTint("M_Concrete", new Color(0.55f, 0.55f, 0.58f));
            Slab("roof edge", new Vector3(0, -0.3f, 5f), new Vector3(30.6f, 0.56f, 20.6f), conc, 0.02f, false);
            var facade = Mats.Tex("M_Facade1");
            var fm = new MeshBuilder();
            float top = -0.58f, bottom = -150f;
            AddFacade(fm, new Vector3(-15.3f, 0, -5.3f), new Vector3(15.3f, 0, -5.3f), top, bottom);
            AddFacade(fm, new Vector3(15.3f, 0, -5.3f), new Vector3(15.3f, 0, 15.3f), top, bottom);
            AddFacade(fm, new Vector3(15.3f, 0, 15.3f), new Vector3(-15.3f, 0, 15.3f), top, bottom);
            AddFacade(fm, new Vector3(-15.3f, 0, 15.3f), new Vector3(-15.3f, 0, -5.3f), top, bottom);
            var fg = new GameObject("tower");
            fg.transform.SetParent(Root, false);
            fg.AddComponent<MeshFilter>().sharedMesh = fm.ToMesh("tower");
            fg.AddComponent<MeshRenderer>().sharedMaterial = facade;
        }

        public static void AddFacade(MeshBuilder mb, Vector3 a, Vector3 b, float top, float bottom, float uOffset = 0)
        {
            var n = Vector3.Cross(Vector3.up, b - a).normalized;
            float w = Vector3.Distance(a, b);
            const float tileW = 48f, tileH = 86.4f;
            int i0 = mb.Add(new Vector3(a.x, bottom, a.z), n, new Vector2(uOffset / tileW, bottom / tileH));
            int i1 = mb.Add(new Vector3(b.x, bottom, b.z), n, new Vector2((uOffset + w) / tileW, bottom / tileH));
            int i2 = mb.Add(new Vector3(b.x, top, b.z), n, new Vector2((uOffset + w) / tileW, top / tileH));
            int i3 = mb.Add(new Vector3(a.x, top, a.z), n, new Vector2(uOffset / tileW, top / tileH));
            mb.Quad(i0, i1, i2, i3);
        }

        // ------------------------------------------------------------------ guest-side bar

        void BarCounter()
        {
            var marble = Mats.TexTint("M_Marble", new Color(0.86f, 0.86f, 0.86f));
            var walnut = Mats.TexTint("M_Walnut", Color.white);
            var black = Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.45f);
            var brass = Mats.Brass;
            // marble top (with bullnose edge)
            Slab("bar top", new Vector3(0, Layout.BarTopY - 0.025f, 0.3f), new Vector3(6.6f, 0.05f, 0.66f), marble, 0.022f, true, true);
            // body
            Slab("bar body", new Vector3(0, 0.56f, 0.24f), new Vector3(6.4f, 1.02f, 0.5f), black, 0.01f);
            // fluted walnut front
            var flutes = MeshGen.Fluted(6.36f, 0.86f, 0.045f, 0.012f, 8, 1f);
            Props.Part("flutes", Root, flutes, walnut, new Vector3(0, 0.14f, 0.49f)).isStatic = true;
            // brass trims
            var trim = MeshGen.RoundedBox(new Vector3(6.38f, 0.018f, 0.02f), 0.006f, 2);
            Props.Part("trim top", Root, trim, brass, new Vector3(0, 1.012f, 0.5f));
            Props.Part("trim bottom", Root, trim, brass, new Vector3(0, 0.13f, 0.5f));
            // plinth and LED glow
            Slab("plinth", new Vector3(0, 0.065f, 0.42f), new Vector3(6.36f, 0.13f, 0.06f), black, 0.004f, false);
            var led = Mats.Emissive(Color.black, Kelvin(2400), 4f);
            Props.Part("led under top", Root, MeshGen.RoundedBox(new Vector3(6.3f, 0.008f, 0.01f), 0.003f, 1), led, new Vector3(0, 1.085f, 0.585f), null, null, false);
            Props.Part("led floor", Root, MeshGen.RoundedBox(new Vector3(6.3f, 0.006f, 0.012f), 0.002f, 1), led, new Vector3(0, 0.012f, 0.465f), null, null, false);
            for (int i = 0; i < 4; i++) PointLight(new Vector3(-2.4f + i * 1.6f, 0.08f, 0.62f), Kelvin(2400), 0.35f, 1.6f);
            // foot rail
            var rail = new MeshBuilder();
            MeshGen.TubeInto(rail, new List<Vector3> { new Vector3(-3.15f, 0.22f, 0.66f), new Vector3(3.15f, 0.22f, 0.66f) }, 0.025f, 20, true, 1f, 0.5f);
            for (int i = 0; i < 6; i++)
            {
                float x = -3f + i * 1.2f;
                MeshGen.TubeInto(rail, new List<Vector3> { new Vector3(x, 0.22f, 0.66f), new Vector3(x, 0.2f, 0.58f), new Vector3(x, 0.17f, 0.5f) }, 0.012f, 12, true, 1f, 0.5f);
            }
            Props.Part("foot rail", Root, rail.ToMesh("rail"), brass);
            // coasters
            var coaster = MeshGen.Cylinder(0.052f, 0.004f, 40, 0.0015f);
            var coasterMat = Mats.TexTint("M_Leather", new Color(0.12f, 0.08f, 0.06f), 0.35f);
            var ring = MeshGen.Torus(0.05f, 0.0012f, 48, 6);
            for (int i = 0; i < Layout.SeatX.Length; i++)
            {
                var p = new Vector3(Layout.SeatX[i], Layout.BarTopY, Layout.CoasterZ);
                var c = Props.Part("coaster" + i, Root, coaster, coasterMat, p);
                var cc = c.AddComponent<BoxCollider>(); cc.center = new Vector3(0, 0.002f, 0); cc.size = new Vector3(0.1f, 0.004f, 0.1f);
                c.AddComponent<Surface>();
                Props.Part("ring", c.transform, ring, Mats.Gold, new Vector3(0, 0.004f, 0), null, null, false);
                var s = c.AddComponent<ServeSpot>();
                s.Seat = i;
                Coasters.Add(s);
            }
            // tasting mat for free mode
            var mat = Slab("tasting mat", new Vector3(0, Layout.BarTopY + 0.003f, 0.27f), new Vector3(0.44f, 0.006f, 0.2f), Mats.TexTint("M_RubberMat", Color.white), 0.003f, true, true);
            var border = new MeshBuilder();
            MeshGen.RoundedBoxInto(border, new Vector3(0.448f, 0.004f, 0.006f), 0.002f, 1, 1, Matrix4x4.Translate(new Vector3(0, 0.002f, 0.1f)));
            MeshGen.RoundedBoxInto(border, new Vector3(0.448f, 0.004f, 0.006f), 0.002f, 1, 1, Matrix4x4.Translate(new Vector3(0, 0.002f, -0.1f)));
            MeshGen.RoundedBoxInto(border, new Vector3(0.006f, 0.004f, 0.2f), 0.002f, 1, 1, Matrix4x4.Translate(new Vector3(0.222f, 0.002f, 0)));
            MeshGen.RoundedBoxInto(border, new Vector3(0.006f, 0.004f, 0.2f), 0.002f, 1, 1, Matrix4x4.Translate(new Vector3(-0.222f, 0.002f, 0)));
            Props.Part("border", mat.transform, border.ToMesh(), Mats.Gold, Vector3.zero, null, null, false);
            TastingMat = new GameObject("taste").AddComponent<ServeSpot>();
            TastingMat.transform.SetParent(mat.transform, false);
            TastingMat.transform.localPosition = new Vector3(0, 0.003f, 0);
            TastingMat.Seat = -1;
        }

        // ------------------------------------------------------------------ bartender work station

        void WorkStation()
        {
            var steel = Mats.Steel;
            var walnut = Mats.TexTint("M_Walnut", Color.white);
            var black = Mats.Lit(new Color(0.035f, 0.035f, 0.04f), 0.4f);
            float y = Layout.WorkY;
            const float z0 = -0.6f, z1 = -0.02f;
            float zc = (z0 + z1) * 0.5f, depth = z1 - z0;
            // cabinets below
            // cabinet stops below the sink and ice well so they stay open
            Slab("work cabinet", new Vector3(0, 0.36f, zc + 0.02f), new Vector3(6.3f, 0.68f, depth - 0.04f), black, 0.006f);
            Slab("work back", new Vector3(0, 0.8f, z1 - 0.01f), new Vector3(6.3f, 0.2f, 0.02f), black, 0.004f, false);
            for (int i = 0; i < 9; i++)
            {
                float x = -2.8f + i * 0.7f;
                Slab("door", new Vector3(x, 0.47f, z0 + 0.008f), new Vector3(0.66f, 0.78f, 0.02f), walnut, 0.006f, false);
                Props.Part("handle", Root, MeshGen.RoundedBox(new Vector3(0.012f, 0.16f, 0.016f), 0.005f, 2), Mats.Brass, new Vector3(x + 0.27f, 0.72f, z0 - 0.01f));
            }
            // top in pieces around the sink and ice well cut-outs
            void Top(float xa, float xb) => Slab("work top", new Vector3((xa + xb) * 0.5f, y - 0.015f, zc), new Vector3(xb - xa, 0.03f, depth), steel, 0.004f, true, true);
            Top(-3.15f, -2.94f);
            Top(-2.36f, -0.5f);
            Top(0.5f, 3.15f);
            // front/back strips over the cut-outs
            Slab("strip", new Vector3(-2.65f, y - 0.015f, z0 + 0.05f), new Vector3(0.58f, 0.03f, 0.1f), steel, 0.004f, true, true);
            Slab("strip", new Vector3(-2.65f, y - 0.015f, z1 - 0.05f), new Vector3(0.58f, 0.03f, 0.1f), steel, 0.004f, true, true);
            Slab("strip", new Vector3(0, y - 0.015f, z0 + 0.05f), new Vector3(1.0f, 0.03f, 0.1f), steel, 0.004f, true, true);
            Slab("strip", new Vector3(0, y - 0.015f, z1 - 0.05f), new Vector3(1.0f, 0.03f, 0.1f), steel, 0.004f, true, true);

            // sink
            Basin(new Vector3(-2.65f, y, zc), new Vector2(0.58f, depth - 0.2f), 0.16f, steel);
            var sink = new GameObject("sink").AddComponent<Sink>();
            sink.transform.SetParent(Root, false);
            sink.transform.position = new Vector3(-2.65f, y - 0.06f, zc);
            var sc = sink.gameObject.AddComponent<BoxCollider>(); sc.size = new Vector3(0.56f, 0.1f, depth - 0.22f);
            // faucet
            var faucet = new MeshBuilder();
            var fpath = new List<Vector3> { new Vector3(-2.65f, y, z1 - 0.06f), new Vector3(-2.65f, y + 0.25f, z1 - 0.06f) };
            for (int i = 0; i <= 10; i++) { float a = Mathf.PI * i / 10f; fpath.Add(new Vector3(-2.65f, y + 0.25f + Mathf.Sin(a) * 0.08f, z1 - 0.06f - (1 - Mathf.Cos(a)) * 0.08f)); }
            fpath.Add(new Vector3(-2.65f, y + 0.2f, z1 - 0.22f));
            MeshGen.TubeInto(faucet, fpath, 0.012f, 14, true);
            Props.Part("faucet", Root, faucet.ToMesh(), Mats.Chrome);

            // bus tub
            Basin(new Vector3(-2.12f, y + 0.0f, zc), new Vector2(0.38f, 0.36f), -0.13f, Mats.Lit(new Color(0.05f, 0.05f, 0.055f), 0.35f), true);
            var bus = new GameObject("bus tub").AddComponent<BusTub>();
            bus.transform.SetParent(Root, false);
            bus.transform.position = new Vector3(-2.12f, y + 0.065f, zc);
            var bc = bus.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(0.38f, 0.13f, 0.36f);

            // rubber bar mat for tools
            Slab("bar mat", new Vector3(-1.2f, y + 0.004f, zc), new Vector3(1.3f, 0.008f, 0.42f), Mats.TexTint("M_RubberMat", Color.white), 0.004f, true, true);

            // ice well with a heap of ice
            Basin(new Vector3(0, y, zc), new Vector2(0.98f, depth - 0.2f), 0.2f, steel);
            IceHeap(new Vector3(0, y - 0.035f, zc), new Vector2(0.9f, depth - 0.26f));
            var well = new GameObject("ice well").AddComponent<IceWell>();
            well.transform.SetParent(Root, false);
            well.transform.position = new Vector3(0, y - 0.03f, zc);
            var wc = well.gameObject.AddComponent<BoxCollider>(); wc.size = new Vector3(0.96f, 0.08f, depth - 0.22f);

            // garnish tray: 2 x 3 compartments
            var tray = Mats.Lit(new Color(0.04f, 0.04f, 0.045f), 0.5f);
            Slab("garnish tray", new Vector3(1.05f, y + 0.012f, zc), new Vector3(0.96f, 0.024f, 0.42f), tray, 0.006f, false);
            Garnish[] gs = { Garnish.LimeWedge, Garnish.LemonTwist, Garnish.OrangePeel, Garnish.Cherry, Garnish.Mint, Garnish.Olive };
            for (int i = 0; i < 6; i++)
            {
                int cx = i % 3, cz = i / 3;
                var p = new Vector3(0.75f + cx * 0.3f, y + 0.024f, zc + (cz == 0 ? 0.1f : -0.1f));
                Basin(p, new Vector2(0.27f, 0.18f), -0.045f, steel, true);
                var bin = new GameObject("garnish " + gs[i]).AddComponent<GarnishBin>();
                bin.G = gs[i];
                bin.transform.SetParent(Root, false);
                bin.transform.position = p + Vector3.up * 0.02f;
                var gc = bin.gameObject.AddComponent<BoxCollider>(); gc.size = new Vector3(0.27f, 0.05f, 0.18f);
                FillGarnish(gs[i], p + Vector3.up * 0.012f);
            }

            // glass rack: one of each glass, refilled automatically
            Slab("glass mat", new Vector3(2.35f, y + 0.004f, zc), new Vector3(1.4f, 0.008f, 0.44f), Mats.TexTint("M_RubberMat", Color.white), 0.004f, true, true);
            GlassType[] gt = { GlassType.Highball, GlassType.Rocks, GlassType.Coupe, GlassType.Martini, GlassType.Wine, GlassType.Mug };
            for (int i = 0; i < gt.Length; i++)
            {
                var slot = new GameObject("slot " + gt[i]).AddComponent<GlassSlot>();
                slot.Type = gt[i];
                slot.transform.SetParent(Root, false);
                slot.transform.position = new Vector3(1.8f + (i % 3) * 0.24f + (i / 3) * 0.12f, y + 0.008f, zc + (i < 3 ? 0.1f : -0.1f));
            }

            // tools with home spots
            float ty = y + 0.008f;
            // back row: shaker tin and cap, mixing glass, strainer
            Home(Props.ShakerTin(), new Vector3(-1.76f, ty, zc + 0.1f));
            Home(Props.ShakerCap(), new Vector3(-1.64f, ty, zc - 0.03f));
            Home(Props.MixingGlass(), new Vector3(-1.48f, ty, zc + 0.1f));
            Home(Props.Strainer(), new Vector3(-1.2f, ty, zc + 0.11f), Quaternion.Euler(0, -90f, 0));
            // front row: a double jigger for every measure in the recipes (60/30, 50/25, 40/20, 30/15, 20/10 ml)
            (float big, float small, Material mat)[] jiggers =
            {
                (60f, 30f, Mats.Metal(new Color(0.9f, 0.5f, 0.32f), 0.85f)),   // copper
                (50f, 25f, Mats.Gold),
                (40f, 20f, Mats.Chrome),
                (30f, 15f, Mats.BlackMetal),
                (20f, 10f, Mats.Brass),
            };
            for (int i = 0; i < jiggers.Length; i++)
            {
                var j = jiggers[i];
                float kb = Mathf.Pow(j.big / 50f, 1f / 3f), ks = Mathf.Pow(j.small / 25f, 1f / 3f);
                Home(Props.Jigger(j.big, j.small, 0.0225f * kb, 0.057f * kb, 0.018f * ks, 0.039f * ks, j.mat), new Vector3(-1.78f + i * 0.155f, ty, zc - 0.13f));
            }
            Home(Props.BarSpoon(), new Vector3(-0.8f, ty + 0.012f, zc + 0.15f), Quaternion.Euler(0, 180f, 0) * Quaternion.Euler(90f, 0, 0));
            Home(Props.Muddler(), new Vector3(-0.66f, ty + 0.017f, zc + 0.11f), Quaternion.Euler(0, 180f, 0) * Quaternion.Euler(90f, 0, 0));
            Home(Props.IceScoop(), new Vector3(0.3f, y + 0.0f, zc - 0.02f), Quaternion.Euler(0, 160f, 0));
        }

        static void Home(Item it, Vector3 p, Quaternion? rot = null)
        {
            it.transform.SetPositionAndRotation(p, rot ?? Quaternion.Euler(0, 180f, 0));
            it.SetHome();
        }

        /// <summary>Open-top box sunk into (depth > 0) or standing on (depth < 0) the counter.</summary>
        void Basin(Vector3 topCenter, Vector2 size, float depth, Material m, bool standing = false)
        {
            float d = Mathf.Abs(depth);
            float yc = standing ? topCenter.y + d * 0.5f : topCenter.y - d * 0.5f;
            const float t = 0.008f;
            Slab("basin", new Vector3(topCenter.x, yc - d * 0.5f + t * 0.5f, topCenter.z), new Vector3(size.x, t, size.y), m, 0.003f, false);
            Slab("basin", new Vector3(topCenter.x - size.x * 0.5f + t * 0.5f, yc, topCenter.z), new Vector3(t, d, size.y), m, 0.003f, false);
            Slab("basin", new Vector3(topCenter.x + size.x * 0.5f - t * 0.5f, yc, topCenter.z), new Vector3(t, d, size.y), m, 0.003f, false);
            Slab("basin", new Vector3(topCenter.x, yc, topCenter.z - size.y * 0.5f + t * 0.5f), new Vector3(size.x, d, t), m, 0.003f, false);
            Slab("basin", new Vector3(topCenter.x, yc, topCenter.z + size.y * 0.5f - t * 0.5f), new Vector3(size.x, d, t), m, 0.003f, false);
        }

        void IceHeap(Vector3 c, Vector2 size)
        {
            var comb = new Combiner();
            var rnd = new System.Random(5);
            var mesh = MeshGen.RoundedBox(new Vector3(0.028f, 0.026f, 0.028f), 0.007f, 2, 0.03f);
            var heapA = Mats.Lit(new Color(0.95f, 0.97f, 1f), 0.9f);
            var heapB = Mats.Lit(new Color(0.86f, 0.91f, 0.97f), 0.93f);
            PointLight(c + new Vector3(0, 0.15f, 0), new Color(0.78f, 0.9f, 1f), 0.22f, 0.65f);
            for (int i = 0; i < 420; i++)
            {
                float u = (float)rnd.NextDouble() - 0.5f, v = (float)rnd.NextDouble() - 0.5f;
                float edge = 1f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) * 2f;
                float h = (float)rnd.NextDouble() * 0.05f * Mathf.Sqrt(Mathf.Max(0, edge));
                var p = c + new Vector3(u * size.x, h - 0.05f, v * size.y);
                var r = Quaternion.Euler((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f);
                comb.Add(mesh, (i & 1) == 0 ? heapA : heapB, Matrix4x4.TRS(p, r, Vector3.one * (0.85f + (float)rnd.NextDouble() * 0.3f)), false);
            }
            comb.Build(Root, "ice heap");
        }

        void FillGarnish(Garnish g, Vector3 c)
        {
            var comb = new Combiner();
            var rnd = new System.Random((int)g * 7 + 1);
            int n = g == Garnish.Mint ? 6 : g == Garnish.Cherry || g == Garnish.Olive ? 14 : 9;
            for (int i = 0; i < n; i++)
            {
                var go = Props.GarnishMesh(g);
                float u = (float)rnd.NextDouble() - 0.5f, v = (float)rnd.NextDouble() - 0.5f;
                var p = c + new Vector3(u * 0.2f, (float)rnd.NextDouble() * 0.012f, v * 0.11f);
                Quaternion r;
                switch (g)
                {
                    case Garnish.LimeWedge: r = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 90f); p.y += 0.01f; break;
                    case Garnish.Mint: r = Quaternion.Euler(80f, (float)rnd.NextDouble() * 360f, 0); p.y += 0.008f; break;
                    case Garnish.Olive: r = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0); p.y += 0.002f; break;
                    default: r = Quaternion.Euler((float)rnd.NextDouble() * 40f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 40f); p.y += 0.008f; break;
                }
                go.transform.SetPositionAndRotation(p, r);
                comb.Absorb(go);
            }
            comb.Build(Root, "garnish " + g);
        }

        // ------------------------------------------------------------------ back bar

        void BackBar()
        {
            var walnut = Mats.TexTint("M_Walnut", Color.white);
            var black = Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.45f);
            var top = Mats.TexTint("M_Marble", new Color(0.35f, 0.35f, 0.37f), 0.85f);
            float y = Layout.BackY;
            // cabinet
            Slab("back cabinet", new Vector3(0, (y - 0.04f) * 0.5f + 0.02f, -1.86f), new Vector3(6.5f, y - 0.04f, 0.58f), black, 0.006f);
            for (int i = 0; i < 9; i++)
            {
                float x = -2.86f + i * 0.715f;
                Slab("door", new Vector3(x, 0.48f, -1.565f), new Vector3(0.68f, 0.82f, 0.02f), walnut, 0.006f, false);
                Props.Part("handle", Root, MeshGen.RoundedBox(new Vector3(0.18f, 0.012f, 0.016f), 0.005f, 2), Mats.Brass, new Vector3(x, 0.82f, -1.55f));
            }
            Slab("back top", new Vector3(0, y - 0.02f, -1.86f), new Vector3(6.56f, 0.04f, 0.62f), top, 0.012f, true, true);
            // bottle steps
            Slab("step 1", new Vector3(0, y + 0.07f, -1.83f), new Vector3(2.3f, 0.14f, 0.2f), walnut, 0.008f, true, true);
            Slab("step 2", new Vector3(0, y + 0.14f, -2.02f), new Vector3(2.3f, 0.28f, 0.18f), walnut, 0.008f, true, true);
            var stepLed = Mats.Emissive(Color.black, Kelvin(2700), 3f);
            Props.Part("step led", Root, MeshGen.RoundedBox(new Vector3(2.26f, 0.006f, 0.008f), 0.002f, 1), stepLed, new Vector3(0, y + 0.138f, -1.728f), null, null, false);
            Props.Part("step led", Root, MeshGen.RoundedBox(new Vector3(2.26f, 0.006f, 0.008f), 0.002f, 1), stepLed, new Vector3(0, y + 0.278f, -1.928f), null, null, false);

            // back wall with backlit onyx
            Slab("back wall", new Vector3(0, 1.6f, -2.2f), new Vector3(7.2f, 3.2f, 0.08f), Mats.TexTint("M_Walnut", new Color(0.55f, 0.5f, 0.48f)), 0.01f);
            var onyxTex = Mats.Tex("M_Onyx");
            var onyx = Props.Part("onyx", Root, MeshGen.RoundedBox(new Vector3(5.4f, 1.5f, 0.02f), 0.004f, 1, 1.4f), onyxTex, new Vector3(0, 2.05f, -2.15f));
            onyx.isStatic = true;
            var frame = new MeshBuilder();
            MeshGen.RoundedBoxInto(frame, new Vector3(5.48f, 0.04f, 0.04f), 0.01f, 2, 1, Matrix4x4.Translate(new Vector3(0, 2.8f, -2.14f)));
            MeshGen.RoundedBoxInto(frame, new Vector3(5.48f, 0.04f, 0.04f), 0.01f, 2, 1, Matrix4x4.Translate(new Vector3(0, 1.3f, -2.14f)));
            MeshGen.RoundedBoxInto(frame, new Vector3(0.04f, 1.54f, 0.04f), 0.01f, 2, 1, Matrix4x4.Translate(new Vector3(-2.72f, 2.05f, -2.14f)));
            MeshGen.RoundedBoxInto(frame, new Vector3(0.04f, 1.54f, 0.04f), 0.01f, 2, 1, Matrix4x4.Translate(new Vector3(2.72f, 2.05f, -2.14f)));
            Props.Part("onyx frame", Root, frame.ToMesh(), Mats.Brass);
            // light from the onyx onto the bottles
            for (int i = 0; i < 3; i++) PointLight(new Vector3(-1.8f + i * 1.8f, 2.0f, -1.9f), new Color(1f, 0.65f, 0.35f), 0.45f, 2.4f);

            // glass shelves with LED strips and decorative bottles
            var shelfGlass = Mats.Glass(new Color(0.85f, 0.95f, 0.95f, 0.25f));
            float[] shelfY = { 1.66f, 2.04f, 2.42f };
            var comb = new Combiner();
            var rnd = new System.Random(42);
            var liquidMats = new Dictionary<int, Material>();
            Color[] decoLiquids = { new Color(0.72f, 0.36f, 0.08f), new Color(0.45f, 0.2f, 0.06f), new Color(0.93f, 0.96f, 1f), new Color(0.85f, 0.1f, 0.12f), new Color(0.98f, 0.85f, 0.4f), new Color(0.2f, 0.5f, 0.25f), new Color(0.6f, 0.15f, 0.35f) };
            float[] decoAlpha = { 0.75f, 0.86f, 0.06f, 0.82f, 0.4f, 0.7f, 0.8f };
            Color[] glassTints = { new Color(0.92f, 0.96f, 0.98f, 0.18f), new Color(0.25f, 0.55f, 0.38f, 0.5f), new Color(0.45f, 0.25f, 0.1f, 0.55f), new Color(0.95f, 0.97f, 1f, 0.42f) };
            Color[] labelCols = { new Color(0.95f, 0.92f, 0.85f), new Color(0.08f, 0.08f, 0.1f), new Color(0.55f, 0.08f, 0.1f), new Color(0.85f, 0.7f, 0.4f), new Color(0.1f, 0.2f, 0.35f) };
            BottleStyle[] styles = { BottleStyle.Classic, BottleStyle.Shoulder, BottleStyle.Slim, BottleStyle.Wine, BottleStyle.Flask, BottleStyle.Square, BottleStyle.Classic };
            foreach (float sy in shelfY)
            {
                Slab("shelf", new Vector3(0, sy - 0.006f, -2.06f), new Vector3(5.2f, 0.012f, 0.22f), shelfGlass, 0.004f, false, false, null, false);
                Props.Part("shelf led", Root, MeshGen.RoundedBox(new Vector3(5.1f, 0.005f, 0.008f), 0.002f, 1), stepLed, new Vector3(0, sy - 0.016f, -1.96f), null, null, false);
                float x = -2.5f;
                while (x < 2.5f)
                {
                    var st = styles[rnd.Next(styles.Length)];
                    int li = rnd.Next(decoLiquids.Length);
                    if (!liquidMats.TryGetValue(li, out var lm))
                    {
                        lm = Mats.Liquid(new Color(decoLiquids[li].r, decoLiquids[li].g, decoLiquids[li].b, Mathf.Lerp(0.04f, 0.95f, decoAlpha[li])));
                        liquidMats[li] = lm;
                    }
                    var lab = Mats.Lit(labelCols[rnd.Next(labelCols.Length)], 0.35f);
                    var cap = rnd.Next(3) == 0 ? Mats.Gold : rnd.Next(2) == 0 ? Mats.BlackMetal : Mats.Chrome;
                    var go = new GameObject("deco");
                    Props.BottleVisual(go.transform, st, glassTints[rnd.Next(glassTints.Length)], decoLiquids[li], decoAlpha[li], lab, cap, false, true, lm);
                    float w = st == BottleStyle.Shoulder || st == BottleStyle.Flask ? 0.11f : 0.085f;
                    go.transform.SetPositionAndRotation(new Vector3(x + w * 0.5f, sy, -2.06f + ((float)rnd.NextDouble() - 0.5f) * 0.04f), Quaternion.Euler(0, ((float)rnd.NextDouble() - 0.5f) * 30f, 0));
                    comb.Absorb(go);
                    x += w + 0.02f + (float)rnd.NextDouble() * 0.03f;
                }
            }
            comb.Build(Root, "deco bottles");

            // the real bottles on the steps
            var front = new List<string> { "lime", "lemon", "orange", "cranberry", "pineapple", "simple", "grenadine", "agave", "coconut", "bitters" };
            var mid = new List<string> { "vodka", "gin", "rum", "darkrum", "tequila", "bourbon", "cola", "tonic", "soda", "gingerbeer" };
            var back = new List<string> { "triplesec", "curacao", "campari", "aperol", "coffeeliq", "sweetvermouth", "dryvermouth", "prosecco" };
            Row(front, y, -1.64f, 0.122f);
            Row(mid, y + 0.14f, -1.83f, 0.125f);
            Row(back, y + 0.28f, -2.02f, 0.13f);

            // neon sign
            var neonTex = TextTex.Neon("Skyline", "ROOFTOP  ·  BAR  ·  LOUNGE", 1024, 384);
            var neonMat = Mats.EmissiveMap(neonTex, new Color(1f, 0.42f, 0.6f), 5f, Vector2.one);
            Props.Part("neon", Root, MeshGen.Quad(1.6f, 0.6f), neonMat, new Vector3(0, 2.9f, -2.15f), Quaternion.Euler(0, 180f, 0), null, false);
            var glow = PointLight(new Vector3(0, 2.9f, -1.9f), new Color(1f, 0.4f, 0.6f), 0.6f, 2.5f);
            _ = glow;

            // espresso machine (decor) on the left
            EspressoMachine(new Vector3(-2.35f, y, -1.86f));
            // register, tip jar and a menu board on the right
            Register(new Vector3(2.0f, y, -1.86f));
            var board = TextTex.MenuBoard(Recipes.All);
            var bm = Mats.Label(board, 0.2f);
            var bgo = Props.Part("menu board", Root, MeshGen.Quad(0.42f, 0.63f), bm, new Vector3(2.75f, y + 0.36f, -1.98f), Quaternion.Euler(-8f, 180f, 0));
            Props.Part("menu frame", bgo.transform, MeshGen.RoundedBox(new Vector3(0.45f, 0.66f, 0.015f), 0.006f, 2), walnut, new Vector3(0, 0, 0.009f));
        }

        void Row(List<string> keys, float y, float z, float spacing)
        {
            float start = -(keys.Count - 1) * spacing * 0.5f;
            for (int i = 0; i < keys.Count; i++)
            {
                var ing = Ingredients.Get(keys[i]);
                var b = Props.Bottle(ing);
                b.transform.SetPositionAndRotation(new Vector3(start + i * spacing, y, z), Quaternion.identity);
                b.SetHome();
            }
        }

        void EspressoMachine(Vector3 p)
        {
            var chrome = Mats.Chrome;
            var black = Mats.Lit(new Color(0.06f, 0.06f, 0.065f), 0.8f);
            var body = Props.Part("espresso", Root, MeshGen.RoundedBox(new Vector3(0.62f, 0.42f, 0.44f), 0.04f, 4), chrome, p + new Vector3(0, 0.21f, 0));
            Props.Part("panel", body.transform, MeshGen.RoundedBox(new Vector3(0.58f, 0.2f, 0.02f), 0.02f, 3), black, new Vector3(0, 0.06f, 0.215f));
            for (int i = 0; i < 2; i++)
            {
                var x = -0.14f + i * 0.28f;
                Props.Part("group", body.transform, MeshGen.Cylinder(0.035f, 0.06f, 24, 0.005f), chrome, new Vector3(x, -0.14f, 0.24f));
                Props.Part("handle", body.transform, MeshGen.Tube(new List<Vector3> { new Vector3(x, -0.13f, 0.26f), new Vector3(x, -0.135f, 0.4f) }, 0.01f, 10), black);
            }
            Props.Part("gauge", body.transform, MeshGen.Cylinder(0.03f, 0.01f, 24, 0.002f), Mats.Emissive(new Color(0.9f, 0.9f, 0.85f), new Color(1f, 0.9f, 0.7f), 0.6f), new Vector3(0, 0.1f, 0.226f), Quaternion.Euler(90, 0, 0));
            // drip tray in front of the group heads
            Props.Part("drip tray", Root, MeshGen.RoundedBox(new Vector3(0.6f, 0.012f, 0.16f), 0.004f, 2), chrome, p + new Vector3(0, 0.006f, 0.27f));
            var grate = new MeshBuilder();
            for (int i = 0; i < 22; i++) MeshGen.RoundedBoxInto(grate, new Vector3(0.006f, 0.003f, 0.14f), 0.001f, 1, 1, Matrix4x4.Translate(new Vector3(-0.27f + i * 0.0257f, 0.0135f, 0)));
            Props.Part("grate", Root, grate.ToMesh(), Mats.BlackMetal, p + new Vector3(0, 0, 0.27f));
            var surf = Slab("tray surface", p + new Vector3(0, 0.0125f, 0.27f), new Vector3(0.6f, 0.003f, 0.16f), chrome, 0.001f, true, true, null, false);
            surf.GetComponent<MeshRenderer>().enabled = false;
            // the machine works: click it to pull a shot into the cup under the right group
            var em = body.AddComponent<EspressoMachine>();
            body.AddComponent<BoxCollider>().size = new Vector3(0.62f, 0.42f, 0.44f);
            em.CupSpot = p + new Vector3(0.14f, 0.015f, 0.27f);
            em.Spout = p + new Vector3(0.14f, 0.21f - 0.19f + 0.05f, 0.27f);
            for (int i = 0; i < 5; i++)
            {
                var cup = MeshGen.Lathe(new List<LP> { new LP(0, 0), new LP(0.025f, 0), new LP(0.032f, 0.05f), new LP(0.029f, 0.05f), new LP(0.022f, 0.006f), new LP(0, 0.006f) }, 24);
                Props.Part("cup", Root, cup, Mats.Lit(new Color(0.95f, 0.94f, 0.9f), 0.85f), p + new Vector3(-0.22f + i * 0.11f, 0.42f, -0.05f));
            }
        }

        void Register(Vector3 p)
        {
            var black = Mats.Lit(new Color(0.05f, 0.05f, 0.055f), 0.7f);
            Props.Part("pos base", Root, MeshGen.RoundedBox(new Vector3(0.24f, 0.05f, 0.2f), 0.012f, 3), black, p + new Vector3(0, 0.025f, 0));
            Props.Part("pos neck", Root, MeshGen.Cylinder(0.02f, 0.18f, 16, 0.004f), Mats.Chrome, p + new Vector3(0, 0.05f, -0.02f));
            var screen = Props.Part("pos screen", Root, MeshGen.RoundedBox(new Vector3(0.3f, 0.2f, 0.02f), 0.01f, 3), black, p + new Vector3(0, 0.3f, 0), Quaternion.Euler(-20f, 180f, 0));
            Props.Part("pos display", screen.transform, MeshGen.Quad(0.27f, 0.17f), Mats.Emissive(new Color(0.05f, 0.08f, 0.12f), new Color(0.35f, 0.55f, 0.9f), 0.8f), new Vector3(0, 0, -0.011f), Quaternion.identity, null, false);
            // tip jar
            var jar = MeshGen.Lathe(new List<LP> { new LP(0, 0), new LP(0.05f, 0), new LP(0.055f, 0.01f), new LP(0.055f, 0.13f), new LP(0.045f, 0.15f), new LP(0.04f, 0.16f), new LP(0.038f, 0.155f), new LP(0.051f, 0.13f), new LP(0.051f, 0.012f), new LP(0, 0.006f) }, 40);
            Props.Part("tip jar", Root, jar, Mats.ClearGlass, p + new Vector3(0.36f, 0, 0.06f));
            var coins = new Combiner();
            var coin = MeshGen.Cylinder(0.011f, 0.002f, 16, 0.0005f);
            var rnd = new System.Random(3);
            for (int i = 0; i < 40; i++)
                coins.Add(coin, Mats.Gold, Matrix4x4.TRS(p + new Vector3(0.36f + ((float)rnd.NextDouble() - 0.5f) * 0.07f, 0.008f + (float)rnd.NextDouble() * 0.04f, 0.06f + ((float)rnd.NextDouble() - 0.5f) * 0.07f), Quaternion.Euler((float)rnd.NextDouble() * 60f, (float)rnd.NextDouble() * 360f, 0), Vector3.one));
            coins.Build(Root, "coins");
        }

        // ------------------------------------------------------------------ canopy with pendants

        void Canopy()
        {
            var walnut = Mats.TexTint("M_Walnut", new Color(0.85f, 0.8f, 0.75f));
            var black = Mats.BlackMetal;
            Slab("roof", new Vector3(0, 3.28f, -0.4f), new Vector3(7.6f, 0.12f, 4.0f), Mats.Lit(new Color(0.05f, 0.05f, 0.055f), 0.3f), 0.02f, false);
            var slats = new MeshBuilder();
            for (int i = 0; i < 52; i++)
            {
                float z = -2.3f + i * 0.075f;
                MeshGen.RoundedBoxInto(slats, new Vector3(7.5f, 0.05f, 0.04f), 0.008f, 2, 1f, Matrix4x4.Translate(new Vector3(0, 3.19f, z)));
            }
            Props.Part("slats", Root, slats.ToMesh("slats"), walnut).isStatic = true;
            // fascia with LED
            Slab("fascia", new Vector3(0, 3.22f, 1.6f), new Vector3(7.7f, 0.22f, 0.08f), black, 0.01f, false);
            Props.Part("fascia led", Root, MeshGen.RoundedBox(new Vector3(7.6f, 0.01f, 0.01f), 0.003f, 1), Mats.Emissive(Color.black, Kelvin(2400), 5f), new Vector3(0, 3.1f, 1.64f), null, null, false);
            // posts
            foreach (var x in new[] { -3.75f, 3.75f })
                foreach (var z in new[] { -2.25f, 1.55f })
                    Slab("post", new Vector3(x, 1.6f, z), new Vector3(0.1f, 3.2f, 0.1f), black, 0.02f);
            // pendant lamps over the bar
            var cord = Mats.Lit(new Color(0.02f, 0.02f, 0.02f), 0.3f);
            var globe = Mats.Emissive(new Color(1f, 0.95f, 0.88f), Kelvin(2200), 2.2f);
            var capMesh = MeshGen.Lathe(new List<LP> { new LP(0, 0.07f), new LP(0.02f, 0.07f), new LP(0.035f, 0.04f), new LP(0.05f, 0.005f), new LP(0.048f, 0f), new LP(0, 0.002f) }, 32, 0.2f);
            var globeMesh = MeshGen.Sphere(0.1f, 40, 20);
            for (int i = 0; i < Layout.SeatX.Length; i++)
            {
                float x = Layout.SeatX[i];
                var p = new Vector3(x, 2.25f, 0.32f);
                Props.Part("cord", Root, MeshGen.Tube(new List<Vector3> { p + Vector3.up * 0.15f, new Vector3(x, 3.17f, 0.32f) }, 0.004f, 6), cord, Vector3.zero, null, null, false);
                Props.Part("cap", Root, capMesh, Mats.Brass, p + Vector3.up * 0.085f);
                Props.Part("globe", Root, globeMesh, globe, p, null, null, false);
                var spot = SpotLight(p - Vector3.up * 0.09f, Vector3.down, Kelvin(2200), 2.4f, 3.8f, 125f, true);
                spot.innerSpotAngle = 60f;
                ShadowLights.Add(spot);
                PointLight(p + Vector3.up * 0.02f, Kelvin(2200), 0.55f, 3.2f);
                LightCone(p - Vector3.up * 0.1f, Layout.BarTopY + 0.02f);
                Dust(p - Vector3.up * 0.5f);
            }
            // task lights over the work station and back bar (these cast the nice item shadows)
            ShadowLights.Add(SpotLight(new Vector3(-1.4f, 3.1f, -0.9f), new Vector3(0, -1f, 0.25f), Kelvin(3200), 3.2f, 5f, 64f, true));
            ShadowLights.Add(SpotLight(new Vector3(1.4f, 3.1f, -0.9f), new Vector3(0, -1f, 0.25f), Kelvin(3200), 3.2f, 5f, 64f, true));
            SpotLight(new Vector3(0f, 3.1f, -1.1f), new Vector3(0, -1f, -0.45f), Kelvin(3200), 2.2f, 4f, 80f, false);
            foreach (var x in new[] { -1.4f, 1.4f })
                Props.Part("spot can", Root, MeshGen.Cylinder(0.05f, 0.08f, 24, 0.004f), black, new Vector3(x, 3.08f, -0.9f));
        }

        /// <summary>Soft visible beam under a lamp: a camera-facing quad with a cone gradient.</summary>
        void LightCone(Vector3 top, float bottomY)
        {
            if (coneTex == null)
            {
                const int W = 128, H = 256;
                coneTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "cone" };
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    float v = (float)y / (H - 1);           // 0 bottom .. 1 top
                    float half = Mathf.Lerp(0.5f, 0.07f, v);
                    float fade = Mathf.Pow(v, 1.6f) * Mathf.SmoothStep(0f, 0.25f, 1f - v + 0.02f) + 0.15f * v;
                    for (int x = 0; x < W; x++)
                    {
                        float u = Mathf.Abs((x + 0.5f) / W - 0.5f);
                        float edge = Mathf.Clamp01(1f - u / half);
                        float a = edge * edge * (3f - 2f * edge) * fade;
                        px[y * W + x] = new Color(1, 1, 1, a);
                    }
                }
                coneTex.SetPixels(px);
                coneTex.Apply();
            }
            float h = top.y - bottomY;
            var mat = Mats.Additive(new Color(1f, 0.74f, 0.45f, 0.42f), coneTex);
            var go = Props.Part("light cone", Root, MeshGen.Quad(1.25f, h), mat, top - Vector3.up * h * 0.5f, null, null, false);
            cones.Add(go.transform);
        }

        /// <summary>A few dust motes drifting in the lamp light.</summary>
        void Dust(Vector3 c)
        {
            var go = new GameObject("dust");
            go.transform.SetParent(Root, false);
            go.transform.position = c;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = 0.01f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.002f, 0.005f);
            main.startColor = new Color(1f, 0.85f, 0.6f, 0.5f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 5f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.7f, 0.9f, 0.6f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.02f; noise.frequency = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Additive(new Color(1f, 0.85f, 0.6f, 0.6f), Mats.SoftDot);
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
        }

        /// <summary>Lamp shadows are the costliest part of the lighting: off on Low.</summary>
        public void ApplyQuality(int q)
        {
            foreach (var l in ShadowLights) if (l != null) l.shadows = q >= 1 ? LightShadows.Soft : LightShadows.None;
        }

        // ------------------------------------------------------------------ stools

        void Stools()
        {
            var chrome = Mats.Chrome;
            var leather = Mats.TexTint("M_Leather", new Color(0.42f, 0.16f, 0.1f), 0.5f);
            var seat = MeshGen.Lathe(new List<LP>
            {
                new LP(0, 0), new LP(0.17f, 0), new LP(0.2f, 0.015f), new LP(0.215f, 0.04f), new LP(0.21f, 0.065f), new LP(0.18f, 0.085f), new LP(0.08f, 0.095f), new LP(0, 0.096f),
            }, 48, 0.3f);
            var basePl = MeshGen.Lathe(new List<LP> { new LP(0, 0), new LP(0.24f, 0), new LP(0.245f, 0.008f), new LP(0.2f, 0.022f), new LP(0.04f, 0.04f), new LP(0, 0.04f) }, 48, 0.3f);
            var ring = MeshGen.Torus(0.19f, 0.011f, 48, 10);
            for (int i = 0; i < Layout.SeatX.Length; i++)
            {
                var p = new Vector3(Layout.SeatX[i], 0, Layout.StoolZ);
                Props.Part("stool base", Root, basePl, chrome, p);
                Props.Part("stool pole", Root, MeshGen.Cylinder(0.028f, 0.68f, 24, 0.002f), chrome, p + Vector3.up * 0.03f);
                Props.Part("stool ring", Root, ring, chrome, p + Vector3.up * 0.32f);
                Props.Part("stool seat", Root, seat, leather, p + Vector3.up * 0.7f);
            }
        }

        // ------------------------------------------------------------------ lounge

        void Lounge()
        {
            var velvet = Mats.TexTint("M_Fabric", new Color(0.12f, 0.3f, 0.25f), 0.25f);
            var cream = Mats.TexTint("M_Fabric", new Color(0.78f, 0.72f, 0.62f), 0.2f);
            var rust = Mats.TexTint("M_Fabric", new Color(0.55f, 0.22f, 0.12f), 0.25f);
            var marble = Mats.TexTint("M_Marble", Color.white);
            Sofa(new Vector3(-6f, 0, 6f), 90f, velvet);
            Sofa(new Vector3(-3.2f, 0, 8.8f), 180f, cream);
            Sofa(new Vector3(4.2f, 0, 6.5f), -90f, velvet);
            Sofa(new Vector3(6.8f, 0, 10.5f), 180f, rust);
            Chair(new Vector3(-3.4f, 0, 4.6f), 20f, rust);
            Chair(new Vector3(-1.8f, 0, 5.2f), -25f, cream);
            Chair(new Vector3(1.6f, 0, 6.0f), 160f, cream);
            Chair(new Vector3(2.6f, 0, 4.4f), 200f, rust);
            CoffeeTable(new Vector3(-3.6f, 0, 6.4f), marble);
            CoffeeTable(new Vector3(2.2f, 0, 5.4f), marble);
            CoffeeTable(new Vector3(6.6f, 0, 8.6f), marble);
            FireTable(new Vector3(0, 0, 10.2f));
            // planters with tall grass along the edge
            for (int i = 0; i < 7; i++) Planter(new Vector3(-12f + i * 4f, 0, 13.2f), 0f);
            Planter(new Vector3(-13.2f, 0, 8f), 90f);
            Planter(new Vector3(13.2f, 0, 8f), 90f);
            Planter(new Vector3(13.2f, 0, 3f), 90f);
            OliveTree(new Vector3(-8.2f, 0, 0.2f));
            OliveTree(new Vector3(8.5f, 0, 1.2f));
            OliveTree(new Vector3(10.5f, 0, 11.5f));
            StringLights();
        }

        void Sofa(Vector3 p, float yaw, Material fabric)
        {
            var g = new GameObject("sofa").transform;
            g.SetParent(Root, false);
            g.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
            var dark = Mats.Lit(new Color(0.03f, 0.03f, 0.03f), 0.4f);
            Props.Part("base", g, BoxMesh(new Vector3(2.2f, 0.28f, 0.9f), 0.05f, 4), fabric, new Vector3(0, 0.2f, 0));
            Props.Part("back", g, BoxMesh(new Vector3(2.2f, 0.5f, 0.24f), 0.1f, 5), fabric, new Vector3(0, 0.55f, -0.34f), Quaternion.Euler(-8f, 0, 0));
            Props.Part("arm", g, BoxMesh(new Vector3(0.22f, 0.36f, 0.9f), 0.09f, 5), fabric, new Vector3(-1.04f, 0.45f, 0));
            Props.Part("arm", g, BoxMesh(new Vector3(0.22f, 0.36f, 0.9f), 0.09f, 5), fabric, new Vector3(1.04f, 0.45f, 0));
            for (int i = 0; i < 2; i++)
                Props.Part("cushion", g, BoxMesh(new Vector3(0.9f, 0.14f, 0.7f), 0.06f, 5), fabric, new Vector3(-0.47f + i * 0.94f, 0.4f, 0.05f));
            for (int i = 0; i < 4; i++)
                Props.Part("leg", g, MeshGen.Cylinder(0.025f, 0.07f, 12, 0.004f), dark, new Vector3(i % 2 == 0 ? -1f : 1f, 0, i < 2 ? -0.35f : 0.35f));
            var bc = g.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.4f, 0); bc.size = new Vector3(2.2f, 0.8f, 0.9f);
        }

        void Chair(Vector3 p, float yaw, Material fabric)
        {
            var g = new GameObject("chair").transform;
            g.SetParent(Root, false);
            g.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
            var seat = MeshGen.Lathe(new List<LP> { new LP(0, 0.25f), new LP(0.38f, 0.25f), new LP(0.42f, 0.3f), new LP(0.41f, 0.4f), new LP(0.3f, 0.43f), new LP(0, 0.44f) }, 48, 0.5f);
            Props.Part("seat", g, seat, fabric);
            var back = MeshGen.Lathe(new List<LP> { new LP(0.36f, 0.35f), new LP(0.42f, 0.42f), new LP(0.43f, 0.7f), new LP(0.4f, 0.76f), new LP(0.36f, 0.72f), new LP(0.35f, 0.45f) }, 40, 0.5f, 200f, 340f);
            Props.Part("back", g, back, fabric);
            Props.Part("base", g, MeshGen.Lathe(new List<LP> { new LP(0, 0), new LP(0.3f, 0), new LP(0.31f, 0.01f), new LP(0.05f, 0.03f), new LP(0.04f, 0.25f), new LP(0, 0.25f) }, 32, 0.3f), Mats.Brass);
        }

        void CoffeeTable(Vector3 p, Material top)
        {
            Props.Part("table top", Root, MeshGen.Cylinder(0.45f, 0.035f, 64, 0.012f, 1f), top, p + Vector3.up * 0.42f);
            Props.Part("table base", Root, MeshGen.Lathe(new List<LP> { new LP(0, 0), new LP(0.3f, 0), new LP(0.3f, 0.012f), new LP(0.06f, 0.04f), new LP(0.035f, 0.42f), new LP(0, 0.42f) }, 40, 0.3f), Mats.Brass, p);
            Candle(p + Vector3.up * 0.455f + new Vector3(0.1f, 0, 0.05f));
            // a finished cocktail on the table for atmosphere
            var g = Props.Glass(Random.value < 0.5f ? GlassType.Coupe : GlassType.Rocks);
            var r = Recipes.All[Random.Range(0, Recipes.All.Count)];
            foreach (var part in r.Parts) g.C.Add(part.ing, part.ml * 0.6f);
            g.transform.position = p + Vector3.up * 0.455f + new Vector3(-0.15f, 0, -0.08f);
            g.Locked = true;
            g.SetColliders(false);
        }

        void Candle(Vector3 p)
        {
            var votive = MeshGen.Lathe(new List<LP> { new LP(0, 0), new LP(0.035f, 0), new LP(0.038f, 0.008f), new LP(0.038f, 0.07f), new LP(0.035f, 0.072f), new LP(0.034f, 0.008f), new LP(0, 0.006f) }, 32);
            Props.Part("votive", Root, votive, Mats.Glass(new Color(1f, 0.85f, 0.65f, 0.25f)), p);
            Props.Part("wax", Root, MeshGen.Cylinder(0.03f, 0.03f, 24, 0.003f), Mats.Lit(new Color(0.95f, 0.92f, 0.85f), 0.3f), p + Vector3.up * 0.006f);
            var flame = Props.Part("flame", Root, MeshGen.Sphere(0.006f, 12, 8), Mats.Emissive(Color.black, new Color(1f, 0.6f, 0.2f), 9f), p + Vector3.up * 0.046f, null, new Vector3(0.8f, 1.8f, 0.8f), false);
            flames.Add(flame.transform);
            var l = PointLight(p + Vector3.up * 0.08f, Kelvin(1900), 0.55f, 1.8f);
            Flicker.Add(l);
            flickerBase.Add(l.intensity);
        }

        void FireTable(Vector3 p)
        {
            var conc = Mats.TexTint("M_Concrete", new Color(0.6f, 0.58f, 0.55f));
            Slab("fire table", p + Vector3.up * 0.22f, new Vector3(1.8f, 0.44f, 0.9f), conc, 0.04f);
            Slab("fire glass", p + new Vector3(0, 0.55f, 0), new Vector3(1.0f, 0.22f, 0.4f), Mats.Glass(new Color(0.9f, 0.95f, 1f, 0.12f)), 0.006f, false, false, null, false);
            Slab("burner", p + new Vector3(0, 0.445f, 0), new Vector3(0.96f, 0.01f, 0.36f), Mats.Lit(new Color(0.04f, 0.04f, 0.04f), 0.2f), 0.003f, false);
            // flames: a particle system of additive soft blobs
            var go = new GameObject("flames");
            go.transform.SetParent(Root, false);
            go.transform.position = p + new Vector3(0, 0.46f, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(1f, 0.55f, 0.15f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var em = ps.emission; em.rateOverTime = 110f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.85f, 0.01f, 0.2f);
            sh.rotation = new Vector3(-90f, 0, 0);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0.2f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0), new GradientColorKey(new Color(1f, 0.4f, 0.08f), 0.5f), new GradientColorKey(new Color(0.6f, 0.1f, 0.02f), 1) },
                       new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = gr;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Additive(new Color(2.5f, 1.4f, 0.6f, 1f), Mats.SoftDot);
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            var l = PointLight(p + Vector3.up * 0.8f, Kelvin(1900), 2.2f, 6f);
            Flicker.Add(l);
            flickerBase.Add(l.intensity);
        }

        void Planter(Vector3 p, float yaw)
        {
            var conc = Mats.TexTint("M_Concrete", new Color(0.3f, 0.3f, 0.32f));
            var g = new GameObject("planter").transform;
            g.SetParent(Root, false);
            g.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
            Props.Part("box", g, BoxMesh(new Vector3(3.2f, 0.55f, 0.6f), 0.03f, 3), conc, new Vector3(0, 0.275f, 0));
            Props.Part("soil", g, BoxMesh(new Vector3(3.1f, 0.02f, 0.5f), 0.005f, 1), Mats.Lit(new Color(0.06f, 0.045f, 0.03f), 0.1f), new Vector3(0, 0.53f, 0));
            var grass = new MeshBuilder();
            var rnd = new System.Random((int)(p.x * 10 + p.z * 7));
            for (int i = 0; i < 260; i++)
            {
                var basePos = new Vector3(((float)rnd.NextDouble() - 0.5f) * 3.0f, 0.53f, ((float)rnd.NextDouble() - 0.5f) * 0.45f);
                float len = 0.45f + (float)rnd.NextDouble() * 0.55f;
                var tr = Matrix4x4.TRS(basePos, Quaternion.Euler(-70f - (float)rnd.NextDouble() * 15f, (float)rnd.NextDouble() * 360f, 0), Vector3.one);
                MeshGen.LeafInto(grass, tr, len, 0.014f, 0.35f + (float)rnd.NextDouble() * 0.3f, 0.002f, 6, 1);
            }
            Props.Part("grass", g, grass.ToMesh("grass"), Mats.Lit(new Color(0.28f, 0.36f, 0.16f), 0.3f), Vector3.zero, null, null, true);
            var bc = g.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.3f, 0); bc.size = new Vector3(3.2f, 0.6f, 0.6f);
        }

        void OliveTree(Vector3 p)
        {
            var pot = MeshGen.Lathe(new List<LP> { new LP(0, 0.02f), new LP(0.28f, 0), new LP(0.36f, 0.6f), new LP(0.38f, 0.62f), new LP(0.34f, 0.62f), new LP(0.33f, 0.58f), new LP(0, 0.58f) }, 48, 1f);
            Props.Part("pot", Root, pot, Mats.TexTint("M_Concrete", new Color(0.75f, 0.72f, 0.68f)), p);
            var bark = Mats.Lit(new Color(0.22f, 0.18f, 0.14f), 0.2f);
            var trunk = new MeshBuilder();
            var rnd = new System.Random((int)(p.x * 13));
            var path = new List<Vector3>();
            for (int i = 0; i <= 8; i++) path.Add(p + new Vector3(Mathf.Sin(i * 0.7f) * 0.06f, 0.55f + i * 0.2f, Mathf.Cos(i * 0.5f) * 0.05f));
            MeshGen.TubeInto(trunk, path, 0.06f, 10, true, 1f, 0.3f, t => Mathf.Lerp(1f, 0.45f, t));
            var tips = new List<Vector3>();
            for (int b = 0; b < 5; b++)
            {
                var start = path[4 + b % 4];
                var dir = new Vector3((float)rnd.NextDouble() - 0.5f, 0.8f, (float)rnd.NextDouble() - 0.5f).normalized;
                var bp = new List<Vector3> { start, start + dir * 0.35f, start + dir * 0.7f + Vector3.up * 0.1f };
                MeshGen.TubeInto(trunk, bp, 0.025f, 8, true, 1f, 0.3f, t => Mathf.Lerp(1f, 0.4f, t));
                tips.Add(bp[2]);
            }
            Props.Part("trunk", Root, trunk.ToMesh("trunk"), bark);
            var leaves = new MeshBuilder();
            foreach (var tip in tips)
                for (int i = 0; i < 140; i++)
                {
                    var off = Random.insideUnitSphere * 0.45f;
                    off.y *= 0.7f;
                    var tr = Matrix4x4.TRS(tip + off, Quaternion.Euler((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, 0), Vector3.one);
                    MeshGen.LeafInto(leaves, tr, 0.07f, 0.014f, 0.1f, 0.002f, 3, 1);
                }
            Props.Part("leaves", Root, leaves.ToMesh("leaves"), Mats.Lit(new Color(0.24f, 0.3f, 0.2f), 0.35f));
            var bc = new GameObject("tree col").AddComponent<CapsuleCollider>();
            bc.transform.SetParent(Root, false);
            bc.transform.position = p + Vector3.up * 0.9f;
            bc.radius = 0.4f; bc.height = 1.8f;
        }

        void StringLights()
        {
            var black = Mats.BlackMetal;
            var bulbMat = Mats.Emissive(new Color(1f, 0.9f, 0.7f), Kelvin(2200), 7f);
            var wire = new MeshBuilder();
            var bulbs = new MeshBuilder();
            var bulb = MeshGen.Sphere(0.028f, 14, 8);
            // poles across the lounge
            Vector3[] poles = { new Vector3(-11f, 0, 4f), new Vector3(-11f, 0, 12.5f), new Vector3(11f, 0, 4f), new Vector3(11f, 0, 12.5f), new Vector3(0, 0, 13f) };
            foreach (var pl in poles)
                Slab("pole", pl + Vector3.up * 1.65f, new Vector3(0.07f, 3.3f, 0.07f), black, 0.03f);
            var anchors = new List<(Vector3, Vector3)>
            {
                (new Vector3(-3.8f, 3.1f, 1.6f), new Vector3(-11f, 3.25f, 12.5f)),
                (new Vector3(-1.5f, 3.1f, 1.6f), new Vector3(0f, 3.25f, 13f)),
                (new Vector3(1.5f, 3.1f, 1.6f), new Vector3(0f, 3.25f, 13f)),
                (new Vector3(3.8f, 3.1f, 1.6f), new Vector3(11f, 3.25f, 12.5f)),
                (new Vector3(-11f, 3.25f, 4f), new Vector3(11f, 3.25f, 4f)),
                (new Vector3(-11f, 3.25f, 12.5f), new Vector3(0f, 3.25f, 13f)),
                (new Vector3(0f, 3.25f, 13f), new Vector3(11f, 3.25f, 12.5f)),
                (new Vector3(-11f, 3.25f, 4f), new Vector3(-3.8f, 3.1f, 1.6f)),
                (new Vector3(11f, 3.25f, 4f), new Vector3(3.8f, 3.1f, 1.6f)),
            };
            int li = 0;
            foreach (var (a, b) in anchors)
            {
                float len = Vector3.Distance(a, b);
                var pts = MeshGen.Catenary(a, b, len * 0.06f, Mathf.Max(8, (int)(len * 3)));
                MeshGen.TubeInto(wire, pts, 0.004f, 5, false);
                int count = Mathf.RoundToInt(len / 0.55f);
                for (int i = 1; i < count; i++)
                {
                    float t = (float)i / count;
                    var p = Vector3.Lerp(a, b, t);
                    p.y -= len * 0.06f * 4f * t * (1 - t) + 0.05f;
                    bulbs.Append(bulb, Matrix4x4.TRS(p, Quaternion.identity, new Vector3(1f, 1.25f, 1f)));
                    if (i % 6 == 3) { PointLight(p, Kelvin(2200), 0.55f, 5f); li++; }
                }
            }
            Props.Part("wires", Root, wire.ToMesh("wires"), Mats.Lit(new Color(0.02f, 0.02f, 0.02f), 0.3f), Vector3.zero, null, null, false);
            Props.Part("bulbs", Root, bulbs.ToMesh("bulbs"), bulbMat, Vector3.zero, null, null, false);
        }

        void Balustrade()
        {
            var glass = Mats.Glass(new Color(0.75f, 0.85f, 0.9f, 0.1f));
            var brass = Mats.Brass;
            void Run(Vector3 a, Vector3 b)
            {
                var mid = (a + b) * 0.5f;
                var dir = b - a;
                float len = dir.magnitude;
                var rot = Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir.normalized));
                var go = Props.Part("glass rail", Root, BoxMesh(new Vector3(len, 1.05f, 0.015f), 0.004f, 1), glass, mid + Vector3.up * 0.55f, rot, null, false);
                var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(len, 1.05f, 0.05f);
                Props.Part("top rail", Root, MeshGen.Tube(new List<Vector3> { a + Vector3.up * 1.1f, b + Vector3.up * 1.1f }, 0.025f, 16), brass);
                int posts = Mathf.CeilToInt(len / 1.6f);
                for (int i = 0; i <= posts; i++)
                    Props.Part("clamp", Root, MeshGen.RoundedBox(new Vector3(0.06f, 0.1f, 0.05f), 0.01f, 2), brass, Vector3.Lerp(a, b, (float)i / posts) + Vector3.up * 0.06f, rot);
            }
            Run(new Vector3(-14.8f, 0, 14.8f), new Vector3(14.8f, 0, 14.8f));
            Run(new Vector3(-14.8f, 0, -4.8f), new Vector3(-14.8f, 0, 14.8f));
            Run(new Vector3(14.8f, 0, -4.8f), new Vector3(14.8f, 0, 14.8f));
            Run(new Vector3(-14.8f, 0, -4.8f), new Vector3(14.8f, 0, -4.8f));
        }

        void Entrance()
        {
            var conc = Mats.TexTint("M_Concrete", new Color(0.45f, 0.44f, 0.46f));
            var steel = Mats.Steel;
            Slab("lobby", new Vector3(-12.6f, 1.6f, -0.2f), new Vector3(2.8f, 3.2f, 3.6f), conc, 0.02f);
            Slab("door l", new Vector3(-11.19f, 1.15f, -0.48f), new Vector3(0.02f, 2.3f, 0.58f), steel, 0.004f, false);
            Slab("door r", new Vector3(-11.19f, 1.15f, 0.12f), new Vector3(0.02f, 2.3f, 0.58f), steel, 0.004f, false);
            Slab("door frame", new Vector3(-11.18f, 2.38f, -0.18f), new Vector3(0.03f, 0.1f, 1.3f), Mats.Brass, 0.004f, false);
            var sconce = Mats.Emissive(new Color(1f, 0.9f, 0.75f), Kelvin(2400), 3f);
            Props.Part("sconce", Root, MeshGen.RoundedBox(new Vector3(0.03f, 0.4f, 0.1f), 0.012f, 3), sconce, new Vector3(-11.18f, 2.0f, 0.75f), null, null, false);
            Props.Part("sconce", Root, MeshGen.RoundedBox(new Vector3(0.03f, 0.4f, 0.1f), 0.012f, 3), sconce, new Vector3(-11.18f, 2.0f, -1.1f), null, null, false);
            PointLight(new Vector3(-10.8f, 2.2f, -0.2f), Kelvin(2400), 1.2f, 4f);
            var sign = TextTex.Neon("Welcome", "", 768, 256);
            Props.Part("welcome", Root, MeshGen.Quad(1.1f, 0.37f), Mats.EmissiveMap(sign, new Color(1f, 0.75f, 0.45f), 3f, Vector2.one), new Vector3(-11.17f, 2.75f, -0.18f), Quaternion.Euler(0, -90f, 0), null, false);
        }

        // ------------------------------------------------------------------ sky and light

        void Sky()
        {
            var sky = Resources.Load<Material>("Materials/Sky");
            if (sky != null) RenderSettings.skybox = sky;
            // moon (front right, above the skyline)
            var moonTex = Resources.Load<Texture2D>("Textures/moon");
            var dir = new Vector3(0.42f, 0.36f, 0.83f).normalized;
            var moon = Props.Part("moon", Root, MeshGen.Quad(1, 1), Mats.UnlitTex(moonTex, new Color(2.2f, 2.15f, 2.0f, 1f), true), dir * 320f, Quaternion.LookRotation(dir), Vector3.one * 13f, false);
            _ = moon;
        }

        void Lighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.075f, 0.085f, 0.15f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.055f, 0.065f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.028f, 0.026f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0026f;
            RenderSettings.fogColor = new Color(0.1f, 0.08f, 0.115f);
            RenderSettings.reflectionIntensity = 1f;

            var moonGo = new GameObject("moonlight");
            moonGo.transform.SetParent(Root, false);
            moonGo.transform.rotation = Quaternion.LookRotation(-new Vector3(0.42f, 0.36f, 0.83f));
            var ml = moonGo.AddComponent<Light>();
            ml.type = LightType.Directional;
            ml.color = new Color(0.55f, 0.65f, 1f);
            ml.intensity = 0.16f;
            ml.shadows = LightShadows.Soft;
            ml.shadowStrength = 0.6f;
            RenderSettings.sun = ml;

            BarProbe = Probe(new Vector3(0, 1.6f, -0.6f), new Vector3(8f, 3.4f, 4.6f), 512);
            BarProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            LoungeProbe = Probe(new Vector3(0, 1.8f, 7f), new Vector3(30f, 6f, 16f), 128);
            beaconMat = Mats.Emissive(Color.black, new Color(1f, 0.1f, 0.08f), 6f);
            City.SetBeaconMaterial(beaconMat);
        }

        ReflectionProbe Probe(Vector3 p, Vector3 size, int res)
        {
            var go = new GameObject("reflection probe");
            go.transform.SetParent(Root, false);
            go.transform.position = p;
            var rp = go.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Realtime;
            rp.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            rp.resolution = res;
            rp.hdr = true;
            rp.boxProjection = true;
            rp.size = size;
            rp.nearClipPlane = 0.05f;
            rp.farClipPlane = 1500f;
            rp.clearFlags = ReflectionProbeClearFlags.Skybox;
            rp.intensity = 1f;
            return rp;
        }

        public void RefreshProbes()
        {
            if (BarProbe != null) BarProbe.RenderProbe();
            if (LoungeProbe != null) LoungeProbe.RenderProbe();
        }

        void PostProcessing()
        {
            var go = new GameObject("post fx");
            go.transform.SetParent(Root, false);
            PostFx = go.AddComponent<Volume>();
            PostFx.isGlobal = true;
            PostFx.priority = 10;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = prof.Add<Bloom>(true);
            bloom.threshold.value = 1.05f;
            bloom.intensity.value = 0.7f;
            bloom.scatter.value = 0.72f;
            bloom.highQualityFiltering.value = true;
            var tm = prof.Add<Tonemapping>(true);
            tm.mode.value = TonemappingMode.ACES;
            var ca = prof.Add<ColorAdjustments>(true);
            ca.postExposure.value = 0.3f;
            ca.contrast.value = 12f;
            ca.saturation.value = 6f;
            ca.colorFilter.value = new Color(1f, 0.97f, 0.94f);
            ca.hueShift.value = 0f;
            var wb = prof.Add<WhiteBalance>(true);
            wb.temperature.value = 4f;
            wb.tint.value = 2f;
            var vig = prof.Add<Vignette>(true);
            vig.intensity.value = 0.27f;
            vig.smoothness.value = 0.45f;
            vig.color.value = Color.black;
            var grain = prof.Add<FilmGrain>(true);
            grain.type.value = FilmGrainLookup.Thin1;
            grain.intensity.value = 0.12f;
            grain.response.value = 0.8f;
            Dof = prof.Add<DepthOfField>(true);
            Dof.mode.value = DepthOfFieldMode.Gaussian;
            Dof.gaussianStart.value = 1.2f;
            Dof.gaussianEnd.value = 6f;
            Dof.gaussianMaxRadius.value = 1.2f;
            Dof.highQualitySampling.value = true;
            Dof.active = false;
            bloom.dirtTexture.value = LensDirt();
            bloom.dirtIntensity.value = 2.2f;
            var flare = prof.Add<ScreenSpaceLensFlare>(true);
            flare.intensity.value = 0.18f;
            var split = prof.Add<SplitToning>(true);
            split.shadows.value = new Color(0.5f, 0.5f, 0.54f);
            split.highlights.value = new Color(1f, 0.84f, 0.66f);
            split.balance.value = 25f;
            var smh = prof.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.value = new Vector4(1f, 0.99f, 1.01f, 0f);
            smh.midtones.value = new Vector4(1.03f, 1f, 0.96f, 0f);
            smh.highlights.value = new Vector4(1.03f, 1f, 0.95f, 0.02f);
            var chroma = prof.Add<ChromaticAberration>(true);
            chroma.intensity.value = 0.05f;
            PostFx.sharedProfile = prof;
        }

        /// <summary>Faint smudges on the "lens" that light up around bright lights.</summary>
        static Texture2D LensDirt()
        {
            const int W = 512, H = 288;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "lens dirt" };
            var px = new Color[W * H];
            var rnd = new System.Random(8);
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 1);
            for (int b = 0; b < 90; b++)
            {
                float cx = (float)rnd.NextDouble() * W, cy = (float)rnd.NextDouble() * H;
                float r = 6f + Mathf.Pow((float)rnd.NextDouble(), 3f) * 46f;
                float k = 0.04f + (float)rnd.NextDouble() * 0.12f;
                int x0 = Mathf.Max(0, (int)(cx - r)), x1 = Mathf.Min(W - 1, (int)(cx + r)), y0 = Mathf.Max(0, (int)(cy - r)), y1 = Mathf.Min(H - 1, (int)(cy + r));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                        if (d > 1) continue;
                        float v = (1 - d * d) * k * (0.7f + 0.3f * d);
                        px[y * W + x] += new Color(v, v * 0.95f, v * 0.85f, 0);
                    }
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        // ------------------------------------------------------------------ animation

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < Flicker.Count; i++)
            {
                var l = Flicker[i];
                if (l == null) continue;
                float n = Mathf.PerlinNoise(t * 7f + i * 13.1f, i * 3.3f);
                l.intensity = flickerBase[i] * (0.82f + n * 0.36f);
            }
            for (int i = 0; i < flames.Count; i++)
            {
                float n = Mathf.PerlinNoise(t * 9f + i * 5f, 0.5f);
                flames[i].localScale = new Vector3(0.8f, 1.5f + n * 0.6f, 0.8f);
            }
            if (beaconMat != null)
            {
                float k = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 2.4f)), 8f);
                Mats.SetEmission(beaconMat, new Color(1f, 0.1f, 0.08f) * (0.2f + k * 7f));
            }
            var cam = Camera.main;
            if (cam != null)
                foreach (var c in cones)
                {
                    var d = c.position - cam.transform.position;
                    d.y = 0;
                    if (d.sqrMagnitude > 1e-4f) c.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                }
            City.Animate(t);
            // keep the bar reflections current (drinks, hands, guests) without a frame spike
            probeTimer -= Time.deltaTime;
            if (probeTimer <= 0 && BarProbe != null)
            {
                probeTimer = 2.5f;
                BarProbe.RenderProbe();
            }
        }
    }
}
