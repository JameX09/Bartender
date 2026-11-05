using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bartender
{
    /// <summary>Merges many small static meshes into one renderer per material (fewer draw calls).</summary>
    public sealed class Combiner
    {
        readonly Dictionary<Material, MeshBuilder> builders = new Dictionary<Material, MeshBuilder>();
        readonly Dictionary<Material, bool> shadow = new Dictionary<Material, bool>();

        public MeshBuilder For(Material m, bool castShadows = true)
        {
            if (!builders.TryGetValue(m, out var b)) { b = new MeshBuilder(); builders[m] = b; shadow[m] = castShadows; }
            return b;
        }

        public void Add(Mesh mesh, Material m, Matrix4x4 tr, bool castShadows = true) => For(m, castShadows).Append(mesh, tr, 0);

        /// <summary>Adds every renderer under root (positions relative to world), then destroys root.</summary>
        public void Absorb(GameObject root, bool destroy = true)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;
                var mats = mr.sharedMaterials;
                var mesh = mf.sharedMesh;
                var tr = mf.transform.localToWorldMatrix;
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    if (mats[s] == null) continue;
                    var b = For(mats[s], mr.shadowCastingMode != ShadowCastingMode.Off);
                    AppendSub(b, mesh, s, tr);
                }
            }
            if (destroy) Object.Destroy(root);
        }

        static void AppendSub(MeshBuilder b, Mesh m, int sub, Matrix4x4 tr)
        {
            var v = m.vertices; var n = m.normals; var uv = m.uv;
            var tris = m.GetTriangles(sub);
            var map = new Dictionary<int, int>();
            var nm = tr.inverse.transpose;
            var list = b.Subs[0];
            bool flip = tr.determinant < 0;
            for (int i = 0; i < tris.Length; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int src = tris[i + (flip && k > 0 ? 3 - k : k)];
                    if (!map.TryGetValue(src, out int dst))
                    {
                        dst = b.V.Count;
                        b.V.Add(tr.MultiplyPoint3x4(v[src]));
                        b.N.Add(n.Length > src ? nm.MultiplyVector(n[src]).normalized : Vector3.up);
                        b.UV.Add(uv.Length > src ? uv[src] : Vector2.zero);
                        b.C.Add(Color.white);
                        map[src] = dst;
                    }
                    list.Add(dst);
                }
            }
        }

        public void Build(Transform parent, string name)
        {
            foreach (var kv in builders)
            {
                if (kv.Value.V.Count == 0) continue;
                var go = new GameObject(name + "_" + kv.Key.name);
                go.transform.SetParent(parent, false);
                go.isStatic = true;
                go.AddComponent<MeshFilter>().sharedMesh = kv.Value.ToMesh(name);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key;
                r.shadowCastingMode = shadow[kv.Key] ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            builders.Clear();
        }
    }
}
