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
            return Mark("Spot_" + name, parent, new Vector3(x, h, edge));
        }
    }
}
