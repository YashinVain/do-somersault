using System.Collections.Generic;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // Old town on a canal: a quay lined with houses, a stone footbridge, a balcony, a bell tower and a cathedral dome.
    public static partial class PrototypeSceneBuilder
    {
        static LocationSite BuildTownSite()
        {
            var site = new GameObject("Site_Town").transform;
            SeaBlock(site, "CanalWater", "#3F8F8C", 0.9f, 28);

            var stone = Mat("Town_Stone", "#C9BEAA");
            var stoneDark = Mat("Town_StoneDark", "#9A9080");
            var roofTile = Mat("Town_Roof", "#B5512F");
            var glassMat = Mat("Town_Glass", "#2B3A48", smoothness: 0.8f);
            var wood = Mat("Town_Wood", "#7A5233");
            var green = Mat("Town_Green", "#4E7A4A");
            var plaster = new[]
            {
                Mat("Town_Sand", "#E8C99B"), Mat("Town_Salmon", "#E7A58D"), Mat("Town_Cream", "#EDE1B4"),
                Mat("Town_Mint", "#9CC7B8"), Mat("Town_Mauve", "#C9A3B8"), Mat("Town_Yellow", "#F0D58A")
            };
            var shard = RockMesh;

            var root = Child("Town", site);
            var solids = new List<Bounds>();
            var spots = new Transform[7];

            const float quayTop = 0.9f, quayEdge = -3f, front = -4.2f;
            Solid("Quay", root, new Vector3(0f, (quayTop - 6f) * 0.5f, (quayEdge - 30f) * 0.5f), new Vector3(SeaBlockSize, quayTop + 6f, 27f), stone, solids);
            Box("QuayCap", root, new Vector3(0f, quayTop - 0.04f, quayEdge + 0.08f), new Vector3(SeaBlockSize, 0.12f, 0.4f), stoneDark);
            for (float x = -43f; x <= 43f; x += 7f)
            {
                Put(hex, "Mooring", root, new Vector3(x, quayTop + 0.25f, quayEdge - 0.5f), new Vector3(0.28f, 0.5f, 0.28f), stoneDark);
                Box("Ring", root, new Vector3(x, quayTop - 0.6f, quayEdge + 0.02f), new Vector3(0.3f, 0.3f, 0.05f), wood);
            }

            // The row of houses. Each: x centre, width, height, colour; every house has windows and a tiled roof.
            (float x, float w, float h, int c)[] houses =
            {
                (-40f, 8f, 10f, 0), (-33f, 6f, 12f, 1), (-27.5f, 5f, 9f, 2), (-20f, 10f, 13f, 3), (-12f, 6f, 8f, 4),
                (-6f, 6f, 10f, 5), (0f, 6f, 9f, 0), (6f, 6.5f, 12f, 1), (12.5f, 7f, 8f, 2), (26f, 6.5f, 9f, 3)
            };
            foreach (var (x, w, h, c) in houses)
            {
                var body = new Vector3(x, quayTop + h * 0.5f, front - 4.5f);
                Solid("House", root, body, new Vector3(w, h, 9f), plaster[c], solids);
                // Gable roof: two tilted slabs meeting at a ridge that runs along X.
                for (int s = -1; s <= 1; s += 2)
                {
                    var slope = Box("Roof", root, new Vector3(x, quayTop + h + 0.85f, front - 4.5f + s * 2.2f), new Vector3(w + 0.8f, 0.3f, 5.4f), roofTile);
                    slope.localRotation = Quaternion.Euler(-s * 26f, 0f, 0f);
                }
                for (float fy = 2.5f; fy < h - 1.5f; fy += 3f)
                    for (float wx = -w * 0.5f + 1.6f; wx < w * 0.5f - 0.8f; wx += 2.4f)
                    {
                        Box("Window", root, new Vector3(x + wx, quayTop + fy, front + 0.03f), new Vector3(0.9f, 1.4f, 0.07f), glassMat);
                        Box("Sill", root, new Vector3(x + wx, quayTop + fy - 0.78f, front + 0.12f), new Vector3(1.2f, 0.1f, 0.26f), stoneDark);
                    }
            }

            // 0 — the quay steps, 0.9 m, with stairs going down into the canal.
            for (int i = 0; i < 4; i++)
                Solid("Step", root, new Vector3(-34f, quayTop - 0.3f - i * 0.3f, quayEdge + 0.35f + i * 0.5f), new Vector3(3f, 0.3f, 0.7f), stoneDark, solids);
            spots[0] = Mark("Spot_Steps", root, new Vector3(-34f, quayTop, quayEdge));

            // 1 — a stone footbridge on an arch between two piers, 5 m.
            foreach (float px in new[] { -10.5f, 10.5f })
            {
                Solid("BridgePier", root, new Vector3(px, (quayTop - 6f) * 0.5f, 0f), new Vector3(5f, quayTop + 6f, 7f), stone, solids);
            }
            var arch = LowPolyMeshes.ViaductBay("BridgeArch", 3.2f, 14f, -6f, 4.6f, -1.3f, 5.5f, 12);
            var archT = Put(arch, "Arch", root, new Vector3(0f, 0f, 1.5f), Vector3.one, stone);
            archT.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Solid("BridgeDeck", root, new Vector3(0f, 4.8f, 1.5f), new Vector3(14f, 0.4f, 3.2f), stoneDark, solids);
            Box("BridgeBack", root, new Vector3(0f, 5.3f, -0.05f), new Vector3(14f, 0.6f, 0.3f), stone);
            foreach (var (x0, x1) in new[] { (-7f, -2f), (2f, 7f) })
                Box("BridgeFront", root, new Vector3((x0 + x1) * 0.5f, 5.3f, 3.05f), new Vector3(x1 - x0, 0.6f, 0.3f), stone);
            // Ramps down from the ends of the deck to the piers.
            foreach (float s in new[] { -1f, 1f })
            {
                var ramp = Box("BridgeRamp", root, new Vector3(s * 9.9f, 2.95f, 1.5f), new Vector3(6.6f, 0.4f, 3.2f), stoneDark);
                ramp.localRotation = Quaternion.Euler(0f, 0f, -s * 38f);
                // The ramp is solid underneath: a few steps down to the pier.
                for (int k = 0; k < 4; k++)
                {
                    float xa = 7.6f + k * 1.3f, xb = xa + 1.3f;
                    float top = 5f - (xb - 7.3f) * (4.1f / 5.2f);
                    solids.Add(new Bounds(new Vector3(s * (xa + xb) * 0.5f, (quayTop + top) * 0.5f, 1.5f), new Vector3(1.3f, top - quayTop, 3.2f)));
                }
            }
            spots[1] = Mark("Spot_Bridge", root, new Vector3(0f, 5f, 3.1f));

            // 2 — a balcony on a tall house, 9 m.
            float bx = -20f;
            var balconySlab = new Vector3(bx, 8.875f, front + 1.2f);
            var balconySize = new Vector3(4f, 0.25f, 2.4f);
            Solid("Balcony", root, balconySlab, balconySize, stone, solids);
            foreach (float s in new[] { -1f, 1f })
            {
                Box("BalconyRail", root, new Vector3(bx + s * 1.9f, 9.5f, front + 1.2f), new Vector3(0.06f, 0.06f, 2.3f), Mat("Town_Iron", "#2D3138"));
                Beam("BalconyBracket", root, new Vector3(bx + s * 1.6f, 8.6f, front + 0.2f), new Vector3(bx + s * 1.6f, 7.5f, front + 0.2f - 0.1f), 0.12f, stoneDark);
            }
            Box("BalconyFrontRail", root, new Vector3(bx - 1f, 9.5f, front + 2.35f), new Vector3(1.8f, 0.06f, 0.06f), Mat("Town_Iron", "#2D3138"));
            Box("BalconyFrontRail", root, new Vector3(bx + 1.3f, 9.5f, front + 2.35f), new Vector3(1.4f, 0.06f, 0.06f), Mat("Town_Iron", "#2D3138"));
            Box("BalconyDoor", root, new Vector3(bx, 10f, front + 0.04f), new Vector3(1.1f, 2f, 0.08f), wood);
            Box("Planter", root, new Vector3(bx + 1.5f, 9.15f, front + 2.1f), new Vector3(0.9f, 0.3f, 0.3f), wood);
            Put(Smooth, "Flowers", root, new Vector3(bx + 1.5f, 9.4f, front + 2.1f), new Vector3(0.8f, 0.35f, 0.3f), Mat("Town_Geranium", "#D8434F"));
            spots[2] = Mark("Spot_Balcony", root, new Vector3(bx, 9f, front + 2.4f));

            // 3 — a bell tower with a stone balcony, 15 m.
            float tx = 20f;
            Solid("BellTower", root, new Vector3(tx, quayTop + 11f, front - 2.5f), new Vector3(5f, 22f, 5f), plaster[2], solids);
            Box("BelfryBand", root, new Vector3(tx, quayTop + 17.5f, front - 2.5f), new Vector3(5.3f, 0.4f, 5.3f), stone);
            foreach (float s in new[] { -1f, 0f, 1f })
                Box("BelfryArch", root, new Vector3(tx + s * 1.5f, quayTop + 19.4f, front + 0.03f), new Vector3(1f, 2.4f, 0.08f), glassMat);
            Box("BelfryTop", root, new Vector3(tx, quayTop + 21.2f, front - 2.5f), new Vector3(5.5f, 0.5f, 5.5f), stone);
            var spire = Put(LowPolyMeshes.Prism("BellRoof", 4, 3.9f, 0f, 5f), "BellRoof", root, new Vector3(tx, quayTop + 24f, front - 2.5f), Vector3.one, roofTile);
            spire.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Put(hex, "BellMast", root, new Vector3(tx, quayTop + 27f, front - 2.5f), new Vector3(0.12f, 2f, 0.12f), Mat("Town_Iron", "#2D3138"));
            for (float y = 2f; y < 17f; y += 3f)
                Box("TowerWindow", root, new Vector3(tx, quayTop + y, front + 0.03f), new Vector3(0.7f, 1.4f, 0.07f), glassMat);
            var bell = new Vector3(tx, 14.875f, front + 1.2f);
            Solid("BellBalcony", root, bell, new Vector3(3.6f, 0.25f, 2.4f), stone, solids);
            foreach (float s in new[] { -1f, 1f })
            {
                Box("BellRail", root, new Vector3(tx + s * 1.7f, 15.5f, front + 1.2f), new Vector3(0.06f, 0.06f, 2.3f), Mat("Town_Iron", "#2D3138"));
                Beam("BellBracket", root, new Vector3(tx + s * 1.4f, 14.7f, front + 0.2f), new Vector3(tx + s * 1.4f, 13.6f, front + 0.1f), 0.12f, stoneDark);
            }
            spots[3] = Mark("Spot_Tower", root, new Vector3(tx, 15f, front + 2.4f));

            // 4 — the drum of a cathedral dome, 22 m.
            float dx = 36f, dz = front - 7f;
            Solid("Cathedral", root, new Vector3(dx, quayTop + 7f, front - 8f), new Vector3(14f, 14f, 16f), plaster[2], solids);
            Box("CathedralPediment", root, new Vector3(dx, quayTop + 14.4f, front - 0.2f), new Vector3(14.6f, 0.8f, 0.6f), stone);
            for (float cx = -5f; cx <= 5f; cx += 2.5f)
                Put(hex, "Column", root, new Vector3(dx + cx, quayTop + 5f, front + 0.5f), new Vector3(0.7f, 10f, 0.7f), stone);
            Put(LowPolyMeshes.Prism("DomeDrum", 14, 4.9f, 4.9f, 8f), "DomeDrum", root, new Vector3(dx, 18f, dz), Vector3.one, plaster[2]);
            solids.Add(new Bounds(new Vector3(dx, 18f, dz), new Vector3(9.8f, 8f, 9.8f)));
            Put(LowPolyMeshes.Prism("DomeRing", 14, 5.9f, 5.9f, 0.4f), "DomeRing", root, new Vector3(dx, 21.8f, dz), Vector3.one, stone);
            solids.Add(new Bounds(new Vector3(dx, 21.8f, dz), new Vector3(11.8f, 0.4f, 11.8f)));
            Put(LowPolyMeshes.Sphere("Dome", 4.9f, 18, 8, 0f, 90f), "Dome", root, new Vector3(dx, 22f, dz), Vector3.one, Mat("Town_Dome", "#7BA8A0", smoothness: 0.4f));
            Put(hex, "Lantern", root, new Vector3(dx, 28.3f, dz), new Vector3(1.2f, 2.4f, 1.2f), stone);
            Put(Smooth, "LanternTop", root, new Vector3(dx, 29.9f, dz), Vector3.one * 1.2f, Mat("Town_Gold", "#D9B04A", smoothness: 0.7f));
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                Box("DrumWindow", root, new Vector3(dx + Mathf.Sin(a) * 4.95f, 18.5f, dz + Mathf.Cos(a) * 4.95f), new Vector3(0.8f, 2f, 0.1f), glassMat).localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            }
            var porch = new Vector3(dx, 21.875f, front + 1.1f);
            Solid("DomePorch", root, porch, new Vector3(3.4f, 0.25f, 2.6f), stone, solids);
            foreach (float s in new[] { -1f, 1f })
                Beam("DomeBracket", root, new Vector3(dx + s * 1.4f, 21.7f, front + 0.5f), new Vector3(dx + s * 1.4f, 20.2f, front - 0.9f), 0.14f, stoneDark);
            spots[4] = Mark("Spot_Dome", root, new Vector3(dx, 22f, front + 2.3f));

            // Gondolas, one tied up and one drifting.
            var gondolaMat = Mat("Gondola", "#15161A", smoothness: 0.5f);
            var gondolaTrim = Mat("Gondola_Trim", "#C69A3C", smoothness: 0.6f);
            foreach (var (gx, gz, yaw) in new[] { (-24f, 0.5f, 90f), (24f, 6f, 80f) })
            {
                var g = Mark("Gondola", root, new Vector3(gx, 0.05f, gz));
                g.localRotation = Quaternion.Euler(0f, yaw, 0f);
                Box("Hull", g, new Vector3(0f, 0.15f, 0f), new Vector3(1.1f, 0.4f, 6.5f), gondolaMat);
                foreach (float s in new[] { -1f, 1f })
                {
                    var prow = Box("Prow", g, new Vector3(0f, 0.55f, s * 3.4f), new Vector3(0.18f, 0.9f, 0.18f), gondolaTrim);
                    prow.localRotation = Quaternion.Euler(s * -20f, 0f, 0f);
                }
                Box("Seat", g, new Vector3(0f, 0.4f, 0.4f), new Vector3(0.8f, 0.12f, 1.2f), Mat("Gondola_Seat", "#9B2F36"));
                AddSway(g, Vector3.forward, 2.5f, 0.7f, gx);
            }
            foreach (var (px, pz) in new[] { (-26f, 4f), (-18f, 9f), (14f, 12f) })
                Put(shard, "Stone", root, new Vector3(px, -0.3f, pz), new Vector3(1.4f, 0.8f, 1.4f), stoneDark);

            // The far bank of the canal: a second quay with houses facing back, and two more places to jump from.
            const float farEdge = 48f, farFront = 51f;
            Solid("FarQuay", root, new Vector3(0f, (quayTop - 6f) * 0.5f, (farEdge + 60f) * 0.5f), new Vector3(SeaBlockSize, quayTop + 6f, 60f - farEdge), stone, solids);
            Box("FarQuayCap", root, new Vector3(0f, quayTop - 0.04f, farEdge - 0.08f), new Vector3(SeaBlockSize, 0.12f, 0.4f), stoneDark);
            (float x, float w, float h, int c)[] farHouses =
            {
                (-40f, 8f, 9f, 3), (-31f, 7f, 11f, 0), (-23f, 8f, 8f, 5), (-14f, 9f, 12f, 1), (-5f, 7f, 9f, 2),
                (4f, 8f, 11f, 4), (13f, 7f, 8f, 0), (33f, 8f, 10f, 3), (41f, 6f, 9f, 5)
            };
            foreach (var (x, w, h, c) in farHouses)
            {
                Solid("FarHouse", root, new Vector3(x, quayTop + h * 0.5f, farFront + 4.5f), new Vector3(w, h, 9f), plaster[c], solids);
                for (int s = -1; s <= 1; s += 2)
                {
                    var slope = Box("Roof", root, new Vector3(x, quayTop + h + 0.85f, farFront + 4.5f + s * 2.2f), new Vector3(w + 0.8f, 0.3f, 5.4f), roofTile);
                    slope.localRotation = Quaternion.Euler(-s * 26f, 0f, 0f);
                }
                for (float fy = 2.5f; fy < h - 1.5f; fy += 3f)
                    for (float wx = -w * 0.5f + 1.6f; wx < w * 0.5f - 0.8f; wx += 2.4f)
                        Box("Window", root, new Vector3(x + wx, quayTop + fy, farFront - 0.03f), new Vector3(0.9f, 1.4f, 0.07f), glassMat);
            }
            var plasterFar = plaster[5];

            // 5 — a balcony on the far bank, 7 m.
            float fbx = -14f;
            Solid("FarBalcony", root, new Vector3(fbx, 6.875f, farFront - 1.2f), new Vector3(4f, 0.25f, 2.4f), stone, solids);
            foreach (float s in new[] { -1f, 1f })
                Box("FarBalconyRail", root, new Vector3(fbx + s * 1.9f, 7.5f, farFront - 1.2f), new Vector3(0.06f, 0.06f, 2.3f), Mat("Town_Iron", "#2D3138"));
            Box("FarBalconyRail", root, new Vector3(fbx - 1f, 7.5f, farFront - 2.35f), new Vector3(1.8f, 0.06f, 0.06f), Mat("Town_Iron", "#2D3138"));
            Box("FarBalconyRail", root, new Vector3(fbx + 1.3f, 7.5f, farFront - 2.35f), new Vector3(1.4f, 0.06f, 0.06f), Mat("Town_Iron", "#2D3138"));
            Box("FarBalconyDoor", root, new Vector3(fbx, 8f, farFront - 0.04f), new Vector3(1.1f, 2f, 0.08f), wood);
            spots[5] = Mark("Spot_FarBalcony", root, new Vector3(fbx, 7f, farFront - 2.4f));
            spots[5].localRotation = Quaternion.Euler(0f, 180f, 0f);

            // 6 — the balcony of a clock tower, 18 m.
            float cxt = 24f;
            Solid("ClockTower", root, new Vector3(cxt, quayTop + 13f, farFront + 2.5f), new Vector3(5f, 26f, 5f), plaster[2], solids);
            Box("ClockFace", root, new Vector3(cxt, quayTop + 22f, farFront - 0.04f), new Vector3(2.6f, 2.6f, 0.08f), Mat("Town_ClockFace", "#F4F1E8"));
            Box("ClockHandA", root, new Vector3(cxt, quayTop + 22.3f, farFront - 0.1f), new Vector3(0.12f, 1f, 0.05f), Mat("Town_Iron", "#2D3138"));
            Box("ClockHandB", root, new Vector3(cxt + 0.4f, quayTop + 22f, farFront - 0.1f), new Vector3(0.8f, 0.12f, 0.05f), Mat("Town_Iron", "#2D3138"));
            Box("ClockBand", root, new Vector3(cxt, quayTop + 25.2f, farFront + 2.5f), new Vector3(5.4f, 0.4f, 5.4f), stone);
            var clockRoof = Put(LowPolyMeshes.Prism("ClockRoof", 4, 3.9f, 0f, 5f), "ClockRoof", root, new Vector3(cxt, quayTop + 28f, farFront + 2.5f), Vector3.one, roofTile);
            clockRoof.localRotation = Quaternion.Euler(0f, 45f, 0f);
            for (float y = 2f; y < 17f; y += 3f)
                Box("TowerWindow", root, new Vector3(cxt, quayTop + y, farFront - 0.03f), new Vector3(0.7f, 1.4f, 0.07f), glassMat);
            Solid("ClockBalcony", root, new Vector3(cxt, 17.875f, farFront - 1.2f), new Vector3(3.6f, 0.25f, 2.4f), stone, solids);
            foreach (float s in new[] { -1f, 1f })
                Box("ClockRail", root, new Vector3(cxt + s * 1.7f, 18.5f, farFront - 1.2f), new Vector3(0.06f, 0.06f, 2.3f), Mat("Town_Iron", "#2D3138"));
            spots[6] = Mark("Spot_ClockTower", root, new Vector3(cxt, 18f, farFront - 2.4f));
            spots[6].localRotation = Quaternion.Euler(0f, 180f, 0f);

            var tower = MakeTower(root, spots, solids, new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f }, new[] { -3f, -3f, -3f, -3f, -3f, -3f, -3f });
            SeaGulls(site, new Vector3(0f, 0f, 12f));
            site.position = SiteOffsets[5];

            return MakeSite(site, tower, new Look
            {
                sun = new Vector3(38f, 165f, 0f), sunColor = "#FFE2B8", sunIntensity = 1.1f,
                ambientSky = "#A8C0D8", ambientEquator = "#CDB89A", ambientGround = "#7A6A58",
                fog = "#D8E6EA", fogStart = 55f, fogEnd = 190f,
                skyTint = "#7DB8E8", skyGround = "#BDD7E2", exposure = 1.05f, atmosphere = 0.65f,
                swell = 0.04f, swellPeriod = 3.6f, swellLength = 10f,
                mapCenter = new Vector3(0f, 6f, 6f), mapDistance = 135f, mapDirection = new Vector3(-0.5f, -0.6f, -1f)
            });
        }
    }
}
