using System.Collections.Generic;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // Jungle lagoon under a waterfall: a dock, a rock ledge, a treehouse, a stepped temple and the lip of the falls.
    public static partial class PrototypeSceneBuilder
    {
        const float JungleTop = 28f, JungleBack = -32f;

        static float JungleFaceZ(float x) => -4f + 1.2f * Mathf.Sin(x * 0.14f) + 0.5f * Mathf.Sin(x * 0.4f);

        // Where the beach ends and the lagoon starts: close to the wall in the middle, further out on both sides.
        static float JungleShoreZ(float x) => -4f + 14f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(18f, 38f, Mathf.Abs(x)));

        static float JungleHeight(float x, float z)
        {
            float shore = JungleShoreZ(x);
            float land = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shore + 2.2f, shore - 1.2f, z));
            float h = Mathf.Lerp(-4.2f, 1.2f, land);
            h += land * (Mathf.PerlinNoise(x * 0.07f + 5f, z * 0.07f + 1f) - 0.5f) * 0.9f;
            return h;
        }

        static void JungleTree(Transform parent, Vector3 at, float s, Material trunk, Material crownA, Material crownB, int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var t = Mark("Tree", parent, at);
            t.localScale = Vector3.one * s;
            float h = R(5.5f, 8f);
            Put(hex, "Trunk", t, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.7f, h, 0.7f), trunk);
            var crown = Mark("Crown", t, new Vector3(0f, h, 0f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.7f + R(0f, 1f);
                float size = R(3.2f, 4.6f);
                Put(rough, "Leaves", crown, new Vector3(Mathf.Cos(a) * R(0.8f, 2f), R(-0.4f, 1.2f), Mathf.Sin(a) * R(0.8f, 2f)), new Vector3(size, size * 0.7f, size), i % 2 == 0 ? crownA : crownB);
            }
            AddSway(crown, Vector3.forward, 2f, R(0.5f, 0.9f), R(0f, 6f));
        }

        static LocationSite BuildJungleSite()
        {
            var site = new GameObject("Site_Jungle").transform;
            var water = Mat("LagoonWater", "#1FA89E", transparent: true, alpha: 0.82f, smoothness: 0.35f);
            WaterSurface("LagoonWater", site, new Vector3(0f, 0f, SeaBlockZ), SeaBlockSize, 30, water);
            var ground = LowPolyMeshes.Terrain("JungleGround", SeaBlockSize, SeaBlockSize, 56,
                (x, z) => JungleHeight(x, z + SeaBlockZ), -0.4f, 1.4f, -6f);
            var groundT = Put(ground, "Ground", site, new Vector3(0f, 0f, SeaBlockZ), Vector3.one, water);
            groundT.GetComponent<MeshRenderer>().sharedMaterials = new[]
            {
                Mat("JungleMud", "#3E5A4A"), Mat("JungleSand", "#D9C48E"), Mat("JungleGrass", "#4F9444"), Mat("Block_Soil", "#5A4330")
            };
            BlockBase(site, new Vector2(0f, SeaBlockZ), new Vector2(SeaBlockSize, SeaBlockSize), -6f, null);

            var mossA = Mat("Jungle_RockA", "#6A7552");
            var mossB = Mat("Jungle_RockB", "#58643F");
            var mossC = Mat("Jungle_RockC", "#788460");
            var capGrass = Mat("Jungle_Cap", "#3F8A3A");
            var wood = Mat("Jungle_Wood", "#8A5E3B");
            var woodDark = Mat("Jungle_WoodDark", "#5E3F28");
            var thatch = Mat("Jungle_Thatch", "#B59A4E");
            var stone = Mat("Jungle_Stone", "#8D8F7C");
            var stoneDark = Mat("Jungle_StoneDark", "#6F715F");
            var leafA = Mat("Jungle_LeafA", "#2F7D3C");
            var leafB = Mat("Jungle_LeafB", "#4FA04A");
            var shard = RockMesh;
            var rng = new System.Random(21);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            var root = Child("Jungle", site);
            var solids = new List<Bounds>();
            var spots = new Transform[7];
            float half = SeaBlockSize * 0.5f;

            // The cliff behind the lagoon, moss-green, with a flat jungle plateau on top.
            var wallMesh = LowPolyMeshes.CliffWall("JungleWall", SeaBlockSize, -6f, JungleTop, 24, 10, 3, JungleBack, JungleFaceZ, 52);
            var wall = Put(wallMesh, "Wall", root, Vector3.zero, Vector3.one, mossA);
            wall.GetComponent<MeshRenderer>().sharedMaterials = new[] { mossA, mossB, mossC, capGrass };
            foreach (float side in new[] { -1f, 1f })
            {
                float zf = JungleFaceZ(side * half) - 0.5f;
                Box("End", root, new Vector3(side * (half - 0.3f), (JungleTop - 6f) * 0.5f, (zf + JungleBack) * 0.5f), new Vector3(0.6f, JungleTop + 6f, zf - JungleBack), mossB);
            }
            Box("Back", root, new Vector3(0f, (JungleTop - 6f) * 0.5f, JungleBack + 0.3f), new Vector3(SeaBlockSize, JungleTop + 6f, 0.6f), mossB);
            WallSolids(wallMesh, solids, SeaBlockSize, 24, 10, -6f, JungleTop, JungleBack);

            // The waterfall: a pale sheet down the wall, mist at its foot.
            var fall = Mat("Waterfall", "#E8F6F8", transparent: true, alpha: 0.78f, smoothness: 0.6f);
            Glow(fall, 0.45f);
            float fx = 15f;
            Box("Waterfall", root, new Vector3(fx, (JungleTop - 1f) * 0.5f, JungleFaceZ(fx) + 0.55f), new Vector3(3.4f, JungleTop - 1f, 0.5f), fall);
            Box("FallStrip", root, new Vector3(fx - 0.7f, (JungleTop - 1f) * 0.5f, JungleFaceZ(fx) + 0.85f), new Vector3(0.5f, JungleTop - 3f, 0.2f), fall);
            Box("FallStrip", root, new Vector3(fx + 0.9f, (JungleTop - 1f) * 0.5f, JungleFaceZ(fx) + 0.85f), new Vector3(0.4f, JungleTop - 5f, 0.2f), fall);
            for (int i = 0; i < 5; i++)
            {
                var mist = Put(Smooth, "Mist", root, new Vector3(fx + R(-2f, 2f), R(0f, 0.8f), JungleFaceZ(fx) + R(1.2f, 3.2f)), Vector3.one * R(2.5f, 4.2f), fall);
                mist.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                AddSway(mist, Vector3.up, 8f, R(0.3f, 0.6f), R(0f, 6f));
            }
            for (int i = 0; i < 9; i++)
                Put(shard, "FallStone", root, new Vector3(fx + R(-5f, 5f), R(-0.6f, 0.1f), JungleFaceZ(fx) + R(0.8f, 3.5f)), new Vector3(R(1.2f, 2.4f), R(0.8f, 1.6f), R(1.2f, 2f)), i % 2 == 0 ? mossB : mossC);

            // The beaches on both sides: low blocks that follow the shoreline, so a short dive lands on sand.
            for (float bx = 17f; bx < half; bx += 3f)
                foreach (float sgn in new[] { -1f, 1f })
                {
                    float cx = sgn * (bx + 1.5f), shoreZ = JungleShoreZ(cx) - 0.8f;
                    if (shoreZ < JungleFaceZ(cx) + 1f) continue;
                    float frontEdge = JungleFaceZ(cx) + 0.5f;
                    solids.Add(new Bounds(new Vector3(cx, (1.0f - 6f) * 0.5f, (frontEdge + shoreZ) * 0.5f), new Vector3(3f, 7f, shoreZ - frontEdge)));
                }

            // 0 — a wooden dock under the cliff, 1.2 m.
            float dockX = -12f, dockTop = 1.2f, dockEnd = 1f;
            for (float z = -3.4f; z < dockEnd - 0.05f; z += 0.3f)
                Box("Board", root, new Vector3(dockX, dockTop - 0.03f, z + 0.13f), new Vector3(3.4f, 0.06f, 0.26f), (int)(z * 3f) % 3 == 0 ? wood : woodDark);
            solids.Add(new Bounds(new Vector3(dockX, dockTop - 0.06f, (-3.4f + dockEnd) * 0.5f), new Vector3(3.4f, 0.12f, dockEnd + 3.4f)));
            foreach (float px in new[] { dockX - 1.5f, dockX + 1.5f })
                foreach (float pz in new[] { -2.8f, 0.4f })
                    Put(hex, "Pile", root, new Vector3(px, -1.6f, pz), new Vector3(0.22f, 5.6f, 0.22f), woodDark);
            // A canoe pulled up beside the dock.
            var canoe = Mark("Canoe", root, new Vector3(dockX + 3.2f, 0.05f, -1f));
            canoe.localRotation = Quaternion.Euler(0f, 8f, 0f);
            Box("Hull", canoe, new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.34f, 4f), wood);
            foreach (float s in new[] { -1f, 1f })
            {
                var tip = Box("Tip", canoe, new Vector3(0f, 0.22f, s * 2.1f), new Vector3(0.5f, 0.3f, 0.5f), wood);
                tip.localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
            AddSway(canoe, Vector3.forward, 2f, 0.8f, 1f);
            spots[0] = Mark("Spot_Dock", root, new Vector3(dockX, dockTop, dockEnd));

            // 1 — a rock ledge on the wall, 6 m.
            spots[1] = WallLedge(root, solids, "RockLedge", -3f, 6f, 2.9f, JungleFaceZ(-3f), mossC, mossB, shard);

            // 2 — a treehouse on the left bank, 11 m.
            float thx = -29f;
            Put(LowPolyMeshes.Prism("JungleTrunk", 8, 1.15f, 0.8f, 20f), "Trunk", root, new Vector3(thx, 10.4f, -2.8f), Vector3.one, woodDark);
            solids.Add(new Bounds(new Vector3(thx, 10.4f, -2.8f), new Vector3(2f, 20f, 2f)));
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.6f + 0.4f;
                Put(rough, "Crown", root, new Vector3(thx + Mathf.Cos(a) * 3.2f, 20f + (i % 2) * 1.2f, -2.8f + Mathf.Sin(a) * 3.2f), new Vector3(7f, 5f, 7f), i % 2 == 0 ? leafA : leafB);
            }
            var thFloor = new Vector3(thx, 10.85f, -0.6f);
            var thSize = new Vector3(5.4f, 0.3f, 4.6f);
            Solid("TreehouseFloor", root, thFloor, thSize, wood, solids);
            foreach (float px in new[] { thx - 2.2f, thx + 2.2f })
            {
                Beam("TreehousePost", root, new Vector3(px, 1f, 1.5f), new Vector3(px, 10.7f, 1.5f), 0.34f, woodDark);
                solids.Add(new Bounds(new Vector3(px, 5.8f, 1.5f), new Vector3(0.4f, 9.8f, 0.4f)));
            }
            Solid("TreehouseHut", root, new Vector3(thx, 12.35f, -1.8f), new Vector3(3.6f, 2.8f, 2.4f), wood, solids);
            var hutRoof = Put(cone, "HutRoof", root, new Vector3(thx, 14.6f, -1.8f), new Vector3(5.6f, 1.9f, 4.2f), thatch);
            hutRoof.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Box("HutDoor", root, new Vector3(thx, 12f, -0.58f), new Vector3(0.9f, 1.8f, 0.06f), woodDark);
            foreach (float s in new[] { -1f, 1f })
            {
                Box("Rope", root, new Vector3(thx + s * 2.6f, 11.6f, 0.4f), new Vector3(0.05f, 0.05f, 3.6f), woodDark);
                Put(hex, "RailPost", root, new Vector3(thx + s * 2.6f, 11.3f, 2.1f), new Vector3(0.09f, 1f, 0.09f), woodDark);
            }
            for (float y = 1.4f; y < 10.7f; y += 0.45f)
                Box("LadderRung", root, new Vector3(thx + 1f, y, -1.7f), new Vector3(0.7f, 0.05f, 0.05f), wood);
            spots[2] = Mark("Spot_Treehouse", root, new Vector3(thx, 11f, 1.7f));

            // 3 — a stepped temple on the right bank, 16 m.
            float tmx = 30f;
            var tiers = new (float w, float depth, float front)[] { (14f, 12f, 4.5f), (11f, 10f, 3.3f), (8.2f, 8f, 2.1f), (5.6f, 6f, 0.9f) };
            float y0 = 1.2f, tierH = 3.7f;
            for (int i = 0; i < tiers.Length; i++)
            {
                var (w, depth, frontZ) = tiers[i];
                var c = new Vector3(tmx, y0 + tierH * (i + 0.5f), frontZ - depth * 0.5f);
                var s = new Vector3(w, tierH, depth);
                Solid("TempleTier", root, c, s, i % 2 == 0 ? stone : stoneDark, solids);
                Box("TierEdge", root, new Vector3(tmx, y0 + tierH * (i + 1) - 0.05f, frontZ - 0.1f), new Vector3(w + 0.2f, 0.12f, 0.3f), stone);
            }
            for (int i = 0; i < 12; i++)
            {
                float zz = 4.7f - i * 0.28f;
                Box("TempleStairs", root, new Vector3(tmx, 1.2f + i * 0.3f, zz), new Vector3(3f, 0.3f, 0.5f), stone);
            }
            Solid("TempleShrine", root, new Vector3(tmx, 16f + 1.4f, -1.9f), new Vector3(4.2f, 2.8f, 2.2f), stoneDark, solids);
            Box("ShrineDoor", root, new Vector3(tmx, 16f + 1f, -0.77f), new Vector3(1.1f, 2f, 0.1f), Mat("Jungle_Dark", "#1E221C"));
            foreach (float sx in new[] { -1f, 1f })
                Put(Smooth, "Vine", root, new Vector3(tmx + sx * 6.2f, 4.4f, 3.2f), new Vector3(2.4f, 3.2f, 1.2f), leafA);
            spots[3] = Mark("Spot_Temple", root, new Vector3(tmx, 16f, 0.85f));
            Box("TempleTopEdge", root, new Vector3(tmx, 16.005f, 0.78f), new Vector3(5.6f, 0.01f, 0.14f), Mat("Jungle_Gold", "#D9B04A"));

            // 4 — the lip of the waterfall, 24 m.
            spots[4] = WallLedge(root, solids, "FallsLedge", 7.5f, 24f, 2.9f, JungleFaceZ(7.5f), mossC, mossB, shard);

            // Plants: jungle trees on the plateau and the banks, palms and ferns on the beaches.
            for (int i = 0; i < 18; i++)
                JungleTree(root, new Vector3(R(-42f, 42f), JungleTop, R(-28f, -8f)), R(0.9f, 1.5f), woodDark, leafA, leafB, 100 + i);
            for (int i = 0; i < 9; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * R(36f, 43f);
                float z = R(-1f, JungleShoreZ(x) - 2f);
                JungleTree(root, new Vector3(x, JungleHeight(x, z), z), R(0.8f, 1.2f), woodDark, leafA, leafB, 200 + i);
            }
            Vector3[] palms = { new(-20f, 1.2f, -2f), new(-17f, 1.2f, -1.5f), new(22f, 1.2f, -2.5f), new(-38f, 1.4f, 5f), new(38f, 1.4f, 7f) };
            foreach (var p in palms) Palm(root, new Vector3(p.x, Mathf.Max(JungleHeight(p.x, p.z), 0.4f), p.z), R(0.9f, 1.2f), R(0f, 360f));
            for (int i = 0; i < 26; i++)
            {
                float x = R(-42f, 42f), z = R(-3.5f, JungleShoreZ(x) - 1f);
                float h = JungleHeight(x, z);
                if (h < 0.8f || Mathf.Abs(x - 30f) < 8f || Mathf.Abs(x + 29f) < 4f || Mathf.Abs(x + 12f) < 4f) continue;
                for (int k = 0; k < 5; k++)
                {
                    var leaf = Put(cone, "Fern", root, new Vector3(x, h + 0.35f, z), new Vector3(0.3f, 1.4f, 0.1f), i % 2 == 0 ? leafA : leafB);
                    leaf.localRotation = Quaternion.Euler(60f, k * 72f, 0f);
                }
            }

            // 5 — a small rock island with a carved idol, jumping back towards the falls, 9 m.
            var idolIsland = new Vector3(0f, 2f, 34f);
            RockPillar(root, solids, "IdolIsland", idolIsland, -5f, 9f, 4.6f, 3.2f, new[] { mossB, mossC }, capGrass, 93);
            Solid("IdolBody", root, new Vector3(0f, 10.2f, 35.4f), new Vector3(1.7f, 2.6f, 1.4f), stone, solids);
            Box("IdolBrow", root, new Vector3(0f, 11.2f, 34.65f), new Vector3(1.9f, 0.35f, 0.3f), stoneDark);
            Box("IdolEyeL", root, new Vector3(-0.4f, 10.7f, 34.68f), new Vector3(0.3f, 0.2f, 0.1f), Mat("Jungle_Dark", "#1E221C"));
            Box("IdolEyeR", root, new Vector3(0.4f, 10.7f, 34.68f), new Vector3(0.3f, 0.2f, 0.1f), Mat("Jungle_Dark", "#1E221C"));
            Box("IdolMouth", root, new Vector3(0f, 9.7f, 34.68f), new Vector3(0.9f, 0.2f, 0.1f), Mat("Jungle_Dark", "#1E221C"));
            spots[5] = Mark("Spot_Idol", root, new Vector3(idolIsland.x, 9f, idolIsland.z - 2.7f));
            spots[5].localRotation = Quaternion.Euler(0f, 180f, 0f);

            // 6 — a totem pole on a rock off the west beach, jumping east, 4.5 m.
            var totemRock = new Vector3(-24f, 0.3f, 30f);
            RockPillar(root, solids, "TotemRock", totemRock, -3.5f, 4.1f, 3.4f, 2.6f, new[] { mossC, mossB }, capGrass, 94);
            Solid("TotemPole", root, new Vector3(totemRock.x - 1.1f, 7f, totemRock.z), new Vector3(0.9f, 5.8f, 0.9f), wood, solids);
            foreach (float ty in new[] { 5.3f, 6.6f, 7.9f })
                Box("TotemFace", root, new Vector3(totemRock.x - 0.62f, ty, totemRock.z), new Vector3(0.08f, 0.8f, 0.8f), ty > 7f ? thatch : woodDark);
            spots[6] = Mark("Spot_Totem", root, new Vector3(totemRock.x + 2.2f, 4.1f, totemRock.z));
            spots[6].localRotation = Quaternion.Euler(0f, 90f, 0f);

            var tower = MakeTower(root, spots, solids, new[] { -1f, 1f, -1f, 1f, 1f, 1f, 1f }, new[] { 0f, 0f, -2.5f, -2.5f, -3f, 0f, 0f });
            SeaGulls(site, new Vector3(0f, 0f, 14f));
            site.position = SiteOffsets[6];

            return MakeSite(site, tower, new Look
            {
                sun = new Vector3(48f, 160f, 0f), sunColor = "#FFF0CC", sunIntensity = 1.05f,
                ambientSky = "#9FD0B0", ambientEquator = "#8FB890", ambientGround = "#3E5A44",
                fog = "#CFE8D8", fogStart = 50f, fogEnd = 185f,
                skyTint = "#78C4EA", skyGround = "#C4E0CE", exposure = 1.05f, atmosphere = 0.7f,
                swell = 0.02f, swellPeriod = 3f, swellLength = 8f,
                mapCenter = new Vector3(0f, 8f, 6f), mapDistance = 140f, mapDirection = new Vector3(-0.5f, -0.6f, -1f)
            });
        }
    }
}
