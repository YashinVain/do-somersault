using System.Collections.Generic;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // Night harbour: a quay with lamps, a tug, stacked containers and a gantry crane.
    public static partial class PrototypeSceneBuilder
    {
        static LocationSite BuildPortSite()
        {
            var site = new GameObject("Site_Port").transform;
            SeaBlock(site, "PortWater", "#15507A", 0.9f);

            var concrete = Mat("Port_Concrete", "#6E7378");
            var concreteDark = Mat("Port_ConcreteDark", "#4C5157");
            var steel = Mat("Port_Steel", "#D9A62E");
            var steelGrey = Mat("Port_Grey", "#59616B");
            var hullRed = Mat("Tug_Hull", "#8E2B2B", smoothness: 0.5f);
            var hullBlack = Mat("Tug_Black", "#1E2126");
            var white = Mat("Tug_White", "#E8E6DE");
            var lit = Glow(Mat("Port_Lit", "#FFD98A", smoothness: 0.2f), 1.6f);
            var red = Glow(Mat("Port_RedLight", "#FF5A4A"), 1.3f);
            var cont = new[]
            {
                Mat("Cont_Blue", "#2F5B94"), Mat("Cont_Orange", "#C2602B"), Mat("Cont_Green", "#3C7A52"),
                Mat("Cont_Red", "#9B3030"), Mat("Cont_Grey", "#8A9097"), Mat("Cont_Yellow", "#C9A22E")
            };

            var root = Child("Port", site);
            var shard = LowPolyMeshes.Icosphere("Shard", 0, 0.5f, 0.28f, 13);
            var solids = new List<Bounds>();
            var spots = new Transform[7];
            float half = SeaBlockSize * 0.5f;

            // Quay: a concrete apron along the back of the block, its front edge at z = -3.
            const float quayTop = 1.2f, quayEdge = -3f, quayBack = -30f;
            var quay = new Vector3(0f, (quayTop - 6f) * 0.5f, (quayEdge + quayBack) * 0.5f);
            var quaySize = new Vector3(SeaBlockSize, quayTop + 6f, quayEdge - quayBack);
            Solid("Quay", root, quay, quaySize, concrete, solids);
            Box("QuayLip", root, new Vector3(0f, quayTop - 0.05f, quayEdge + 0.1f), new Vector3(SeaBlockSize, 0.1f, 0.5f), steelGrey);
            Box("QuayStripe", root, new Vector3(0f, quayTop + 0.005f, quayEdge - 0.45f), new Vector3(SeaBlockSize, 0.01f, 0.28f), steel);
            for (float x = -42f; x <= 42f; x += 6f)
            {
                var bollard = Put(hex, "Bollard", root, new Vector3(x, quayTop + 0.3f, quayEdge - 1.1f), new Vector3(0.5f, 0.6f, 0.5f), concreteDark);
                Put(Smooth, "BollardTop", bollard, new Vector3(0f, 0.55f, 0f), new Vector3(1.3f, 0.5f, 1.3f), concreteDark);
                Box("Fender", root, new Vector3(x + 3f, quayTop - 1.2f, quayEdge + 0.12f), new Vector3(0.5f, 1.4f, 0.24f), hullBlack);
            }

            // Lamps along the quay.
            for (float x = -36f; x <= 36f; x += 12f)
            {
                Put(hex, "LampPost", root, new Vector3(x, quayTop + 3.3f, quayEdge - 2.2f), new Vector3(0.18f, 6.6f, 0.18f), steelGrey);
                Box("LampArm", root, new Vector3(x, quayTop + 6.5f, quayEdge - 1.7f), new Vector3(0.14f, 0.14f, 1.2f), steelGrey);
                Box("LampHead", root, new Vector3(x, quayTop + 6.4f, quayEdge - 1.2f), new Vector3(0.7f, 0.12f, 0.35f), lit);
            }

            // Warehouses at the back with lit windows.
            var wallMat = Mat("Port_Wall", "#8A8F96");
            var roofMat = Mat("Port_Roof", "#3D4249");
            float[] whX = { -30f, 6f, 36f };
            float[] whW = { 22f, 16f, 18f };
            for (int i = 0; i < 3; i++)
            {
                Box("Warehouse", root, new Vector3(whX[i], quayTop + 5f, -24f), new Vector3(whW[i], 10f, 10f), wallMat);
                Box("WarehouseRoof", root, new Vector3(whX[i], quayTop + 10.3f, -24f), new Vector3(whW[i] + 0.6f, 0.6f, 10.6f), roofMat);
                for (float wx = -whW[i] * 0.5f + 2f; wx < whW[i] * 0.5f - 1f; wx += 3.2f)
                    LitWindow(root, new Vector3(whX[i] + wx, quayTop + 6f, -18.95f), new Vector3(1.4f, 1f, 0.06f), lit);
            }

            // 0 — the quay edge, 1.2 m, with a life ring on a post.
            spots[0] = Mark("Spot_Quay", root, new Vector3(-30f, quayTop, quayEdge));

            // 1 — a tug moored off the quay, nose to the sea; dive off the roof of its wheelhouse, 4.8 m.
            var t0 = new Vector3(-14f, 0f, 9f);
            Box("TugHull", root, t0 + new Vector3(0f, 1.3f, 0f), new Vector3(4.2f, 2.6f, 10f), hullRed);
            var tugBow = Box("TugBow", root, t0 + new Vector3(0f, 1.3f, 5f), new Vector3(2.9f, 2.6f, 2.9f), hullRed);
            tugBow.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Box("TugBoot", root, t0 + new Vector3(0f, 0.2f, 0f), new Vector3(4.26f, 0.5f, 10.06f), hullBlack);
            Box("TugDeck", root, t0 + new Vector3(0f, 2.62f, 0f), new Vector3(3.9f, 0.05f, 9.6f), steelGrey);
            Solid("Wheelhouse", root, t0 + new Vector3(0f, 3.7f, 1.8f), new Vector3(3f, 2.2f, 3.2f), white, solids);
            Box("WheelhouseRoof", root, t0 + new Vector3(0f, 4.85f, 1.8f), new Vector3(3.3f, 0.12f, 3.5f), steelGrey);
            Box("WheelhouseGlass", root, t0 + new Vector3(0f, 3.95f, 3.42f), new Vector3(2.6f, 0.8f, 0.06f), lit);
            Put(hex, "Funnel", root, t0 + new Vector3(0f, 4.4f, -2.2f), new Vector3(1f, 3.2f, 1f), hullBlack);
            Put(hex, "FunnelBand", root, t0 + new Vector3(0f, 5.4f, -2.2f), new Vector3(1.06f, 0.4f, 1.06f), red);
            solids.Add(new Bounds(t0 + new Vector3(0f, 1.3f, 0f), new Vector3(4.2f, 2.6f, 10.4f)));
            solids.Add(new Bounds(t0 + new Vector3(0f, 4.4f, -2.2f), new Vector3(1.1f, 3.2f, 1.1f)));
            solids.Add(new Bounds(t0 + new Vector3(0f, 1.3f, 6.2f), new Vector3(2f, 2.6f, 1.8f)));
            spots[1] = Mark("Spot_Tug", root, t0 + new Vector3(0f, 4.9f, 3.4f));

            // 2 — a stack of containers on the quay, three high; dive off the top one, 9 m.
            float stackX = 0f, stackFront = quayEdge - 0.6f;
            for (int col = 0; col < 3; col++)
            {
                int high = col == 1 ? 3 : 2;
                for (int row = 0; row < high; row++)
                {
                    var m = cont[(col * 2 + row * 3 + 1) % cont.Length];
                    var c = new Vector3(stackX + (col - 1) * 2.6f, quayTop + 1.3f + row * 2.6f, stackFront - 3.05f);
                    Solid("Container", root, c, new Vector3(2.44f, 2.6f, 6.1f), m, solids);
                    for (float rib = -2.6f; rib <= 2.6f; rib += 0.52f)
                        Box("Rib", root, c + new Vector3(0f, 0f, rib), new Vector3(2.5f, 2.2f, 0.06f), steelGrey);
                }
            }
            float topY = quayTop + 2.6f * 3;
            // A few lower stacks around so it reads as a yard.
            for (int k = 0; k < 4; k++)
            {
                float sx = k < 2 ? -12f - k * 2.7f : 11f + (k - 2) * 2.7f;
                for (int row = 0; row < 2 + k % 2; row++)
                    Solid("Container", root, new Vector3(sx, quayTop + 1.3f + row * 2.6f, stackFront - 3.05f), new Vector3(2.44f, 2.6f, 6.1f), cont[(k + row * 2) % cont.Length], solids);
            }
            spots[2] = Mark("Spot_Containers", root, new Vector3(stackX, topY, stackFront));

            // 3 and 4 — a gantry crane: legs on the quay, a catwalk at 15 m and a boom reaching out over the water.
            float cx = 24f;
            foreach (float lx in new[] { cx - 3f, cx + 3f })
                foreach (float lz in new[] { -8f, -14f })
                {
                    var c = new Vector3(lx, (quayTop + 22f) * 0.5f, lz);
                    var s = new Vector3(0.9f, 22f - quayTop, 0.9f);
                    Solid("CraneLeg", root, c, s, steel, solids);
                }
            Solid("CraneBeam", root, new Vector3(cx, 22.6f, -11f), new Vector3(8.4f, 1.6f, 7.4f), steel, solids);
            Solid("CraneBoom", root, new Vector3(cx, 22.6f, 1f), new Vector3(1.6f, 1.2f, 22f), steel, solids);
            for (float z = -8f; z <= 11f; z += 2.4f)
                Beam("BoomStrut", root, new Vector3(cx, 22f, z), new Vector3(cx, 24.4f, z + 1.2f), 0.14f, steel);
            Box("BoomRail", root, new Vector3(cx, 24.2f, 4f), new Vector3(0.06f, 0.06f, 15f), steelGrey);
            Solid("CraneCabin", root, new Vector3(cx, 21.2f, -4.5f), new Vector3(2f, 1.8f, 2.4f), white, solids);
            Box("CabinGlass", root, new Vector3(cx, 21.35f, -3.28f), new Vector3(1.6f, 0.8f, 0.05f), lit);
            // Catwalk at 15 m in front of the legs, with a ladder up the side leg.
            var catwalk = new Vector3(cx, 14.85f, -5f);
            var catSize = new Vector3(6.6f, 0.3f, 6.4f);
            Solid("Catwalk", root, catwalk, catSize, steelGrey, solids);
            Box("CatwalkEdge", root, new Vector3(cx, 15.005f, -1.9f), new Vector3(6.6f, 0.01f, 0.18f), steel);
            foreach (float rx in new[] { cx - 3.2f, cx + 3.2f })
                Box("CatwalkRail", root, new Vector3(rx, 15.6f, -6.2f), new Vector3(0.06f, 0.06f, 4.2f), steel);
            foreach (float lx in new[] { cx - 0.35f, cx + 0.35f })
                Box("LadderSide", root, new Vector3(lx, 12f, -14.7f), new Vector3(0.06f, 22f, 0.06f), steelGrey);
            for (float y = 2.4f; y < 22f; y += 0.4f)
                Box("Rung", root, new Vector3(cx, y, -14.7f), new Vector3(0.7f, 0.04f, 0.05f), steelGrey);
            Put(Smooth, "BoomLight", root, new Vector3(cx, 24.6f, 11.2f), Vector3.one * 0.4f, red);
            spots[3] = Mark("Spot_Catwalk", root, new Vector3(cx, 15f, -1.8f));
            spots[4] = Mark("Spot_Boom", root, new Vector3(cx, 23.2f, 11.6f));
            Box("BoomTipEdge", root, new Vector3(cx, 23.205f, 11.55f), new Vector3(1.6f, 0.01f, 0.14f), lit);

            // Crane rails and a couple of rail wagons on the quay, so the crane stands somewhere.
            foreach (float rz in new[] { -8f, -14f })
                Box("CraneRail", root, new Vector3(cx, quayTop + 0.05f, rz), new Vector3(SeaBlockSize * 0.4f, 0.1f, 0.2f), steelGrey);

            // 5 — a flat barge out in the harbour, jumping back at the quay, 2.2 m.
            var bg = new Vector3(-4f, 0f, 28f);
            Solid("BargeHull", root, bg + new Vector3(0f, 0.9f, 0f), new Vector3(9f, 2.2f, 4.4f), hullBlack, solids);
            Box("BargeRim", root, bg + new Vector3(0f, 2.05f, 0f), new Vector3(9.3f, 0.2f, 4.7f), hullRed);
            Box("BargeStripe", root, bg + new Vector3(0f, 1.1f, 2.22f), new Vector3(9f, 0.25f, 0.05f), steel);
            for (int k = 0; k < 3; k++)
                Solid("BargeCrate", root, bg + new Vector3(-2.6f + k * 2.6f, 3.35f, 0.9f), new Vector3(2.2f, 2.2f, 2.2f), cont[(k * 2 + 1) % cont.Length], solids);
            Box("BargeLamp", root, bg + new Vector3(3.8f, 2.4f, -1.9f), new Vector3(0.14f, 0.9f, 0.14f), steelGrey);
            Put(Smooth, "BargeLight", root, bg + new Vector3(3.8f, 2.95f, -1.9f), Vector3.one * 0.3f, red);
            spots[5] = Mark("Spot_Barge", root, bg + new Vector3(0f, 2.15f, -1.8f));
            spots[5].localRotation = Quaternion.Euler(0f, 180f, 0f);

            // 6 — a harbour light on the end of the breakwater, jumping west, 12 m.
            var bc = new Vector3(34f, 0f, 32f);
            Put(shard, "BreakwaterRock", root, bc + new Vector3(0f, -0.4f, 0f), new Vector3(10f, 3.6f, 10f), concreteDark);
            Put(LowPolyMeshes.Prism("HarbourLight", 8, 1.7f, 1.25f, 20f), "HarbourLight", root, bc + new Vector3(0f, 4f, 0f), Vector3.one, white);
            Put(LowPolyMeshes.Prism("HarbourLightBand", 8, 1.62f, 1.55f, 1.8f), "Band", root, bc + new Vector3(0f, 6f, 0f), Vector3.one, red);
            Put(LowPolyMeshes.Prism("HarbourGallery", 12, 2.9f, 2.9f, 0.3f), "Gallery", root, bc + new Vector3(0f, 11.85f, 0f), Vector3.one, steelGrey);
            Put(LowPolyMeshes.Prism("HarbourLamp", 8, 0.9f, 0.9f, 1.4f), "Lamp", root, bc + new Vector3(0f, 12.7f, 0f), Vector3.one, lit);
            Put(cone, "HarbourCap", root, bc + new Vector3(0f, 13.9f, 0f), new Vector3(2f, 1.1f, 2f), red);
            solids.Add(new Bounds(bc + new Vector3(0f, 1f, 0f), new Vector3(9f, 6f, 9f)));
            solids.Add(new Bounds(bc + new Vector3(0f, 4f, 0f), new Vector3(2.8f, 16f, 2.8f)));
            solids.Add(new Bounds(bc + new Vector3(0f, 11.85f, 0f), new Vector3(5.6f, 0.3f, 5.6f)));
            spots[6] = Mark("Spot_Harbour", root, bc + new Vector3(-2.3f, 12f, 0f));
            spots[6].localRotation = Quaternion.Euler(0f, -90f, 0f);

            var tower = MakeTower(root, spots, solids, new[] { -1f, 1f, 1f, 1f, 1f, 1f, 1f }, new[] { 0f, 0f, -2.5f, -2.5f, -2.5f, 0f, 0f });
            SeaGulls(site, new Vector3(0f, 0f, 10f));
            site.position = SiteOffsets[3];

            return MakeSite(site, tower, new Look
            {
                sun = new Vector3(48f, 150f, 0f), sunColor = "#9DB4E8", sunIntensity = 0.85f,
                ambientSky = "#5A74B4", ambientEquator = "#44527E", ambientGround = "#222B48",
                fog = "#0E1A36", fogStart = 45f, fogEnd = 175f,
                skyTint = "#101C4A", skyGround = "#1A264E", exposure = 0.8f, atmosphere = 0.45f,
                swell = 0.05f, swellPeriod = 3.8f, swellLength = 12f,
                mapCenter = new Vector3(0f, 5f, 8f), mapDistance = 135f, mapDirection = new Vector3(-0.5f, -0.6f, -1f)
            });
        }
    }
}
