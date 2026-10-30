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

        // A rock ledge sticking out of a straight cliff or wall face at x, with its top at height h and reaching `reach` past the face.
        static Transform WallLedge(Transform parent, List<Bounds> solids, string name, float x, float h, float reach, float faceZ,
                                   Material top, Material under, Mesh shard)
        {
            float edge = faceZ + reach, back = faceZ - 2.5f;
            var c = new Vector3(x, h - 0.35f, (edge + back) * 0.5f);
            var s = new Vector3(2.8f, 0.7f, edge - back);
            Box(name, parent, c, s, top);
            Put(shard, name + "_Under", parent, new Vector3(x, h - 1.6f, faceZ + reach * 0.35f), new Vector3(3.2f, 2.6f, reach + 1.2f), under);
            solids.Add(new Bounds(c, s));
            solids.Add(new Bounds(new Vector3(x, h - 1.7f, faceZ + reach * 0.4f), new Vector3(2.6f, 2.2f, reach * 0.8f + 0.6f)));
            return Mark("Spot_" + name, parent, new Vector3(x, h, edge));
        }
    }
}
