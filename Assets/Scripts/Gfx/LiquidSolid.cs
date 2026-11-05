using System.Collections.Generic;
using UnityEngine;

namespace Bartender
{
    /// <summary>
    /// The inside of a vessel as a closed solid. Liquid is drawn by cutting this solid with a level
    /// plane whose height is found so the volume matches the poured millilitres.
    /// </summary>
    public sealed class LiquidSolid
    {
        readonly Vector3[] tv;    // triangle soup positions (3 per triangle)
        readonly Vector3[] tn;    // normals
        public readonly float Volume;  // m^3
        public readonly float RimY, RimR, BottomY;
        readonly List<Vector2> profile;

        // scratch
        readonly List<Vector3> segA = new List<Vector3>(256), segB = new List<Vector3>(256);

        /// <summary>profile: inner wall from the bottom centre (r = 0) up to the rim.</summary>
        public LiquidSolid(List<Vector2> inner, int seg = 32)
        {
            profile = inner;
            RimY = inner[inner.Count - 1].y;
            RimR = inner[inner.Count - 1].x;
            BottomY = inner[0].y;
            var lp = new List<LP>();
            foreach (var p in inner) lp.Add(new LP(p.x, p.y));
            lp.Add(new LP(0, RimY)); // close with a lid at the rim
            var mb = new MeshBuilder();
            MeshGen.LatheInto(mb, lp, seg);
            var tris = mb.Subs[0];
            tv = new Vector3[tris.Count];
            tn = new Vector3[tris.Count];
            for (int i = 0; i < tris.Count; i++) { tv[i] = mb.V[tris[i]]; tn[i] = mb.N[tris[i]]; }
            float raw = ClipVolume(Vector3.up, 10f);
            if (raw < 0)
            {
                // winding came out inverted: flip every triangle so volumes are positive
                for (int i = 0; i < tv.Length; i += 3)
                {
                    var p = tv[i + 1]; tv[i + 1] = tv[i + 2]; tv[i + 2] = p;
                    var q = tn[i + 1]; tn[i + 1] = tn[i + 2]; tn[i + 2] = q;
                }
                raw = ClipVolume(Vector3.up, 10f);
            }
            Volume = raw;
        }

        /// <summary>Inner radius at local height y (for placing ice and garnish).</summary>
        public float RadiusAt(float y)
        {
            if (y <= profile[0].y) return profile[Mathf.Min(1, profile.Count - 1)].x;
            for (int i = 1; i < profile.Count; i++)
            {
                if (y <= profile[i].y)
                {
                    float t = Mathf.InverseLerp(profile[i - 1].y, profile[i].y, y);
                    return Mathf.Lerp(profile[i - 1].x, profile[i].x, t);
                }
            }
            return RimR;
        }

        static float Dist(Vector3 n, float d, Vector3 p) => n.x * p.x + n.y * p.y + n.z * p.z - d;

        /// <summary>Volume of the solid below the plane n·x = d.</summary>
        public float ClipVolume(Vector3 n, float d)
        {
            float vol = 0;
            segA.Clear(); segB.Clear();
            for (int i = 0; i < tv.Length; i += 3)
            {
                Vector3 a = tv[i], b = tv[i + 1], c = tv[i + 2];
                float da = Dist(n, d, a), db = Dist(n, d, b), dc = Dist(n, d, c);
                int inside = (da <= 0 ? 1 : 0) + (db <= 0 ? 1 : 0) + (dc <= 0 ? 1 : 0);
                if (inside == 3) { vol += Vector3.Dot(a, Vector3.Cross(b, c)); continue; }
                if (inside == 0) continue;
                // rotate so the "odd" vertex is first
                if (inside == 1)
                {
                    if (db <= 0) { Rot(ref a, ref b, ref c, ref da, ref db, ref dc); }
                    else if (dc <= 0) { Rot(ref a, ref b, ref c, ref da, ref db, ref dc); Rot(ref a, ref b, ref c, ref da, ref db, ref dc); }
                    Vector3 ab = Vector3.Lerp(a, b, da / (da - db));
                    Vector3 ac = Vector3.Lerp(a, c, da / (da - dc));
                    vol += Vector3.Dot(a, Vector3.Cross(ab, ac));
                    segA.Add(ab); segB.Add(ac);
                }
                else
                {
                    if (db > 0) { Rot(ref a, ref b, ref c, ref da, ref db, ref dc); }
                    else if (dc > 0) { Rot(ref a, ref b, ref c, ref da, ref db, ref dc); Rot(ref a, ref b, ref c, ref da, ref db, ref dc); }
                    // now a is outside, b and c inside
                    Vector3 ab = Vector3.Lerp(b, a, db / (db - da));
                    Vector3 ac = Vector3.Lerp(c, a, dc / (dc - da));
                    vol += Vector3.Dot(b, Vector3.Cross(c, ac));
                    vol += Vector3.Dot(b, Vector3.Cross(ac, ab));
                    segA.Add(ac); segB.Add(ab);
                }
            }
            // cap: planar polygon on the plane, its contribution is d * area / 3 (times 6 for the scale used above)
            Vector3 area = Vector3.zero;
            for (int i = 0; i < segA.Count; i++) area += Vector3.Cross(segA[i], segB[i]);
            float capArea = Mathf.Abs(Vector3.Dot(area, n)) * 0.5f;
            vol += 2f * d * capArea; // (d * A / 3) * 6
            return vol / 6f;
        }

        static void Rot(ref Vector3 a, ref Vector3 b, ref Vector3 c, ref float da, ref float db, ref float dc)
        {
            // (a,b,c) -> (b,c,a): keeps winding
            var t = a; a = b; b = c; c = t;
            float f = da; da = db; db = dc; dc = f;
        }

