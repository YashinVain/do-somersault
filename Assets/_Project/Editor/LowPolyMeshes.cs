using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // Flat-shaded meshes for the grey-box. Every triangle gets its own vertices so faces read as facets.
    public static class LowPolyMeshes
    {
        const string Dir = "Assets/_Project/Meshes/Generated";

        public static Mesh Prism(string name, int sides, float bottomRadius, float topRadius, float height, bool pivotTop = false)
        {
            var verts = new List<Vector3>();
            float y0 = pivotTop ? -height : -height * 0.5f;
            float y1 = y0 + height;

            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 b0 = Ring(a0, bottomRadius, y0), b1 = Ring(a1, bottomRadius, y0);
                Vector3 t0 = Ring(a0, topRadius, y1), t1 = Ring(a1, topRadius, y1);

                if (topRadius > 0f) Tri(verts, b0, t0, t1);
                Tri(verts, b0, t1, b1);

                Tri(verts, new Vector3(0f, y0, 0f), b0, b1);
                if (topRadius > 0f) Tri(verts, new Vector3(0f, y1, 0f), t1, t0);
            }
            return Save(name, verts);
        }

        public static Mesh Icosphere(string name, int subdivisions, float radius, float jitter = 0f, int seed = 1)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var p = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1)
            };
            var f = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };

            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var next = new List<int>();
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = Mid(p, cache, a, b), bc = Mid(p, cache, b, c), ca = Mid(p, cache, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = next;
            }

            var rng = new System.Random(seed);
            for (int i = 0; i < p.Count; i++)
            {
                float r = radius * (1f + (float)(rng.NextDouble() * 2.0 - 1.0) * jitter);
                p[i] = p[i].normalized * r;
            }

            var verts = new List<Vector3>();
            for (int i = 0; i < f.Count; i += 3)
                Tri(verts, p[f[i]], p[f[i + 2]], p[f[i + 1]]);
            return Save(name, verts);
        }

        static int Mid(List<Vector3> p, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int i)) return i;
            p.Add(((p[a] + p[b]) * 0.5f).normalized * p[a].magnitude);
            cache[key] = p.Count - 1;
            return p.Count - 1;
        }

        static Vector3 Ring(float angle, float r, float y) => new(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);

        static void Tri(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c)
        {
            v.Add(a); v.Add(b); v.Add(c);
        }

        static Mesh Save(string name, List<Vector3> verts)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.asset";

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;

            var tris = new int[verts.Count];
            for (int i = 0; i < tris.Length; i++) tris[i] = i;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
