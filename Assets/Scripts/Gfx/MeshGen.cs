using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bartender
{
    /// <summary>Collects vertices and triangles; triangles are oriented automatically to face along the vertex normals.</summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<Color> C = new List<Color>();
        public readonly List<List<int>> Subs = new List<List<int>> { new List<int>() };
        public bool UseColors;
        int sub;

        public int Count => V.Count;

        public void SetSub(int s)
        {
            while (Subs.Count <= s) Subs.Add(new List<int>());
            sub = s;
        }

        public int Add(Vector3 p, Vector3 n, Vector2 uv)
        {
            V.Add(p); N.Add(n); UV.Add(uv); C.Add(Color.white);
            return V.Count - 1;
        }

        public int Add(Vector3 p, Vector3 n, Vector2 uv, Color c)
        {
            V.Add(p); N.Add(n); UV.Add(uv); C.Add(c);
            UseColors = true;
            return V.Count - 1;
        }

        /// <summary>Adds a triangle, flipping it if needed so it faces the same way as its vertex normals.</summary>
        public void Tri(int a, int b, int c)
        {
            Vector3 fn = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (fn.sqrMagnitude < 1e-20f) return;
            Vector3 vn = N[a] + N[b] + N[c];
            var t = Subs[sub];
            if (Vector3.Dot(fn, vn) >= 0) { t.Add(a); t.Add(b); t.Add(c); }
            else { t.Add(a); t.Add(c); t.Add(b); }
        }

        public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

        public void Append(Mesh m, Matrix4x4 tr, int toSub = -1)
        {
            var mv = m.vertices; var mn = m.normals; var mu = m.uv; var mc = m.colors;
            int off = V.Count;
            var nm = tr.inverse.transpose;
            for (int i = 0; i < mv.Length; i++)
            {
                V.Add(tr.MultiplyPoint3x4(mv[i]));
                N.Add(nm.MultiplyVector(mn[i]).normalized);
                UV.Add(mu.Length > i ? mu[i] : Vector2.zero);
                C.Add(mc.Length > i ? mc[i] : Color.white);
            }
            if (mc.Length > 0) UseColors = true;
            bool flip = tr.determinant < 0;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                int target = toSub >= 0 ? toSub : s;
                SetSub(target);
                var tris = m.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    if (!flip) { Subs[target].Add(tris[i] + off); Subs[target].Add(tris[i + 1] + off); Subs[target].Add(tris[i + 2] + off); }
                    else { Subs[target].Add(tris[i] + off); Subs[target].Add(tris[i + 2] + off); Subs[target].Add(tris[i + 1] + off); }
                }
            }
            SetSub(0);
        }

        public Mesh ToMesh(string name = "mesh", bool tangents = true)
        {
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(V);
            m.SetNormals(N);
            m.SetUVs(0, UV);
            if (UseColors) m.SetColors(C);
            int used = 0;
            for (int i = 0; i < Subs.Count; i++) if (Subs[i].Count > 0 || i == 0) used = i + 1;
            m.subMeshCount = used;
            for (int i = 0; i < used; i++) m.SetTriangles(Subs[i], i);
            m.RecalculateBounds();
            if (tangents) m.RecalculateTangents();
            return m;
        }
    }

    /// <summary>Profile point for a lathe: radius, height, and whether the surface has a crease here.</summary>
    public struct LP
    {
        public float r, y;
        public bool hard;
        public LP(float r, float y, bool hard = false) { this.r = r; this.y = y; this.hard = hard; }
    }

    public static class MeshGen
    {
        // ------------------------------------------------------------------ lathe

        /// <summary>Surface of revolution around Y. uvMeters sets texture size (0 = 0..1 mapping).</summary>
        public static Mesh Lathe(IList<LP> prof, int seg, float uvMeters = 0f, float a0 = 0f, float a1 = 360f, string name = "lathe")
        {
            var mb = new MeshBuilder();
            LatheInto(mb, prof, seg, uvMeters, a0, a1);
            return mb.ToMesh(name);
        }

        public static void LatheInto(MeshBuilder mb, IList<LP> prof, int seg, float uvMeters = 0f, float a0 = 0f, float a1 = 360f, Matrix4x4? tr = null)
        {
            int n = prof.Count;
            // segment normals in profile space (r, y)
            var segN = new Vector2[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                var d = new Vector2(prof[i + 1].r - prof[i].r, prof[i + 1].y - prof[i].y);
                segN[i] = d.sqrMagnitude > 1e-12f ? new Vector2(d.y, -d.x).normalized : Vector2.up;
            }
            float maxR = 0.001f;
            foreach (var p in prof) maxR = Mathf.Max(maxR, p.r);
            // rows: each profile point once (smooth) or twice (hard)
            var rowsP = new List<Vector2>();
            var rowsN = new List<Vector2>();
            var rowsV = new List<float>();
            var breakBefore = new List<bool>();
            float len = 0;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) len += Vector2.Distance(new Vector2(prof[i].r, prof[i].y), new Vector2(prof[i - 1].r, prof[i - 1].y));
                var p = new Vector2(prof[i].r, prof[i].y);
                if (i == 0) { rowsP.Add(p); rowsN.Add(segN[0]); rowsV.Add(len); breakBefore.Add(false); }
                else if (i == n - 1) { rowsP.Add(p); rowsN.Add(segN[n - 2]); rowsV.Add(len); breakBefore.Add(false); }
                else if (prof[i].hard)
                {
                    rowsP.Add(p); rowsN.Add(segN[i - 1]); rowsV.Add(len); breakBefore.Add(false);
                    rowsP.Add(p); rowsN.Add(segN[i]); rowsV.Add(len); breakBefore.Add(true);
                }
                else
                {
                    var nn = (segN[i - 1] + segN[i]).normalized;
                    if (nn.sqrMagnitude < 0.01f) nn = segN[i];
                    rowsP.Add(p); rowsN.Add(nn); rowsV.Add(len); breakBefore.Add(false);
                }
            }
            bool full = Mathf.Abs(a1 - a0) >= 359.9f;
            Matrix4x4 m = tr ?? Matrix4x4.identity;
            Matrix4x4 nm = m.inverse.transpose;
            int baseIdx = mb.Count;
            int cols = seg + 1;
            float uMul = uvMeters > 0 ? (2f * Mathf.PI * maxR * (a1 - a0) / 360f) / uvMeters : 1f;
            for (int r = 0; r < rowsP.Count; r++)
            {
                for (int j = 0; j <= seg; j++)
                {
                    float t = (float)j / seg;
                    float a = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, t);
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    var p = new Vector3(rowsP[r].x * ca, rowsP[r].y, rowsP[r].x * sa);
                    var nrm = new Vector3(rowsN[r].x * ca, rowsN[r].y, rowsN[r].x * sa);
                    var uv = new Vector2(t * uMul, uvMeters > 0 ? rowsV[r] / uvMeters : rowsV[r] / Mathf.Max(len, 1e-4f));
                    mb.Add(m.MultiplyPoint3x4(p), nm.MultiplyVector(nrm).normalized, uv);
                }
            }
            for (int r = 0; r < rowsP.Count - 1; r++)
            {
                if (breakBefore[r + 1]) continue;
                for (int j = 0; j < seg; j++)
                {
                    int a = baseIdx + r * cols + j, b = a + 1, c = a + cols, d = c + 1;
                    mb.Tri(a, c, d);
                    mb.Tri(a, d, b);
                }
            }
            _ = full;
        }

        /// <summary>Closed cylinder with slightly rounded edges.</summary>
        public static List<LP> CylinderProfile(float r, float h, float bevel = 0.002f)
        {
            bevel = Mathf.Min(bevel, r * 0.3f, h * 0.3f);
            return new List<LP>
            {
                new LP(0, 0), new LP(r - bevel, 0), new LP(r - bevel * 0.3f, bevel * 0.3f), new LP(r, bevel),
                new LP(r, h - bevel), new LP(r - bevel * 0.3f, h - bevel * 0.3f), new LP(r - bevel, h), new LP(0, h),
            };
        }

        public static Mesh Cylinder(float r, float h, int seg = 32, float bevel = 0.002f, float uvMeters = 0f) =>
            Lathe(CylinderProfile(r, h, bevel), seg, uvMeters, name: "cylinder");

        public static Mesh Sphere(float r, int seg = 32, int rings = 16)
        {
            var p = new List<LP>();
            for (int i = 0; i <= rings; i++)
            {
                float a = -Mathf.PI / 2 + Mathf.PI * i / rings;
                p.Add(new LP(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
            var lp = p[0]; lp.r = 0; p[0] = lp;
            lp = p[rings]; lp.r = 0; p[rings] = lp;
            return Lathe(p, seg, name: "sphere");
        }

        // ------------------------------------------------------------------ rounded box

        static List<float> RoundAxis(float h, float r, int k)
        {
            var l = new List<float>();
            for (int j = k; j >= 1; j--) l.Add(-(h - r) - r * Mathf.Tan(Mathf.PI / 4f * j / k));
            l.Add(-(h - r));
            l.Add(h - r);
            for (int j = 1; j <= k; j++) l.Add((h - r) + r * Mathf.Tan(Mathf.PI / 4f * j / k));
            return l;
        }

        /// <summary>Box with rounded edges and corners, centred on the origin.</summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int k = 4, float uvMeters = 1f)
        {
            var mb = new MeshBuilder();
            RoundedBoxInto(mb, size, radius, k, uvMeters, Matrix4x4.identity);
            return mb.ToMesh("roundedbox");
        }

        public static void RoundedBoxInto(MeshBuilder mb, Vector3 size, float radius, int k, float uvMeters, Matrix4x4 tr)
        {
            Vector3 h = size * 0.5f;
            radius = Mathf.Min(radius, h.x * 0.999f, h.y * 0.999f, h.z * 0.999f);
            radius = Mathf.Max(radius, 0.0001f);
            Vector3 inner = h - Vector3.one * radius;
            var nm = tr.inverse.transpose;
            // face: axis index of normal, sign
            for (int axis = 0; axis < 3; axis++)
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    int ua = (axis + 1) % 3, va = (axis + 2) % 3;
                    var lu = RoundAxis(h[ua], radius, k);
                    var lv = RoundAxis(h[va], radius, k);
                    int start = mb.Count;
                    for (int j = 0; j < lv.Count; j++)
                        for (int i = 0; i < lu.Count; i++)
                        {
                            Vector3 p = Vector3.zero;
                            p[axis] = h[axis] * sgn;
                            p[ua] = lu[i];
                            p[va] = lv[j];
                            Vector3 c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                            Vector3 n = (p - c).normalized;
                            Vector3 pos = c + n * radius;
                            Vector2 uv = new Vector2(pos[ua], pos[va]) / uvMeters;
                            if (axis == 1) uv = new Vector2(pos.x, pos.z) / uvMeters;
                            else if (axis == 0) uv = new Vector2(pos.z, pos.y) / uvMeters;
                            else uv = new Vector2(pos.x, pos.y) / uvMeters;
                            mb.Add(tr.MultiplyPoint3x4(pos), nm.MultiplyVector(n).normalized, uv);
                        }
                    int w = lu.Count;
                    for (int j = 0; j < lv.Count - 1; j++)
                        for (int i = 0; i < w - 1; i++)
                        {
                            int a = start + j * w + i;
                            mb.Quad(a, a + 1, a + w + 1, a + w);
                        }
                }
        }

        // ------------------------------------------------------------------ tubes

        /// <summary>Tube along a path (parallel-transport frames). flatten squashes the cross-section.</summary>
        public static void TubeInto(MeshBuilder mb, IList<Vector3> path, float radius, int sides, bool caps = true, float flatten = 1f, float uvMeters = 0.1f, System.Func<float, float> radiusAt = null)
        {
            int n = path.Count;
            if (n < 2) return;
            var tangents = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 t = i == 0 ? path[1] - path[0] : i == n - 1 ? path[n - 1] - path[n - 2] : path[i + 1] - path[i - 1];
                tangents[i] = t.normalized;
            }
            Vector3 normal = Vector3.Cross(tangents[0], Mathf.Abs(tangents[0].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            float len = 0;
            int start = mb.Count;
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                {
                    len += Vector3.Distance(path[i], path[i - 1]);
                    var q = Quaternion.FromToRotation(tangents[i - 1], tangents[i]);
                    normal = (q * normal).normalized;
                }
                Vector3 bin = Vector3.Cross(tangents[i], normal).normalized;
                float rr = radiusAt != null ? radiusAt((float)i / (n - 1)) * radius : radius;
                for (int s = 0; s <= sides; s++)
                {
                    float a = Mathf.PI * 2f * s / sides;
                    Vector3 off = normal * Mathf.Cos(a) + bin * Mathf.Sin(a) * flatten;
                    Vector3 nrm = (normal * Mathf.Cos(a) * flatten + bin * Mathf.Sin(a)).normalized;
                    mb.Add(path[i] + off * rr, nrm, new Vector2((float)s / sides, len / uvMeters));
                }
            }
            int cols = sides + 1;
            for (int i = 0; i < n - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = start + i * cols + s;
                    mb.Quad(a, a + 1, a + cols + 1, a + cols);
                }
            if (caps)
            {
                for (int e = 0; e < 2; e++)
                {
                    int i = e == 0 ? 0 : n - 1;
                    Vector3 cn = e == 0 ? -tangents[0] : tangents[n - 1];
                    int c = mb.Add(path[i], cn, new Vector2(0.5f, 0.5f));
                    int ring = mb.Count;
                    for (int s = 0; s <= sides; s++)
                        mb.Add(mb.V[start + i * cols + s], cn, Vector2.zero);
                    for (int s = 0; s < sides; s++) mb.Tri(c, ring + s, ring + s + 1);
                }
            }
        }

        public static Mesh Tube(IList<Vector3> path, float radius, int sides = 10, bool caps = true, float flatten = 1f)
        {
            var mb = new MeshBuilder();
            TubeInto(mb, path, radius, sides, caps, flatten);
            return mb.ToMesh("tube");
        }

        public static List<Vector3> Helix(Vector3 start, float radius, float height, float turns, int steps)
        {
            var l = new List<Vector3>();
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float a = t * turns * Mathf.PI * 2f;
                l.Add(start + new Vector3(Mathf.Cos(a) * radius, t * height, Mathf.Sin(a) * radius));
            }
            return l;
        }

        /// <summary>Circle of points (for rings / tori).</summary>
        public static List<Vector3> Circle(Vector3 c, float r, int steps, float a0 = 0, float a1 = 360)
        {
            var l = new List<Vector3>();
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, (float)i / steps);
                l.Add(c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            }
            return l;
        }

        public static Mesh Torus(float R, float r, int seg = 48, int sides = 12)
        {
            var mb = new MeshBuilder();
            TubeInto(mb, Circle(Vector3.zero, R, seg), r, sides, false);
            return mb.ToMesh("torus");
        }

        /// <summary>Hanging cable between two points.</summary>
        public static List<Vector3> Catenary(Vector3 a, Vector3 b, float sag, int steps)
        {
            var l = new List<Vector3>();
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                var p = Vector3.Lerp(a, b, t);
                p.y -= sag * 4f * t * (1f - t);
                l.Add(p);
            }
            return l;
        }

        // ------------------------------------------------------------------ organic shapes

        /// <summary>Two-sided curved leaf along +Z.</summary>
        public static void LeafInto(MeshBuilder mb, Matrix4x4 tr, float length, float width, float curl, float cup, int ls = 8, int ws = 4)
        {
            var nm = tr.inverse.transpose;
            for (int side = 0; side < 2; side++)
            {
                int start = mb.Count;
                for (int i = 0; i <= ls; i++)
                {
                    float t = (float)i / ls;
                    float halfW = width * 0.5f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.8f))), 0.9f);
                    for (int j = 0; j <= ws; j++)
                    {
                        float s = (float)j / ws * 2f - 1f;
                        float x = s * halfW;
                        float z = t * length;
                        float y = curl * t * t * length + cup * s * s * halfW;
                        var p = new Vector3(x, y, z);
                        // approximate normal from partial derivatives
                        var du = new Vector3(1, 2f * cup * s / Mathf.Max(width * 0.5f, 1e-4f) * halfW / Mathf.Max(halfW, 1e-4f), 0);
                        var dv = new Vector3(0, 2f * curl * t, 1);
                        var n = Vector3.Cross(dv, du).normalized;
                        if (side == 1) { n = -n; p += n * 0.0004f; }
                        mb.Add(tr.MultiplyPoint3x4(p), nm.MultiplyVector(n).normalized, new Vector2(s * 0.5f + 0.5f, t));
                    }
                }
                int w = ws + 1;
                for (int i = 0; i < ls; i++)
                    for (int j = 0; j < ws; j++)
                    {
                        int a = start + i * w + j;
                        mb.Quad(a, a + 1, a + w + 1, a + w);
                    }
            }
        }

        /// <summary>Citrus wedge: peel (sub 0) and flesh (sub 1). Wedge spans angle around Y.</summary>
        public static Mesh CitrusWedge(float r, float len, float angle, int seg = 10, int rings = 10)
        {
            var mb = new MeshBuilder();
            // peel: ellipsoid patch
            int start = mb.Count;
            for (int i = 0; i <= rings; i++)
            {
                float t = (float)i / rings;
                float phi = Mathf.PI * t;
                float rr = Mathf.Sin(phi) * r;
                float y = -Mathf.Cos(phi) * len * 0.5f;
                for (int j = 0; j <= seg; j++)
                {
                    float a = Mathf.Deg2Rad * angle * j / seg;
                    var p = new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                    var n = new Vector3(Mathf.Cos(a) * Mathf.Sin(phi) / r, -Mathf.Cos(phi) / (len * 0.5f) * -1f * -1f, Mathf.Sin(a) * Mathf.Sin(phi) / r);
                    n = new Vector3(p.x / (r * r), p.y / (len * len * 0.25f), p.z / (r * r)).normalized;
                    mb.Add(p, n, new Vector2((float)j / seg, t));
                }
            }
            int w = seg + 1;
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = start + i * w + j;
                    mb.Quad(a, a + 1, a + w + 1, a + w);
                }
            // flesh: two flat sides
            mb.SetSub(1);
            for (int side = 0; side < 2; side++)
            {
                float a = side == 0 ? 0 : Mathf.Deg2Rad * angle;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var n = Vector3.Cross(dir, Vector3.up).normalized * (side == 0 ? 1f : -1f);
                int axis0 = mb.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float t = (float)i / rings;
                    float y = -Mathf.Cos(Mathf.PI * t) * len * 0.5f;
                    mb.Add(new Vector3(0, y, 0), n, new Vector2(0.5f, t), new Color(1, 1, 1));
                }
                int rim = mb.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float t = (float)i / rings;
                    float phi = Mathf.PI * t;
                    float rr = Mathf.Sin(phi) * r * 0.94f;
                    float y = -Mathf.Cos(phi) * len * 0.5f;
                    mb.Add(dir * rr + Vector3.up * y, n, new Vector2(1f, t), new Color(1, 1, 1));
                }
                for (int i = 0; i < rings; i++) mb.Quad(axis0 + i, rim + i, rim + i + 1, axis0 + i + 1);
            }
            mb.UseColors = false;
            mb.SetSub(0);
            return mb.ToMesh("wedge");
        }

        /// <summary>Corrugated (fluted) panel facing +Z, flutes vertical. Origin at bottom centre.</summary>
        public static Mesh Fluted(float width, float height, float flute, float depth, int stepsPerFlute = 8, float uvMeters = 1f)
        {
            var mb = new MeshBuilder();
            int flutes = Mathf.Max(1, Mathf.RoundToInt(width / flute));
            float fw = width / flutes;
            int cols = flutes * stepsPerFlute;
            for (int c = 0; c <= cols; c++)
            {
                float x = -width / 2 + fw * c / stepsPerFlute;
                float ph = (float)(c % stepsPerFlute) / stepsPerFlute;
                if (c == cols) ph = 1f;
                float z = depth * Mathf.Sin(Mathf.PI * ph);
                float dz = depth * Mathf.PI * Mathf.Cos(Mathf.PI * ph) / fw;
                var n = new Vector3(-dz, 0, 1).normalized;
                mb.Add(new Vector3(x, 0, z), n, new Vector2(x / uvMeters, 0));
                mb.Add(new Vector3(x, height, z), n, new Vector2(x / uvMeters, height / uvMeters));
            }
            for (int c = 0; c < cols; c++)
            {
                int a = c * 2;
                mb.Quad(a, a + 2, a + 3, a + 1);
            }
            return mb.ToMesh("fluted");
        }

        /// <summary>Flat quad facing -Z (like Unity's built-in quad), readable from the front.</summary>
        public static Mesh Quad(float w, float h)
        {
            var mb = new MeshBuilder();
            int a = mb.Add(new Vector3(-w / 2, -h / 2, 0), Vector3.back, new Vector2(0, 0));
            int b = mb.Add(new Vector3(w / 2, -h / 2, 0), Vector3.back, new Vector2(1, 0));
            int c = mb.Add(new Vector3(w / 2, h / 2, 0), Vector3.back, new Vector2(1, 1));
            int d = mb.Add(new Vector3(-w / 2, h / 2, 0), Vector3.back, new Vector2(0, 1));
            mb.Quad(a, b, c, d);
            return mb.ToMesh("quad");
        }

        /// <summary>Horizontal plane facing up, UV in metres / uvMeters.</summary>
        public static Mesh Plane(float w, float d, float uvMeters = 1f)
        {
            var mb = new MeshBuilder();
            int a = mb.Add(new Vector3(-w / 2, 0, -d / 2), Vector3.up, new Vector2(-w / 2, -d / 2) / uvMeters);
            int b = mb.Add(new Vector3(w / 2, 0, -d / 2), Vector3.up, new Vector2(w / 2, -d / 2) / uvMeters);
            int c = mb.Add(new Vector3(w / 2, 0, d / 2), Vector3.up, new Vector2(w / 2, d / 2) / uvMeters);
            int e = mb.Add(new Vector3(-w / 2, 0, d / 2), Vector3.up, new Vector2(-w / 2, d / 2) / uvMeters);
            mb.Quad(a, b, c, e);
            return mb.ToMesh("plane");
        }

        /// <summary>Flat disc (or ring) facing up.</summary>
        public static Mesh Disc(float r, float inner = 0f, int seg = 48)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                mb.Add(d * inner, Vector3.up, new Vector2(0.5f + d.x * inner / r * 0.5f, 0.5f + d.z * inner / r * 0.5f));
                mb.Add(d * r, Vector3.up, new Vector2(0.5f + d.x * 0.5f, 0.5f + d.z * 0.5f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                mb.Quad(a, a + 1, a + 3, a + 2);
            }
            return mb.ToMesh("disc");
        }
    }
}
