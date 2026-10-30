using System.Collections.Generic;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // Northern bay: a glacier wall, an icebreaker, an iceberg, an ice arch and penguins.
    public static partial class PrototypeSceneBuilder
    {
        const float GlacierTop = 26f, GlacierBack = -30f;

        static float GlacierFaceZ(float x) => -3f + 1.6f * Mathf.Sin(x * 0.11f + 1f) + 0.7f * Mathf.Sin(x * 0.31f);

        static LocationSite BuildArcticSite()
        {
            var site = new GameObject("Site_Arctic").transform;
            SeaBlock(site, "ArcticWater", "#3C86A6", 0.88f);

            var iceA = Mat("Ice_A", "#DCEBF3", smoothness: 0.45f);
            var iceB = Mat("Ice_B", "#B3D3E5", smoothness: 0.5f);
            var iceC = Mat("Ice_C", "#8DB9D2", smoothness: 0.5f);
            var snow = Mat("Snow", "#F3F8FA", smoothness: 0.2f);
            var shard = LowPolyMeshes.Icosphere("Shard", 0, 0.5f, 0.28f, 13);
            var rng = new System.Random(8);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            var root = Child("Ice", site);
            var solids = new List<Bounds>();
            var spots = new Transform[7];
            float half = SeaBlockSize * 0.5f;

            // The glacier: a faceted wall with a snow cap running back.
            var wallMesh = LowPolyMeshes.CliffWall("GlacierWall", SeaBlockSize, -6f, GlacierTop, 24, 10, 3, GlacierBack, GlacierFaceZ, 33);
            var wall = Put(wallMesh, "Wall", root, Vector3.zero, Vector3.one, iceA);
            wall.GetComponent<MeshRenderer>().sharedMaterials = new[] { iceA, iceB, iceC, snow };
            foreach (float side in new[] { -1f, 1f })
            {
                float zf = GlacierFaceZ(side * half) - 0.5f;
                Box("End", root, new Vector3(side * (half - 0.3f), (GlacierTop - 6f) * 0.5f, (zf + GlacierBack) * 0.5f), new Vector3(0.6f, GlacierTop + 6f, zf - GlacierBack), iceB);
            }
            Box("Back", root, new Vector3(0f, (GlacierTop - 6f) * 0.5f, GlacierBack + 0.3f), new Vector3(SeaBlockSize, GlacierTop + 6f, 0.6f), iceB);
            WallSolids(wallMesh, solids, SeaBlockSize, 24, 10, -6f, GlacierTop, GlacierBack);

            // Snow pines on top of the glacier.
            var needles = Mat("SnowPine", "#CFE0DA");
            var bark = Mat("Trunk", "#8A5B3D");
            for (int i = 0; i < 16; i++)
                Pine(root, new Vector3(R(-40f, 40f), GlacierTop, R(-27f, -9f)), R(0.9f, 1.5f), needles, bark);

            // Ice at the foot of the wall and snow shelves at both ends where the penguins stand.
            for (int k = 0; k < 8; k++)
            {
                float x = R(-38f, 38f);
                if (Mathf.Abs(x - 18f) < 6f) continue;
                Put(shard, "Chunk", root, new Vector3(x, R(-0.7f, 0f), GlacierFaceZ(x) + R(0.6f, 1.6f)), new Vector3(R(1.8f, 3.2f), R(1.2f, 2.2f), R(1.8f, 2.6f)), k % 2 == 0 ? iceB : iceC);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float sx = side * 39f;
                Solid("Shelf", root, new Vector3(sx, (0.7f - 6f) * 0.5f, 2.5f), new Vector3(12f, 6.7f, 11f), snow, solids);
            }

            // 0 — an ice floe, 0.6 m, with a few penguins on it.
            var floe = new Vector3(-30f, 0f, 2f);
            Box("Floe", root, floe + new Vector3(0f, 0.1f, 0f), new Vector3(7.4f, 1f, 6f), snow);
            Put(shard, "FloeUnder", root, floe + new Vector3(0f, -1.1f, 0f), new Vector3(7f, 2.2f, 5.6f), iceB);
            solids.Add(new Bounds(floe + new Vector3(0f, 0.1f, 0f), new Vector3(7.4f, 1f, 6f)));
            spots[0] = Mark("Spot_Floe", root, floe + new Vector3(0f, 0.6f, 2.9f));
            Penguin(root, floe + new Vector3(-2.2f, 0.6f, -1.6f), 20f, 1f);
            Penguin(root, floe + new Vector3(2.4f, 0.6f, -1.2f), -10f, 0.85f);
            Penguin(root, new Vector3(-37f, 0.7f, 5f), 0f, 1f);
            Penguin(root, new Vector3(-40f, 0.7f, 3.6f), -25f, 0.9f);
            Penguin(root, new Vector3(37f, 0.7f, 4f), 180f - 160f, 1f);
            Penguin(root, new Vector3(41f, 0.7f, 5.5f), 20f, 0.8f);
            for (int k = 0; k < 6; k++)
            {
                var s = new Vector3(R(-14f, 40f), 0f, R(24f, 48f));
                Put(shard, "Brash", root, s, new Vector3(R(1.5f, 3f), 0.5f, R(1.5f, 3f)), iceA);
            }

            // 1 — an icebreaker, bow to the open sea; dive off the foredeck, 3.4 m.
            var hullRed = Mat("Breaker_Hull", "#B23A32", smoothness: 0.5f);
            var hullDark = Mat("Breaker_Dark", "#222831");
            var white = Mat("Breaker_White", "#EDEBE4");
            var glass = Mat("Breaker_Glass", "#27394B", smoothness: 0.9f);
            var deckGrey = Mat("Breaker_Deck", "#6C7680");
            var b0 = new Vector3(-19f, 0f, 9f);
            Box("BreakerHull", root, b0 + new Vector3(0f, 1.55f, 0f), new Vector3(4.8f, 3.4f, 15f), hullRed);
            var bow = Box("BreakerBow", root, b0 + new Vector3(0f, 1.55f, 7.5f), new Vector3(3.4f, 3.4f, 3.4f), hullRed);
            bow.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Box("BreakerWaterline", root, b0 + new Vector3(0f, 0.2f, 0f), new Vector3(4.86f, 0.6f, 15.06f), hullDark);
            var bowLine = Box("BreakerBowLine", root, b0 + new Vector3(0f, 0.2f, 7.5f), new Vector3(3.44f, 0.6f, 3.44f), hullDark);
            bowLine.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Box("BreakerDeck", root, b0 + new Vector3(0f, 3.27f, 0f), new Vector3(4.5f, 0.06f, 14.6f), deckGrey);
            var bowDeck = Box("BreakerBowDeck", root, b0 + new Vector3(0f, 3.27f, 7.5f), new Vector3(3.2f, 0.06f, 3.2f), deckGrey);
            bowDeck.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Solid("BreakerHouse", root, b0 + new Vector3(0f, 5.4f, -3.5f), new Vector3(4.2f, 4.2f, 5.5f), white, solids);
            Box("BreakerBridge", root, b0 + new Vector3(0f, 8.2f, -2.4f), new Vector3(4.4f, 1.4f, 3.2f), white);
            Box("BreakerGlass", root, b0 + new Vector3(0f, 8.4f, -0.78f), new Vector3(3.8f, 0.7f, 0.05f), glass);
            Put(hex, "BreakerFunnel", root, b0 + new Vector3(0f, 8.8f, -5.2f), new Vector3(1.1f, 2.8f, 1.1f), hullRed);
            Put(hex, "BreakerFunnelBand", root, b0 + new Vector3(0f, 9.6f, -5.2f), new Vector3(1.16f, 0.4f, 1.16f), white);
            solids.Add(new Bounds(b0 + new Vector3(0f, 1.55f, 0.3f), new Vector3(4.8f, 3.4f, 16f)));
            solids.Add(new Bounds(b0 + new Vector3(0f, 8f, -2.4f), new Vector3(4.4f, 2.2f, 3.2f)));
            solids.Add(new Bounds(b0 + new Vector3(0f, 1.55f, 8.3f), new Vector3(2.6f, 3.4f, 2.2f)));
            solids.Add(new Bounds(b0 + new Vector3(0f, 1.55f, 9.6f), new Vector3(1.2f, 3.4f, 1.0f)));
            spots[1] = Mark("Spot_Icebreaker", root, b0 + new Vector3(0f, 3.3f, 8.5f));

            // 2 — an iceberg with a flat top, 8 m.
            var berg = new Vector3(-5f, 0.5f, 8f);
            var bergMesh = LowPolyMeshes.Prism("Iceberg", 8, 6.6f, 4.3f, 15f);
            var bergT = Put(bergMesh, "Iceberg", root, berg, Vector3.one, iceA);
            bergT.localRotation = Quaternion.Euler(0f, 22.5f, 0f);
            Put(shard, "BergPeak", root, berg + new Vector3(-1.4f, 7.8f, -1.2f), new Vector3(3.4f, 3.4f, 3f), iceB);
            Put(shard, "BergSpur", root, berg + new Vector3(3.6f, 2.2f, -2.4f), new Vector3(4.4f, 5.6f, 4.4f), iceC);
            Put(shard, "BergFoot", root, berg + new Vector3(-3.6f, -0.2f, 3.2f), new Vector3(4.6f, 3.4f, 4.2f), iceB);
            // The berg is wider under the water; follow the slope in steps so a fall onto its side lands on ice.
            TaperedSolids(solids, berg, -7f, 8f, 6f, 4.1f, 5);
            spots[2] = Mark("Spot_Iceberg", root, new Vector3(berg.x, 8f, 8f + 4.3f * 0.924f - 0.15f));

            // 3 — a ledge in the glacier wall, 14 m.
            spots[3] = WallLedge(root, solids, "GlacierLedge", 18f, 14f, 2.9f, GlacierFaceZ(18f), iceA, iceB, shard);

            // 4 — the top of an ice arch standing off the wall, 20 m.
            var pillar = LowPolyMeshes.Prism("IcePillar", 7, 2.7f, 2.1f, 26f);
            foreach (float px in new[] { 27f, 37f })
            {
                Put(pillar, "ArchPillar", root, new Vector3(px, 7f, 0.5f), Vector3.one, iceB);
                solids.Add(new Bounds(new Vector3(px, 7f, 0.5f), new Vector3(4.2f, 26f, 4.2f)));
                Put(shard, "ArchFoot", root, new Vector3(px + (px < 30f ? -1f : 1f), -0.2f, 0f), new Vector3(5.2f, 2.8f, 5f), iceC);
            }
            var slab = new Vector3(32f, 19.7f, 0.6f);
            var slabSize = new Vector3(15f, 0.6f, 7f);
            Box("ArchTop", root, slab, slabSize, snow);
            solids.Add(new Bounds(slab, slabSize));
            Put(shard, "ArchBlock", root, new Vector3(32f, 18.6f, 0.5f), new Vector3(9f, 2.2f, 5.4f), iceA);
            Put(shard, "ArchSnow", root, new Vector3(30f, 20.2f, -1.6f), new Vector3(3.6f, 1.2f, 2.8f), snow);
            spots[4] = Mark("Spot_Arch", root, new Vector3(32f, 20f, 3.9f));

            // 5 — a second, smaller iceberg far out, jumping back at the glacier, 5.5 m.
            var berg2 = new Vector3(16f, 0.5f, 28f);
            Put(LowPolyMeshes.Prism("IcebergSmall", 7, 4.6f, 3.1f, 12f), "IcebergSmall", root, berg2, Vector3.one, iceB);
            Put(shard, "IcebergSmallPeak", root, berg2 + new Vector3(1.2f, 6.2f, 0.6f), new Vector3(2.6f, 3f, 2.4f), iceA);
            TaperedSolids(solids, berg2, -5.5f, 5.5f, 4.5f, 3f, 4);
            spots[5] = Mark("Spot_IcebergSmall", root, new Vector3(berg2.x, 5.5f, berg2.z - 2.7f));
            spots[5].localRotation = Quaternion.Euler(0f, 180f, 0f);

            // 6 — a tall ice tower on the west side, jumping east, 16 m.
            var tower2 = new Vector3(-30f, 5f, 26f);
            Put(LowPolyMeshes.Prism("IceTower", 6, 3.8f, 2.6f, 22f), "IceTower", root, tower2, Vector3.one, iceC);
            Put(shard, "IceTowerFoot", root, tower2 + new Vector3(-2.4f, -9f, 1f), new Vector3(6f, 3.4f, 5.4f), iceB);
            Put(shard, "IceTowerCap", root, tower2 + new Vector3(0f, 11.1f, 0f), new Vector3(4.2f, 0.6f, 4f), snow);
            TaperedSolids(solids, tower2, -6f, 16f, 3.7f, 2.5f, 5);
            spots[6] = Mark("Spot_IceTower", root, new Vector3(tower2.x + 2.2f, 16f, tower2.z));
            spots[6].localRotation = Quaternion.Euler(0f, 90f, 0f);

            var tower = MakeTower(root, spots, solids, new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f }, new[] { 0f, -2.5f, -2.5f, -2.5f, -2.5f, 0f, 0f });
            SeaGulls(site, new Vector3(0f, 0f, 14f));
            site.position = SiteOffsets[4];

            return MakeSite(site, tower, new Look
            {
                sun = new Vector3(34f, 205f, 0f), sunColor = "#FFF1E2", sunIntensity = 1.05f,
                ambientSky = "#A6C6DE", ambientEquator = "#C4D4DE", ambientGround = "#7C8D9C",
                fog = "#D3E3EC", fogStart = 55f, fogEnd = 190f,
                skyTint = "#9CC4E4", skyGround = "#D8E6EE", exposure = 1.1f, atmosphere = 0.65f,
                swell = 0.07f, swellPeriod = 5.2f, swellLength = 20f,
                mapCenter = new Vector3(0f, 6f, 8f), mapDistance = 140f, mapDirection = new Vector3(-0.5f, -0.6f, -1f)
            });
        }

        // A standing penguin, a little over a metre tall, facing +Z rotated by yaw.
        static void Penguin(Transform parent, Vector3 at, float yaw, float scale)
        {
            var black = Mat("Penguin_Black", "#1C2230", smoothness: 0.3f);
            var white = Mat("Penguin_White", "#F4F4F0", smoothness: 0.2f);
            var orange = Mat("Penguin_Orange", "#F28C28");

            var p = Mark("Penguin", parent, at);
            p.localRotation = Quaternion.Euler(0f, yaw, 0f);
            p.localScale = Vector3.one * scale * 1.3f;
            Put(Smooth, "Body", p, new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.9f, 0.42f), black);
            Put(Smooth, "Belly", p, new Vector3(0f, 0.42f, 0.07f), new Vector3(0.38f, 0.7f, 0.32f), white);
            Put(Smooth, "Head", p, new Vector3(0f, 0.98f, 0.02f), new Vector3(0.3f, 0.3f, 0.3f), black);
            var beak = Put(cone, "Beak", p, new Vector3(0f, 0.96f, 0.19f), new Vector3(0.08f, 0.14f, 0.08f), orange);
            beak.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (float x in new[] { -1f, 1f })
            {
                var flipper = Box("Flipper", p, new Vector3(x * 0.27f, 0.5f, 0f), new Vector3(0.05f, 0.4f, 0.2f), black);
                flipper.localRotation = Quaternion.Euler(0f, 0f, x * -14f);
                Box("Foot", p, new Vector3(x * 0.09f, 0.02f, 0.1f), new Vector3(0.12f, 0.04f, 0.22f), orange);
            }
            AddSway(p, Vector3.forward, 3f, 0.9f, yaw * 0.05f);
        }
    }
}
