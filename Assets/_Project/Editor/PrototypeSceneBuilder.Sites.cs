using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace JumpGame.EditorTools
{
    // Locations, the life in them, and the 3D level map.
    public static partial class PrototypeSceneBuilder
    {
        // Components built before the diver exists that need a reference to it.
        static readonly List<(Object owner, string field, bool asTransform)> needsDiver = new();

        struct Look
        {
            public Vector3 sun;
            public string sunColor, ambientSky, ambientEquator, ambientGround, fog, skyTint, skyGround;
            public float sunIntensity, fogStart, fogEnd, exposure, atmosphere;
            public float swell, swellPeriod, swellLength;
            public Vector3 mapCenter, mapDirection;
            public float mapDistance;
        }

        static LocationSite MakeSite(Transform root, DivingTower tower, Look l)
        {
            var site = root.gameObject.AddComponent<LocationSite>();
            var so = new SerializedObject(site);
            so.FindProperty("tower").objectReferenceValue = tower;
            so.FindProperty("sunAngles").vector3Value = l.sun;
            so.FindProperty("sunColor").colorValue = Hex(l.sunColor);
            so.FindProperty("sunIntensity").floatValue = l.sunIntensity;
            so.FindProperty("ambientSky").colorValue = Hex(l.ambientSky);
            so.FindProperty("ambientEquator").colorValue = Hex(l.ambientEquator);
            so.FindProperty("ambientGround").colorValue = Hex(l.ambientGround);
            so.FindProperty("fogColor").colorValue = Hex(l.fog);
            so.FindProperty("fogStart").floatValue = l.fogStart;
            so.FindProperty("fogEnd").floatValue = l.fogEnd;
            so.FindProperty("skyTint").colorValue = Hex(l.skyTint);
            so.FindProperty("skyGround").colorValue = Hex(l.skyGround);
            so.FindProperty("skyExposure").floatValue = l.exposure;
            so.FindProperty("atmosphere").floatValue = l.atmosphere;
            so.FindProperty("swell").floatValue = l.swell;
            so.FindProperty("swellPeriod").floatValue = l.swellPeriod;
            so.FindProperty("swellLength").floatValue = l.swellLength;
            so.FindProperty("mapCenter").vector3Value = l.mapCenter;
            so.FindProperty("mapDistance").floatValue = l.mapDistance;
            so.FindProperty("mapDirection").vector3Value = l.mapDirection;
            so.ApplyModifiedPropertiesWithoutUndo();
            return site;
        }

        static void Set(Object owner, string field, float value)
        {
            var so = new SerializedObject(owner);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object owner, string field, Vector3 value)
        {
            var so = new SerializedObject(owner);
            so.FindProperty(field).vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object owner, string field, Vector2 value)
        {
            var so = new SerializedObject(owner);
            so.FindProperty(field).vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddSway(Transform t, Vector3 axis, float angle, float speed, float phase)
        {
            var s = t.gameObject.AddComponent<Sway>();
            Set(s, "axis", axis);
            Set(s, "angle", angle);
            Set(s, "speed", speed);
            Set(s, "phase", phase);
        }

        static Material Glow(Material m, float amount)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", m.color * amount);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        // Faceted water that follows the shared swell at runtime; a flat quad stands in for it in the editor.
        static void WaterSurface(string name, Transform parent, Vector3 at, float size, int cells, Material mat)
        {
            var t = Put(LowPolyMeshes.Quad(name + "_Flat", size, size), name, parent, at, Vector3.one, mat);
            t.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var w = t.gameObject.AddComponent<WaveSurface>();
            Set(w, "size", new Vector2(size, size));
            var so = new SerializedObject(w);
            so.FindProperty("cells").intValue = cells;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Mesh smooth;
        static Mesh Smooth => smooth != null ? smooth : smooth = LowPolyMeshes.Sphere("Smooth", 0.5f, 14, 8);

        // ---------------------------------------------------------------- Pool

        // The block a location stands on: soil and rock under the ground line. Where water reaches the edge,
        // clear walls hold it in like a tank so the block reads as a cut-out slice of the place.
        static void BlockBase(Transform site, Vector2 centerXZ, Vector2 size, float top, Material water)
        {
            var soil = Mat("Block_Soil", "#5A4330");
            var earth = Mat("Block_Earth", "#7A5A3E");
            var rock = Mat("Block_Rock", "#5E5A55");
            const float bottom = -16f, soilDepth = 1.4f, earthBottom = -9f;
            float cx = centerXZ.x, cz = centerXZ.y;

            Box("Base_Soil", site, new Vector3(cx, top - soilDepth * 0.5f, cz), new Vector3(size.x, soilDepth, size.y), soil);
            Box("Base_Earth", site, new Vector3(cx, (top - soilDepth + earthBottom) * 0.5f, cz), new Vector3(size.x, top - soilDepth - earthBottom, size.y), earth);
            Box("Base_Rock", site, new Vector3(cx, (earthBottom + bottom) * 0.5f, cz), new Vector3(size.x, earthBottom - bottom, size.y), rock);

            if (water == null) return;
            float h = -top, y = top * 0.5f;
            var walls = new[]
            {
                (new Vector3(cx - size.x * 0.5f, y, cz), new Vector3(0.06f, h, size.y)),
                (new Vector3(cx + size.x * 0.5f, y, cz), new Vector3(0.06f, h, size.y)),
                (new Vector3(cx, y, cz - size.y * 0.5f), new Vector3(size.x, h, 0.06f)),
                (new Vector3(cx, y, cz + size.y * 0.5f), new Vector3(size.x, h, 0.06f))
            };
            foreach (var (at, s) in walls)
                Box("WaterSide", site, at, s, water).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static LocationSite BuildPoolSite()
        {
            var site = new GameObject("Site_Pool").transform;
            BuildPool(site);
            var tower = BuildTower(site);
            BuildSurroundings(site);
            PoolLife(site);
            BlockBase(site, new Vector2(0f, PoolBlockZ), new Vector2(PoolBlockHalf * 2f, PoolBlockHalf * 2f), PoolBlockTop, null);
            site.position = SiteOffsets[0];

            return MakeSite(site, tower, new Look
            {
                sun = new Vector3(44f, 160f, 0f), sunColor = "#FFF0D8", sunIntensity = 1f,
                ambientSky = "#7FA6BF", ambientEquator = "#958E80", ambientGround = "#55636A",
                fog = "#CDE6F0", fogStart = 30f, fogEnd = 120f,
                skyTint = "#7FBFEA", skyGround = "#B4D3DE", exposure = 1f, atmosphere = 0.8f,
                mapCenter = new Vector3(0f, -2f, PoolBlockZ), mapDistance = 92f, mapDirection = new Vector3(-0.5f, -0.62f, -1f)
            });
        }

        static void PoolLife(Transform site)
        {
            var root = Child("Life", site);
            var pole = Mat("Rail", "#F7F7F2", smoothness: 0.4f);
            var stringMat = Mat("Bunting_String", "#3A3F4A");
            var flags = new[]
            {
                Mat("Flag_Coral", "#EE6A55"), Mat("Flag_Yellow", "#FFC743"),
                Mat("Flag_Teal", "#2BA39C"), Mat("Flag_White", "#F7F5EE")
            };
            var pennant = LowPolyMeshes.Pennant("Pennant", 0.34f, 0.44f);

            // Two strings of flags across the pool, sagging a little in the middle.
            int n = 0;
            foreach (float z in new[] { 6.5f, 11.5f })
            {
                foreach (float x in new[] { -5.7f, 5.7f })
                    Put(hex, "BuntingPole", root, new Vector3(x, DeckTop + 2.2f, z), new Vector3(0.08f, 4.4f, 0.08f), pole);

                float Sag(float x) => DeckTop + 4.3f - 0.45f * (1f - x * x / (5.7f * 5.7f));
                for (float x = -5.7f; x < 5.69f; x += 0.95f)
                {
                    float x2 = Mathf.Min(x + 0.95f, 5.7f);
                    var a = new Vector3(x, Sag(x), z);
                    var b = new Vector3(x2, Sag(x2), z);
                    var seg = Box("String", root, (a + b) * 0.5f, new Vector3((b - a).magnitude, 0.02f, 0.02f), stringMat);
                    seg.localRotation = Quaternion.FromToRotation(Vector3.right, b - a);
                }
                for (float x = -5.3f; x < 5.31f; x += 0.53f)
                {
                    var pivot = Mark("Flag", root, new Vector3(x, Sag(x) - 0.01f, z));
                    Put(pennant, "Cloth", pivot, Vector3.zero, Vector3.one, flags[n++ % flags.Length]);
                    AddSway(pivot, Vector3.right, 14f, 2.1f + (n % 3) * 0.3f, n * 0.7f);
                }
            }

            // Lifeguard on a tall chair at the far end, following the diver with his head.
            var chairWhite = Mat("Lifeguard_Chair", "#F4F2EC");
            var shirt = Mat("Lifeguard_Shirt", "#D9433A");
            var shorts = Mat("Lifeguard_Shorts", "#283B8F");
            var skin = Mat("Skin", "#EFBC98", smoothness: 0.3f);
            var cap = Mat("Lifeguard_Cap", "#F7F5EE");
            var shades = Mat("Goggles", "#1E2A4A", smoothness: 0.6f);

            var chair = Mark("LifeguardChair", root, new Vector3(3.2f, DeckTop, 16.4f));
            chair.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var p in new[] { new Vector3(-0.35f, 1f, -0.3f), new Vector3(0.35f, 1f, -0.3f), new Vector3(-0.35f, 1f, 0.3f), new Vector3(0.35f, 1f, 0.3f) })
                Box("Leg", chair, p, new Vector3(0.07f, 2f, 0.07f), chairWhite);
            Box("Seat", chair, new Vector3(0f, 2f, 0f), new Vector3(0.8f, 0.08f, 0.7f), chairWhite);
            Box("Back", chair, new Vector3(0f, 2.4f, -0.33f), new Vector3(0.8f, 0.7f, 0.06f), chairWhite);
            Box("Step", chair, new Vector3(0f, 1.15f, 0.42f), new Vector3(0.8f, 0.06f, 0.2f), chairWhite);
            Put(hex, "ShadePole", chair, new Vector3(0.3f, 2.9f, -0.3f), new Vector3(0.05f, 1.8f, 0.05f), chairWhite);
            Put(cone, "Shade", chair, new Vector3(0.3f, 3.8f, -0.3f), new Vector3(1.8f, 0.4f, 1.8f), shirt);

            var torso = LowPolyMeshes.Capsule("Npc_Torso", 0.2f, 0.17f, 0.36f);
            var limb = LowPolyMeshes.Capsule("Npc_Limb", 0.065f, 0.05f, 0.38f);
            var head = LowPolyMeshes.Sphere("Npc_Head", 0.14f);
            var capMesh = LowPolyMeshes.Sphere("Npc_Cap", 0.15f, 14, 6, 0f, 95f);

            Put(torso, "Torso", chair, new Vector3(0f, 2.62f, -0.05f), new Vector3(1f, 1f, 0.7f), shirt);
            Put(Smooth, "Hips", chair, new Vector3(0f, 2.14f, -0.02f), new Vector3(0.38f, 0.2f, 0.3f), shorts);
            var headPivot = Mark("Head", chair, new Vector3(0f, 2.88f, -0.03f));
            Put(head, "Face", headPivot, Vector3.zero, Vector3.one, skin);
            Put(capMesh, "Cap", headPivot, new Vector3(0f, 0.02f, 0f), Vector3.one, cap);
            Box("Shades", headPivot, new Vector3(0f, 0.01f, 0.13f), new Vector3(0.22f, 0.05f, 0.04f), shades);
            var tracker = headPivot.gameObject.AddComponent<HeadTracker>();
            needsDiver.Add((tracker, "target", true));

            foreach (float x in new[] { -0.11f, 0.11f })
            {
                var thigh = Put(limb, "Thigh", chair, new Vector3(x, 2.12f, 0.02f), Vector3.one, skin);
                thigh.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                Put(limb, "Shin", chair, new Vector3(x, 2.1f, 0.42f), Vector3.one, skin);
                var arm = Put(limb, "Arm", chair, new Vector3(x * 2.1f, 2.62f, -0.02f), new Vector3(0.85f, 0.95f, 0.85f), skin);
                arm.localRotation = Quaternion.Euler(-20f, 0f, x > 0 ? 8f : -8f);
            }

            // A rubber duck ring drifting about the pool; splashes push it away.
            var yellow = Mat("RubberDuck", "#FFD23F", smoothness: 0.6f);
            var orange = Mat("Beak", "#F28C28");
            var black = Mat("Eye", "#1B1F2A", smoothness: 0.8f);
            var duck = Mark("RubberDuck", root, new Vector3(2.4f, 0f, 9.5f));
            Put(LowPolyMeshes.Torus("Ring_Tube", 0.42f, 0.16f), "Ring", duck, Vector3.zero, Vector3.one, yellow);
            Put(Smooth, "Head", duck, new Vector3(0f, 0.3f, 0.42f), Vector3.one * 0.3f, yellow);
            Box("Beak", duck, new Vector3(0f, 0.28f, 0.6f), new Vector3(0.14f, 0.06f, 0.12f), orange);
            foreach (float x in new[] { -0.08f, 0.08f })
                Put(Smooth, "Eye", duck, new Vector3(x, 0.36f, 0.54f), Vector3.one * 0.05f, black);
            var drifter = duck.gameObject.AddComponent<Drifter>();
            Set(drifter, "minXZ", new Vector2(PoolX.x + 0.7f, PoolZ.x + 0.7f));
            Set(drifter, "maxXZ", new Vector2(PoolX.y - 0.7f, PoolZ.y - 0.7f));
            needsDiver.Add((drifter, "diver", false));
        }

        // ---------------------------------------------------------------- Lake

        static readonly (float height, float edgeZ)[] LakePlatforms = { (5f, -0.3f), (8f, 0.3f), (11f, 0.9f) };
        const float LakeBlockSize = 100f, LakeBlockZ = 12f;

        static LocationSite BuildLakeSite()
        {
            var site = new GameObject("Site_Lake").transform;
            var water = Mat("LakeWater", "#2C7C84", transparent: true, alpha: 0.86f, smoothness: 0.7f);
            var bed = Mat("LakeBed", "#2E4D40");
            var woodA = Mat("WoodA", "#9C6B43");
            var woodB = Mat("WoodB", "#AF7E50");
            var woodDark = Mat("WoodDark", "#6E4A2F");
            var ropeMat = Mat("RopeMat", "#3A3329");
            var paint = Mat("PaintWhite", "#F2EFE6");

            // The lake block: 100 x 100 m, water up to the walls on the open sides.
            WaterSurface("LakeWater", site, new Vector3(0f, 0f, LakeBlockZ), LakeBlockSize, 30, water);
            BlockBase(site, new Vector2(0f, LakeBlockZ), new Vector2(LakeBlockSize, LakeBlockSize), -4.5f, water);
            Box("LakeBed", site, new Vector3(0f, -4.5f, LakeBlockZ), new Vector3(LakeBlockSize, 0.1f, LakeBlockSize), bed);

            var solids = new List<Bounds>();
            const float pierTop = 0.7f;

            // Pier from the shore out to the tower.
            var pier = Child("Pier", site);
            int b = 0;
            for (float z = -30f; z < -1.2f; z += 0.32f)
                Box("Board", pier, new Vector3(0f, pierTop - 0.03f, z + 0.14f), new Vector3(2.8f, 0.06f, 0.28f), b++ % 3 == 0 ? woodB : woodA);
            foreach (float x in new[] { -1.1f, 1.1f })
                Box("Beam", pier, new Vector3(x, pierTop - 0.15f, -15.6f), new Vector3(0.14f, 0.18f, 28.8f), woodDark);
            for (float z = -29f; z < -1f; z += 3f)
                foreach (float x in new[] { -1.3f, 1.3f })
                    Put(hex, "Pile", pier, new Vector3(x, -1.6f, z), new Vector3(0.22f, 4.6f, 0.22f), woodDark);
            solids.Add(new Bounds(new Vector3(0f, pierTop - 0.05f, -15.6f), new Vector3(2.8f, 0.1f, 28.8f)));

            var ringRed = Mat("LifeRing", "#E04A3A", smoothness: 0.4f);
            var ring = Put(LowPolyMeshes.Torus("LifeRing", 0.26f, 0.07f), "LifeRing", pier, new Vector3(-1.46f, 0.25f, -8f), Vector3.one, ringRed);
            ring.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (float a in new[] { 0f, 90f, 180f, 270f })
            {
                var band = Box("Band", ring, Vector3.zero, new Vector3(0.6f, 0.17f, 0.06f), paint);
                band.localRotation = Quaternion.Euler(0f, a, 0f);
                band.localScale = new Vector3(0.06f, 0.17f, 0.17f);
                band.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0.26f, 0f, 0f);
            }

            // A rowing boat tied to the pier.
            var boat = Mark("Boat", site, new Vector3(2.9f, 0.05f, -13f));
            boat.localRotation = Quaternion.Euler(0f, 4f, 0f);
            Box("Hull", boat, new Vector3(0f, 0.1f, 0f), new Vector3(1.1f, 0.4f, 2.8f), woodDark);
            Box("Inside", boat, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.32f, 2.5f), woodB);
            Box("Bench", boat, new Vector3(0f, 0.32f, 0.2f), new Vector3(0.9f, 0.05f, 0.3f), woodA);
            var bow = Box("Bow", boat, new Vector3(0f, 0.12f, 1.5f), new Vector3(0.78f, 0.36f, 0.78f), woodDark);
            bow.localRotation = Quaternion.Euler(0f, 45f, 0f);
            AddSway(boat, Vector3.forward, 2.5f, 0.9f, 0.4f);

            // Wooden tower standing on the end of the pier.
            var towerRoot = Child("Tower", site);
            const float back = -4.2f, front = -1f, top = 11.6f;
            foreach (float x in new[] { -1.5f, 1.5f })
                foreach (float z in new[] { back, front })
                {
                    var c = new Vector3(x, (pierTop + top) * 0.5f, z);
                    var s = new Vector3(0.24f, top - pierTop, 0.24f);
                    Box("Post", towerRoot, c, s, woodDark);
                    solids.Add(new Bounds(c, s));
                }
            // Cross bracing on both sides.
            float span = front - back;
            for (float y = pierTop; y < top - 2.6f; y += 2.7f)
                foreach (float x in new[] { -1.53f, 1.53f })
                    foreach (int dir in new[] { 1, -1 })
                    {
                        float len = Mathf.Sqrt(span * span + 2.7f * 2.7f);
                        var brace = Box("Brace", towerRoot, new Vector3(x, y + 1.35f, (back + front) * 0.5f), new Vector3(0.08f, 0.12f, len), woodA);
                        brace.localRotation = Quaternion.Euler(-dir * Mathf.Atan2(2.7f, span) * Mathf.Rad2Deg, 0f, 0f);
                    }

            var edges = new Transform[LakePlatforms.Length];
            for (int i = 0; i < LakePlatforms.Length; i++)
            {
                var (h, edgeZ) = LakePlatforms[i];
                var c = new Vector3(0f, h - 0.11f, (edgeZ + back) * 0.5f);
                var s = new Vector3(3f, 0.22f, edgeZ - back);
                Box("Deck_" + h, towerRoot, c, s, woodA);
                solids.Add(new Bounds(c, s));
                Box("EdgePaint", towerRoot, new Vector3(0f, h + 0.005f, edgeZ - 0.08f), new Vector3(3f, 0.01f, 0.16f), paint);

                float railEnd = edgeZ - 1.5f;
                foreach (float x in new[] { -1.45f, 1.45f })
                {
                    Box("Rope", towerRoot, new Vector3(x, h + 0.95f, (railEnd + back) * 0.5f), new Vector3(0.03f, 0.03f, railEnd - back), ropeMat);
                    Put(hex, "RailPost", towerRoot, new Vector3(x, h + 0.5f, railEnd), new Vector3(0.08f, 1f, 0.08f), woodDark);
                }
                edges[i] = Mark("Edge_" + h, towerRoot, new Vector3(0f, h, edgeZ));
            }
            foreach (float x in new[] { -0.3f, 0.3f })
                Box("LadderSide", towerRoot, new Vector3(x, (pierTop + top) * 0.5f, back - 0.14f), new Vector3(0.06f, top - pierTop, 0.06f), woodDark);
            for (float y = pierTop + 0.35f; y < top; y += 0.36f)
                Box("Rung", towerRoot, new Vector3(0f, y, back - 0.14f), new Vector3(0.6f, 0.04f, 0.05f), woodA);
            var tower = MakeTower(towerRoot, edges, solids);

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
                mapCenter = new Vector3(0f, -2f, LakeBlockZ), mapDistance = 138f, mapDirection = new Vector3(-0.5f, -0.62f, -1f)
            });
        }

        static void LakeShore(Transform site)
        {
            var root = Child("Shore", site);
            var grass = Mat("LakeGrass", "#5E9A4E");
            var grassDark = Mat("LakeGrassDark", "#4C8743");
            var pineA = Mat("PineA", "#2F6B45");
            var pineB = Mat("PineB", "#3C7D52");
            var bark = Mat("Trunk", "#8A5B3D");
            var mountain = Mat("Mountain", "#8796A8");
            var snow = Mat("Snow", "#F3F6F8");
            var reed = Mat("Reed", "#8A9A4F");
            var pad = Mat("LilyPad", "#4E8F45");
            var lily = Mat("Lily", "#F2B8C6");

            // Banks: flat-sided where they meet the block edge, a few soft mounds on top.
            const float bankTop = 1.2f, bed = -4.5f;
            float half = LakeBlockSize * 0.5f, z0 = LakeBlockZ - half, z1 = LakeBlockZ + half;
            (Vector3 at, Vector3 size)[] banks =
            {
                (new Vector3(-half + 9f, 0f, LakeBlockZ), new Vector3(18f, 0f, LakeBlockSize)),
                (new Vector3(half - 9f, 0f, LakeBlockZ), new Vector3(18f, 0f, LakeBlockSize)),
                (new Vector3(0f, 0f, z0 + 8f), new Vector3(LakeBlockSize - 36f, 0f, 16f)),
                (new Vector3(0f, 0f, z1 - 7f), new Vector3(LakeBlockSize - 36f, 0f, 14f))
            };
            int i = 0;
            foreach (var (at, size) in banks)
            {
                Box("Bank", root, new Vector3(at.x, (bankTop + bed) * 0.5f, at.z), new Vector3(size.x, bankTop - bed, size.z), i++ % 2 == 0 ? grass : grassDark);
                Put(rough, "Mound", root, new Vector3(at.x, bankTop - 0.6f, at.z), new Vector3(size.x * 0.7f, 3.2f, size.z * 0.7f), grassDark);
            }

            var rng = new System.Random(11);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int t = 0; t < 46; t++)
            {
                Vector3 p = t switch
                {
                    < 14 => new Vector3(R(-half + 2f, -half + 15f), bankTop, R(z0 + 2f, z1 - 2f)),
                    < 28 => new Vector3(R(half - 15f, half - 2f), bankTop, R(z0 + 2f, z1 - 2f)),
                    < 38 => new Vector3(R(-30f, 30f), bankTop, R(z0 + 2f, z0 + 12f)),
                    _ => new Vector3(R(-30f, 30f), bankTop, R(z1 - 12f, z1 - 2f))
                };
                // Keep the shore end of the pier clear.
                if (Mathf.Abs(p.x) < 4f && p.z < z0 + 16f) p.x += 8f * Mathf.Sign(p.x + 0.01f);
                Pine(root, p, R(0.8f, 1.4f), rng.Next(2) == 0 ? pineA : pineB, bark);
            }

            // Two grey crags on the far bank instead of a distant range.
            var crag = LowPolyMeshes.Icosphere("Shard", 0, 0.5f, 0.28f, 13);
            Put(crag, "Crag", root, new Vector3(-34f, bankTop + 3f, z1 - 7f), new Vector3(14f, 11f, 10f), mountain);
            Put(crag, "Crag", root, new Vector3(36f, bankTop + 2.5f, z1 - 8f), new Vector3(11f, 9f, 9f), mountain);
            Put(crag, "SnowCap", root, new Vector3(-34f, bankTop + 7.6f, z1 - 7f), new Vector3(5f, 2.4f, 4f), snow);

            // Reeds and lily pads near the shore end of the pier and along both banks.
            Vector3[] clumps = { new(-3.5f, 0f, -24f), new(4f, 0f, -21f), new(-27f, 0f, 6f), new(29f, 0f, 14f), new(-26f, 0f, 30f), new(28f, 0f, 38f) };
            foreach (var c in clumps)
            {
                for (int k = 0; k < 9; k++)
                {
                    var p = c + new Vector3(R(-1.4f, 1.4f), 0f, R(-1.4f, 1.4f));
                    var stem = Mark("Reed", root, p);
                    float h = R(0.9f, 1.6f);
                    Put(cone, "Stem", stem, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.07f, h, 0.07f), reed);
                    AddSway(stem, Vector3.forward, 5f, R(1f, 1.6f), R(0f, 6f));
                }
                for (int k = 0; k < 5; k++)
                {
                    var p = c + new Vector3(R(-3f, 3f), 0.01f, R(-3f, 3f));
                    Put(hex, "Pad", root, p, new Vector3(0.7f, 0.02f, 0.7f), pad);
                    if (k % 2 == 0) Put(Smooth, "Lily", root, p + Vector3.up * 0.06f, new Vector3(0.14f, 0.1f, 0.14f), lily);
                }
            }
        }

        static void Pine(Transform parent, Vector3 at, float s, Material needles, Material bark)
        {
            var t = Mark("Pine", parent, at);
            t.localScale = Vector3.one * s;
            Put(hex, "Trunk", t, new Vector3(0f, 1f, 0f), new Vector3(0.35f, 2f, 0.35f), bark);
            Put(cone, "Low", t, new Vector3(0f, 2.6f, 0f), new Vector3(3.2f, 2.8f, 3.2f), needles);
            Put(cone, "Mid", t, new Vector3(0f, 4.1f, 0f), new Vector3(2.4f, 2.4f, 2.4f), needles);
            Put(cone, "Top", t, new Vector3(0f, 5.4f, 0f), new Vector3(1.5f, 2f, 1.5f), needles);
        }

        static void LakeLife(Transform site)
        {
            var root = Child("Life", site);
            var brown = Mat("Duck_Brown", "#8A6A45");
            var green = Mat("Duck_Green", "#2F5D3A", smoothness: 0.5f);
            var chick = Mat("Duck_Chick", "#F2D35B");
            var orange = Mat("Beak", "#F28C28");
            var black = Mat("Eye", "#1B1F2A", smoothness: 0.8f);

            // Mother duck and four ducklings paddling an oval behind her.
            for (int i = 0; i < 5; i++)
            {
                bool young = i > 0;
                float s = young ? 0.5f : 1f;
                var d = Child(young ? "Duckling" : "Duck", root);
                Put(Smooth, "Body", d, new Vector3(0f, 0.08f * s, 0f), new Vector3(0.42f, 0.3f, 0.62f) * s, young ? chick : brown);
                var tail = Put(cone, "Tail", d, new Vector3(0f, 0.16f * s, -0.32f * s), new Vector3(0.18f, 0.2f, 0.18f) * s, young ? chick : brown);
                tail.localRotation = Quaternion.Euler(-120f, 0f, 0f);
                var head = Mark("Head", d, new Vector3(0f, 0.28f * s, 0.24f * s));
                Put(Smooth, "Skull", head, Vector3.zero, Vector3.one * 0.22f * s, young ? chick : green);
                Box("Beak", head, new Vector3(0f, -0.02f * s, 0.13f * s), new Vector3(0.08f, 0.035f, 0.1f) * s, orange);
                foreach (float x in new[] { -0.06f, 0.06f })
                    Put(Smooth, "Eye", head, new Vector3(x * s, 0.03f * s, 0.07f * s), Vector3.one * 0.035f * s, black);

                var swim = d.gameObject.AddComponent<PathSwimmer>();
                Set(swim, "center", new Vector3(10f, 0f, 16f));
                Set(swim, "radii", new Vector2(6f, 3.5f));
                Set(swim, "speed", 0.11f);
                Set(swim, "phase", -i * 0.14f);
                Link(swim, "head", head);
            }

            // A fish that jumps now and then.
            var silver = Mat("Fish", "#B9C4C8", smoothness: 0.7f);
            var fish = Child("Fish", root);
            var body = Put(LowPolyMeshes.Capsule("Fish_Body", 0.07f, 0.04f, 0.3f), "Body", fish, new Vector3(0f, 0f, 0.15f), Vector3.one, silver);
            body.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var fin = Put(cone, "Tail", fish, new Vector3(0f, 0f, -0.22f), new Vector3(0.02f, 0.14f, 0.12f), silver);
            fin.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var jumper = fish.gameObject.AddComponent<FishJumper>();
            Set(jumper, "areaCenter", new Vector3(2f, 0f, 18f));
            Set(jumper, "areaSize", new Vector2(26f, 18f));
        }

        // ---------------------------------------------------------------- Cliffs

        static readonly (float height, float x)[] Ledges = { (9f, -4f), (13f, 3.5f), (17f, -0.5f) };
        const float LedgeEdge = 0.2f, CliffFace = -2.2f;
        const float CliffBlockSize = 90f, CliffBlockZ = 15f;

        static LocationSite BuildCliffSite()
        {
            var site = new GameObject("Site_Cliffs").transform;
            var sea = Mat("Sea", "#169FB2", transparent: true, alpha: 0.84f, smoothness: 0.75f);
            var sand = Mat("Sand", "#E3CB98");
            var wetSand = Mat("WetSand", "#C9B07D");
            var rockMats = new[] { Mat("RockA", "#C48A56"), Mat("RockB", "#A9713F"), Mat("RockC", "#8E5E36") };
            var grassTop = Mat("CliffGrass", "#7FAE5A");
            var chalk = Mat("PaintWhite", "#F2EFE6");

            // The cliff block: 90 x 90 m; open sea runs right up to its glass walls.
            WaterSurface("Sea", site, new Vector3(0f, 0f, CliffBlockZ), CliffBlockSize, 30, sea);
            BlockBase(site, new Vector2(0f, CliffBlockZ), new Vector2(CliffBlockSize, CliffBlockSize), -6f, sea);
            Box("Seabed", site, new Vector3(0f, -6f, CliffBlockZ), new Vector3(CliffBlockSize, 0.1f, CliffBlockSize), sand);

            var solids = new List<Bounds>();
            var cliffRoot = Child("Cliff", site);

            // The wall: one solid box for physics. For looks, one faceted surface bent into a headland,
            // its rows of facets coloured as sandstone strata, with a grass cap along the ragged rim.
            var body = new Bounds(new Vector3(0f, 8f, (CliffFace - 22f) * 0.5f), new Vector3(32f, 20f, 22f + CliffFace));
            solids.Add(body);
            var rng = new System.Random(5);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var shard = LowPolyMeshes.Icosphere("Shard", 0, 0.5f, 0.28f, 13);

            const float halfWidth = 17f, cliffTop = 19.2f, capBack = -22f;
            float FaceZ(float x) => CliffFace - 0.6f - 0.032f * x * x;
            var wallMesh = LowPolyMeshes.CliffWall("CliffWall", halfWidth * 2f, -6f, cliffTop, 14, 8, 3, capBack, FaceZ, 21);
            var wall = Put(wallMesh, "Wall", cliffRoot, Vector3.zero, Vector3.one, rockMats[0]);
            wall.GetComponent<MeshRenderer>().sharedMaterials = new[] { rockMats[0], rockMats[1], rockMats[2], grassTop };

            // Close the open ends behind the curve.
            foreach (float side in new[] { -1f, 1f })
            {
                float zFront = FaceZ(halfWidth) - 0.3f;
                Box("End", cliffRoot, new Vector3(side * (halfWidth - 0.4f), (cliffTop - 6f) * 0.5f, (zFront + capBack) * 0.5f),
                    new Vector3(0.8f, cliffTop + 6f, zFront - capBack), rockMats[2]);
            }

            // A few fallen blocks at the waterline and loose stones on the rim.
            for (int k = 0; k < 7; k++)
            {
                float x = R(-13f, 13f);
                Put(shard, "Fallen", cliffRoot, new Vector3(x, R(-0.5f, 0.3f), FaceZ(x) + R(0.4f, 1.4f)),
                    new Vector3(R(1.6f, 2.8f), R(1.2f, 2f), R(1.6f, 2.4f)), rockMats[rng.Next(rockMats.Length)]);
            }
            for (int k = 0; k < 5; k++)
            {
                float x = R(-12f, 12f);
                Put(shard, "RimRock", cliffRoot, new Vector3(x, cliffTop + 0.2f, FaceZ(x) - R(2f, 5f)),
                    new Vector3(R(1f, 2f), R(0.8f, 1.3f), R(1f, 2f)), rockMats[1]);
            }

            var edges = new Transform[Ledges.Length];
            for (int i = 0; i < Ledges.Length; i++)
            {
                var (h, x) = Ledges[i];
                float backZ = CliffFace - 2.2f;
                var c = new Vector3(x, h - 0.3f, (LedgeEdge + backZ) * 0.5f);
                var s = new Vector3(2.4f, 0.6f, LedgeEdge - backZ);
                Box("Ledge_" + h, cliffRoot, c, s, rockMats[1]);
                solids.Add(new Bounds(c, s));
                Box("Chalk", cliffRoot, new Vector3(x, h + 0.005f, LedgeEdge - 0.1f), new Vector3(2.2f, 0.01f, 0.12f), chalk);
                Put(shard, "Under", cliffRoot, new Vector3(x, h - 1.4f, CliffFace - 0.5f), new Vector3(2.6f, 2.2f, 2.4f), rockMats[0]);
                edges[i] = Mark("Edge_" + h, cliffRoot, new Vector3(x, h, LedgeEdge));
            }
            var tower = MakeTower(cliffRoot, edges, solids);

            // Beaches on both sides so either camera side has something behind the dive.
            foreach (float side in new[] { 1f, -1f })
            {
                // Beaches on both sides of the cliff, in the back half of the block; the front is open sea.
                float inner = 17.5f, outer = CliffBlockSize * 0.5f;
                float bz0 = CliffBlockZ - outer, bz1 = 12f;
                Box("Beach", site, new Vector3((inner + outer) * 0.5f * side, (0.3f - 6f) * 0.5f, (bz0 + bz1) * 0.5f),
                    new Vector3(outer - inner, 6.3f, bz1 - bz0), sand);
                Box("WetSand", site, new Vector3((inner + outer) * 0.5f * side, 0.01f, bz1 + 0.6f), new Vector3(outer - inner, 0.6f, 1.6f), wetSand);
            }
            // Behind the cliff, a grassy top fills the rest of the block.
            float back0 = CliffBlockZ - CliffBlockSize * 0.5f;
            Box("Hinterland", cliffRoot, new Vector3(0f, (19.2f - 6f) * 0.5f, (back0 - 22f) * 0.5f),
                new Vector3(35f, 25.2f, -22f - back0), rockMats[2]);
            Box("HinterlandGrass", cliffRoot, new Vector3(0f, 19.25f, (back0 - 22f) * 0.5f), new Vector3(35f, 0.1f, -22f - back0), grassTop);
            Vector3[] palms = { new(21f, 0.3f, -4f), new(25f, 0.3f, 6f), new(30f, 0.3f, -14f), new(36f, 0.3f, 0f), new(23f, 0.3f, -22f),
                                new(-21f, 0.3f, 1f), new(-26f, 0.3f, -9f), new(-33f, 0.3f, 4f), new(-8f, 19.2f, -12f), new(7f, 19.2f, -17f) };
            foreach (var p in palms) Palm(site, p, R(0.9f, 1.25f), R(0f, 360f));

            Vector3[] stones = { new(11f, -0.6f, 22f), new(-12f, -0.7f, 28f), new(15f, -0.8f, 36f), new(-16f, -0.5f, 14f) };
            foreach (var p in stones) Put(rough, "SeaRock", site, p, new Vector3(R(2.5f, 4f), R(1.8f, 2.6f), R(2.5f, 4f)), rockMats[2]);

            CliffLife(site);
            site.position = SiteOffsets[2];

            return MakeSite(site, tower, new Look
            {
                sun = new Vector3(38f, 205f, 0f), sunColor = "#FFD9A8", sunIntensity = 1.1f,
                ambientSky = "#9DB7CC", ambientEquator = "#C9A98A", ambientGround = "#6E5A4A",
                fog = "#EBD9C4", fogStart = 50f, fogEnd = 180f,
                skyTint = "#7FB4E2", skyGround = "#E8D6BE", exposure = 1.05f, atmosphere = 0.7f,
                swell = 0.16f, swellPeriod = 4.6f, swellLength = 18f,
                mapCenter = new Vector3(0f, 2f, CliffBlockZ), mapDistance = 130f, mapDirection = new Vector3(-0.5f, -0.6f, -1f)
            });
        }

        static void Palm(Transform parent, Vector3 at, float s, float yaw)
        {
            var trunkMat = Mat("PalmTrunk", "#9B7448");
            var leafMat = Mat("PalmLeaf", "#4F9A4A");
            var nut = Mat("Coconut", "#6B4A2B");

            var t = Mark("Palm", parent, at);
            t.localScale = Vector3.one * s;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // A gently curving trunk: each segment tilts a bit more than the last.
            var tip = Vector3.zero;
            var dir = Vector3.up;
            for (int i = 0; i < 6; i++)
            {
                dir = Quaternion.Euler(4f, 0f, 0f) * dir;
                var seg = Put(hex, "Trunk", t, tip + dir * 0.45f, new Vector3(0.28f - i * 0.02f, 0.9f, 0.28f - i * 0.02f), trunkMat);
                seg.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                tip += dir * 0.9f;
            }
            var crown = Mark("Crown", t, tip);
            for (int i = 0; i < 7; i++)
            {
                var leaf = Mark("Leaf", crown, Vector3.zero);
                leaf.localRotation = Quaternion.Euler(0f, i * 360f / 7f, 0f) * Quaternion.Euler(70f, 0f, 0f);
                Put(cone, "Blade", leaf, new Vector3(0f, 0.9f, 0f), new Vector3(0.5f, 1.8f, 0.08f), leafMat);
            }
            for (int i = 0; i < 3; i++)
                Put(Smooth, "Coconut", crown, Quaternion.Euler(0f, i * 120f, 0f) * new Vector3(0.14f, -0.12f, 0f), Vector3.one * 0.18f, nut);
            AddSway(crown, Vector3.forward, 4f, 0.8f, yaw * 0.1f);
        }

        static void CliffLife(Transform site)
        {
            var root = Child("Life", site);
            var shell = Mat("Crab", "#D9563A", smoothness: 0.4f);
            var shellDark = Mat("CrabDark", "#9E3626");
            var black = Mat("Eye", "#1B1F2A", smoothness: 0.8f);

            (Vector3 at, float yaw)[] crabs =
            {
                // Facing the sea, scuttling sideways along the waterline.
                (new Vector3(22f, 0.3f, 9.5f), 0f), (new Vector3(29f, 0.3f, 8.5f), 0f), (new Vector3(37f, 0.3f, 10f), 0f),
                (new Vector3(-24f, 0.3f, 9.5f), 0f), (new Vector3(-32f, 0.3f, 8.5f), 0f)
            };
            foreach (var (at, yaw) in crabs) Crab(root, at, yaw, shell, shellDark, black);

            var white = Mat("Gull_White", "#F4F4F0");
            var grey = Mat("Gull_Grey", "#9AA3AA");
            var tipMat = Mat("Gull_Tip", "#3B4048");
            var beak = Mat("Gull_Beak", "#F2B233");
            Gull(root, new Vector3(0f, 0f, 8f), 16f, 20f, 0.22f, 0f, white, grey, tipMat, beak);
            Gull(root, new Vector3(-3f, 0f, 12f), 22f, 24f, -0.17f, 2f, white, grey, tipMat, beak);
            Gull(root, new Vector3(5f, 0f, 4f), 12f, 17f, 0.28f, 4f, white, grey, tipMat, beak);
        }

        // Cartoon-sized so they still read from the camera far above.
        static void Crab(Transform parent, Vector3 at, float yaw, Material shell, Material dark, Material eye)
        {
            var c = Mark("Crab", parent, at);
            c.localRotation = Quaternion.Euler(0f, yaw, 0f);
            c.localScale = Vector3.one * 1.8f;
            Put(Smooth, "Body", c, new Vector3(0f, 0.1f, 0f), new Vector3(0.36f, 0.14f, 0.26f), shell);
            foreach (float x in new[] { -0.05f, 0.05f })
            {
                Put(hex, "Stalk", c, new Vector3(x, 0.19f, 0.08f), new Vector3(0.02f, 0.08f, 0.02f), dark);
                Put(Smooth, "Eye", c, new Vector3(x, 0.24f, 0.08f), Vector3.one * 0.045f, eye);
            }

            var claws = new List<Transform>();
            foreach (float x in new[] { -0.17f, 0.17f })
            {
                var claw = Mark("Claw", c, new Vector3(x, 0.12f, 0.12f));
                Put(Smooth, "Pincer", claw, new Vector3(0f, 0f, 0.04f), new Vector3(0.12f, 0.08f, 0.11f), shell);
                Box("Tip", claw, new Vector3(0f, 0.035f, 0.09f), new Vector3(0.03f, 0.05f, 0.08f), dark);
                claws.Add(claw);
            }
            var legs = new List<Transform>();
            foreach (float side in new[] { -1f, 1f })
                foreach (float z in new[] { -0.08f, 0f, 0.08f })
                {
                    var leg = Mark("Leg", c, new Vector3(side * 0.15f, 0.08f, z));
                    var bone = Box("Bone", leg, new Vector3(side * 0.08f, -0.04f, 0f), new Vector3(0.16f, 0.025f, 0.025f), dark);
                    bone.localRotation = Quaternion.Euler(0f, 0f, side * -30f);
                    legs.Add(leg);
                }

            var s = c.gameObject.AddComponent<Scuttler>();
            LinkArray(s, "legs", legs.ToArray());
            LinkArray(s, "claws", claws.ToArray());
        }

        static void Gull(Transform parent, Vector3 center, float radius, float height, float speed, float phase,
                         Material white, Material grey, Material tip, Material beak)
        {
            var g = Child("Seagull", parent);
            var body = Put(LowPolyMeshes.Capsule("Gull_Body", 0.13f, 0.07f, 0.45f), "Body", g, new Vector3(0f, 0f, 0.25f), new Vector3(2f, 2f, 2f), white);
            body.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Put(Smooth, "Head", g, new Vector3(0f, 0.08f, 0.58f), Vector3.one * 0.26f, white);
            Box("Beak", g, new Vector3(0f, 0.06f, 0.78f), new Vector3(0.06f, 0.05f, 0.16f), beak);
            Box("Tail", g, new Vector3(0f, 0f, -0.5f), new Vector3(0.24f, 0.03f, 0.26f), grey);

            Transform Wing(string n, float side)
            {
                var w = Mark(n, g, new Vector3(side * 0.12f, 0.05f, 0.12f));
                Box("Feathers", w, new Vector3(side * 0.6f, 0f, 0f), new Vector3(1.2f, 0.03f, 0.42f), grey);
                Box("Tip", w, new Vector3(side * 1.12f, 0f, -0.04f), new Vector3(0.36f, 0.035f, 0.34f), tip);
                return w;
            }
            var left = Wing("Wing_L", -1f);
            var right = Wing("Wing_R", 1f);

            var c = g.gameObject.AddComponent<Circler>();
            Set(c, "center", center);
            Set(c, "radius", radius);
            Set(c, "height", height);
            Set(c, "speed", speed);
            Set(c, "phase", phase);
            Link(c, "wingL", left);
            Link(c, "wingR", right);
        }

        // ---------------------------------------------------------------- Title screen

        const string TextureDir = "Assets/_Project/Textures";

        // Saves a generated texture as a PNG asset with the given wrap mode.
        static Texture2D SaveTexture(string name, int w, int h, System.Func<int, int, Color> pixel, TextureWrapMode wrap)
        {
            System.IO.Directory.CreateDirectory(TextureDir);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, pixel(x, y));
            string path = $"{TextureDir}/{name}.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static (RectTransform panel, Button play, Text medals) BuildTitle(UiKit ui)
        {
            // Sky from deep blue at the top to a pale haze at the horizon.
            var top = Hex("#5DB3E6");
            var horizon = Hex("#CDEFF8");
            var skyTex = SaveTexture("TitleSky", 4, 256, (x, y) => Color.Lerp(horizon, top, Mathf.Pow(y / 255f, 0.8f)), TextureWrapMode.Clamp);

            // Wave band: solid below a crest built from two sines that tile across the width.
            var waveTex = SaveTexture("TitleWave", 512, 128, (x, y) =>
            {
                float u = x / 512f;
                float crest = 98f + Mathf.Sin(u * Mathf.PI * 4f) * 9f + Mathf.Sin(u * Mathf.PI * 10f + 1f) * 3.5f;
                float a = Mathf.Clamp01(crest - y + 0.5f);
                return new Color(1f, 1f, 1f, a);
            }, TextureWrapMode.Repeat);

            var softTex = SaveTexture("SoftDot", 128, 128, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 64f;
                float a = Mathf.Clamp01(1f - d);
                return new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
            }, TextureWrapMode.Clamp);

            var center = new Vector2(0.5f, 0.5f);
            var panel = ui.Stretch("TitleScreen", ui.canvas);

            RawImage Raw(string n, Transform parent, Texture tex, Color c)
            {
                var img = ui.Stretch(n, parent).gameObject.AddComponent<RawImage>();
                img.texture = tex;
                img.color = c;
                img.raycastTarget = false;
                return img;
            }
            RawImage Blob(string n, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color c)
            {
                var rt = ui.Rect(n, parent, anchor, pos, size);
                var img = rt.gameObject.AddComponent<RawImage>();
                img.texture = softTex;
                img.color = c;
                img.raycastTarget = false;
                return img;
            }
            Image Block(string n, Transform parent, Vector2 pos, Vector2 size, string color)
            {
                var rt = ui.Rect(n, parent, Vector2.zero, pos, size);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = pos;
                var img = rt.gameObject.AddComponent<Image>();
                img.color = Hex(color);
                img.raycastTarget = false;
                return img;
            }

            // The sky catches taps too, so nothing behind the title reacts.
            var sky = Raw("Sky", panel, skyTex, Color.white);
            sky.raycastTarget = true;

            var sunAnchor = new Vector2(0.8f, 0.78f);
            Blob("SunGlow", panel, sunAnchor, Vector2.zero, new Vector2(460f, 460f), new Color(1f, 0.95f, 0.7f, 0.55f));
            var sun = ui.Rect("Sun", panel, sunAnchor, Vector2.zero, new Vector2(150f, 150f)).gameObject.AddComponent<Image>();
            sun.sprite = ui.knob;
            sun.color = Hex("#FFE07A");
            sun.raycastTarget = false;

            // Clouds: a few soft puffs grouped together, drifting slowly to the right.
            var clouds = new List<RectTransform>();
            Vector2[] cloudSpots = { new(-430f, -120f), new(120f, -80f), new(470f, -210f), new(-120f, -240f) };
            foreach (var spot in cloudSpots)
            {
                var c = ui.Rect("Cloud", panel, new Vector2(0.5f, 1f), spot, new Vector2(10f, 10f));
                Blob("Puff", c, center, new Vector2(-60f, -6f), new Vector2(180f, 100f), new Color(1f, 1f, 1f, 0.95f));
                Blob("Puff", c, center, new Vector2(30f, 12f), new Vector2(200f, 130f), new Color(1f, 1f, 1f, 0.95f));
                Blob("Puff", c, center, new Vector2(100f, -10f), new Vector2(150f, 90f), new Color(1f, 1f, 1f, 0.95f));
                clouds.Add(c);
            }

            // The diving tower standing on the left, in the game's own colours.
            var tower = ui.Rect("Tower", panel, Vector2.zero, Vector2.zero, new Vector2(10f, 10f));
            Block("Pillar", tower, new Vector2(165f, 120f), new Vector2(84f, 440f), "#E4DED2");
            Block("PillarShade", tower, new Vector2(195f, 120f), new Vector2(24f, 440f), "#CFC7B8");
            float[] heights = { 250f, 390f, 540f };
            foreach (float h in heights)
            {
                Block("Platform", tower, new Vector2(205f, h), new Vector2(200f, 22f), "#EE6A55");
                Block("Edge", tower, new Vector2(296f, h + 22f), new Vector2(18f, 4f), "#FBF8F0");
                Block("Rail", tower, new Vector2(140f, h + 22f), new Vector2(4f, 44f), "#FBF8F0");
                Block("RailTop", tower, new Vector2(165f, h + 62f), new Vector2(54f, 4f), "#FBF8F0");
            }

            // Water: a pale far wave behind a darker near one, both rolling sideways.
            var far = Raw("WaveFar", panel, waveTex, Hex("#6CCBE0"));
            var farRt = far.rectTransform;
            farRt.anchorMin = Vector2.zero;
            farRt.anchorMax = new Vector2(1f, 0f);
            farRt.pivot = new Vector2(0.5f, 0f);
            farRt.sizeDelta = new Vector2(0f, 300f);
            far.uvRect = new Rect(0f, 0f, 2.2f, 1f);
            var near = Raw("WaveNear", panel, waveTex, Hex("#2A9FC4"));
            var nearRt = near.rectTransform;
            nearRt.anchorMin = Vector2.zero;
            nearRt.anchorMax = new Vector2(1f, 0f);
            nearRt.pivot = new Vector2(0.5f, 0f);
            nearRt.sizeDelta = new Vector2(0f, 230f);
            near.uvRect = new Rect(0.3f, 0f, 1.6f, 1f);

            var bubbles = new List<RectTransform>();
            var rng = new System.Random(4);
            for (int i = 0; i < 7; i++)
            {
                float s = 10f + (float)rng.NextDouble() * 14f;
                var b = Blob("Bubble", panel, new Vector2(0.5f, 0f), new Vector2(-520f + i * 170f, 20f + i * 23f), new Vector2(s, s), new Color(1f, 1f, 1f, 0.55f));
                bubbles.Add(b.rectTransform);
            }

            // Title, tilted a little, with a heavy outline so it holds up over anything.
            var titleRt = ui.Rect("TitleBlock", panel, center, new Vector2(70f, 150f), new Vector2(1000f, 200f));
            var title = ui.Label("Title", titleRt, center, Vector2.zero, new Vector2(1000f, 200f), 150, TextAnchor.MiddleCenter);
            title.text = "НЫРЯЙ!";
            title.color = Hex("#FFD45A");
            var thick = title.gameObject.AddComponent<Outline>();
            thick.effectColor = Hex("#1B2340");
            thick.effectDistance = new Vector2(-4f, 4f);
            var sub = ui.Label("Subtitle", panel, center, new Vector2(70f, 40f), new Vector2(800f, 56f), 40, TextAnchor.MiddleCenter);
            sub.text = "прыжки с вышки";

            var play = ui.Btn("Play", panel, center, new Vector2(70f, -95f), new Vector2(380f, 110f), "Играть", "#EE6A55", 52);
            var medals = ui.Label("Medals", panel, center, new Vector2(70f, -190f), new Vector2(600f, 40f), 26, TextAnchor.MiddleCenter, FontStyle.Normal);

            var backdrop = panel.gameObject.AddComponent<TitleBackdrop>();
            LinkArray(backdrop, "waves", new Object[] { far, near });
            var speeds = new SerializedObject(backdrop);
            var sp = speeds.FindProperty("waveSpeeds");
            sp.arraySize = 2;
            sp.GetArrayElementAtIndex(0).floatValue = 0.03f;
            sp.GetArrayElementAtIndex(1).floatValue = -0.06f;
            speeds.ApplyModifiedPropertiesWithoutUndo();
            LinkArray(backdrop, "clouds", clouds.ToArray());
            LinkArray(backdrop, "bubbles", bubbles.ToArray());
            Link(backdrop, "title", titleRt);
            Link(backdrop, "playButton", play.transform);

            return (panel, play, medals);
        }

        // ---------------------------------------------------------------- Level map

        static void BuildMap(UiKit ui, GameFlow flow, Diver diver, DiveCamera cam)
        {
            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            var topLeft = new Vector2(0f, 1f);
            var right = new Vector2(1f, 0.5f);

            var (titlePanel, play, titleMedals) = BuildTitle(ui);

            // Map overlay: location name on top, a card for the picked dive at the bottom.
            var mapPanel = ui.Stretch("MapScreen", ui.canvas);
            var locName = ui.Label("LocationName", mapPanel, top, new Vector2(0f, -44f), new Vector2(900f, 70f), 50, TextAnchor.MiddleCenter);
            var locInfo = ui.Label("LocationInfo", mapPanel, top, new Vector2(0f, -96f), new Vector2(600f, 34f), 24, TextAnchor.MiddleCenter, FontStyle.Normal);
            var locked = ui.Label("Locked", mapPanel, center, new Vector2(0f, -40f), new Vector2(1000f, 60f), 32, TextAnchor.MiddleCenter);

            var card = ui.Rect("Card", mapPanel, bottom, new Vector2(0f, 24f), new Vector2(780f, 150f));
            var cardBg = card.gameObject.AddComponent<Image>();
            cardBg.sprite = ui.pill;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.08f, 0.12f, 0.22f, 0.78f);
            var cardTitle = ui.Label("Title", card, topLeft, new Vector2(26f, -14f), new Vector2(380f, 40f), 28, TextAnchor.UpperLeft);
            var cardBest = ui.Label("Best", card, topLeft, new Vector2(400f, -16f), new Vector2(150f, 36f), 22, TextAnchor.UpperRight, FontStyle.Normal);
            var dots = new Image[3];
            var goals = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var dot = ui.Rect("Goal_" + i, card, topLeft, new Vector2(30f + i * 110f, -64f), new Vector2(28f, 28f)).gameObject.AddComponent<Image>();
                dot.sprite = ui.knob;
                dot.raycastTarget = false;
                dots[i] = dot;
                goals[i] = ui.Label("GoalText_" + i, card, topLeft, new Vector2(66f + i * 110f, -62f), new Vector2(80f, 32f), 24, TextAnchor.UpperLeft);
            }
            var cardTask = ui.Label("Task", card, topLeft, new Vector2(26f, -106f), new Vector2(540f, 30f), 22, TextAnchor.UpperLeft, FontStyle.Normal);
            var jump = ui.Btn("Jump", card, right, new Vector2(-112f, 0f), new Vector2(190f, 100f), "Прыгать!", "#EE6A55", 34);

            // 3D markers: a life buoy with the dive number, its medals below and a pole down to the water.
            var markersRoot = new GameObject("MapMarkers").transform;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var ringOpen = Glow(Mat("Marker_Open", "#2BA39C", smoothness: 0.5f), 0.45f);
            var ringCurrent = Glow(Mat("Marker_Current", "#EE6A55", smoothness: 0.5f), 0.5f);
            var ringLocked = Glow(Mat("Marker_Locked", "#7D8794", smoothness: 0.3f), 0.25f);
            var discMat = Glow(Mat("Marker_Disc", "#FBF8F0", smoothness: 0.3f), 0.55f);
            var lockMat = Mat("Marker_Lock", "#2E3440", smoothness: 0.4f);
            var poleMat = Mat("Marker_Pole", "#F4F2EC");
            var medalOn = new[]
            {
                Glow(Mat("Medal_Bronze", "#CD8550", smoothness: 0.6f), 0.4f),
                Glow(Mat("Medal_Silver", "#CDD6E0", smoothness: 0.6f), 0.4f),
                Glow(Mat("Medal_Gold", "#FFCC42", smoothness: 0.6f), 0.5f)
            };
            var medalOff = Mat("Medal_Off", "#3A4452");

            var marker = Child("Marker", markersRoot);
            var collider = marker.gameObject.AddComponent<SphereCollider>();
            collider.radius = 0.85f;
            var face = Child("Face", marker);
            var ringT = Put(LowPolyMeshes.Torus("Marker_Ring", 0.5f, 0.15f), "Ring", face, Vector3.zero, Vector3.one, ringOpen);
            ringT.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Put(Smooth, "Disc", face, Vector3.zero, new Vector3(0.78f, 0.78f, 0.1f), discMat);
            var numberGo = new GameObject("Number");
            numberGo.transform.SetParent(face, false);
            numberGo.transform.localPosition = new Vector3(0f, 0.02f, -0.07f);
            var number = numberGo.AddComponent<TextMesh>();
            number.font = font;
            number.fontSize = 64;
            number.characterSize = 0.09f;
            number.anchor = TextAnchor.MiddleCenter;
            number.alignment = TextAlignment.Center;
            number.fontStyle = FontStyle.Bold;
            number.color = Hex("#1B2340");
            number.text = "1";
            numberGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            var padlock = Child("Padlock", face);
            padlock.localPosition = new Vector3(0f, -0.04f, -0.08f);
            Box("Body", padlock, new Vector3(0f, -0.04f, 0f), new Vector3(0.28f, 0.22f, 0.08f), lockMat);
            var shackle = Put(LowPolyMeshes.Torus("Marker_Shackle", 0.09f, 0.025f, 16, 6), "Shackle", padlock, new Vector3(0f, 0.09f, 0f), Vector3.one, lockMat);
            shackle.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var medalRenderers = new Renderer[3];
            for (int i = 0; i < 3; i++)
                medalRenderers[i] = Put(Smooth, "Medal_" + i, face, new Vector3((i - 1) * 0.24f, -0.78f, -0.02f), Vector3.one * 0.18f, medalOff).GetComponent<Renderer>();
            var pole = Put(hex, "Pole", marker, new Vector3(0f, -1f, 0f), new Vector3(0.05f, 2f, 0.05f), poleMat);

            var mm = marker.gameObject.AddComponent<MapMarker>();
            Link(mm, "face", face);
            Link(mm, "ring", ringT.GetComponent<Renderer>());
            Link(mm, "number", number);
            Link(mm, "padlock", padlock.gameObject);
            LinkArray(mm, "medals", medalRenderers);
            Link(mm, "pole", pole);
            marker.gameObject.SetActive(false);

            // 3D arrows riding with the camera at the screen edges, a white outline behind a coral face.
            var arrowsRoot = Child("MapArrows", cam.transform);
            var arrowMesh = LowPolyMeshes.Arrow("MapArrow", 1.2f, 0.42f, 0.95f, 0.55f, 0.22f);
            var arrowFace = Glow(Mat("Arrow_Face", "#E8553F", smoothness: 0.4f), 0.22f);
            var arrowRim = Glow(Mat("Arrow_Rim", "#FBF8F0", smoothness: 0.3f), 0.45f);
            Transform Arrow(string n)
            {
                var a = Child(n, arrowsRoot);
                Put(arrowMesh, "Face", a, new Vector3(0f, 0f, -0.04f), Vector3.one, arrowFace);
                Put(arrowMesh, "Rim", a, new Vector3(0.02f, 0f, 0.06f), new Vector3(1.18f, 1.25f, 1f), arrowRim);
                var box = a.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(1.4f, 1.1f, 0.5f);
                return a;
            }
            var arrowR = Arrow("Arrow_Right");
            var arrowL = Arrow("Arrow_Left");
            arrowsRoot.gameObject.SetActive(false);
            mapPanel.gameObject.SetActive(false);

            var map = ui.canvas.gameObject.AddComponent<MapScreen>();
            Link(map, "flow", flow);
            Link(map, "diver", diver);
            Link(map, "diveCam", cam);
            Link(map, "hud", ui.hud);
            Link(map, "menuButton", ui.menuButton);
            Link(map, "titlePanel", titlePanel.gameObject);
            Link(map, "playButton", play);
            Link(map, "titleMedals", titleMedals);
            Link(map, "mapPanel", mapPanel.gameObject);
            Link(map, "locationName", locName);
            Link(map, "locationInfo", locInfo);
            Link(map, "card", card.gameObject);
            Link(map, "cardTitle", cardTitle);
            Link(map, "cardTask", cardTask);
            Link(map, "cardBest", cardBest);
            LinkArray(map, "cardMedals", dots);
            LinkArray(map, "cardGoals", goals);
            Link(map, "jumpButton", jump);
            Link(map, "lockedText", locked);
            Link(map, "markerTemplate", mm);
            Link(map, "arrows", arrowsRoot);
            Link(map, "arrowLeft", arrowL);
            Link(map, "arrowRight", arrowR);
            Link(map, "ringOpen", ringOpen);
            Link(map, "ringCurrent", ringCurrent);
            Link(map, "ringLocked", ringLocked);
            Link(map, "medalOff", medalOff);
            LinkArray(map, "medalOn", medalOn);
        }
    }
}
