using System.Collections.Generic;
using UnityEngine;

namespace Bartender
{
    /// <summary>Smooth stylised figure with a simple procedural walk / sit / drink animation.</summary>
    public sealed class Mannequin : MonoBehaviour
    {
        public enum PoseKind { Stand, Walk, Sit }
        public PoseKind Pose;
        public float Sit;          // 0 standing .. 1 seated (blended)
        public float Drink;        // 0 .. 1 right hand to the mouth
        public float Wave;         // 0 .. 1 raised hand (calling the bartender)
        public float LookYaw;      // head turn
        public Transform RightHand, Head;
        public float WalkSpeed = 1.1f;
        public float SitHips = 0.83f;     // hip height when seated (stool 0.83, sofa ~0.52)

        Transform hips, spine, neck, lThigh, lShin, rThigh, rShin, lUpper, lFore, rUpper, rFore;
        float phase;
        float breath;

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        static Mesh Limb(float r1, float r2, float len)
        {
            string key = $"limb{r1:0.000}{r2:0.000}{len:0.000}";
            if (meshes.TryGetValue(key, out var m)) return m;
            var p = new List<LP>();
            for (int i = 0; i <= 6; i++) { float a = Mathf.Lerp(-90f, 0f, i / 6f) * Mathf.Deg2Rad; p.Add(new LP(Mathf.Cos(a) * r2, -len + Mathf.Sin(a) * r2)); }
            for (int i = 0; i <= 6; i++) { float a = Mathf.Lerp(0f, 90f, i / 6f) * Mathf.Deg2Rad; p.Add(new LP(Mathf.Cos(a) * r1, Mathf.Sin(a) * r1)); }
            var l = p[0]; l.r = 0; p[0] = l;
            l = p[p.Count - 1]; l.r = 0; p[p.Count - 1] = l;
            m = MeshGen.Lathe(p, 18);
            return meshes[key] = m;
        }

        static Mesh Shape(string key, List<LP> prof, int seg = 28)
        {
            if (meshes.TryGetValue(key, out var m)) return m;
            m = MeshGen.Lathe(prof, seg);
            return meshes[key] = m;
        }

