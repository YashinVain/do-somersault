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

        // Surface of revolution around Y. Profile points are (radius, height), listed from top to bottom.
        public static Mesh Lathe(string name, IList<Vector2> profile, int sides, bool smooth)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            int rings = profile.Count;

            for (int i = 0; i < rings; i++)
            {
                Vector2 t = profile[Mathf.Min(i + 1, rings - 1)] - profile[Mathf.Max(i - 1, 0)];
                Vector2 n2 = new Vector2(-t.y, t.x).normalized;
                for (int j = 0; j <= sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    verts.Add(new Vector3(c * profile[i].x, profile[i].y, s * profile[i].x));
                    normals.Add(new Vector3(c * n2.x, n2.y, s * n2.x));
                }
            }

            int row = sides + 1;
            for (int i = 0; i < rings - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    int ul = i * row + j, ur = ul + 1, ll = ul + row, lr = ll + 1;
                    tris.AddRange(new[] { ul, ur, lr, ul, lr, ll });
                }

            if (smooth) return Save(name, verts, normals, tris);

            var flat = new List<Vector3>();
            foreach (int i in tris) flat.Add(verts[i]);
            return Save(name, flat);
        }

        public static Mesh Capsule(string name, float topRadius, float bottomRadius, float length, int sides = 14, int capRings = 5)
        {
            var p = new List<Vector2>();
            for (int k = 0; k <= capRings; k++)
            {
                float phi = k / (float)capRings * Mathf.PI * 0.5f;
                p.Add(new Vector2(Mathf.Sin(phi) * topRadius, Mathf.Cos(phi) * topRadius));
            }
            for (int k = 0; k <= capRings; k++)
            {
                float phi = Mathf.PI * 0.5f + k / (float)capRings * Mathf.PI * 0.5f;
                p.Add(new Vector2(Mathf.Sin(phi) * bottomRadius, -length + Mathf.Cos(phi) * bottomRadius));
            }
            return Lathe(name, p, sides, true);
        }

        public static Mesh Sphere(string name, float radius, int sides = 16, int rings = 10, float fromDeg = 0f, float toDeg = 180f)
        {
            var p = new List<Vector2>();
            for (int k = 0; k <= rings; k++)
            {
                float phi = Mathf.Lerp(fromDeg, toDeg, k / (float)rings) * Mathf.Deg2Rad;
                p.Add(new Vector2(Mathf.Sin(phi) * radius, Mathf.Cos(phi) * radius));
            }
            return Lathe(name, p, sides, true);
        }

        // A sea cliff as one faceted surface. The face follows faceZ(x) (a headland curving back at the ends),
        // every corner is nudged by noise, each row of facets goes into one of `bands` submeshes for the rock strata,
        // and the last submesh is the grass cap running back from the ragged top edge.
        public static Mesh CliffWall(string name, float width, float bottom, float top, int cols, int rows,
                                     int bands, float capDepthZ, System.Func<float, float> faceZ, int seed)
        {
            var rng = new System.Random(seed);
            float N(float a) => ((float)rng.NextDouble() * 2f - 1f) * a;

            var grid = new Vector3[(cols + 1) * (rows + 1)];
            float cell = width / cols, rowH = (top - bottom) / rows;
            for (int j = 0; j <= rows; j++)
                for (int i = 0; i <= cols; i++)
                {
                    float x = -width * 0.5f + i * cell + (i > 0 && i < cols ? N(cell * 0.18f) : 0f);
                    float y = bottom + j * rowH + (j > 0 ? N(rowH * (j == rows ? 0.22f : 0.16f)) : 0f);
                    // Deeper notches in the upper half read as overhangs and cracks.
                    float z = faceZ(x) + N(0.75f) - (j > rows / 2 ? Mathf.Abs(N(0.5f)) : 0f);
                    grid[j * (cols + 1) + i] = new Vector3(x, y, z);
                }

            var verts = new List<Vector3>();
            var subs = new List<List<int>>();
            for (int b = 0; b <= bands; b++) subs.Add(new List<int>());
            void Tri(int sub, Vector3 a, Vector3 b, Vector3 c)
            {
                subs[sub].Add(verts.Count); verts.Add(a);
                subs[sub].Add(verts.Count); verts.Add(b);
                subs[sub].Add(verts.Count); verts.Add(c);
            }
            Vector3 G(int i, int j) => grid[j * (cols + 1) + i];

            // Face, seen from +Z: screen right is -X.
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    int band = j % bands;
                    Tri(band, G(i + 1, j), G(i + 1, j + 1), G(i, j + 1));
                    Tri(band, G(i + 1, j), G(i, j + 1), G(i, j));
                }

            // Grass cap from the ragged rim back to a flat line.
            for (int i = 0; i < cols; i++)
            {
                var f0 = G(i, rows);
                var f1 = G(i + 1, rows);
                var b0 = new Vector3(f0.x, top, capDepthZ);
                var b1 = new Vector3(f1.x, top, capDepthZ);
                Tri(bands, b0, f0, f1);
                Tri(bands, b0, f1, b1);
            }

            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.SetVertices(verts);
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Faceted ground over a rectangle centred on the origin. Each triangle goes into a submesh by its average
        // height (lake bed, sand, grass); a fourth submesh is the soil skirt that closes the sides down to skirtBottom.
        public static Mesh Terrain(string name, float sizeX, float sizeZ, int cells, System.Func<float, float, float> height,
                                   float sandBelow, float grassAbove, float skirtBottom)
        {
            int n = cells + 1;
            var grid = new Vector3[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = (i / (float)cells - 0.5f) * sizeX, z = (j / (float)cells - 0.5f) * sizeZ;
                    grid[j * n + i] = new Vector3(x, height(x, z), z);
                }

            var verts = new List<Vector3>();
            var subs = new List<List<int>> { new(), new(), new(), new() };
            void Tri(Vector3 a, Vector3 b, Vector3 c, int forced = -1)
            {
                float h = (a.y + b.y + c.y) / 3f;
                int s = forced >= 0 ? forced : h < sandBelow ? 0 : h < grassAbove ? 1 : 2;
                subs[s].Add(verts.Count); verts.Add(a);
                subs[s].Add(verts.Count); verts.Add(b);
                subs[s].Add(verts.Count); verts.Add(c);
            }
            Vector3 G(int i, int j) => grid[j * n + i];

            // Same winding as Quad: seen from above, x right and z up.
            for (int j = 0; j < cells; j++)
                for (int i = 0; i < cells; i++)
                {
                    // Alternate the diagonal so the facets don't line up in stripes.
                    if ((i + j) % 2 == 0)
                    {
                        Tri(G(i, j), G(i, j + 1), G(i + 1, j + 1));
                        Tri(G(i, j), G(i + 1, j + 1), G(i + 1, j));
                    }
                    else
                    {
                        Tri(G(i, j), G(i, j + 1), G(i + 1, j));
                        Tri(G(i + 1, j), G(i, j + 1), G(i + 1, j + 1));
                    }
                }

            // Skirt: each edge segment dropped to skirtBottom, facing outwards.
            // a is the left end and b the right end as seen from outside the block.
            void Skirt(Vector3 a, Vector3 b)
            {
                var a0 = new Vector3(a.x, skirtBottom, a.z);
                var b0 = new Vector3(b.x, skirtBottom, b.z);
                Tri(a0, a, b, 3);
                Tri(a0, b, b0, 3);
            }
            for (int i = 0; i < cells; i++)
            {
                Skirt(G(i, 0), G(i + 1, 0));                 // -z side
                Skirt(G(i + 1, cells), G(i, cells));         // +z side
                Skirt(G(0, i + 1), G(0, i));                 // -x side
                Skirt(G(cells, i), G(cells, i + 1));         // +x side
            }

            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // One bay of a stone viaduct running along Z: a solid slab of width `width` (X) and length `length` (Z)
        // from baseY up to `top`, with a round arch cut through it. The arch springs at springY and has the given radius.
        public static Mesh ViaductBay(string name, float width, float length, float baseY, float top, float springY, float radius, int steps = 10)
        {
            var verts = new List<Vector3>();
            float hw = width * 0.5f, hl = length * 0.5f;

            // Triangles are wound so that they face along n, whatever order the corners come in.
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f) { Tri(verts, a, c, b); Tri(verts, a, d, c); }
                else { Tri(verts, a, b, c); Tri(verts, a, c, d); }
            }
            float Arch(float z) => springY + Mathf.Sqrt(Mathf.Max(0f, radius * radius - z * z));

            // Sample positions across the length: both piers and the opening between them.
            var zs = new List<float> { -hl };
            for (int i = 0; i <= steps; i++) zs.Add(Mathf.Lerp(-radius, radius, i / (float)steps));
            zs.Add(hl);

            for (int i = 0; i + 1 < zs.Count; i++)
            {
                float z0 = zs[i], z1 = zs[i + 1];
                bool opening = z0 >= -radius - 0.001f && z1 <= radius + 0.001f;
                float y0 = opening ? Arch(z0) : baseY, y1 = opening ? Arch(z1) : baseY;

                foreach (float sx in new[] { -hw, hw })
                    Quad(new Vector3(sx, y0, z0), new Vector3(sx, top, z0), new Vector3(sx, top, z1), new Vector3(sx, y1, z1), new Vector3(sx, 0f, 0f));

                Quad(new Vector3(-hw, top, z0), new Vector3(-hw, top, z1), new Vector3(hw, top, z1), new Vector3(hw, top, z0), Vector3.up);

                if (opening)
                {
                    var down = new Vector3(0f, -(z1 - z0), y1 - y0);
                    Quad(new Vector3(-hw, y0, z0), new Vector3(hw, y0, z0), new Vector3(hw, y1, z1), new Vector3(-hw, y1, z1), down);
                }
            }

            // The straight sides of the opening, below where the arch springs.
            Quad(new Vector3(-hw, baseY, -radius), new Vector3(hw, baseY, -radius), new Vector3(hw, springY, -radius), new Vector3(-hw, springY, -radius), Vector3.forward);
            Quad(new Vector3(-hw, baseY, radius), new Vector3(hw, baseY, radius), new Vector3(hw, springY, radius), new Vector3(-hw, springY, radius), Vector3.back);
            return Save(name, verts);
        }

        // Triangle flag hanging down from its top edge, visible from both sides.
        public static Mesh Pennant(string name, float width, float height)
        {
            var a = new Vector3(-width * 0.5f, 0f, 0f);
            var b = new Vector3(width * 0.5f, 0f, 0f);
            var c = new Vector3(0f, -height, 0f);
            var verts = new List<Vector3> { a, b, c, a, c, b };
            return Save(name, verts);
        }

        // A closed gable roof of size 1x1x1 (scale it): the ridge runs along X at the top, the eaves lie on the Z edges,
        // the ends are flat triangles. The origin is the middle of the base.
        public static Mesh GableRoof(string name)
        {
            const float x = 0.5f, z = 0.5f;
            Vector3 a = new(-x, 0f, -z), b = new(x, 0f, -z), c = new(x, 0f, z), d = new(-x, 0f, z);
            Vector3 r0 = new(-x, 1f, 0f), r1 = new(x, 1f, 0f);
            var centre = new Vector3(0f, 0.3f, 0f);
            var v = new List<Vector3>();
            Facing(v, centre, d, c, r1, r0);   // slope towards +Z
            Facing(v, centre, b, a, r0, r1);   // slope towards -Z
            Facing(v, centre, c, b, r1);       // end at +X
            Facing(v, centre, a, d, r0);       // end at -X
            Facing(v, centre, a, b, c, d);     // underside
            return Save(name, v);
        }

        // Adds a triangle or quad, wound so that it faces away from `inside`.
        static void Facing(List<Vector3> v, Vector3 inside, params Vector3[] corners)
        {
            var normal = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]);
            var centre = Vector3.zero;
            foreach (var p in corners) centre += p;
            centre /= corners.Length;
            if (Vector3.Dot(normal, centre - inside) < 0f) System.Array.Reverse(corners);
            Tri(v, corners[0], corners[1], corners[2]);
            if (corners.Length == 4) Tri(v, corners[0], corners[2], corners[3]);
        }

        // Flat rectangle facing up, centred on the origin.
        public static Mesh Quad(string name, float sizeX, float sizeZ)
        {
            float x = sizeX * 0.5f, z = sizeZ * 0.5f;
            var verts = new List<Vector3> { new(-x, 0f, -z), new(-x, 0f, z), new(x, 0f, z), new(x, 0f, -z) };
            var tris = new List<int> { 0, 1, 2, 0, 2, 3 };
            var normals = new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            return Save(name, verts, normals, tris);
        }

        // Ring-shaped tube (a torus): life buoys, inflatables.
        public static Mesh Torus(string name, float radius, float tube, int sides = 24, int tubeSides = 10)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                var c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= tubeSides; j++)
                {
                    float b = j / (float)tubeSides * Mathf.PI * 2f;
                    var n = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    verts.Add(c * radius + n * tube);
                    normals.Add(n);
                }
            }
            int row = tubeSides + 1;
            for (int i = 0; i < sides; i++)
                for (int j = 0; j < tubeSides; j++)
                {
                    int a = i * row + j, b = a + row, c = a + 1, d = b + 1;
                    tris.AddRange(new[] { a, c, d, a, d, b });
                }
            return Save(name, verts, normals, tris);
        }

        // Flat arrow pointing along +X, extruded along Z, flat-shaded.
        public static Mesh Arrow(string name, float length, float shaft, float head, float headLength, float depth)
        {
            float x0 = -length * 0.5f, x1 = length * 0.5f - headLength, x2 = length * 0.5f;
            Vector2[] outline =
            {
                new(x0, -shaft * 0.5f), new(x1, -shaft * 0.5f), new(x1, -head * 0.5f), new(x2, 0f),
                new(x1, head * 0.5f), new(x1, shaft * 0.5f), new(x0, shaft * 0.5f)
            };
            float zf = -depth * 0.5f, zb = depth * 0.5f;
            var verts = new List<Vector3>();
            void T(Vector3 a, Vector3 b, Vector3 c) { verts.Add(a); verts.Add(b); verts.Add(c); }

            // Front (facing -Z) and back caps: the shaft as two triangles, the head as one.
            int[][] caps = { new[] { 0, 6, 5 }, new[] { 0, 5, 1 }, new[] { 2, 4, 3 } };
            foreach (var t in caps)
            {
                T(V(outline[t[0]], zf), V(outline[t[1]], zf), V(outline[t[2]], zf));
                T(V(outline[t[0]], zb), V(outline[t[2]], zb), V(outline[t[1]], zb));
            }
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                T(V(a, zf), V(b, zf), V(b, zb));
                T(V(a, zf), V(b, zb), V(a, zb));
            }
            return Save(name, verts);

            static Vector3 V(Vector2 p, float z) => new(p.x, p.y, z);
        }

        // Flat ring facing up with alpha in the vertex colours: 0 at both edges, 1 in the middle.
        // With inner = 0 it becomes a disc that is opaque in the centre and fades out to the rim.
        public static Mesh SoftRing(string name, float inner, float outer, int sides = 40, int bands = 6)
        {
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            int row = sides + 1;

            for (int k = 0; k <= bands; k++)
            {
                float u = k / (float)bands;
                float r = Mathf.Lerp(inner, outer, u);
                float a = inner > 0f ? Mathf.Sin(u * Mathf.PI) : 1f - u * u;
                for (int j = 0; j <= sides; j++)
                {
                    float ang = j / (float)sides * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
                    colors.Add(new Color(1f, 1f, 1f, a));
                }
            }

            for (int k = 0; k < bands; k++)
                for (int j = 0; j < sides; j++)
                {
                    int i0 = k * row + j, i1 = i0 + 1, o0 = i0 + row, o1 = o0 + 1;
                    tris.AddRange(new[] { i0, o1, o0, i0, i1, o1 });
                }

            var normals = new List<Vector3>();
            foreach (var _ in verts) normals.Add(Vector3.up);
            var mesh = Save(name, verts, normals, tris);
            mesh.SetColors(colors);
            EditorUtility.SetDirty(mesh);
            return mesh;
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
            var tris = new List<int>(verts.Count);
            for (int i = 0; i < verts.Count; i++) tris.Add(i);
            return Save(name, verts, null, tris);
        }

        static Mesh Save(string name, List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.asset";

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            if (normals != null) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