        /// <summary>Finds the plane height (along n) holding the given volume.</summary>
        public float SolveLevel(Vector3 n, float volume)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < tv.Length; i++)
            {
                float p = Vector3.Dot(n, tv[i]);
                if (p < lo) lo = p;
                if (p > hi) hi = p;
            }
            if (volume <= 0) return lo;
            if (volume >= Volume) return hi;
            for (int it = 0; it < 18; it++)
            {
                float mid = (lo + hi) * 0.5f;
                if (ClipVolume(n, mid) < volume) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>Builds the liquid mesh for plane n·x = d.</summary>
        public void Build(Mesh mesh, Vector3 n, float d, List<Vector3> V, List<Vector3> N, List<int> T)
        {
            V.Clear(); N.Clear(); T.Clear();
            segA.Clear(); segB.Clear();
            for (int i = 0; i < tv.Length; i += 3)
            {
                Vector3 a = tv[i], b = tv[i + 1], c = tv[i + 2];
                Vector3 na = tn[i], nb = tn[i + 1], nc = tn[i + 2];
                float da = Dist(n, d, a), db = Dist(n, d, b), dc = Dist(n, d, c);
                int inside = (da <= 0 ? 1 : 0) + (db <= 0 ? 1 : 0) + (dc <= 0 ? 1 : 0);
                if (inside == 0) continue;
                if (inside == 3) { Emit(V, N, T, a, b, c, na, nb, nc); continue; }
                if (inside == 1)
                {
                    if (db <= 0) { RotN(ref a, ref b, ref c, ref na, ref nb, ref nc, ref da, ref db, ref dc); }
                    else if (dc <= 0) { RotN(ref a, ref b, ref c, ref na, ref nb, ref nc, ref da, ref db, ref dc); RotN(ref a, ref b, ref c, ref na, ref nb, ref nc, ref da, ref db, ref dc); }
                    float tb = da / (da - db), tc = da / (da - dc);
                    Vector3 ab = Vector3.Lerp(a, b, tb), ac = Vector3.Lerp(a, c, tc);
                    Emit(V, N, T, a, ab, ac, na, Vector3.Lerp(na, nb, tb).normalized, Vector3.Lerp(na, nc, tc).normalized);
                    segA.Add(ab); segB.Add(ac);
                }
                else
                {
                    if (db > 0) { RotN(ref a, ref b, ref c, ref na, ref nb, ref nc, ref da, ref db, ref dc); }
                    else if (dc > 0) { RotN(ref a, ref b, ref c, ref na, ref nb, ref nc, ref da, ref db, ref dc); RotN(ref a, ref b, ref c, ref na, ref nb, ref nc, ref da, ref db, ref dc); }
                    float tb = db / (db - da), tc = dc / (dc - da);
                    Vector3 ab = Vector3.Lerp(b, a, tb), ac = Vector3.Lerp(c, a, tc);
                    Vector3 nab = Vector3.Lerp(nb, na, tb).normalized, nac = Vector3.Lerp(nc, na, tc).normalized;
                    Emit(V, N, T, b, c, ac, nb, nc, nac);
                    Emit(V, N, T, b, ac, ab, nb, nac, nab);
                    segA.Add(ac); segB.Add(ab);
                }
            }
            // surface cap: fan around the centroid, sorted by angle
            if (segA.Count >= 3)
            {
                Vector3 cen = Vector3.zero;
                for (int i = 0; i < segA.Count; i++) cen += segA[i];
                cen /= segA.Count;
                Vector3 ax = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 ay = Vector3.Cross(n, ax);
                var pts = new List<Vector3>(segA);
                pts.Sort((p, q) =>
                {
                    float ap = Mathf.Atan2(Vector3.Dot(p - cen, ay), Vector3.Dot(p - cen, ax));
                    float aq = Mathf.Atan2(Vector3.Dot(q - cen, ay), Vector3.Dot(q - cen, ax));
                    return ap.CompareTo(aq);
                });
                int ci = V.Count;
                V.Add(cen); N.Add(n);
                int first = V.Count;
                for (int i = 0; i < pts.Count; i++) { V.Add(pts[i]); N.Add(n); }
                for (int i = 0; i < pts.Count; i++)
                {
                    int a = first + i, b = first + (i + 1) % pts.Count;
                    // orient to face +n
                    Vector3 fn = Vector3.Cross(V[a] - V[ci], V[b] - V[ci]);
                    if (Vector3.Dot(fn, n) >= 0) { T.Add(ci); T.Add(a); T.Add(b); }
                    else { T.Add(ci); T.Add(b); T.Add(a); }
                }
            }
            mesh.Clear();
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds();
        }

        static void Emit(List<Vector3> V, List<Vector3> N, List<int> T, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            N.Add(na); N.Add(nb); N.Add(nc);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
        }

        static void RotN(ref Vector3 a, ref Vector3 b, ref Vector3 c, ref Vector3 na, ref Vector3 nb, ref Vector3 nc, ref float da, ref float db, ref float dc)
        {
            var t = a; a = b; b = c; c = t;
            var tn2 = na; na = nb; nb = nc; nc = tn2;
            float f = da; da = db; db = dc; dc = f;
        }

        /// <summary>Lowest point of the rim for the given up direction (local space).</summary>
        public Vector3 RimLow(Vector3 n, out float height)
        {
            var h = new Vector3(n.x, 0, n.z);
            Vector3 dir = h.sqrMagnitude > 1e-8f ? -h.normalized : Vector3.forward;
            var p = new Vector3(dir.x * RimR, RimY, dir.z * RimR);
            height = Vector3.Dot(n, p);
            return p;
        }
    }
}
