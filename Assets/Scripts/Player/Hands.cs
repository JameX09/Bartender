using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bartender
{
    /// <summary>The bartender's first-person hands and forearms. Each hand wraps around whatever it holds.</summary>
    public sealed class Hands : MonoBehaviour
    {
        sealed class Rig
        {
            public Transform Hand, Fingers, Forearm;
            public MeshFilter FingerMesh;
            public float Radius = -1;
            public bool Left;
            public Vector3 Pos, Target;
            public Quaternion Rot, TargetRot;
            public bool Visible;
            public float Show;      // 0..1 slide in from below
        }

        Rig right, left;
        Material skin, shirt, cuff;
        static readonly Dictionary<int, Mesh> fingerCache = new Dictionary<int, Mesh>();
        Mesh palm, forearm, sleeve;
        Camera cam;

        public void Init(Camera c)
        {
            cam = c;
            skin = Mats.Lit(new Color(0.8f, 0.6f, 0.49f), 0.32f);
            shirt = Mats.TexTint("M_Fabric", new Color(0.93f, 0.92f, 0.9f), 0.15f, 0.12f);
            cuff = Mats.TexTint("M_Fabric", new Color(0.07f, 0.07f, 0.08f), 0.2f, 0.12f);
            palm = MeshGen.RoundedBox(new Vector3(0.026f, 0.082f, 0.08f), 0.012f, 4, 0.1f);
            forearm = MeshGen.Lathe(new List<LP> { new LP(0, 0), new LP(0.026f, 0.006f), new LP(0.029f, 0.08f), new LP(0.037f, 0.35f), new LP(0.041f, 0.58f), new LP(0.041f, 0.6f), new LP(0, 0.6f) }, 24, 0.2f);
            sleeve = MeshGen.Lathe(new List<LP> { new LP(0, 0.52f), new LP(0.046f, 0.52f), new LP(0.052f, 0.55f), new LP(0.053f, 0.6f), new LP(0.05f, 0.64f), new LP(0.052f, 1f), new LP(0, 1f) }, 24, 0.2f);
            right = Make(false);
            left = Make(true);
        }

        Rig Make(bool isLeft)
        {
            var r = new Rig { Left = isLeft };
            var root = new GameObject(isLeft ? "left hand" : "right hand").transform;
            root.SetParent(transform, false);
            r.Hand = new GameObject("hand").transform;
            r.Hand.SetParent(root, false);
            if (isLeft) r.Hand.localScale = new Vector3(-1, 1, 1);
            Props.Part("palm", r.Hand, palm, skin, Vector3.zero, null, null, false);
            var f = Props.Part("fingers", r.Hand, palm, skin, Vector3.zero, null, null, false);
            r.Fingers = f.transform;
            r.FingerMesh = f.GetComponent<MeshFilter>();
            r.Forearm = new GameObject("forearm").transform;
            r.Forearm.SetParent(root, false);
            Props.Part("arm", r.Forearm, forearm, skin, Vector3.zero, null, null, false);
            Props.Part("sleeve", r.Forearm, sleeve, shirt, Vector3.zero, null, null, false);
            var band = MeshGen.Torus(0.0295f, 0.005f, 32, 8);
            Props.Part("cuff", r.Forearm, band, isLeft ? Mats.Lit(new Color(0.08f, 0.08f, 0.09f), 0.8f) : cuff, new Vector3(0, 0.05f, 0), null, null, false);
            if (isLeft) Props.Part("watch", r.Forearm, MeshGen.Cylinder(0.016f, 0.006f, 24, 0.002f), Mats.Gold, new Vector3(0, 0.05f, -0.032f), Quaternion.Euler(90, 0, 0), null, false);
            root.gameObject.SetActive(false);
            return r;
        }

        /// <summary>Curled fingers and thumb for an object of the given radius (hand frame: object axis = Y, palm on +X).</summary>
        static Mesh FingerMesh(float radius)
        {
            int key = Mathf.RoundToInt(radius * 1000f);
            if (fingerCache.TryGetValue(key, out var m)) return m;
            var mb = new MeshBuilder();
            float R = Mathf.Max(radius, 0.006f) + 0.009f;
            float[] ys = { 0.03f, 0.01f, -0.01f, -0.029f };
            float[] lens = { 0.082f, 0.092f, 0.088f, 0.07f };
            for (int f = 0; f < 4; f++)
            {
                var path = new List<Vector3>();
                float sweep = Mathf.Min(lens[f] / R, Mathf.Deg2Rad * 250f);
                // start at the palm edge, wrap around the far side of the object
                path.Add(new Vector3(R + 0.012f, ys[f], 0.034f));
                for (int i = 0; i <= 10; i++)
                {
                    float a = Mathf.Deg2Rad * 22f + sweep * i / 10f;
                    path.Add(new Vector3(Mathf.Cos(a) * R, ys[f] - i * 0.0012f, Mathf.Sin(a) * R));
                }
                MeshGen.TubeInto(mb, path, 0.0072f, 8, true, 0.9f, 0.05f, t => Mathf.Lerp(1f, 0.82f, t));
            }
            // thumb wraps the near side
            var tp = new List<Vector3> { new Vector3(R + 0.012f, 0.0f, -0.03f) };
            float ts = Mathf.Min(0.06f / R, Mathf.Deg2Rad * 160f);
            for (int i = 0; i <= 8; i++)
            {
                float a = -Mathf.Deg2Rad * 30f - ts * i / 8f;
                tp.Add(new Vector3(Mathf.Cos(a) * R, 0.035f + i * 0.001f, Mathf.Sin(a) * R));
            }
            MeshGen.TubeInto(mb, tp, 0.0088f, 8, true, 0.88f, 0.05f, t => Mathf.Lerp(1f, 0.82f, t));
            m = mb.ToMesh("fingers");
            return fingerCache[key] = m;
        }

        /// <summary>Place a hand on an object: grip point and orientation in world space.</summary>
        public void SetRight(bool visible, Vector3 pos, Quaternion rot, float radius) => Set(right, visible, pos, rot, radius);
        public void SetLeft(bool visible, Vector3 pos, Quaternion rot, float radius) => Set(left, visible, pos, rot, radius);

        void Set(Rig r, bool visible, Vector3 pos, Quaternion rot, float radius)
        {
            r.Visible = visible;
            if (!visible) return;
            r.Target = pos;
            r.TargetRot = rot;
            if (Mathf.Abs(r.Radius - radius) > 0.0015f)
            {
                r.Radius = radius;
                r.FingerMesh.sharedMesh = FingerMesh(radius);
            }
        }

        void LateUpdate()
        {
            if (cam == null) return;
            Apply(right);
            Apply(left);
        }

        void Apply(Rig r)
        {
            var root = r.Hand.parent.gameObject;
            r.Show = Mathf.MoveTowards(r.Show, r.Visible ? 1f : 0f, Time.deltaTime * 6f);
            if (r.Show <= 0.001f) { if (root.activeSelf) root.SetActive(false); return; }
            if (!root.activeSelf) { root.SetActive(true); r.Pos = r.Target - cam.transform.up * 0.3f; r.Rot = r.TargetRot; }
            r.Pos = r.Target;
            r.Rot = r.TargetRot;
            var ct = cam.transform;
            // slide in from below the view when grabbing / out when releasing
            Vector3 hide = -ct.up * 0.35f * (1f - r.Show);
            float side = r.Left ? -1f : 1f;
            // palm sits on the outside of the object (+X of the hand frame for the right hand)
            r.Hand.SetPositionAndRotation(r.Pos + hide, r.Rot);
            float off = r.Radius + 0.013f;
            r.Hand.GetChild(0).localPosition = new Vector3(off, 0, 0);
            // forearm from the wrist towards an elbow below and outside the view
            var wrist = r.Hand.TransformPoint(new Vector3(off + 0.004f, -0.045f, -0.012f));
            var elbow = ct.position + ct.right * (0.25f * side) - ct.up * 0.4f + ct.forward * 0.1f;
            var d = elbow - wrist;
            float len = d.magnitude;
            r.Forearm.position = wrist;
            r.Forearm.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            r.Forearm.localScale = new Vector3(1f, len, 1f);
        }

        // ---------------------------------------------------------------- grip helpers

        /// <summary>Where and how to grip an item (local to the item).</summary>
        public static void Grip(Item it, out Vector3 local, out Quaternion localRot, out float radius)
        {
            localRot = Quaternion.identity;
            switch (it)
            {
                case Bottle b: local = new Vector3(0, b.Height * 0.34f, 0); radius = b.Radius; break;
                case Vessel v when v.VKind == VesselKind.Jigger: local = new Vector3(0, v.WaistY, 0); radius = 0.012f; break;
                case Vessel v when v.VKind == VesselKind.Cup: local = new Vector3(0, 0.03f, 0); radius = 0.03f; break;
                case Vessel v: local = new Vector3(0, Mathf.Min(v.Height * 0.4f, 0.07f), 0); radius = v.Radius * 0.96f; break;
                case BarSpoon _: local = new Vector3(0, 0.235f, 0); radius = 0.004f; break;
                case Muddler _: local = new Vector3(0, 0.165f, 0); radius = 0.011f; break;
                case IceScoop _: local = new Vector3(0, 0.04f, -0.1f); localRot = Quaternion.Euler(90f, 0, 0); radius = 0.009f; break;
                case StrainerItem _: local = new Vector3(0, 0.008f, -0.1f); localRot = Quaternion.Euler(90f, 0, 0); radius = 0.006f; break;
                case ShakerCap _: local = new Vector3(0, 0.07f, 0); radius = 0.04f; break;
                default: local = new Vector3(0, 0.01f, 0); radius = 0.008f; break;
            }
        }
    }
}
