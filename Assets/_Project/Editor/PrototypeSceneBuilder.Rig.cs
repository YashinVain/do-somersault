using System.Collections.Generic;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // Offshore platform at sunset: a boat landing, a lower walkway, the main deck, a helideck and a crane boom.
    public static partial class PrototypeSceneBuilder
    {
        static LocationSite BuildRigSite()
        {
            var site = new GameObject("Site_Rig").transform;
            SeaBlock(site, "RigWater", "#1D5B74", 0.9f, 32);

            var steel = Mat("Rig_Steel", "#8A9098");
            var steelDark = Mat("Rig_SteelDark", "#4C535B");
            var yellow = Mat("Rig_Yellow", "#E0A92B");
            var white = Mat("Rig_White", "#EEEBE3");
            var red = Mat("Rig_Red", "#C2412F");
            var orange = Mat("Rig_Orange", "#E8742B");
            var lit = Glow(Mat("Rig_Lit", "#FFC46A", smoothness: 0.2f), 1.7f);
            var flame = Glow(Mat("Rig_Flame", "#FF8A2B", smoothness: 0.1f), 2.2f);
            var glass = Mat("Rig_Glass", "#2A3C4E", smoothness: 0.9f);
            var deckMat = Mat("Rig_Deck", "#5E656D");

            var root = Child("Rig", site);
            var solids = new List<Bounds>();
            var spots = new Transform[7];

            // Four legs standing on the sea floor, with bracing between them that shows from the side.
            float[] legX = { -14f, 14f };
            float[] legZ = { -9f, -27f };
            var legMesh = LowPolyMeshes.Prism("RigLeg", 10, 1.55f, 1.3f, 22f);
            foreach (float lx in legX)
                foreach (float lz in legZ)
                {
                    Put(legMesh, "Leg", root, new Vector3(lx, 2f, lz), Vector3.one, steel);
                    solids.Add(new Bounds(new Vector3(lx, 2f, lz), new Vector3(2.9f, 22f, 2.9f)));
                    Put(hex, "LegBand", root, new Vector3(lx, 0.3f, lz), new Vector3(3.4f, 0.5f, 3.4f), yellow);
                    Put(hex, "LegBand", root, new Vector3(lx, 8f, lz), new Vector3(3.2f, 0.4f, 3.2f), yellow);
                }
            foreach (float lx in legX)
            {
                Beam("Brace", root, new Vector3(lx, -2f, legZ[0]), new Vector3(lx, 12f, legZ[1]), 0.5f, steelDark);
                Beam("Brace", root, new Vector3(lx, -2f, legZ[1]), new Vector3(lx, 12f, legZ[0]), 0.5f, steelDark);
                Beam("Strut", root, new Vector3(lx, 5f, legZ[0]), new Vector3(lx, 5f, legZ[1]), 0.5f, steelDark);
            }
            foreach (float lz in legZ)
                Beam("Strut", root, new Vector3(legX[0], 5f, lz), new Vector3(legX[1], 5f, lz), 0.5f, steelDark);

            // Main deck at 13 m with a handrail and the buildings on it.
            var deck = new Vector3(0f, 12.7f, -18f);
            var deckSize = new Vector3(38f, 0.6f, 28f);
            Solid("MainDeck", root, deck, deckSize, deckMat, solids);
            Box("DeckSkirt", root, new Vector3(0f, 12.2f, -4.05f), new Vector3(38f, 0.6f, 0.14f), yellow);
            foreach (var (x0, x1) in new[] { (-19f, 0.5f), (7.5f, 19f) })
            {
                Box("DeckRail", root, new Vector3((x0 + x1) * 0.5f, 14.05f, -4.2f), new Vector3(x1 - x0, 0.07f, 0.07f), yellow);
                Box("DeckRailMid", root, new Vector3((x0 + x1) * 0.5f, 13.55f, -4.2f), new Vector3(x1 - x0, 0.05f, 0.05f), yellow);
                for (float x = x0 + 0.4f; x < x1; x += 2.2f)
                    Put(hex, "RailPost", root, new Vector3(x, 13.6f, -4.2f), new Vector3(0.08f, 1.1f, 0.08f), yellow);
            }
            Box("DeckEdgePaint", root, new Vector3(4f, 13.005f, -4.12f), new Vector3(6f, 0.01f, 0.2f), white);

            // Quarters block at the back with lit windows, and a stack of tanks.
            Solid("Quarters", root, new Vector3(9f, 17.6f, -26f), new Vector3(12f, 9f, 8f), white, solids);
            Box("QuartersRoof", root, new Vector3(9f, 22.3f, -26f), new Vector3(12.6f, 0.4f, 8.6f), steelDark);
            for (float fy = 15.2f; fy < 21f; fy += 2.4f)
                for (float wx = 4f; wx <= 14f; wx += 2f)
                    LitWindow(root, new Vector3(wx, fy, -21.96f), new Vector3(1f, 1.1f, 0.06f), lit);
            foreach (float tx in new[] { -2f, 1.5f })
                Put(Smooth, "Tank", root, new Vector3(tx, 15.8f, -24f), new Vector3(3.2f, 5.6f, 3.2f), white);
            foreach (float pz in new[] { -10f, -12f, -14f })
                Put(hex, "Pipe", root, new Vector3(2f, 13.6f, pz), new Vector3(0.5f, 14f, 0.5f), orange).localRotation = Quaternion.Euler(0f, 0f, 90f);

            // The drilling derrick: four slanted legs with rings, a lit top.
            Vector3 dc = new Vector3(-4f, 13f, -17f);
            var derrickModel = Prop("Structures", "Derrick", root, dc, Vector3.one, 0f, StructureSlots(("DerrickRed", red), ("Lamp", lit)));
            if (derrickModel == null)
            {
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Beam("DerrickLeg", root, dc + new Vector3(sx * 3.2f, 0f, sz * 3.2f), dc + new Vector3(sx * 0.8f, 32f, sz * 0.8f), 0.32f, red);
            for (float y = 5f; y < 31f; y += 6f)
            {
                float k = 3.2f - (y / 32f) * 2.4f;
                foreach (float s in new[] { -1f, 1f })
                {
                    Beam("DerrickRing", root, dc + new Vector3(-k, y, s * k), dc + new Vector3(k, y, s * k), 0.16f, red);
                    Beam("DerrickRing", root, dc + new Vector3(s * k, y, -k), dc + new Vector3(s * k, y, k), 0.16f, red);
                }
            }
            Put(Smooth, "DerrickLight", root, dc + new Vector3(0f, 33f, 0f), Vector3.one * 0.7f, lit);
            }
            solids.Add(new Bounds(dc + new Vector3(0f, 16f, 0f), new Vector3(7f, 32f, 7f)));

            // Flare stack on the corner with a flame at the tip.
            Put(hex, "FlareStack", root, new Vector3(17f, 28f, -29f), new Vector3(0.8f, 30f, 0.8f), steelDark);
            var fl = Put(cone, "Flame", root, new Vector3(17f, 44.4f, -29f), new Vector3(1.8f, 3.4f, 1.8f), flame);
            AddSway(fl, Vector3.forward, 10f, 3f, 0f);

            // 0 — the boat landing at the foot of a leg, 2.5 m.
            var landing = new Vector3(-14f, 2.35f, -5.9f);
            var landingSize = new Vector3(5f, 0.25f, 4.2f);
            Solid("BoatLanding", root, landing, landingSize, steelDark, solids);
            for (float y = 2.6f; y < 3.6f; y += 0.5f)
                Box("LandingRail", root, new Vector3(-16.4f, y + 0.5f, -5.9f), new Vector3(0.05f, 0.05f, 4.2f), yellow);
            foreach (float rx in new[] { -16.5f, -11.5f })
                Beam("LandingBrace", root, new Vector3(rx, 2.2f, -3.9f), new Vector3(rx, -0.8f, -7.6f), 0.18f, steelDark);
            for (float y = 0.4f; y < 2.3f; y += 0.4f)
                Box("LandingLadder", root, new Vector3(-12.5f, y, -7.9f), new Vector3(0.7f, 0.04f, 0.05f), white);
            spots[0] = Mark("Spot_Landing", root, new Vector3(-14f, 2.475f, -3.8f));

            // Lifeboat hanging on its davits, to give the sea level something to look at.
            var boat = Mark("Lifeboat", root, new Vector3(-17.5f, 3.2f, -9f));
            Put(LowPolyMeshes.Capsule("Lifeboat", 0.9f, 0.9f, 3f), "Hull", boat, Vector3.zero, new Vector3(1f, 1f, 1f), orange).localRotation = Quaternion.Euler(90f, 0f, 0f);

            // 1 — a lower walkway under the main deck, 8 m.
            var walk = new Vector3(-2f, 7.875f, -5.9f);
            var walkSize = new Vector3(14f, 0.25f, 5.4f);
            Solid("Walkway", root, walk, walkSize, steelDark, solids);
            foreach (float rx in new[] { -8.8f, 4.8f })
                Box("WalkRail", root, new Vector3(rx, 8.5f, -5.9f), new Vector3(0.06f, 0.06f, 5.2f), yellow);
            Box("WalkRailBack", root, new Vector3(-2f, 8.5f, -8.5f), new Vector3(13.6f, 0.06f, 0.06f), yellow);
            Beam("WalkBrace", root, new Vector3(-8f, 7.7f, -3.5f), new Vector3(-8f, 5f, -8.5f), 0.2f, steelDark);
            Beam("WalkBrace", root, new Vector3(4f, 7.7f, -3.5f), new Vector3(4f, 5f, -8.5f), 0.2f, steelDark);
            Box("WalkEdgePaint", root, new Vector3(-2f, 8.005f, -3.3f), new Vector3(4f, 0.01f, 0.16f), white);
            spots[1] = Mark("Spot_Walkway", root, new Vector3(-2f, 8f, -3.2f));

            // 2 — the edge of the main deck, 13 m.
            spots[2] = Mark("Spot_Deck", root, new Vector3(4f, 13f, -4f));

            // 3 — the helideck on a cantilever, 19 m.
            var pad = new Vector3(12f, 18.8f, -3.5f);
            Put(LowPolyMeshes.Prism("Helipad", 20, 4.9f, 4.9f, 0.4f), "Helipad", root, pad, Vector3.one, steelDark);
            Put(LowPolyMeshes.Prism("HelipadRing", 20, 4.2f, 4.2f, 0.02f), "HelipadRing", root, pad + new Vector3(0f, 0.21f, 0f), Vector3.one, yellow);
            Put(LowPolyMeshes.Prism("HelipadInner", 20, 3.9f, 3.9f, 0.03f), "HelipadInner", root, pad + new Vector3(0f, 0.215f, 0f), Vector3.one, steelDark);
            foreach (float hx in new[] { -0.9f, 0.9f })
                Box("HelipadH", root, pad + new Vector3(hx, 0.24f, 0f), new Vector3(0.4f, 0.02f, 2.6f), white);
            Box("HelipadH", root, pad + new Vector3(0f, 0.24f, 0f), new Vector3(1.9f, 0.02f, 0.4f), white);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                Put(Smooth, "PadLight", root, pad + new Vector3(Mathf.Cos(a) * 4.75f, 0.28f, Mathf.Sin(a) * 4.75f), Vector3.one * 0.22f, lit);
            }
            solids.Add(new Bounds(pad, new Vector3(9.8f, 0.4f, 9.8f)));
            foreach (float bx in new[] { 8.5f, 15.5f })
                Beam("HelipadStrut", root, new Vector3(bx, 13.2f, -6.8f), new Vector3(bx, 18.6f, -1.2f), 0.34f, steel);
            Beam("HelipadStrut", root, new Vector3(12f, 13.2f, -5f), new Vector3(12f, 18.6f, -0.2f), 0.34f, steel);
            spots[3] = Mark("Spot_Helideck", root, new Vector3(12f, 19f, 1f));

            // 4 — the end of a crane boom, 27 m.
            Put(LowPolyMeshes.Prism("CranePedestal", 8, 1.3f, 1.1f, 12f), "Pedestal", root, new Vector3(-12f, 19f, -8f), Vector3.one, yellow);
            solids.Add(new Bounds(new Vector3(-12f, 19f, -8f), new Vector3(2.6f, 12f, 2.6f)));
            Solid("CraneCab", root, new Vector3(-12f, 25.8f, -8.4f), new Vector3(2.6f, 1.8f, 2.4f), white, solids);
            Box("CraneGlass", root, new Vector3(-12f, 26f, -7.18f), new Vector3(2.1f, 0.8f, 0.05f), glass);
            var boom = new Vector3(-12f, 26.4f, -1.9f);
            var boomSize = new Vector3(1.4f, 0.25f, 13.4f);
            Solid("CraneBoom", root, boom, boomSize, yellow, solids);
            foreach (float bx in new[] { -12.65f, -11.35f })
            {
                Beam("BoomTruss", root, new Vector3(bx, 26.3f, -8f), new Vector3(bx, 25.2f, 4.2f), 0.14f, yellow);
                for (float z = -7f; z < 4f; z += 2.2f)
                    Beam("BoomWeb", root, new Vector3(bx, 26.3f, z), new Vector3(bx, 25.5f, z + 1.1f), 0.1f, yellow);
            }
            foreach (float bx in new[] { -12.7f, -11.3f })
                Box("BoomRail", root, new Vector3(bx, 27f, -1.9f), new Vector3(0.05f, 0.05f, 12.4f), yellow);
            Put(Smooth, "BoomLight", root, new Vector3(-12f, 27.4f, 4.6f), Vector3.one * 0.35f, lit);
            Box("BoomTipEdge", root, new Vector3(-12f, 26.535f, 4.7f), new Vector3(1.4f, 0.01f, 0.14f), white);
            spots[4] = Mark("Spot_Crane", root, new Vector3(-12f, 26.525f, 4.7f));

            // 5 — a supply vessel standing off the rig, bow towards it; dive off the bow deck, 3.2 m.
            var vessel = new Vector3(24f, 0f, 30f);
            Solid("VesselHull", root, vessel + new Vector3(0f, 1.6f, 0f), new Vector3(4.4f, 3.2f, 14f), red, solids);
            var vesselBow = Box("VesselBow", root, vessel + new Vector3(0f, 1.6f, -7f), new Vector3(3.1f, 3.2f, 3.1f), red);
            vesselBow.localRotation = Quaternion.Euler(0f, 45f, 0f);
            solids.Add(new Bounds(vessel + new Vector3(0f, 1.6f, -8f), new Vector3(2.4f, 3.2f, 2.2f)));
            Box("VesselStripe", root, vessel + new Vector3(0f, 0.7f, 0f), new Vector3(4.46f, 0.3f, 14.06f), white);
            Box("VesselDeck", root, vessel + new Vector3(0f, 3.22f, 0f), new Vector3(4.1f, 0.05f, 13.6f), deckMat);
            Solid("VesselHouse", root, vessel + new Vector3(0f, 5.2f, 4.2f), new Vector3(3.8f, 3.6f, 4.2f), white, solids);
            Box("VesselGlass", root, vessel + new Vector3(0f, 5.6f, 2.07f), new Vector3(3.2f, 0.8f, 0.05f), glass);
            Box("VesselCrane", root, vessel + new Vector3(1.2f, 4.2f, -1.6f), new Vector3(0.3f, 2f, 0.3f), yellow);
            Put(hex, "VesselFunnel", root, vessel + new Vector3(0f, 7.7f, 5.2f), new Vector3(1f, 1.8f, 1f), yellow);
            spots[5] = Mark("Spot_Vessel", root, vessel + new Vector3(0f, 3.2f, -8.3f));
            spots[5].localRotation = Quaternion.Euler(0f, 180f, 0f);

            // 6 — a navigation tower on a pile of rock, jumping east off its gallery, 14 m.
            var navTower = new Vector3(-30f, 4f, 30f);
            Put(LowPolyMeshes.Prism("NavTower", 8, 2.2f, 1.5f, 20f), "NavTower", root, navTower, Vector3.one, yellow);
            Put(LowPolyMeshes.Prism("NavTowerBand", 8, 2.1f, 1.9f, 1.4f), "Band", root, navTower + new Vector3(0f, -2f, 0f), Vector3.one, white);
            Put(LowPolyMeshes.Prism("NavGallery", 12, 3f, 3f, 0.3f), "Gallery", root, new Vector3(navTower.x, 13.85f, navTower.z), Vector3.one, steelDark);
            Put(LowPolyMeshes.Prism("NavLamp", 8, 1f, 1f, 1.5f), "Lamp", root, new Vector3(navTower.x, 14.8f, navTower.z), Vector3.one, lit);
            Put(cone, "NavCap", root, new Vector3(navTower.x, 16.2f, navTower.z), new Vector3(2.2f, 1.2f, 2.2f), red);
            Put(RockMesh, "NavRock", root, navTower + new Vector3(0f, -8f, 0f), new Vector3(9f, 5f, 8f), steelDark);
            solids.Add(new Bounds(navTower + new Vector3(0f, -6f, 0f), new Vector3(7f, 4f, 6.4f)));
            solids.Add(new Bounds(navTower + new Vector3(0f, 0f, 0f), new Vector3(3f, 16f, 3f)));
            solids.Add(new Bounds(new Vector3(navTower.x, 13.85f, navTower.z), new Vector3(6f, 0.3f, 6f)));
            spots[6] = Mark("Spot_NavTower", root, new Vector3(navTower.x + 2.6f, 14f, navTower.z));
            spots[6].localRotation = Quaternion.Euler(0f, 90f, 0f);

            var tower = MakeTower(root, spots, solids, new[] { 1f, 1f, -1f, 1f, 1f, 1f, 1f }, new[] { 0f, -2.5f, -2.5f, -2.5f, -2.5f, -2.5f, -2.5f });
            SeaGulls(site, new Vector3(0f, 0f, 12f));
            site.position = SiteOffsets[7];

            return MakeSite(site, tower, new Look
            {
                sun = new Vector3(13f, 205f, 0f), sunColor = "#FFB070", sunIntensity = 1.15f,
                ambientSky = "#B07A78", ambientEquator = "#C98F6A", ambientGround = "#4A3A44",
                fog = "#E4A57E", fogStart = 55f, fogEnd = 200f,
                skyTint = "#E8855A", skyGround = "#6B6670", exposure = 1.15f, atmosphere = 1.2f,
                swell = 0.2f, swellPeriod = 5f, swellLength = 20f,
                mapCenter = new Vector3(0f, 12f, 4f), mapDistance = 150f, mapDirection = new Vector3(-0.5f, -0.58f, -1f)
            });
        }
    }
}