        static Transform Joint(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static readonly Color[] Skins = { new Color(0.93f, 0.76f, 0.64f), new Color(0.84f, 0.63f, 0.5f), new Color(0.7f, 0.5f, 0.37f), new Color(0.5f, 0.34f, 0.24f), new Color(0.96f, 0.82f, 0.72f), new Color(0.78f, 0.58f, 0.44f), new Color(0.36f, 0.24f, 0.17f) };
        static readonly Color[] Hairs = { new Color(0.06f, 0.045f, 0.04f), new Color(0.2f, 0.12f, 0.07f), new Color(0.42f, 0.27f, 0.15f), new Color(0.78f, 0.62f, 0.38f), new Color(0.55f, 0.55f, 0.56f), new Color(0.4f, 0.13f, 0.06f) };
        static readonly Color[] Irises = { new Color(0.25f, 0.14f, 0.07f), new Color(0.2f, 0.36f, 0.55f), new Color(0.24f, 0.36f, 0.2f), new Color(0.09f, 0.06f, 0.04f), new Color(0.38f, 0.28f, 0.14f) };
        static readonly Color[] Tops = { new Color(0.05f, 0.05f, 0.06f), new Color(0.08f, 0.12f, 0.25f), new Color(0.42f, 0.06f, 0.1f), new Color(0.06f, 0.28f, 0.22f), new Color(0.9f, 0.88f, 0.84f), new Color(0.55f, 0.42f, 0.3f), new Color(0.3f, 0.12f, 0.35f), new Color(0.72f, 0.58f, 0.22f), new Color(0.2f, 0.2f, 0.22f) };
        static readonly Color[] Bottoms = { new Color(0.04f, 0.04f, 0.05f), new Color(0.12f, 0.12f, 0.14f), new Color(0.1f, 0.12f, 0.2f), new Color(0.3f, 0.26f, 0.22f), new Color(0.2f, 0.17f, 0.14f) };

        readonly List<Transform> eyes = new List<Transform>();
        float blink, nextBlink;

        /// <summary>Hair that follows a hairline: low at the back, at the ears on the sides, high on the forehead.</summary>
        static Mesh HairCap(float r, float yFront, float ySide, float yBack, string key)
        {
            if (meshes.TryGetValue(key, out var m)) return m;
            const int lon = 40, lat = 12;
            var mb = new MeshBuilder();
            for (int j = 0; j <= lon; j++)
            {
                float phi = Mathf.PI * 2f * j / lon;
                float c = Mathf.Cos(phi);
                float wf = Mathf.Max(0, c), wb = Mathf.Max(0, -c);
                float y = yFront * wf * wf + yBack * wb + ySide * (1f - wf * wf - wb);
                float latMin = Mathf.Asin(Mathf.Clamp(y / r, -0.95f, 0.95f));
                for (int i = 0; i <= lat; i++)
                {
                    float la = Mathf.Lerp(Mathf.PI * 0.5f, latMin, (float)i / lat);
                    var p = new Vector3(Mathf.Cos(la) * Mathf.Sin(phi), Mathf.Sin(la), Mathf.Cos(la) * Mathf.Cos(phi)) * r;
                    mb.Add(p, p.normalized, new Vector2((float)j / lon, (float)i / lat));
                }
            }
            int w = lat + 1;
            for (int j = 0; j < lon; j++)
                for (int i = 0; i < lat; i++)
                {
                    int a = j * w + i;
                    mb.Quad(a, a + 1, a + w + 1, a + w);
                }
            m = mb.ToMesh(key);
            return meshes[key] = m;
        }

        static Mesh Ball => meshes.TryGetValue("ball", out var m) ? m : meshes["ball"] = MeshGen.Sphere(1f, 24, 14);

        static GameObject Blob(string name, Transform parent, Material mat, Vector3 pos, Vector3 size, Vector3 euler = default, bool shadows = true) =>
            Props.Part(name, parent, Ball, mat, pos, Quaternion.Euler(euler), size, shadows);

        public static Mannequin Create(int seed, Transform parent)
        {
            var rnd = new System.Random(seed);
            var go = new GameObject("guest");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<Mannequin>();
            bool fem = rnd.Next(2) == 0;
            float scale = (fem ? 0.95f : 1.02f) + (float)rnd.NextDouble() * 0.06f;
            var skinC = Skins[rnd.Next(Skins.Length)];
            var skin = Mats.Lit(skinC, 0.42f);
            var lip = Mats.Lit(new Color(skinC.r * 0.82f, skinC.g * 0.52f, skinC.b * 0.52f), 0.55f);
            var hairC = Hairs[rnd.Next(Hairs.Length)];
            var hair = Mats.TexTint("M_Fabric", hairC * 1.25f, 0.55f, 0.06f);
            var brow = Mats.Lit(hairC * 0.8f, 0.3f);
            var topC = Tops[rnd.Next(Tops.Length)];
            var top = Mats.TexTint("M_Fabric", topC, 0.35f, 0.25f);
            var bottom = Mats.TexTint("M_Fabric", Bottoms[rnd.Next(Bottoms.Length)], 0.25f, 0.25f);
            var shoe = Mats.Lit(new Color(0.03f, 0.03f, 0.03f), 0.85f);
            var white = Mats.TexTint("M_Fabric", new Color(0.93f, 0.92f, 0.9f), 0.2f, 0.2f);
            bool dress = fem && rnd.Next(3) > 0;
            bool blazer = !fem && rnd.Next(2) == 0;

            var body = Joint("body", go.transform, Vector3.zero);
            body.localScale = Vector3.one * scale;
            m.hips = Joint("hips", body, new Vector3(0, 0.95f, 0));
            float waist = fem ? 0.115f : 0.135f, chest = fem ? 0.155f : 0.178f, sh = fem ? 0.165f : 0.198f;
            Props.Part("pelvis", m.hips, Shape("pelvis" + fem, new List<LP> { new LP(0, -0.1f), new LP(0.1f, -0.095f), new LP(fem ? 0.16f : 0.145f, -0.04f), new LP(waist + 0.01f, 0.05f), new LP(0, 0.06f) }), dress ? top : bottom, Vector3.zero, null, new Vector3(1f, 1f, 0.72f));
            if (!dress) Props.Part("belt", m.hips, MeshGen.Torus(waist + 0.012f, 0.011f, 40, 8), Mats.Lit(new Color(0.08f, 0.05f, 0.03f), 0.6f), new Vector3(0, 0.045f, 0), null, new Vector3(1f, 1f, 0.72f));
            m.spine = Joint("spine", m.hips, new Vector3(0, 0.03f, 0));
            Props.Part("torso", m.spine, Shape("torso" + fem, new List<LP>
            {
                new LP(0, 0), new LP(waist, 0.0f), new LP(waist * 1.02f, 0.12f), new LP(chest, 0.3f), new LP(sh, 0.42f), new LP(sh * 0.8f, 0.48f), new LP(0.06f, 0.52f), new LP(0, 0.52f),
            }, 36), top, Vector3.zero, null, new Vector3(1f, 1f, 0.66f));
            if (fem) { Blob("bust", m.spine, top, new Vector3(-0.055f, 0.34f, 0.075f), new Vector3(0.06f, 0.055f, 0.05f)); Blob("bust", m.spine, top, new Vector3(0.055f, 0.34f, 0.075f), new Vector3(0.06f, 0.055f, 0.05f)); }
            if (blazer)
            {
                // open jacket: shirt and tie visible in the V
                Blob("shirt", m.spine, white, new Vector3(0, 0.43f, 0.108f), new Vector3(0.03f, 0.065f, 0.01f), new Vector3(-12f, 0, 0), false);
                Blob("tie", m.spine, Mats.TexTint("M_Fabric", Tops[rnd.Next(Tops.Length)] * 0.8f, 0.4f, 0.1f), new Vector3(0, 0.4f, 0.116f), new Vector3(0.011f, 0.07f, 0.005f), new Vector3(-10f, 0, 0), false);
                for (int b = 0; b < 2; b++) Blob("button", m.spine, Mats.Lit(new Color(0.1f, 0.08f, 0.06f), 0.7f), new Vector3(0, 0.2f + b * 0.07f, 0.106f - b * 0.004f), Vector3.one * 0.007f, default, false);
            }
            m.neck = Joint("neck", m.spine, new Vector3(0, 0.5f, 0));
            Props.Part("neck", m.neck, Limb(0.042f, 0.048f, 0.07f), skin, new Vector3(0, 0.07f, 0));
            Props.Part("collar", m.spine, MeshGen.Torus(0.058f, 0.012f, 32, 8), blazer ? white : top, new Vector3(0, 0.505f, 0.005f), Quaternion.Euler(-8f, 0, 0), new Vector3(1f, 1f, 0.9f), false);
            if (fem && rnd.Next(2) == 0) Props.Part("necklace", m.spine, MeshGen.Torus(0.07f, 0.0022f, 40, 5), Mats.Gold, new Vector3(0, 0.47f, 0.02f), Quaternion.Euler(-28f, 0, 0), new Vector3(1f, 1f, 0.9f), false);

            // ---- head
            m.Head = Joint("head", m.neck, new Vector3(0, 0.16f, 0.008f));
            var h = m.Head;
            Props.Part("skull", h, Ball, skin, Vector3.zero, null, new Vector3(0.086f, 0.11f, 0.097f));
            Blob("jaw", h, skin, new Vector3(0, -0.052f, 0.012f), new Vector3(fem ? 0.062f : 0.07f, 0.058f, 0.07f));
            Blob("ear", h, skin, new Vector3(-0.084f, -0.004f, -0.004f), new Vector3(0.012f, 0.026f, 0.018f), new Vector3(0, -12f, 0));
            Blob("ear", h, skin, new Vector3(0.084f, -0.004f, -0.004f), new Vector3(0.012f, 0.026f, 0.018f), new Vector3(0, 12f, 0));
            var sclera = Mats.Lit(new Color(0.93f, 0.91f, 0.87f), 0.85f);
            var iris = Mats.Lit(Irises[rnd.Next(Irises.Length)], 0.95f);
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = Joint("eye", h, new Vector3(0.033f * s, 0.012f, 0.083f));
                Blob("sclera", eye, sclera, Vector3.zero, new Vector3(0.0125f, 0.0095f, 0.007f), default, false);
                Blob("iris", eye, iris, new Vector3(0, 0, 0.0058f), new Vector3(0.0064f, 0.0064f, 0.0024f), default, false);
                Blob("pupil", eye, Mats.Lit(new Color(0.01f, 0.01f, 0.01f), 0.95f), new Vector3(0, 0, 0.0074f), new Vector3(0.0028f, 0.0028f, 0.001f), default, false);
                m.eyes.Add(eye);
                Blob("lid", h, skin, new Vector3(0.033f * s, 0.0205f, 0.081f), new Vector3(0.0145f, 0.0062f, 0.0085f), new Vector3(-10f, 0, 0), false);
                Blob("brow", h, brow, new Vector3(0.035f * s, 0.035f, 0.086f), new Vector3(0.019f, 0.0038f, 0.005f), new Vector3(0, 0, (fem ? 6f : 4f) * -s), false);
                Blob("cheek", h, skin, new Vector3(0.042f * s, -0.024f, 0.058f), new Vector3(0.026f, 0.02f, 0.024f), default, false);
            }
            Blob("nose bridge", h, skin, new Vector3(0, -0.004f, 0.092f), new Vector3(fem ? 0.0085f : 0.0095f, 0.024f, 0.012f), new Vector3(-16f, 0, 0), false);
            Blob("nose tip", h, skin, new Vector3(0, -0.025f, 0.1f), new Vector3(0.0115f, 0.0095f, 0.01f), default, false);
            Blob("nostrils", h, skin, new Vector3(0, -0.029f, 0.094f), new Vector3(0.017f, 0.007f, 0.009f), default, false);
            Blob("upper lip", h, lip, new Vector3(0, -0.047f, 0.087f), new Vector3(fem ? 0.019f : 0.02f, 0.0045f, 0.0075f), new Vector3(-8f, 0, 0), false);
            Blob("lower lip", h, lip, new Vector3(0, -0.055f, 0.085f), new Vector3(fem ? 0.017f : 0.018f, fem ? 0.0058f : 0.005f, 0.0075f), new Vector3(8f, 0, 0), false);

            // ---- hair
            int style = fem ? rnd.Next(4) : rnd.Next(10) < 8 ? 0 : 4; // 0 short, 1 long, 2 bun, 3 ponytail, 4 shaved
            var capScale = new Vector3(0.088f / 0.1f, 0.112f / 0.1f, 0.099f / 0.1f);
            if (style == 4)
                Props.Part("hair", h, HairCap(0.1005f, 0.052f, 0.0f, -0.03f, "capShaved"), Mats.Lit(Color.Lerp(skinC, hairC, 0.55f), 0.2f), Vector3.zero, null, capScale);
            else
                Props.Part("hair", h, HairCap(0.106f, fem ? 0.05f : 0.058f, fem ? -0.03f : 0.002f, fem ? -0.075f : -0.045f, fem ? "capF" : "capM"), hair, new Vector3(0, 0.004f, -0.004f), null, capScale);

            if (style == 1)
            {
                Blob("hair back", h, hair, new Vector3(0, -0.075f, -0.05f), new Vector3(0.1f, 0.14f, 0.055f));
                Blob("hair side", h, hair, new Vector3(-0.078f, -0.06f, -0.01f), new Vector3(0.025f, 0.11f, 0.045f), new Vector3(0, 0, -6f));
                Blob("hair side", h, hair, new Vector3(0.078f, -0.06f, -0.01f), new Vector3(0.025f, 0.11f, 0.045f), new Vector3(0, 0, 6f));
            }
            if (style == 2) Blob("bun", h, hair, new Vector3(0, 0.085f, -0.075f), new Vector3(0.045f, 0.042f, 0.045f));
            if (style == 3)
            {
                Blob("tie", h, Mats.Lit(new Color(0.1f, 0.05f, 0.05f), 0.5f), new Vector3(0, 0.02f, -0.1f), new Vector3(0.016f, 0.016f, 0.012f));
                var tail = new List<Vector3> { new Vector3(0, 0.02f, -0.105f), new Vector3(0, -0.04f, -0.125f), new Vector3(0, -0.12f, -0.12f), new Vector3(0, -0.18f, -0.1f) };
                Props.Part("ponytail", h, MeshGen.Tube(tail, 0.024f, 12, true, 0.75f), hair);
            }

            // ---- arms
            m.lUpper = Joint("lUpper", m.spine, new Vector3(-sh - 0.02f, 0.43f, 0));
            m.rUpper = Joint("rUpper", m.spine, new Vector3(sh + 0.02f, 0.43f, 0));
            bool bareArms = fem && dress && rnd.Next(2) == 0;
            foreach (var u in new[] { m.lUpper, m.rUpper })
            {
                float side = u == m.lUpper ? -1f : 1f;
                Blob("shoulder", u, top, new Vector3(-0.008f * side, -0.012f, 0), new Vector3(0.05f, 0.047f, 0.05f));
                Props.Part("upper", u, Limb(0.048f, 0.04f, 0.28f), bareArms ? skin : top);
                var f = Joint("fore", u, new Vector3(0, -0.29f, 0));
                Props.Part("fore", f, Limb(0.039f, 0.031f, 0.25f), bareArms || (fem && dress) ? skin : top);
                if (!(bareArms || (fem && dress))) Props.Part("cuff", f, MeshGen.Torus(0.032f, 0.006f, 24, 6), blazer ? white : top, new Vector3(0, -0.24f, 0), null, null, false);
                var hand = Joint("hand", f, new Vector3(0, -0.29f, 0));
                Blob("palm", hand, skin, new Vector3(0, -0.02f, 0.004f), new Vector3(0.024f, 0.045f, 0.013f));
                Blob("fingers", hand, skin, new Vector3(0, -0.068f, 0.008f), new Vector3(0.021f, 0.03f, 0.01f), new Vector3(12f, 0, 0));
                Blob("thumb", hand, skin, new Vector3(-0.02f * side, -0.025f, 0.014f), new Vector3(0.008f, 0.024f, 0.008f), new Vector3(10f, 0, 22f * side));
                if (u == m.lUpper) m.lFore = f; else { m.rFore = f; m.RightHand = hand; }
            }

            // ---- legs
            m.lThigh = Joint("lThigh", m.hips, new Vector3(-0.085f, -0.05f, 0));
            m.rThigh = Joint("rThigh", m.hips, new Vector3(0.085f, -0.05f, 0));
            foreach (var t in new[] { m.lThigh, m.rThigh })
            {
                Props.Part("thigh", t, Limb(0.07f, 0.052f, 0.42f), dress ? skin : bottom);
                var s = Joint("shin", t, new Vector3(0, -0.43f, 0));
                Props.Part("shin", s, Limb(0.05f, 0.035f, 0.41f), dress ? skin : bottom);
                if (dress)
                {
                    // pump with a heel
                    Blob("shoe", s, shoe, new Vector3(0, -0.445f, 0.05f), new Vector3(0.032f, 0.024f, 0.085f), new Vector3(-12f, 0, 0));
                    Props.Part("heel", s, MeshGen.Cylinder(0.008f, 0.06f, 10, 0.002f), shoe, new Vector3(0, -0.5f, -0.02f));
                }
                else
                {
                    Blob("shoe", s, shoe, new Vector3(0, -0.452f, 0.045f), new Vector3(0.042f, 0.03f, 0.11f));
                    Props.Part("sole", s, MeshGen.RoundedBox(new Vector3(0.08f, 0.014f, 0.21f), 0.006f, 2), Mats.Lit(new Color(0.12f, 0.08f, 0.05f), 0.4f), new Vector3(0, -0.478f, 0.045f));
                }
                if (t == m.lThigh) m.lShin = s; else m.rShin = s;
            }
            if (dress)
                Props.Part("skirt", m.hips, Shape("skirt", new List<LP> { new LP(0, -0.42f), new LP(0.22f, -0.42f), new LP(0.2f, -0.3f), new LP(0.15f, -0.05f), new LP(0.125f, 0.05f), new LP(0, 0.05f) }), top, Vector3.zero, null, new Vector3(1f, 1f, 0.8f));
            m.phase = (float)rnd.NextDouble() * 10f;
            m.breath = (float)rnd.NextDouble() * 10f;
            m.nextBlink = (float)rnd.NextDouble() * 4f;
            return m;
        }
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            breath += dt;
            nextBlink -= dt;
            if (nextBlink <= 0) { blink = 1f; nextBlink = Random.Range(2.5f, 6f); }
            blink = Mathf.MoveTowards(blink, 0, dt * 7f);
            float open = 1f - Mathf.Sin(blink * Mathf.PI) * 0.9f;
            foreach (var e in eyes) e.localScale = new Vector3(1f, open, 1f);
            if (Pose == PoseKind.Walk) phase += dt * WalkSpeed * 5.2f;
            float targetSit = Pose == PoseKind.Sit ? 1f : 0f;
            Sit = Mathf.MoveTowards(Sit, targetSit, dt * 1.6f);
            float s = Sit * Sit * (3 - 2 * Sit);
            float walk = Pose == PoseKind.Walk ? 1f : 0f;
            float sw = Mathf.Sin(phase);
            float bob = walk * Mathf.Abs(Mathf.Cos(phase)) * 0.025f;
            hips.localPosition = new Vector3(0, Mathf.Lerp(0.95f + bob, SitHips, s), Mathf.Lerp(0, -0.05f, s));
            spine.localRotation = Quaternion.Euler(Mathf.Lerp(2f, 6f, s) + Mathf.Sin(breath * 1.7f) * 0.8f, walk * sw * 3f, 0);
            neck.localRotation = Quaternion.Euler(Mathf.Lerp(-2f, 2f, s), LookYaw, 0);
            // legs
            float lA = walk * sw * 28f, rA = -walk * sw * 28f;
            float lK = walk * Mathf.Max(0, Mathf.Sin(phase + 1.6f)) * 40f, rK = walk * Mathf.Max(0, Mathf.Sin(phase + 1.6f + Mathf.PI)) * 40f;
            lThigh.localRotation = Quaternion.Euler(Mathf.Lerp(-lA, -86f, s), 0, Mathf.Lerp(0, -4f, s));
            rThigh.localRotation = Quaternion.Euler(Mathf.Lerp(-rA, -86f, s), 0, Mathf.Lerp(0, 4f, s));
            lShin.localRotation = Quaternion.Euler(Mathf.Lerp(lK, 80f, s), 0, 0);
            rShin.localRotation = Quaternion.Euler(Mathf.Lerp(rK, 80f, s), 0, 0);
            // arms
            float armSwing = walk * sw * 20f;
            var lIdle = new Vector3(armSwing, 0, -6f);
            var lSit = new Vector3(-38f, 0, -6f);
            lUpper.localRotation = Quaternion.Euler(Vector3.Lerp(lIdle, lSit, s));
            lFore.localRotation = Quaternion.Euler(Mathf.Lerp(-8f, -62f, s), 0, 0);
            var rIdle = new Vector3(-armSwing, 0, 6f);
            var rSitV = new Vector3(-38f, 0, 6f);
            var rDrink = new Vector3(-62f, -18f, 22f);
            var rWave = new Vector3(-150f, 0, 18f);
            var rv = Vector3.Lerp(Vector3.Lerp(rIdle, rSitV, s), rDrink, Drink);
            rv = Vector3.Lerp(rv, rWave, Wave);
            rUpper.localRotation = Quaternion.Euler(rv);
            float rf = Mathf.Lerp(Mathf.Lerp(-8f, -62f, s), -118f, Drink);
            rf = Mathf.Lerp(rf, -25f + Mathf.Sin(Time.time * 9f) * 15f * Wave, Wave);
            rFore.localRotation = Quaternion.Euler(rf, 0, 0);
        }
    }
}
