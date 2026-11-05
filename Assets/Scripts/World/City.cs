using System.Collections.Generic;
using UnityEngine;

namespace Bartender
{
    /// <summary>Procedural night skyline around the rooftop: towers with lit windows, crowns, beacons and street lights.</summary>
    public static class City
    {
        const float Ground = -150f;
        static Material beacon;
        static readonly List<Matrix4x4> beacons = new List<Matrix4x4>();

        public static void SetBeaconMaterial(Material m)
        {
            beacon = m;
            if (beacons.Count == 0 || beaconRoot == null) return;
            var mb = new MeshBuilder();
            var s = MeshGen.Sphere(1f, 8, 6);
            foreach (var t in beacons) mb.Append(s, t);
            Props.Part("beacons", beaconRoot, mb.ToMesh("beacons"), m, Vector3.zero, null, null, false);
        }

        static Transform beaconRoot;
        static Material trafficWhite, trafficRed;

        /// <summary>Moves the car lights along the streets.</summary>
        public static void Animate(float t)
        {
            if (trafficWhite != null) trafficWhite.SetTextureOffset("_BaseMap", new Vector2(t * 0.06f, 0));
            if (trafficRed != null) trafficRed.SetTextureOffset("_BaseMap", new Vector2(-t * 0.06f, 0));
        }

