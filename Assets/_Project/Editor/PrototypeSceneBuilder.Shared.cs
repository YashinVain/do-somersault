using System.Collections.Generic;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // Small helpers the later locations share.
    public static partial class PrototypeSceneBuilder
    {
        // Every location block is 90 m square, stands 230 m from the last one and has the water at y = 0.
        const float SeaBlockSize = 90f, SeaBlockZ = 15f;

        // A round beam between two points, in the parent's space.
        static Transform Beam(string name, Transform parent, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            var t = Put(hex, name, parent, (a + b) * 0.5f, new Vector3(thickness, (b - a).magnitude, thickness), mat);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            return t;
        }

        // A box that is also something the body bumps into.
        static Transform Solid(string name, Transform parent, Vector3 center, Vector3 size, Material mat, List<Bounds> solids)
        {
            solids.Add(new Bounds(center, size));
            return Box(name, parent, center, size, mat);
        }

        // Water, a floor under it and the soil block, for the locations that stand in the open sea.
        static Material SeaBlock(Transform site, string waterName, string waterColor, float alpha, float swellCells = 30)
        {
            var sea = Mat(waterName, waterColor, transparent: true, alpha: alpha, smoothness: 0.5f);
            WaterSurface(waterName, site, new Vector3(0f, 0f, SeaBlockZ), SeaBlockSize, (int)swellCells, sea);
            BlockBase(site, new Vector2(0f, SeaBlockZ), new Vector2(SeaBlockSize, SeaBlockSize), -6f, sea);
            Box("Seabed", site, new Vector3(0f, -6f, SeaBlockZ), new Vector3(SeaBlockSize, 0.1f, SeaBlockSize), Mat("Sand", "#E3CB98"));
            return sea;
        }

        static void SeaGulls(Transform site, Vector3 center)
        {
            var root = Child("Life", site);
            var white = Mat("Gull_White", "#F4F4F0");
            var grey = Mat("Gull_Grey", "#9AA3AA");
            var tip = Mat("Gull_Tip", "#3B4048");
            var beak = Mat("Gull_Beak", "#F2B233");
            Gull(root, center, 16f, 20f, 0.22f, 0f, white, grey, tip, beak);
            Gull(root, center + new Vector3(-3f, 0f, 4f), 22f, 24f, -0.17f, 2f, white, grey, tip, beak);
        }

        // A lit window: a thin box that glows, for night scenes.
        static void LitWindow(Transform parent, Vector3 at, Vector3 size, Material lit)
        {
            Box("Window", parent, at, size, lit);
        }

        // Collision for a faceted wall (LowPolyMeshes.CliffWall): one box per cell of the wall's grid, its front set to the average
        // height of the real corners there, so the body meets the visible rock instead of a flat plane well inside it.
        static void WallSolids(Mesh wall, List<Bounds> solids, float width, int cols, int rows, float bottom, float top, float backZ, float inset = 0.25f)
        {
            var verts = wall.vertices;
            float cw = width / cols, rh = (top - bottom) / rows;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    float x0 = -width * 0.5f + i * cw, x1 = x0 + cw, y0 = bottom + j * rh, y1 = y0 + rh;
                    float sum = 0f;
                    int n = 0;
                    foreach (var v in verts)
                    {
                        if (v.z < backZ + 1f) continue;
                        if (v.x < x0 - 0.6f || v.x > x1 + 0.6f || v.y < y0 - 0.6f || v.y > y1 + 0.6f) continue;
                        sum += v.z;
                        n++;
                    }
                    if (n == 0) continue;
                    float front = sum / n - inset;
                    solids.Add(new Bounds(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (backZ + front) * 0.5f), new Vector3(cw, rh, front - backZ)));
                }
        }

        // A stack of boxes that follows a sloping side, for rocks, bows and icebergs: from y0 to y1 the half-width goes from r0 to r1.
        static void TaperedSolids(List<Bounds> solids, Vector3 center, float y0, float y1, float r0, float r1, int steps = 4, float lengthScale = 1f)
        {
            for (int k = 0; k < steps; k++)
            {
                float a = y0 + (y1 - y0) * k / steps, b = y0 + (y1 - y0) * (k + 1) / steps;
                // The narrower end of the step, so the box never sticks out of the rock.
                float r = Mathf.Min(Mathf.Lerp(r0, r1, k / (float)steps), Mathf.Lerp(r0, r1, (k + 1f) / steps));
                solids.Add(new Bounds(new Vector3(center.x, (a + b) * 0.5f, center.z), new Vector3(r * 2f, b - a, r * 2f * lengthScale)));
            }
        }

        // A weathered rock pillar: a tapering faceted core, a second twisted core beside it, darker strata bands like the cliff's,
        // a few boulders at the foot and a flat turf (or snow) top. Wide at the water, narrower towards the top.
        static void RockPillar(Transform parent, List<Bounds> solids, string name, Vector3 center, float bottomY, float topY,
                               float radius, float topRadius, Material[] rocks, Material cap, int seed, float squashZ = 1f)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var chunk = RockMesh;
            float height = topY - bottomY;
            Material rock(int i) => rocks[i % rocks.Length];

            string pillarName = topRadius / radius > 0.7f ? "Pillar_B" : "Pillar_A";
            var column = Prop("Cliffs", pillarName, parent, new Vector3(center.x, bottomY, center.z),
                              new Vector3(radius, height, radius * squashZ), R(0f, 360f),
                              ("Rock0", rock(0)), ("Rock1", rock(1)), ("Rock2", rock(2)), ("Cap", cap));
            if (column == null)
            {
                // Main core, a little leaning.
                var main = Put(LowPolyMeshes.Prism(name + "_Core", 7, radius, topRadius * 1.05f, height), name + "_Core", parent,
                               new Vector3(center.x, (bottomY + topY) * 0.5f, center.z), new Vector3(1f, 1f, squashZ), rock(0));
                main.localRotation = Quaternion.Euler(R(-2f, 2f), R(0f, 50f), R(-2f, 2f));
    
                // A second, thinner core twisted against the first breaks the symmetry; it stops short of the top.
                float h2 = height * 0.78f;
                var side = Put(LowPolyMeshes.Prism(name + "_Side", 6, radius * 0.8f, topRadius * 0.82f, h2), name + "_Side", parent,
                               new Vector3(center.x + radius * 0.3f, bottomY + h2 * 0.5f, center.z - radius * 0.18f * squashZ), new Vector3(1f, 1f, squashZ), rock(1));
                side.localRotation = Quaternion.Euler(0f, R(20f, 40f), 0f);
    
                // Strata: thin darker bands a hair wider than the core.
                foreach (float f in new[] { 0.3f, 0.58f, 0.8f })
                {
                    float rr = Mathf.Lerp(radius, topRadius, f) * 1.05f;
                    var band = Put(LowPolyMeshes.Prism(name + "_Band" + (int)(f * 10), 7, rr, rr * 0.99f, 0.45f), name + "_Band", parent,
                                   new Vector3(center.x, bottomY + height * f, center.z), new Vector3(1f, 1f, squashZ), rock(2));
                    band.localRotation = main.localRotation;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                float a = R(0f, 6.28f), r = radius * R(0.95f, 1.25f);
                Put(chunk, name + "_Boulder", parent, center + new Vector3(Mathf.Cos(a) * r, bottomY + R(2.4f, 3.6f), Mathf.Sin(a) * r * squashZ),
                    new Vector3(R(1.8f, 3.2f), R(1.6f, 2.8f), R(1.8f, 3.2f)), rock(i + 1));
            }

            if (column == null)
            {
                var top = Put(LowPolyMeshes.Prism(name + "_Top", 8, topRadius * 1.04f, topRadius, 0.7f), name + "_Top", parent,
                              new Vector3(center.x, topY - 0.35f, center.z), new Vector3(1f, 1f, squashZ), cap);
                top.localRotation = Quaternion.Euler(0f, R(0f, 40f), 0f);
            }
            TaperedSolids(solids, center, bottomY, topY, radius * 0.88f, topRadius * 0.95f, Mathf.Max(3, Mathf.RoundToInt(height / 3.5f)), squashZ);
        }

        // Rounded stones: more facets and little jitter, so they read as boulders rather than spiky shards.
        static int rockCall;
        static Mesh RockMesh => PropMesh("Rocks", "Rock_" + "ABCD"[rockCall++ % 4]) ?? LowPolyMeshes.Icosphere("Boulder", 1, 0.5f, 0.09f, 9);

        // A rock outcrop in a straight wall at x, with its top at height h and reaching `reach` past the face: four blocks stepped
        // in towards the wall as they go down, each turned a few degrees, so it reads as a corbel of the cliff itself.
        static Transform WallLedge(Transform parent, List<Bounds> solids, string name, float x, float h, float reach, float faceZ,
                                   Material top, Material under, Mesh shard)
        {
            (float width, float reachK, float thick, float yaw)[] steps =
            {
                (3.3f, 1f, 1.3f, -3f), (2.9f, 0.76f, 1.5f, 4f), (2.4f, 0.5f, 1.8f, -4f), (1.9f, 0.28f, 2.0f, 5f)
            };
            float y = h;
            var model = Prop("Cliffs", "Ledge_" + "AB"[Variant(new Vector3(x, h, faceZ), 2)], parent, new Vector3(x, h, faceZ),
                             new Vector3(1f, 1f, reach / 2.9f), 0f, ("Top", top), ("Under", under));
            for (int i = 0; i < steps.Length; i++)
            {
                var (w, k, t, yaw) = steps[i];
                float r = reach * k;
                var c = new Vector3(x + (i % 2 == 0 ? 0.1f : -0.1f), y - t * 0.5f, faceZ + r * 0.5f - 0.8f);
                var s = new Vector3(w, t, r + 1.6f);
                if (model == null)
                {
                    var block = Box(name + "_Block", parent, c, s, i % 2 == 0 ? top : under);
                    block.localRotation = Quaternion.Euler(0f, yaw, 0f);
                }
                solids.Add(new Bounds(c, new Vector3(w * 0.96f, t, r + 1.5f)));
                y -= t - 0.1f;
            }

            // A pale line along the front edge to show where to stand.
            Box(name + "_Chalk", parent, new Vector3(x, h + 0.008f, faceZ + reach - 0.1f), new Vector3(2.5f, 0.01f, 0.12f), Mat("PaintWhite", "#F2EFE6"));
            return Mark("Spot_" + name, parent, new Vector3(x, h, faceZ + reach));
        }
    }
}
