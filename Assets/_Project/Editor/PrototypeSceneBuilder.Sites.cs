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

        static void Pine(Transform parent, Vector3 at, float s, Material needles, Material bark)
        {
            var model = Prop("Trees", "Pine_" + "AB"[Variant(at, 2)], parent, at, Vector3.one * s, Variant(at, 360) * 1f,
                             ("Needles", needles), ("Bark", bark));
            if (model != null) return;
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
                Set(swim, "center", new Vector3(-6f, 0f, 12f));
                Set(swim, "radii", new Vector2(7f, 4f));
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
            Set(jumper, "areaCenter", new Vector3(6f, 0f, 8f));
            Set(jumper, "areaSize", new Vector2(36f, 20f));
        }

        static void Palm(Transform parent, Vector3 at, float s, float yaw)
        {
            var trunkMat = Mat("PalmTrunk", "#9B7448");
            var leafMat = Mat("PalmLeaf", "#4F9A4A");
            var nut = Mat("Coconut", "#6B4A2B");

            string palmName = "Palm_" + "AB"[Variant(at, 2)];
            var palmModel = Prop("Trees", palmName, parent, at, Vector3.one * s, yaw, ("PalmLeaf", leafMat), ("PalmTrunk", trunkMat), ("Coconut", nut));
            if (palmModel != null)
            {
                var top = FindDeep(palmModel, palmName + "_Crown");
                if (top != null) AddSway(top, Vector3.forward, 4f, 0.8f, yaw * 0.1f);
                return;
            }

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
                (new Vector3(36f, 0.35f, 2.5f), 0f), (new Vector3(41f, 0.35f, 1.5f), 0f),
                (new Vector3(-36f, 0.35f, 1.5f), 0f), (new Vector3(-41f, 0.35f, 2.5f), 0f)
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

        static (UiPanel panel, Button play, Text medals) BuildTitle(UiKit ui)
        {
            var left = new Vector2(0f, 0.5f);
            var panelRt = ui.Stretch("TitleScreen", ui.canvas);

            // The scene itself is the backdrop: a dark wash from the left edge keeps the type readable and fades out.
            var shadeTex = SaveTexture("TitleShade", 256, 4, (x, y) =>
            {
                float t = x / 255f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - t / 0.62f), 1.6f) * 0.9f;
                return new Color(0.055f, 0.08f, 0.13f, a);
            }, TextureWrapMode.Clamp);
            var shade = ui.Stretch("Shade", panelRt).gameObject.AddComponent<RawImage>();
            shade.texture = shadeTex;
            shade.raycastTarget = true;

            // Flat type without the cartoon outline the other screens use.
            Text Plain(string n, Vector2 pos, Vector2 size, int fontSize, string text, string color, FontStyle style)
            {
                var t = ui.Label(n, panelRt, left, pos, size, fontSize, TextAnchor.MiddleLeft, style);
                Object.DestroyImmediate(t.GetComponent<Outline>());
                Object.DestroyImmediate(t.GetComponent<Shadow>());
                t.rectTransform.pivot = new Vector2(0f, 0.5f);
                t.rectTransform.anchoredPosition = pos;
                t.text = text;
                t.color = Hex(color);
                return t;
            }

            var title = Plain("Title", new Vector2(96f, 92f), new Vector2(700f, 150f), 128, "НЫРЯЙ", "#F4F1EA", FontStyle.Bold);

            var bar = ui.Rect("Bar", panelRt, left, new Vector2(96f, 8f), new Vector2(84f, 6f));
            bar.pivot = new Vector2(0f, 0.5f);
            bar.anchoredPosition = new Vector2(100f, 8f);
            var barImg = bar.gameObject.AddComponent<Image>();
            barImg.color = Hex("#EE6A55");
            barImg.raycastTarget = false;

            var tagline = Plain("Tagline", new Vector2(98f, -36f), new Vector2(620f, 40f), 28, "Прыжки в воду с вышек, мостов и скал", "#C8D2DB", FontStyle.Normal);

            var play = ui.Btn("Play", panelRt, left, new Vector2(98f, -128f), new Vector2(280f, 76f), "Играть", "#EE6A55", 34);
            var playRt = (RectTransform)play.transform;
            playRt.pivot = new Vector2(0f, 0.5f);
            playRt.anchoredPosition = new Vector2(98f, -128f);
            Object.DestroyImmediate(play.GetComponent<Shadow>());
            var playLabel = play.GetComponentInChildren<Text>();
            Object.DestroyImmediate(playLabel.GetComponent<Outline>());
            Object.DestroyImmediate(playLabel.GetComponent<Shadow>());

            titleShop = ui.Btn("Shop", panelRt, left, new Vector2(396f, -128f), new Vector2(220f, 76f), "Магазин", "#34495E", 30);
            var shopRt = (RectTransform)titleShop.transform;
            shopRt.pivot = new Vector2(0f, 0.5f);
            shopRt.anchoredPosition = new Vector2(396f, -128f);
            Object.DestroyImmediate(titleShop.GetComponent<Shadow>());
            var shopLabel = titleShop.GetComponentInChildren<Text>();
            Object.DestroyImmediate(shopLabel.GetComponent<Outline>());
            Object.DestroyImmediate(shopLabel.GetComponent<Shadow>());

            var medals = Plain("Medals", new Vector2(100f, -196f), new Vector2(620f, 34f), 22, "", "#8E9CAB", FontStyle.Normal);

            var soundRt = SoundButton(ui, panelRt, left, new Vector2(98f, -262f), new Vector2(200f, 46f), "#34495E", true);
            soundRt.pivot = new Vector2(0f, 0.5f);
            soundRt.anchoredPosition = new Vector2(98f, -262f);

            titleBoard = ui.Btn("Rating", panelRt, left, new Vector2(316f, -262f), new Vector2(220f, 46f), "Рейтинг", "#34495E", 22);
            var boardRt = (RectTransform)titleBoard.transform;
            boardRt.pivot = new Vector2(0f, 0.5f);
            boardRt.anchoredPosition = new Vector2(316f, -262f);
            Flatten(titleBoard);

            var panel = panelRt.gameObject.AddComponent<UiPanel>();
            LinkArray(panel, "staggered", new Object[] { title.rectTransform, bar, tagline.rectTransform, playRt, shopRt, medals.rectTransform, soundRt, boardRt });
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
            var locInfo = ui.Label("LocationInfo", mapPanel, top, new Vector2(0f, -106f), new Vector2(600f, 34f), 24, TextAnchor.MiddleCenter, FontStyle.Normal);
            var locked = ui.Label("Locked", mapPanel, center, new Vector2(0f, -40f), new Vector2(1000f, 60f), 32, TextAnchor.MiddleCenter);

            var card = ui.Rect("Card", mapPanel, bottom, new Vector2(0f, 24f), new Vector2(780f, 150f));
            var cardBg = card.gameObject.AddComponent<Image>();
            cardBg.sprite = ui.pill;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.08f, 0.12f, 0.22f, 0.78f);
            var cardTitle = ui.Label("Title", card, topLeft, new Vector2(26f, -14f), new Vector2(380f, 40f), 28, TextAnchor.UpperLeft);
            var cardBest = ui.Label("Best", card, topLeft, new Vector2(396f, -64f), new Vector2(170f, 32f), 22, TextAnchor.UpperRight, FontStyle.Normal);
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
            var font = UiFont();
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
            var mapUi = mapPanel.gameObject.AddComponent<UiPanel>();
            LinkArray(mapUi, "staggered", new Object[] { locName.rectTransform, locInfo.rectTransform, card });
            Set(mapUi, "slide", new Vector2(0f, -34f));
            mapPanel.gameObject.SetActive(false);

            var extras = BuildMapExtras(ui, mapPanel);
            var shop = BuildShop(ui, flow, titlePanel);
            var board = BuildLeaderboard(ui, titlePanel);

            var hudUi = ui.hud.AddComponent<UiPanel>();
            LinkArray(hudUi, "staggered", new Object[0]);
            Set(hudUi, "fade", 0.35f);

            // A plain curtain over everything for the jumps between title, map and game.
            var curtainRt = ui.Stretch("Curtain", ui.canvas);
            var curtainImg = curtainRt.gameObject.AddComponent<Image>();
            curtainImg.color = Hex("#21384F");
            var curtain = curtainRt.gameObject.AddComponent<ScreenCurtain>();
            curtainRt.gameObject.SetActive(false);

            var map = ui.canvas.gameObject.AddComponent<MapScreen>();
            Link(map, "flow", flow);
            Link(map, "diver", diver);
            Link(map, "diveCam", cam);
            Link(map, "hud", hudUi);
            Link(map, "curtain", curtain);
            Link(map, "shop", shop);
            Link(map, "board", board);
            Link(map, "menuButton", ui.menuButton);
            Link(map, "titlePanel", titlePanel);
            Link(map, "playButton", play);
            Link(map, "titleMedals", titleMedals);
            Link(map, "mapPanel", mapUi);
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
            Link(map, "buyButton", extras.buy);
            Link(map, "buyLabel", extras.buyLabel);
            Link(map, "backButton", extras.back);
            Link(map, "mapCoins", extras.mapCoins);
            Link(map, "summaryPanel", extras.summary);
            Link(map, "summaryTitle", extras.summaryTitle);
            Link(map, "summaryTotals", extras.summaryTotals);
            Link(map, "summaryCoins", extras.summaryCoins);
            Link(map, "summaryNextLabel", extras.summaryNextLabel);
            LinkArray(map, "summaryRowLabels", extras.rowLabels);
            LinkArray(map, "summaryRowScores", extras.rowScores);
            LinkArray(map, "summaryRowDots", extras.rowDots);
            Link(map, "summaryNext", extras.summaryNext);
            Link(map, "summaryStay", extras.summaryStay);
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