        static Texture2D CarLights(int seed)
        {
            const int W = 512, H = 4;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "cars" };
            var px = new Color[W * H];
            var rnd = new System.Random(seed);
            int x = 0;
            while (x < W)
            {
                x += 6 + rnd.Next(40);
                int len = 2 + rnd.Next(3);
                for (int i = 0; i < len && x + i < W; i++)
                    for (int y = 0; y < H; y++) px[y * W + x + i] = new Color(1, 1, 1, 1f - Mathf.Abs(i - len * 0.5f) / len);
                x += len;
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Thin strips along the avenues; their textures scroll so the cars drive.</summary>
        static void Traffic(Transform root)
        {
            var white = new MeshBuilder();
            var red = new MeshBuilder();
            const float y = Ground + 0.7f, w = 1.4f, len = 1500f;
            for (float g = -700f; g <= 700f; g += 140f)
            {
                for (int dirn = 0; dirn < 2; dirn++)
                {
                    var mb = dirn == 0 ? white : red;
                    float off = dirn == 0 ? -2.2f : 2.2f;
                    // along X
                    int a = mb.Add(new Vector3(-len / 2, y, g + off - w / 2), Vector3.up, new Vector2(0, 0));
                    int b = mb.Add(new Vector3(len / 2, y, g + off - w / 2), Vector3.up, new Vector2(len / 40f, 0));
                    int c = mb.Add(new Vector3(len / 2, y, g + off + w / 2), Vector3.up, new Vector2(len / 40f, 1));
                    int d = mb.Add(new Vector3(-len / 2, y, g + off + w / 2), Vector3.up, new Vector2(0, 1));
                    mb.Quad(a, b, c, d);
                    // along Z
                    a = mb.Add(new Vector3(g + off - w / 2, y + 0.01f, -len / 2), Vector3.up, new Vector2(0, 0));
                    b = mb.Add(new Vector3(g + off - w / 2, y + 0.01f, len / 2), Vector3.up, new Vector2(len / 40f, 0));
                    c = mb.Add(new Vector3(g + off + w / 2, y + 0.01f, len / 2), Vector3.up, new Vector2(len / 40f, 1));
                    d = mb.Add(new Vector3(g + off + w / 2, y + 0.01f, -len / 2), Vector3.up, new Vector2(0, 1));
                    mb.Quad(a, b, c, d);
                }
            }
            trafficWhite = Mats.Additive(new Color(1f, 0.95f, 0.85f, 0.9f), CarLights(5));
            trafficRed = Mats.Additive(new Color(1f, 0.12f, 0.08f, 0.9f), CarLights(9));
            Props.Part("traffic white", root, white.ToMesh("trafficW", false), trafficWhite, Vector3.zero, null, null, false);
            Props.Part("traffic red", root, red.ToMesh("trafficR", false), trafficRed, Vector3.zero, null, null, false);
        }

        public static void Build(Transform root)
        {
            var rnd = new System.Random(2024);
            var f1 = new MeshBuilder();
            var f2 = new MeshBuilder();
            var roofs = new MeshBuilder();
            var crowns = new MeshBuilder();
            var crownsWarm = new MeshBuilder();
            beacons.Clear();
            var placed = new List<Vector4>(); // x, z, radius
            int count = 0, tries = 0;
            while (count < 420 && tries < 6000)
            {
                tries++;
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float u = (float)rnd.NextDouble();
                float dist = Mathf.Lerp(48f, 760f, u * u * 0.7f + u * 0.3f);
                float x = Mathf.Cos(ang) * dist, z = Mathf.Sin(ang) * dist + 5f;
                float w = Mathf.Lerp(14f, 42f, (float)rnd.NextDouble());
                float d = Mathf.Lerp(14f, 42f, (float)rnd.NextDouble());
                float r = Mathf.Max(w, d) * 0.62f;
                bool overlap = false;
                foreach (var p in placed)
                    if (new Vector2(p.x - x, p.y - z).magnitude < p.z + r + 6f) { overlap = true; break; }
                if (overlap) continue;
                placed.Add(new Vector4(x, z, r, 0));
                // heights: close neighbours sit below the roof, the far skyline rises above it
                float tall = (float)rnd.NextDouble();
                float h;
                if (dist < 140f) h = Mathf.Lerp(60f, 145f, tall);
                else if (dist < 320f) h = Mathf.Lerp(50f, 200f, tall * tall);
                else h = Mathf.Lerp(60f, 320f, Mathf.Pow(tall, 1.6f));
                float top = Ground + h;
                float rot = (float)rnd.NextDouble() < 0.7f ? 0 : (float)rnd.NextDouble() * 90f;
                var fm = rnd.Next(2) == 0 ? f1 : f2;
                Tower(fm, roofs, new Vector3(x, 0, z), w, d, Ground, top, rot, (float)rnd.NextDouble() * 200f);
                // setback top
                if (h > 150f && rnd.NextDouble() < 0.6)
                {
                    float sh = Mathf.Lerp(15f, 50f, (float)rnd.NextDouble());
                    Tower(fm, roofs, new Vector3(x, 0, z), w * 0.65f, d * 0.65f, top, top + sh, rot, (float)rnd.NextDouble() * 200f);
                    top += sh;
                }
                // crown light band
                if (h > 170f && rnd.NextDouble() < 0.55)
                {
                    var cm = rnd.Next(2) == 0 ? crowns : crownsWarm;
                    float cw = (h > 150f ? w * 0.65f : w) + 0.4f, cd = (h > 150f ? d * 0.65f : d) + 0.4f;
                    MeshGen.RoundedBoxInto(cm, new Vector3(cw, 1.2f, cd), 0.2f, 1, 10f, Matrix4x4.TRS(new Vector3(x, top - 2f, z), Quaternion.Euler(0, rot, 0), Vector3.one));
                }
                // antenna with a red beacon
                if (h > 140f && rnd.NextDouble() < 0.5)
                {
                    float ah = Mathf.Lerp(8f, 30f, (float)rnd.NextDouble());
                    MeshGen.TubeInto(roofs, new List<Vector3> { new Vector3(x, top, z), new Vector3(x, top + ah, z) }, 0.4f, 5, false);
                    beacons.Add(Matrix4x4.TRS(new Vector3(x, top + ah, z), Quaternion.identity, Vector3.one * 1.1f));
                }
                count++;
            }
            var croot = new GameObject("City").transform;
            croot.SetParent(root, false);
            beaconRoot = croot;
            Props.Part("facades A", croot, f1.ToMesh("facadesA", false), Mats.Tex("M_Facade1"), Vector3.zero, null, null, false);
            Props.Part("facades B", croot, f2.ToMesh("facadesB", false), Mats.Tex("M_Facade2"), Vector3.zero, null, null, false);
            Props.Part("roofs", croot, roofs.ToMesh("roofs", false), Mats.Lit(new Color(0.04f, 0.04f, 0.05f), 0.2f), Vector3.zero, null, null, false);
            Props.Part("crowns", croot, crowns.ToMesh("crowns", false), Mats.Emissive(Color.black, new Color(0.75f, 0.85f, 1f), 2.2f), Vector3.zero, null, null, false);
            Props.Part("crowns warm", croot, crownsWarm.ToMesh("crownsW", false), Mats.Emissive(Color.black, new Color(1f, 0.72f, 0.4f), 2.2f), Vector3.zero, null, null, false);
            StreetLights(croot, rnd);
            Traffic(croot);
            // the ground far below
            Props.Part("ground", croot, MeshGen.Plane(2400f, 2400f, 50f), Mats.Lit(new Color(0.02f, 0.02f, 0.025f), 0.1f), new Vector3(0, Ground, 0), null, null, false);
        }

        static void Tower(MeshBuilder fm, MeshBuilder roofs, Vector3 c, float w, float d, float bottom, float top, float rotDeg, float uOff)
        {
            var q = Quaternion.Euler(0, rotDeg, 0);
            Vector3 P(float x, float z) => c + q * new Vector3(x, 0, z);
            var a = P(-w / 2, -d / 2); var b = P(w / 2, -d / 2); var cc = P(w / 2, d / 2); var dd = P(-w / 2, d / 2);
            // edges run counter-clockwise seen from above so the normals face outwards
            World.AddFacade(fm, a, b, top, bottom, uOff);
            World.AddFacade(fm, b, cc, top, bottom, uOff + w);
            World.AddFacade(fm, cc, dd, top, bottom, uOff + w + d);
            World.AddFacade(fm, dd, a, top, bottom, uOff + w * 2 + d);
            int i0 = roofs.Add(new Vector3(a.x, top, a.z), Vector3.up, Vector2.zero);
            int i1 = roofs.Add(new Vector3(b.x, top, b.z), Vector3.up, Vector2.zero);
            int i2 = roofs.Add(new Vector3(cc.x, top, cc.z), Vector3.up, Vector2.zero);
            int i3 = roofs.Add(new Vector3(dd.x, top, dd.z), Vector3.up, Vector2.zero);
            roofs.Quad(i0, i1, i2, i3);
        }

        static void StreetLights(Transform root, System.Random rnd)
        {
            var mb = new MeshBuilder();
            const float y = Ground + 0.5f;
            void Lamp(float x, float z, float s)
            {
                int a = mb.Add(new Vector3(x - s, y, z - s), Vector3.up, new Vector2(0, 0));
                int b = mb.Add(new Vector3(x + s, y, z - s), Vector3.up, new Vector2(1, 0));
                int c = mb.Add(new Vector3(x + s, y, z + s), Vector3.up, new Vector2(1, 1));
                int d = mb.Add(new Vector3(x - s, y, z + s), Vector3.up, new Vector2(0, 1));
                mb.Quad(a, b, c, d);
            }
            for (float g = -760f; g <= 760f; g += 70f)
            {
                for (float t = -760f; t <= 760f; t += 18f)
                {
                    if (new Vector2(g, t).magnitude > 820f) continue;
                    float j = (float)rnd.NextDouble() * 3f;
                    Lamp(g + 4f, t + j, 2.2f);
                    Lamp(t + j, g + 4f, 2.2f);
                }
            }
            Props.Part("street lights", root, mb.ToMesh("streets", false), Mats.Additive(new Color(1f, 0.55f, 0.2f, 0.8f), Mats.SoftDot), Vector3.zero, null, null, false);
        }
    }
}
