using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JumpGame.EditorTools
{
    // The lake and the sea cliffs. Every location has five places to dive from, each higher than the last
    // and each a different thing: a jetty, a tree, a bridge, a yacht, a roof, a lighthouse.
    public static partial class PrototypeSceneBuilder
    {
        // ---------------------------------------------------------------- Lake

        const float LakeBlockSize = 100f, LakeBlockZ = 6f;

        // Ground height over the lake block, in site space. An oval lake with a wobbly shore, sand at the edge
        // and gently rolling grass beyond; flattened where the jetty and the pier come ashore.
        static float LakeHeight(float x, float z)
        {
            float dx = (x - 6f) / 34f, dz = (z - 4f) / 24f;
            float ang = Mathf.Atan2(dz, dx);
            float wobble = 1f + 0.08f * Mathf.Sin(ang * 3f + 0.7f) + 0.05f * Mathf.Sin(ang * 5f + 2.1f);
            float d = Mathf.Sqrt(dx * dx + dz * dz) / wobble;
            float shore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 1.14f, d));
            float h = Mathf.Lerp(-4.2f, 1.3f, shore);
            h += shore * (Mathf.PerlinNoise(x * 0.06f + 3f, z * 0.06f + 7f) - 0.5f) * 2.4f;

            bool landing = (Mathf.Abs(x) < 3f || Mathf.Abs(x + 12f) < 2.5f) && z < -12f;
            if (landing) h = Mathf.Min(h, 0.75f);
            return h;
        }

        static LocationSite BuildLakeSite()
        {
            var site = new GameObject("Site_Lake").transform;
            var water = Mat("LakeWater", "#2C7C84", transparent: true, alpha: 0.86f, smoothness: 0.3f);
            var woodA = Mat("WoodA", "#9C6B43");
            var woodB = Mat("WoodB", "#AF7E50");
            var woodDark = Mat("WoodDark", "#6E4A2F");
            var ropeMat = Mat("RopeMat", "#3A3329");
            var paint = Mat("PaintWhite", "#F2EFE6");
            var stone = Mat("BridgeStone", "#A69A88");
            var stoneDark = Mat("BridgeStoneDark", "#857A6B");

            WaterSurface("LakeWater", site, new Vector3(0f, 0f, LakeBlockZ), LakeBlockSize, 30, water);
            var ground = LowPolyMeshes.Terrain("LakeGround", LakeBlockSize, LakeBlockSize, 50,
                (x, z) => LakeHeight(x, z + LakeBlockZ), -0.4f, 0.45f, -6f);
            var groundT = Put(ground, "Ground", site, new Vector3(0f, 0f, LakeBlockZ), Vector3.one, woodA);
            groundT.GetComponent<MeshRenderer>().sharedMaterials = new[]
            {
                Mat("LakeMud", "#4F5A45"), Mat("LakeSand", "#D8C38D"), Mat("LakeGrass", "#5E9A4E"), Mat("Block_Soil", "#5A4330")
            };
            BlockBase(site, new Vector2(0f, LakeBlockZ), new Vector2(LakeBlockSize, LakeBlockSize), -6f, null);

            var solids = new List<Bounds>();
            var towerRoot = Child("Spots", site);
            var spots = new Transform[7];

            // 0 — the jetty: a short wooden walkway out from the shore, 0.8 m.
            const float jettyX = -12f, jettyTop = 0.8f, jettyEnd = -7f;
            for (float z = -19f; z < jettyEnd - 0.1f; z += 0.32f)
                Box("Board", towerRoot, new Vector3(jettyX, jettyTop - 0.03f, z + 0.14f), new Vector3(1.8f, 0.06f, 0.28f), (int)(z * 3f) % 3 == 0 ? woodB : woodA);
            for (float z = -18f; z < jettyEnd; z += 2.5f)
                foreach (float x in new[] { jettyX - 0.8f, jettyX + 0.8f })
                    Put(hex, "Pile", towerRoot, new Vector3(x, -1.6f, z), new Vector3(0.2f, 4.8f, 0.2f), woodDark);
            solids.Add(new Bounds(new Vector3(jettyX, jettyTop - 0.05f, (-19f + jettyEnd) * 0.5f), new Vector3(1.8f, 0.1f, jettyEnd + 19f)));
            var ring = Put(LowPolyMeshes.Torus("LifeRing", 0.26f, 0.07f), "LifeRing", towerRoot, new Vector3(jettyX - 0.95f, 0.35f, -10f), Vector3.one, Mat("LifeRing", "#E04A3A", smoothness: 0.4f));
            ring.localRotation = Quaternion.Euler(0f, 0f, 90f);
            spots[0] = Mark("Spot_Jetty", towerRoot, new Vector3(jettyX, jettyTop, jettyEnd));

            var boat = Mark("Boat", site, new Vector3(jettyX + 1.9f, 0.05f, -12.5f));
            boat.localRotation = Quaternion.Euler(0f, 6f, 0f);
            Box("Hull", boat, new Vector3(0f, 0.1f, 0f), new Vector3(1.1f, 0.4f, 2.8f), woodDark);
            Box("Inside", boat, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.32f, 2.5f), woodB);
            Box("Bench", boat, new Vector3(0f, 0.32f, 0.2f), new Vector3(0.9f, 0.05f, 0.3f), woodA);
            var bow = Box("Bow", boat, new Vector3(0f, 0.12f, 1.5f), new Vector3(0.78f, 0.36f, 0.78f), woodDark);
            bow.localRotation = Quaternion.Euler(0f, 45f, 0f);
            AddSway(boat, Vector3.forward, 2.5f, 0.9f, 0.4f);

            // 1 and 3 — the wooden tower at the end of the long pier, 5 m and 9 m.
            const float pierTop = 1f, back = -4.2f, front = -1f, top = 9.6f;
            for (float z = -20f; z < back; z += 0.32f)
                Box("Board", towerRoot, new Vector3(0f, pierTop - 0.03f, z + 0.14f), new Vector3(2.4f, 0.06f, 0.28f), (int)(z * 3f) % 3 == 0 ? woodB : woodA);
            for (float z = -19f; z < back; z += 3f)
                foreach (float x in new[] { -1.1f, 1.1f })
                    Put(hex, "Pile", towerRoot, new Vector3(x, -1.5f, z), new Vector3(0.22f, 5f, 0.22f), woodDark);
            solids.Add(new Bounds(new Vector3(0f, pierTop - 0.05f, (-20f + back) * 0.5f), new Vector3(2.4f, 0.1f, back + 20f)));

            foreach (float x in new[] { -1.5f, 1.5f })
                foreach (float z in new[] { back, front })
                {
                    var c = new Vector3(x, (-4f + top) * 0.5f, z);
                    var s = new Vector3(0.24f, top + 4f, 0.24f);
                    Box("Post", towerRoot, c, s, woodDark);
                    solids.Add(new Bounds(c, s));
                }
            float span = front - back;
            for (float y = pierTop; y < top - 2.6f; y += 2.7f)
                foreach (float x in new[] { -1.53f, 1.53f })
                    foreach (int dir in new[] { 1, -1 })
                    {
                        float len = Mathf.Sqrt(span * span + 2.7f * 2.7f);
                        var brace = Box("Brace", towerRoot, new Vector3(x, y + 1.35f, (back + front) * 0.5f), new Vector3(0.08f, 0.12f, len), woodA);
                        brace.localRotation = Quaternion.Euler(-dir * Mathf.Atan2(2.7f, span) * Mathf.Rad2Deg, 0f, 0f);
                    }
            // The 5 m deck is a side arm on its own two posts, so a dive from the top never has a deck under it.
            foreach (float z in new[] { back, front })
            {
                // A slanted strut from the tower post up under the arm keeps the view from +X open.
                var lo = new Vector3(1.5f, 1.5f, z);
                var hi = new Vector3(3.7f, 4.85f, z);
                var strut = Box("ArmStrut", towerRoot, (lo + hi) * 0.5f, new Vector3(0.18f, (hi - lo).magnitude, 0.18f), woodDark);
                strut.localRotation = Quaternion.FromToRotation(Vector3.up, (hi - lo).normalized);
            }
            (float x, float h, float edgeZ, float[] ropes)[] decks = { (3f, 5f, 0.2f, new[] { 4.45f }), (0f, 9f, 0.5f, new[] { -1.45f, 1.45f }) };
            var deckMarks = new Transform[2];
            for (int i = 0; i < decks.Length; i++)
            {
                var (dx, h, edgeZ, ropes) = decks[i];
                var c = new Vector3(dx, h - 0.11f, (edgeZ + back) * 0.5f);
                var s = new Vector3(3f, 0.22f, edgeZ - back);
                Box("Deck_" + h, towerRoot, c, s, woodA);
                solids.Add(new Bounds(c, s));
                Box("EdgePaint", towerRoot, new Vector3(dx, h + 0.005f, edgeZ - 0.08f), new Vector3(3f, 0.01f, 0.16f), paint);
                float railEnd = edgeZ - 1.5f;
                foreach (float x in ropes)
                {
                    Box("Rope", towerRoot, new Vector3(x, h + 0.95f, (railEnd + back) * 0.5f), new Vector3(0.03f, 0.03f, railEnd - back), ropeMat);
                    Put(hex, "RailPost", towerRoot, new Vector3(x, h + 0.5f, railEnd), new Vector3(0.08f, 1f, 0.08f), woodDark);
                }
                deckMarks[i] = Mark("Spot_Tower_" + h, towerRoot, new Vector3(dx, h, edgeZ));
            }
            foreach (float x in new[] { -0.3f, 0.3f })
                Box("LadderSide", towerRoot, new Vector3(x, (pierTop + top) * 0.5f, back - 0.14f), new Vector3(0.06f, top - pierTop, 0.06f), woodDark);
            for (float y = pierTop + 0.35f; y < top; y += 0.36f)
                Box("Rung", towerRoot, new Vector3(0f, y, back - 0.14f), new Vector3(0.6f, 0.04f, 0.05f), woodA);
            spots[1] = deckMarks[0];
            spots[3] = deckMarks[1];

            // 2 — an old willow-like tree on a rocky islet, with a plank nailed to its long branch, 7 m.
            var bark = Mat("Trunk", "#8A5B3D");
            var crownMat = Mat("Willow", "#6E9E52");
            var rockMat = Mat("LakeRock", "#8C8F8A");
            var shard = LowPolyMeshes.Icosphere("Shard", 0, 0.5f, 0.28f, 13);
            var islet = new Vector3(-17f, 0f, -9f);
            Put(shard, "Islet", towerRoot, islet + new Vector3(0f, -0.6f, 0f), new Vector3(6f, 2.6f, 5.5f), rockMat);
            Put(rough, "IsletGrass", towerRoot, islet + new Vector3(0f, 0.25f, -0.4f), new Vector3(4.2f, 1.2f, 3.6f), Mat("LakeGrass", "#5E9A4E"));
            solids.Add(new Bounds(islet + new Vector3(0f, -0.6f, 0f), new Vector3(5f, 2.6f, 4.6f)));
            solids.Add(new Bounds(islet + new Vector3(0f, 0.2f, -0.4f), new Vector3(3.4f, 1.4f, 3f)));
            var trunkBase = islet + new Vector3(0f, 0.5f, -0.6f);
            var trunkTop = new Vector3(-17f, 6.6f, -4.6f);
            var trunk = Put(hex, "Trunk", towerRoot, (trunkBase + trunkTop) * 0.5f, new Vector3(0.7f, (trunkTop - trunkBase).magnitude, 0.7f), bark);
            trunk.localRotation = Quaternion.FromToRotation(Vector3.up, (trunkTop - trunkBase).normalized);
            var branchEnd = new Vector3(-17f, 6.85f, -1.6f);
            var branch = Put(hex, "Branch", towerRoot, (trunkTop + branchEnd) * 0.5f, new Vector3(0.36f, (branchEnd - trunkTop).magnitude + 0.3f, 0.36f), bark);
            branch.localRotation = Quaternion.FromToRotation(Vector3.up, (branchEnd - trunkTop).normalized);
            Box("Plank", towerRoot, new Vector3(-17f, 6.95f, -2.15f), new Vector3(1.1f, 0.1f, 1.2f), woodB);
            solids.Add(new Bounds(new Vector3(-17f, 6.95f, -2.15f), new Vector3(1.1f, 0.1f, 1.2f)));
            solids.Add(new Bounds(new Vector3(-17f, 3.6f, -6.8f), new Vector3(0.8f, 6.4f, 3.6f)));
            foreach (var (o, s) in new[] { (new Vector3(-1.2f, 8.4f, -6.4f), 4.4f), (new Vector3(1.4f, 8.9f, -5.6f), 3.8f), (new Vector3(0.2f, 9.8f, -7.4f), 4f) })
            {
                var crown = Put(rough, "Crown", towerRoot, new Vector3(-17f, 0f, 0f) + o, new Vector3(s, s * 0.8f, s), crownMat);
                AddSway(crown, Vector3.forward, 1.5f, 0.6f, o.x);
            }
            spots[2] = Mark("Spot_Branch", towerRoot, new Vector3(-17f, 7f, -1.6f));

            // 4 — a ruined stone aqueduct that runs out from the south bank and stops over the lake, 12 m.
            // It runs along Z so the camera sees it from the side, arches and all.
            const float aqTop = 12f, aqX = 26f, aqEnd = -1f, aqStart = -44f, aqHalf = 1.7f;
            var deck = new Vector3(aqX, aqTop - 0.6f, (aqStart + aqEnd) * 0.5f);
            var deckSize = new Vector3(aqHalf * 2f, 1.2f, aqEnd - aqStart);
            Box("AqueductDeck", towerRoot, deck, deckSize, stone);
            solids.Add(new Bounds(deck, deckSize));
            Box("AqueductWall", towerRoot, new Vector3(aqX + aqHalf - 0.2f, aqTop + 0.4f, (aqStart + aqEnd - 5f) * 0.5f), new Vector3(0.4f, 0.8f, aqEnd - aqStart - 5f), stoneDark);
            var bay = LowPolyMeshes.ViaductBay("ViaductBay", aqHalf * 2f, 9f, -6f, aqTop - 1.2f, 3.2f, 3f);
            for (int k = 0; k < 4; k++)
            {
                float zc = -7.5f - 9f * k;
                Put(bay, "Bay", towerRoot, new Vector3(aqX, 0f, zc), Vector3.one, stoneDark);
            }
            for (int k = 0; k <= 4; k++)
            {
                // Solid pier at each joint between bays; the last one carries on to the edge of the block as an abutment.
                float zb = -3f - 9f * k;
                var c = new Vector3(aqX, 2.4f, k == 4 ? -41.5f : zb);
                var s = new Vector3(aqHalf * 2f, 16.8f, k == 4 ? 5f : 3f);
                if (k == 4) Box("Abutment", towerRoot, c, s, stoneDark);
                solids.Add(new Bounds(c, s));
            }
            // The broken end: a few blocks hanging off and lying in the water below.
            for (int k = 0; k < 3; k++)
                Put(shard, "Rubble", towerRoot, new Vector3(aqX + (k - 1) * 0.9f, aqTop - 0.5f - k * 0.2f, aqEnd - 0.1f - k * 0.4f), new Vector3(1.3f, 0.9f, 1.4f), stone);
            Put(shard, "Fallen", towerRoot, new Vector3(aqX - 1.5f, -0.5f, aqEnd + 2.5f), new Vector3(3f, 2f, 2.6f), stoneDark);
            Put(shard, "Fallen", towerRoot, new Vector3(aqX + 2f, -0.8f, aqEnd + 4f), new Vector3(2.4f, 1.6f, 2.2f), stone);
            spots[4] = Mark("Spot_Aqueduct", towerRoot, new Vector3(aqX, aqTop, aqEnd));

            // 5 — a rowing boat out on the east side of the lake, jumping west, 0.5 m.
            var rowboat = new Vector3(30f, 0.05f, 8f);
            Box("RowboatHull", towerRoot, rowboat + new Vector3(0f, 0.1f, 0f), new Vector3(4.2f, 0.4f, 1.5f), woodDark);
            Box("RowboatInside", towerRoot, rowboat + new Vector3(0f, 0.2f, 0f), new Vector3(3.9f, 0.3f, 1.2f), woodB);
            foreach (float bz in new[] { -1f, 1f })
            {
                var tip = Box("RowboatBow", towerRoot, rowboat + new Vector3(bz * 2.2f, 0.12f, 0f), new Vector3(1.05f, 0.4f, 1.05f), woodDark);
                tip.localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
            Box("RowboatSeat", towerRoot, rowboat + new Vector3(0.3f, 0.34f, 0f), new Vector3(0.35f, 0.06f, 1.3f), woodA);
            Box("Oar", towerRoot, rowboat + new Vector3(0.3f, 0.5f, 1.1f), new Vector3(0.05f, 0.05f, 2.4f), woodA);
            solids.Add(new Bounds(rowboat + new Vector3(0f, 0.1f, 0f), new Vector3(4.2f, 0.5f, 1.5f)));
            spots[5] = Mark("Spot_Rowboat", towerRoot, rowboat + new Vector3(-1.9f, 0.3f, 0f));
            spots[5].localRotation = Quaternion.Euler(0f, -90f, 0f);

            // 6 — a boulder standing in the water by the north shore, jumping south, 6 m.
            var boulder = new Vector3(8f, 1f, 25f);
            Put(LowPolyMeshes.Prism("LakeBoulder", 7, 3.6f, 2.5f, 10f), "Boulder", towerRoot, boulder, Vector3.one, rockMat);
            Put(shard, "BoulderChunk", towerRoot, boulder + new Vector3(2.2f, -3.2f, 1.4f), new Vector3(3.4f, 2.4f, 3f), rockMat);
            Put(rough, "BoulderMoss", towerRoot, boulder + new Vector3(0f, 5.1f, 0.4f), new Vector3(3.6f, 0.6f, 3.2f), Mat("LakeGrass", "#5E9A4E"));
            TaperedSolids(solids, boulder, -4f, 6f, 3.5f, 2.35f, 4);
            spots[6] = Mark("Spot_Boulder", towerRoot, new Vector3(boulder.x, 6f, boulder.z - 2.3f));
            spots[6].localRotation = Quaternion.Euler(0f, 180f, 0f);

            var tower = MakeTower(towerRoot, spots, solids, new[] { -1f, -1f, -1f, -1f, 1f, 1f, 1f });

            LakeShore(site);
            LakeLife(site);
            site.position = SiteOffsets[1];

            return MakeSite(site, tower, new Look
            {
                sun = new Vector3(36f, 135f, 0f), sunColor = "#FFE9CC", sunIntensity = 1.05f,
                ambientSky = "#8FB2C4", ambientEquator = "#9AA38E", ambientGround = "#4E5B4C",
                fog = "#B9D3D2", fogStart = 40f, fogEnd = 170f,
                skyTint = "#86BEE0", skyGround = "#B4CCC6", exposure = 1f, atmosphere = 0.6f,
                swell = 0.035f, swellPeriod = 3.2f, swellLength = 9f,
                mapCenter = new Vector3(4f, 0f, LakeBlockZ), mapDistance = 135f, mapDirection = new Vector3(-0.5f, -0.62f, -1f)
            });
        }

        static void LakeShore(Transform site)
        {
            var root = Child("Shore", site);
            var pineA = Mat("PineA", "#2F6B45");
            var pineB = Mat("PineB", "#3C7D52");
            var bark = Mat("Trunk", "#8A5B3D");
            var reed = Mat("Reed", "#8A9A4F");
            var pad = Mat("LilyPad", "#4E8F45");
            var lily = Mat("Lily", "#F2B8C6");
            var rockMat = Mat("LakeRock", "#8C8F8A");
            var shard = LowPolyMeshes.Icosphere("Shard", 0, 0.5f, 0.28f, 13);

            var rng = new System.Random(11);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float half = LakeBlockSize * 0.5f, z0 = LakeBlockZ - half, z1 = LakeBlockZ + half;

            // Pines only on dry ground, kept off the jetty, the pier and the bridge's bank.
            int placed = 0;
            for (int tries = 0; tries < 600 && placed < 55; tries++)
            {
                float x = R(-half + 2f, half - 2f), z = R(z0 + 2f, z1 - 2f);
                float h = LakeHeight(x, z);
                if (h < 0.9f) continue;
                if (z < -10f && (Mathf.Abs(x) < 4f || Mathf.Abs(x + 12f) < 3.5f)) continue;
                if (Mathf.Abs(x - 26f) < 4f && z < 2f) continue;
                Pine(root, new Vector3(x, h - 0.2f, z), R(0.8f, 1.4f), rng.Next(2) == 0 ? pineA : pineB, bark);
                placed++;
            }

            // Reeds where the sand meets the water, lily pads a little further out.
            int clumps = 0;
            for (int tries = 0; tries < 800 && clumps < 9; tries++)
            {
                float x = R(-half + 4f, half - 4f), z = R(z0 + 4f, z1 - 4f);
                float h = LakeHeight(x, z);
                if (h < -0.5f || h > 0.1f) continue;
                if (Mathf.Abs(x + 17f) < 6f && Mathf.Abs(z + 9f) < 6f) continue;
                clumps++;
                for (int k = 0; k < 8; k++)
                {
                    var p = new Vector3(x + R(-1.3f, 1.3f), 0f, z + R(-1.3f, 1.3f));
                    var stem = Mark("Reed", root, p + Vector3.up * Mathf.Min(0f, LakeHeight(p.x, p.z)));
                    float rh = R(0.9f, 1.6f);
                    Put(cone, "Stem", stem, new Vector3(0f, rh * 0.5f, 0f), new Vector3(0.07f, rh, 0.07f), reed);
                    AddSway(stem, Vector3.forward, 5f, R(1f, 1.6f), R(0f, 6f));
                }
                for (int k = 0; k < 4; k++)
                {
                    var p = new Vector3(x + R(-3f, 3f), 0.01f, z + R(-3f, 3f));
                    if (LakeHeight(p.x, p.z) > -0.3f) continue;
                    Put(hex, "Pad", root, p, new Vector3(0.7f, 0.02f, 0.7f), pad);
                    if (k % 2 == 0) Put(Smooth, "Lily", root, p + Vector3.up * 0.06f, new Vector3(0.14f, 0.1f, 0.14f), lily);
                }
            }

            // A few boulders on the shore.
            for (int k = 0, tries = 0; k < 8 && tries < 400; tries++)
            {
                float x = R(-half + 3f, half - 3f), z = R(z0 + 3f, z1 - 3f);
                float h = LakeHeight(x, z);
                if (h < 0.2f || h > 1.2f) continue;
                Put(shard, "ShoreRock", root, new Vector3(x, h, z), new Vector3(R(1.2f, 2.6f), R(0.8f, 1.6f), R(1.2f, 2.4f)), rockMat);
                k++;
            }
        }

        // ---------------------------------------------------------------- Cliffs

        const float CliffBlockSize = 90f, CliffBlockZ = 15f, CliffTop = 22f, CliffBack = -30f;

        // The cliff runs straight across the back of the block with a gentle wave, no headland.
        static float CliffFaceZ(float x) => -3f + 1.2f * Mathf.Sin(x * 0.15f);

        static LocationSite BuildCliffSite()
        {
            var site = new GameObject("Site_Cliffs").transform;
            var sea = Mat("Sea", "#169FB2", transparent: true, alpha: 0.84f, smoothness: 0.75f);
            var sand = Mat("Sand", "#E3CB98");
            var rockMats = new[] { Mat("RockA", "#C48A56"), Mat("RockB", "#A9713F"), Mat("RockC", "#8E5E36") };
            var grassTop = Mat("CliffGrass", "#7FAE5A");
            var chalk = Mat("PaintWhite", "#F2EFE6");
            var shard = LowPolyMeshes.Icosphere("Shard", 0, 0.5f, 0.28f, 13);
            var rng = new System.Random(5);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            WaterSurface("Sea", site, new Vector3(0f, 0f, CliffBlockZ), CliffBlockSize, 30, sea);
            BlockBase(site, new Vector2(0f, CliffBlockZ), new Vector2(CliffBlockSize, CliffBlockSize), -6f, sea);
            Box("Seabed", site, new Vector3(0f, -6f, CliffBlockZ), new Vector3(CliffBlockSize, 0.1f, CliffBlockSize), sand);

            var cliff = Child("Cliff", site);
            float half = CliffBlockSize * 0.5f;
            var wallMesh = LowPolyMeshes.CliffWall("CliffWall", CliffBlockSize, -6f, CliffTop, 24, 9, 3, CliffBack, CliffFaceZ, 21);
            var wall = Put(wallMesh, "Wall", cliff, Vector3.zero, Vector3.one, rockMats[0]);
            wall.GetComponent<MeshRenderer>().sharedMaterials = new[] { rockMats[0], rockMats[1], rockMats[2], grassTop };
            foreach (float side in new[] { -1f, 1f })
            {
                float zf = CliffFaceZ(side * half) - 0.5f;
                Box("End", cliff, new Vector3(side * (half - 0.3f), (CliffTop - 6f) * 0.5f, (zf + CliffBack) * 0.5f), new Vector3(0.6f, CliffTop + 6f, zf - CliffBack), rockMats[2]);
            }
            Box("Back", cliff, new Vector3(0f, (CliffTop - 6f) * 0.5f, CliffBack + 0.3f), new Vector3(CliffBlockSize, CliffTop + 6f, 0.6f), rockMats[2]);

            var solids = new List<Bounds>();
            // The wall as a row of boxes that follow the face, kept just behind the rock surface.
            WallSolids(wallMesh, solids, SeaBlockSize, 24, 9, -6f, CliffTop, CliffBack);

            for (int k = 0; k < 9; k++)
            {
                float x = R(-40f, 40f);
                if (Mathf.Abs(x - 10f) < 8f) continue;
                Put(shard, "Fallen", cliff, new Vector3(x, R(-0.6f, 0.2f), CliffFaceZ(x) + R(0.6f, 1.6f)), new Vector3(R(1.6f, 2.8f), R(1.2f, 2f), R(1.6f, 2.4f)), rockMats[rng.Next(3)]);
            }

            var spots = new Transform[7];
            Transform Ledge(string n, float x, float h, float reach)
            {
                float face = CliffFaceZ(x), edge = face + reach, backZ = face - 2.5f;
                var c = new Vector3(x, h - 0.35f, (edge + backZ) * 0.5f);
                var s = new Vector3(2.8f, 0.7f, edge - backZ);
                // A slab with an angular rock under it, so it reads as part of the cliff rather than a shelf.
                Box(n, cliff, c, s, rockMats[1]);
                Put(shard, n + "_Under", cliff, new Vector3(x, h - 1.6f, face + reach * 0.35f), new Vector3(3.2f, 2.6f, reach + 1.2f), rockMats[0]);
                Box(n + "_Chalk", cliff, new Vector3(x, h + 0.005f, edge - 0.1f), new Vector3(2.4f, 0.01f, 0.12f), chalk);
                solids.Add(new Bounds(c, s));
                solids.Add(new Bounds(new Vector3(x, h - 1.7f, face + reach * 0.4f), new Vector3(2.6f, 2.2f, reach * 0.8f + 0.6f)));
                return Mark("Spot_" + n, cliff, new Vector3(x, h, edge));
            }

            // 0 — a white yacht anchored off the cliff, bow towards the open sea; dive off the bow, 3 m.
            var hullWhite = Mat("Yacht_Hull", "#F4F3EE", smoothness: 0.6f);
            var stripe = Mat("Yacht_Stripe", "#1F4E79");
            var teak = Mat("Yacht_Deck", "#B88A5A");
            var glass = Mat("Yacht_Glass", "#24384A", smoothness: 0.9f);
            var y0 = new Vector3(-24f, 0f, 6f);
            Box("Hull", site, y0 + new Vector3(0f, 1.35f, 0f), new Vector3(3.6f, 3.3f, 12f), hullWhite);
            Box("Stripe", site, y0 + new Vector3(0f, 1f, 0f), new Vector3(3.64f, 0.3f, 12.04f), stripe);
            var yBow = Box("Bow", site, y0 + new Vector3(0f, 1.35f, 6f), new Vector3(2.55f, 3.3f, 2.55f), hullWhite);
            yBow.localRotation = Quaternion.Euler(0f, 45f, 0f);
            var yBowStripe = Box("BowStripe", site, y0 + new Vector3(0f, 1f, 6f), new Vector3(2.59f, 0.3f, 2.59f), stripe);
            yBowStripe.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Box("Deck", site, y0 + new Vector3(0f, 3.02f, 0f), new Vector3(3.3f, 0.05f, 11.6f), teak);
            var yBowDeck = Box("BowDeck", site, y0 + new Vector3(0f, 3.02f, 6f), new Vector3(2.5f, 0.05f, 2.5f), teak);
            yBowDeck.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Box("Cabin", site, y0 + new Vector3(-0.2f, 3.8f, -1.5f), new Vector3(2.4f, 1.6f, 5f), hullWhite);
            Box("CabinWindows", site, y0 + new Vector3(-0.2f, 4f, -1.5f), new Vector3(2.44f, 0.5f, 5.04f), glass);
            Put(hex, "Mast", site, y0 + new Vector3(-0.3f, 7.5f, 1.5f), new Vector3(0.14f, 9f, 0.14f), hullWhite);
            // Portholes along the hull and a rail on both sides, so the side doesn't read as a plain box.
            foreach (float side in new[] { -1f, 1f })
            {
                for (float z = -4.5f; z <= 3.2f; z += 1.5f)
                    Box("Porthole", site, y0 + new Vector3(side * 1.805f, 2.15f, z), new Vector3(0.06f, 0.42f, 0.42f), glass);
                Box("Rail", site, y0 + new Vector3(side * 1.65f, 3.45f, -0.2f), new Vector3(0.04f, 0.04f, 10f), hullWhite);
                for (float z = -5f; z <= 4.2f; z += 1.8f)
                    Box("Stanchion", site, y0 + new Vector3(side * 1.65f, 3.22f, z), new Vector3(0.04f, 0.45f, 0.04f), hullWhite);
            }
            solids.Add(new Bounds(y0 + new Vector3(0f, 1.35f, 0.2f), new Vector3(3.6f, 3.3f, 12.4f)));
            solids.Add(new Bounds(y0 + new Vector3(-0.2f, 3.8f, -1.5f), new Vector3(2.4f, 1.6f, 5f)));
            solids.Add(new Bounds(y0 + new Vector3(0f, 1.35f, 7.05f), new Vector3(1.5f, 3.3f, 1.5f)));
            spots[0] = Mark("Spot_Yacht", site, y0 + new Vector3(0f, 3f, 7.2f));

            spots[1] = Ledge("Ledge_8", -8f, 8f, 2.9f);

            // 2 — a fisherman's house on a rock terrace; dive off the edge of its flat roof, 11.5 m.
            var lime = Mat("House_Lime", "#F2EEE4");
            var roofMat = Mat("House_Roof", "#C8643E");
            var shutter = Mat("House_Shutter", "#3E7CA8");
            var door = Mat("House_Door", "#5B3A28");
            float hx = 10f, terraceTop = 8f, terraceFront = -0.2f, terraceBack = CliffFaceZ(hx) - 3f;
            var terrace = new Vector3(hx, (terraceTop - 6f) * 0.5f, (terraceFront + terraceBack) * 0.5f);
            var terraceSize = new Vector3(14f, terraceTop + 6f, terraceFront - terraceBack);
            Box("Terrace", cliff, terrace, terraceSize, rockMats[1]);
            Box("TerraceTop", cliff, new Vector3(hx, terraceTop + 0.02f, terrace.z), new Vector3(14f, 0.05f, terraceSize.z), grassTop);
            solids.Add(new Bounds(terrace, terraceSize));
            var houseC = new Vector3(hx, (terraceTop + 11.2f) * 0.5f, -1.4f);
            var houseS = new Vector3(8f, 11.2f - terraceTop, 3.2f);
            Box("House", cliff, houseC, houseS, lime);
            solids.Add(new Bounds(houseC, houseS));
            var roofC = new Vector3(hx, 11.35f, -1.2f);
            var roofS = new Vector3(8.8f, 0.3f, 4.4f);
            Box("Roof", cliff, roofC, roofS, roofMat);
            solids.Add(new Bounds(roofC, roofS));
            Box("Door", cliff, new Vector3(hx - 2f, terraceTop + 1f, 0.22f), new Vector3(1f, 2f, 0.06f), door);
            foreach (float wx in new[] { hx + 0.6f, hx + 2.6f })
            {
                Box("Window", cliff, new Vector3(wx, terraceTop + 1.8f, 0.22f), new Vector3(0.8f, 0.9f, 0.05f), glass);
                Box("Shutter", cliff, new Vector3(wx - 0.6f, terraceTop + 1.8f, 0.24f), new Vector3(0.36f, 0.95f, 0.05f), shutter);
            }
            Box("Chimney", cliff, new Vector3(hx + 3f, 12.2f, -2.4f), new Vector3(0.6f, 1.4f, 0.6f), lime);
            Put(Smooth, "Plant", cliff, new Vector3(hx - 3.6f, 11.8f, -2.8f), new Vector3(0.8f, 0.7f, 0.8f), grassTop);
            spots[2] = Mark("Spot_Roof", cliff, new Vector3(hx, 11.5f, roofC.z + roofS.z * 0.5f));

            spots[3] = Ledge("Ledge_15", -2f, 15f, 2.9f);

            // 4 — a lighthouse on a sea rock beside the cliff; dive off its gallery, 18 m.
            var white = Mat("Lighthouse_White", "#F4F2EC");
            var red = Mat("Lighthouse_Red", "#C9413A");
            var lamp = Glow(Mat("Lighthouse_Lamp", "#FFE7A3", smoothness: 0.9f), 0.6f);
            var lh = new Vector3(28f, 0f, -0.6f);
            Put(shard, "Base", cliff, lh + new Vector3(0f, 0.4f, 0f), new Vector3(9f, 6f, 8f), rockMats[2]);
            Put(LowPolyMeshes.Prism("Lighthouse_Tower", 12, 1.7f, 1.25f, 17f), "Tower", cliff, lh + new Vector3(0f, 9.5f, 0f), Vector3.one, white);
            foreach (float by in new[] { 6f, 12f })
                Put(LowPolyMeshes.Prism("Lighthouse_Band", 12, 1.62f, 1.52f, 1.6f), "Band", cliff, lh + new Vector3(0f, by, 0f), new Vector3(1.06f - by * 0.012f, 1f, 1.06f - by * 0.012f), red);
            Put(LowPolyMeshes.Prism("Lighthouse_Gallery", 16, 2.3f, 2.3f, 0.3f), "Gallery", cliff, lh + new Vector3(0f, 17.85f, 0f), Vector3.one, red);
            Put(LowPolyMeshes.Prism("Lighthouse_Lamp", 8, 1f, 1f, 1.6f), "Lamp", cliff, lh + new Vector3(0f, 18.8f, 0f), Vector3.one, lamp);
            Put(cone, "Cap", cliff, lh + new Vector3(0f, 20.1f, 0f), new Vector3(2.5f, 1f, 2.5f), red);
            for (int k = 0; k < 9; k++)
            {
                // Railing round the back of the gallery, open towards the sea.
                float a = Mathf.Deg2Rad * (200f + k * 17.5f);
                Put(hex, "RailPost", cliff, lh + new Vector3(Mathf.Cos(a) * 2.2f, 18.5f, Mathf.Sin(a) * 2.2f), new Vector3(0.05f, 1f, 0.05f), white);
            }
            solids.Add(new Bounds(lh + new Vector3(0f, 0.2f, 0f), new Vector3(7.8f, 6.4f, 6.8f)));
            solids.Add(new Bounds(lh + new Vector3(0f, 9.5f, 0f), new Vector3(2.8f, 17f, 2.8f)));
            solids.Add(new Bounds(lh + new Vector3(0f, 17.85f, 0f), new Vector3(4.6f, 0.3f, 4.6f)));
            solids.Add(new Bounds(lh + new Vector3(0f, 18.8f, 0f), new Vector3(2f, 1.6f, 2f)));
            spots[4] = Mark("Spot_Lighthouse", cliff, lh + new Vector3(0f, 18f, 2.3f));

            // 5 — a sea stack out in the water, jumping back towards the cliff, 7 m.
            var stackA = new Vector3(12f, 0.5f, 30f);
            Put(LowPolyMeshes.Prism("SeaStackA", 7, 4.4f, 2.9f, 13f), "SeaStack", cliff, stackA, Vector3.one, rockMats[2]);
            Put(shard, "SeaStackChunk", cliff, stackA + new Vector3(-2.6f, -5.4f, 1f), new Vector3(4f, 3f, 3.6f), rockMats[1]);
            Put(shard, "SeaStackCap", cliff, stackA + new Vector3(0f, 6.1f, 0.2f), new Vector3(4.6f, 0.5f, 4.4f), grassTop);
            TaperedSolids(solids, stackA, -6f, 7f, 4.2f, 2.75f, 4);
            spots[5] = Mark("Spot_StackA", cliff, new Vector3(stackA.x, 7f, stackA.z - 2.55f));
            spots[5].localRotation = Quaternion.Euler(0f, 180f, 0f);
            Palm(cliff, new Vector3(stackA.x + 0.6f, 7f, stackA.z + 0.6f), 0.8f, 40f);

            // 6 — a tall sea stack on the west side, jumping east, 12 m.
            var stackB = new Vector3(-32f, 3f, 24f);
            Put(LowPolyMeshes.Prism("SeaStackB", 8, 5.4f, 3.5f, 18f), "SeaStack", cliff, stackB, Vector3.one, rockMats[0]);
            Put(shard, "SeaStackChunk", cliff, stackB + new Vector3(3.4f, -7f, -1.2f), new Vector3(5f, 4f, 4.2f), rockMats[2]);
            Put(shard, "SeaStackCap", cliff, stackB + new Vector3(0f, 9.1f, 0f), new Vector3(5.4f, 0.6f, 5.2f), grassTop);
            TaperedSolids(solids, stackB, -6f, 12f, 5.2f, 3.4f, 5);
            spots[6] = Mark("Spot_StackB", cliff, new Vector3(stackB.x + 3.1f, 12f, stackB.z));
            spots[6].localRotation = Quaternion.Euler(0f, 90f, 0f);
            Put(cone, "StackBird", cliff, new Vector3(stackB.x - 1f, 12.4f, stackB.z + 1f), new Vector3(0.3f, 0.5f, 0.3f), Mat("Gull_White", "#F4F4F0"));

            var tower = MakeTower(cliff, spots, solids, new[] { -1f, 1f, 1f, 1f, -1f, 1f, 1f }, new[] { -2.5f, -2.5f, -2.5f, -2.5f, -2.5f, 0f, 0f });

            // Small beaches at both ends of the cliff foot, for the crabs.
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = side < 0 ? -half : 34f, x1 = side < 0 ? -31f : half;
                float zf = CliffFaceZ((x0 + x1) * 0.5f) - 1f;
                Box("Beach", site, new Vector3((x0 + x1) * 0.5f, (0.35f - 6f) * 0.5f, (zf + 7f) * 0.5f), new Vector3(x1 - x0, 6.35f, 7f - zf), sand);
            }
            Vector3[] palms = { new(-40f, 0.35f, -1f), new(-34f, 0.35f, 4f), new(38f, 0.35f, -1.5f), new(42f, 0.35f, 4.5f),
                                new(-22f, CliffTop, -12f), new(-6f, CliffTop, -18f), new(18f, CliffTop, -14f), new(32f, CliffTop, -20f) };
            foreach (var p in palms) Palm(site, p, R(0.9f, 1.25f), R(0f, 360f));
            Vector3[] stones = { new(-10f, -0.6f, 26f), new(16f, -0.8f, 34f), new(-30f, -0.5f, 20f) };
            foreach (var p in stones) Put(shard, "SeaRock", site, p, new Vector3(R(2.5f, 4f), R(1.8f, 2.6f), R(2.5f, 4f)), rockMats[2]);

            CliffLife(site);
            site.position = SiteOffsets[2];

            return MakeSite(site, tower, new Look
            {
                sun = new Vector3(38f, 205f, 0f), sunColor = "#FFD9A8", sunIntensity = 1.1f,
                ambientSky = "#9DB7CC", ambientEquator = "#C9A98A", ambientGround = "#6E5A4A",
                fog = "#D3E1E4", fogStart = 50f, fogEnd = 180f,
                skyTint = "#7FB4E2", skyGround = "#B8D2DE", exposure = 1.05f, atmosphere = 0.7f,
                swell = 0.16f, swellPeriod = 4.6f, swellLength = 18f,
                mapCenter = new Vector3(0f, 5f, 8f), mapDistance = 135f, mapDirection = new Vector3(-0.5f, -0.6f, -1f)
            });
        }
    }
}
