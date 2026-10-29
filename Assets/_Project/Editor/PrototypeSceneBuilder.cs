using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JumpGame.EditorTools
{
    // Builds the grey-box pool scene used to tune the dive. Safe to rerun: it overwrites the scene.
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Pool_Prototype.unity";
        const string MaterialDir = "Assets/_Project/Materials/Prototype";

        const string LevelsPath = "Assets/_Project/Levels/PoolLevels.asset";

        const float DeckTop = 0.3f;
        const float PoolDepth = 3f;
        static readonly Vector2 PoolX = new(-5f, 5f);
        static readonly Vector2 PoolZ = new(-3f, 15f);
        const float TowerBack = -4.8f;

        // Platform heights, sideways position and how far the front edge reaches over the water.
        static readonly (float height, float x, float edgeZ)[] Platforms =
        {
            (3f, -2.6f, -0.9f),
            (5f, 2.6f, -0.4f),
            (7.5f, -2.6f, 0f),
            (10f, 0f, 0.3f)
        };

        // Meshes
        static Mesh cube, hex, disc, rough, cone, drop;

        [MenuItem("Jump Game/Build Prototype Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            MakeMeshes();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildAtmosphere();
            BuildPool();
            var tower = BuildTower();
            BuildSurroundings();

            var zone = BuildZone();
            var splash = BuildSplash();
            var diver = BuildDiver();
            Link(diver, "zone", zone);
            Link(diver, "splash", splash);
            Link(diver, "tower", tower);

            var cam = BuildCamera(diver, zone);

            var flow = new GameObject("Game").AddComponent<GameFlow>();
            Link(flow, "levels", LevelAsset());
            Link(flow, "tower", tower);
            Link(flow, "diver", diver);
            Link(flow, "zone", zone);
            Link(flow, "cam", cam);

            BuildHud(diver, flow);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Prototype scene saved to " + ScenePath);
        }

        static void MakeMeshes()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);

            hex = LowPolyMeshes.Prism("Hex", 6, 0.5f, 0.5f, 1f);
            disc = LowPolyMeshes.Lathe("RippleRing", new[] { new Vector2(0.43f, 0f), new Vector2(0.5f, 0f) }, 32, true);
            cone = LowPolyMeshes.Prism("Cone", 8, 0.5f, 0f, 1f);
            rough = LowPolyMeshes.Icosphere("Rough", 1, 0.5f, 0.14f, 7);
            drop = LowPolyMeshes.Icosphere("Drop", 0, 0.5f);
        }

        static void BuildAtmosphere()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1f;
            sun.color = Hex("#FFF0D8");
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            // Sun from the far end of the pool: the diver's front is lit whichever side the camera films from.
            sun.transform.rotation = Quaternion.Euler(44f, 160f, 0f);

            var sky = Mat("Sky", "#FFFFFF", shader: "Skybox/Procedural");
            sky.SetFloat("_SunDisk", 0f);
            sky.SetFloat("_AtmosphereThickness", 0.8f);
            sky.SetColor("_SkyTint", Hex("#7FBFEA"));
            sky.SetColor("_GroundColor", Hex("#B4D3DE"));
            sky.SetFloat("_Exposure", 1f);
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#7FA6BF");
            RenderSettings.ambientEquatorColor = Hex("#958E80");
            RenderSettings.ambientGroundColor = Hex("#55636A");
            RenderSettings.reflectionIntensity = 0.5f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("#CDE6F0");
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 120f;
        }

        static void BuildPool()
        {
            var root = new GameObject("Pool").transform;
            var deck = Mat("Deck", "#D8C29E");
            var tile = Mat("PoolTile", "#6CC4D4");
            var line = Mat("PoolLine", "#245F92");
            var coping = Mat("Coping", "#F4F2EC");
            var water = Mat("Water", "#1C98B8", transparent: true, alpha: 0.7f, smoothness: 0.6f);

            float floorY = -PoolDepth;
            float w = PoolX.y - PoolX.x, l = PoolZ.y - PoolZ.x;
            float cz = (PoolZ.x + PoolZ.y) * 0.5f;

            // Deck slabs around the basin, with tiled lining underneath so the walls read through the water.
            void Slab(string n, float x0, float x1, float z0, float z1)
            {
                var c = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
                var s = new Vector3(x1 - x0, 0f, z1 - z0);
                Box(n, root, new Vector3(c.x, DeckTop * 0.5f, c.z), new Vector3(s.x, DeckTop, s.z), deck);
                Box(n + "_Lining", root, new Vector3(c.x, floorY * 0.5f, c.z), new Vector3(s.x, PoolDepth, s.z), tile);
            }
            Slab("Deck_Left", -12f, PoolX.x, -8f, 20f);
            Slab("Deck_Right", PoolX.y, 12f, -8f, 20f);
            Slab("Deck_Back", PoolX.x, PoolX.y, -8f, PoolZ.x);
            Slab("Deck_Far", PoolX.x, PoolX.y, PoolZ.y, 20f);

            Box("Floor", root, new Vector3(0f, floorY - 0.05f, cz), new Vector3(w, 0.1f, l), tile);
            for (int i = -1; i <= 1; i++)
            {
                Box("Line_" + (i + 1), root, new Vector3(i * 3f, floorY + 0.005f, cz), new Vector3(0.22f, 0.01f, l - 3f), line);
                Box("LineEnd_" + (i + 1), root, new Vector3(i * 3f, floorY + 0.005f, PoolZ.y - 1.5f), new Vector3(0.9f, 0.01f, 0.22f), line);
            }

            float cw = 0.35f, ch = 0.06f, cy = DeckTop + ch * 0.5f;
            Box("Coping_L", root, new Vector3(PoolX.x - cw * 0.5f, cy, cz), new Vector3(cw, ch, l + cw * 2f), coping);
            Box("Coping_R", root, new Vector3(PoolX.y + cw * 0.5f, cy, cz), new Vector3(cw, ch, l + cw * 2f), coping);
            Box("Coping_B", root, new Vector3(0f, cy, PoolZ.x - cw * 0.5f), new Vector3(w, ch, cw), coping);
            Box("Coping_F", root, new Vector3(0f, cy, PoolZ.y + cw * 0.5f), new Vector3(w, ch, cw), coping);

            var surface = Box("Water", root, new Vector3(0f, -0.025f, cz), new Vector3(w, 0.05f, l), water);
            surface.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static DivingTower BuildTower()
        {
            var root = new GameObject("Tower").transform;
            var concrete = Mat("Concrete", "#E4DED2");
            var accent = Mat("TowerAccent", "#EE6A55");
            var metal = Mat("Rail", "#F7F7F2", smoothness: 0.4f);

            var solids = new System.Collections.Generic.List<Bounds>();

            // Concrete columns: a central core for the top platform and one on each side.
            void Column(string n, float x, float top)
            {
                float h = top - DeckTop;
                var c = new Vector3(x, DeckTop + h * 0.5f, TowerBack + 0.7f);
                var s = new Vector3(1.4f, h, 1.4f);
                Box(n, root, c, s, concrete);
                solids.Add(new Bounds(c, s));
            }
            Column("Core", 0f, 9.75f);
            Column("Column_L", -2.6f, 7.25f);
            Column("Column_R", 2.6f, 4.75f);

            var edges = new Transform[Platforms.Length];
            for (int i = 0; i < Platforms.Length; i++)
            {
                var (h, x, edgeZ) = Platforms[i];
                float depth = edgeZ - TowerBack;
                float midZ = (edgeZ + TowerBack) * 0.5f;
                string n = "Platform_" + h.ToString("0.#").Replace(',', '_');

                var slabCenter = new Vector3(x, h - 0.125f, midZ);
                var slabSize = new Vector3(2f, 0.25f, depth);
                Box(n, root, slabCenter, slabSize, accent);
                solids.Add(new Bounds(slabCenter, slabSize));
                Box(n + "_Edge", root, new Vector3(x, h + 0.005f, edgeZ - 0.08f), new Vector3(2f, 0.01f, 0.16f), metal);

                // Railings along the back part only; the front stays open for the jump.
                float railEnd = edgeZ - 1.6f;
                float railLen = railEnd - (TowerBack + 0.2f);
                float railMid = (railEnd + TowerBack + 0.2f) * 0.5f;
                foreach (float side in new[] { -0.95f, 0.95f })
                {
                    float rx = x + side;
                    for (float z = TowerBack + 0.2f; z <= railEnd + 0.01f; z += railLen / 2f)
                        Put(hex, "Post", root, new Vector3(rx, h + 0.5f, z), new Vector3(0.06f, 1f, 0.06f), metal);
                    Box("Rail", root, new Vector3(rx, h + 1f, railMid), new Vector3(0.06f, 0.06f, railLen), metal);
                    Box("Rail_Mid", root, new Vector3(rx, h + 0.5f, railMid), new Vector3(0.04f, 0.04f, railLen), metal);
                }

                var edge = new GameObject(n + "_Mark").transform;
                edge.SetParent(root, false);
                edge.position = new Vector3(x, h, edgeZ);
                edges[i] = edge;
            }

            // Ladder up the back of the core.
            foreach (float x in new[] { -0.3f, 0.3f })
                Box("Ladder_Side", root, new Vector3(x, 5f, TowerBack - 0.1f), new Vector3(0.06f, 9.6f, 0.06f), metal);
            for (float y = 0.7f; y < 9.9f; y += 0.38f)
                Box("Rung", root, new Vector3(0f, y, TowerBack - 0.1f), new Vector3(0.6f, 0.04f, 0.05f), metal);

            var tower = root.gameObject.AddComponent<DivingTower>();
            var so = new SerializedObject(tower);
            var arr = so.FindProperty("edges");
            arr.arraySize = edges.Length;
            for (int i = 0; i < edges.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = edges[i];
            var boxes = so.FindProperty("solids");
            boxes.arraySize = solids.Count;
            for (int i = 0; i < solids.Count; i++) boxes.GetArrayElementAtIndex(i).boundsValue = solids[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return tower;
        }

        // Level list. Distances and thresholds come from DiveCapture.Probe runs.
        static LevelSet LevelAsset()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LevelsPath));
            var set = AssetDatabase.LoadAssetAtPath<LevelSet>(LevelsPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<LevelSet>();
                AssetDatabase.CreateAsset(set, LevelsPath);
            }

            LevelInfo L(int platform, float distance, float radius, int b, int s, int g, TaskKind task, float turns = 0f) => new LevelInfo
            {
                platform = platform, zoneDistance = distance, zoneRadius = radius,
                bronze = b, silver = s, gold = g, task = task, taskTurns = turns
            };

            set.levels = new[]
            {
                // Probe, straight dives: 3 m reaches 1.6-5.2 m, 5 m 1.9-5.9, 7.5 m 2.2-6.7, 10 m 2.4-7.5.
                // Max turns: 2 / 2 / 2.5 / 3, clean head-first: 1.5 / 1.5 / 2.5 / 2.5. Gold sits near the best possible dive.
                L(0, 2.2f, 1.6f, 150, 300, 500, TaskKind.HeadFirst),
                L(0, 3.0f, 1.4f, 200, 450, 700, TaskKind.FeetFirst),
                L(1, 3.5f, 1.3f, 250, 500, 800, TaskKind.Turns, 1f),
                L(1, 4.3f, 1.1f, 300, 550, 850, TaskKind.CannonballInZone),
                L(2, 4.0f, 1.2f, 350, 700, 1100, TaskKind.Turns, 1.5f),
                L(2, 5.2f, 1.0f, 400, 750, 1150, TaskKind.ZoneNoBuoys),
                L(3, 4.6f, 1.1f, 450, 850, 1200, TaskKind.Turns, 2f),
                L(3, 6.0f, 0.9f, 500, 950, 1300, TaskKind.CleanTurns, 2.5f)
            };
            EditorUtility.SetDirty(set);
            return set;
        }

        static void BuildSurroundings()
        {
            var root = new GameObject("Surroundings").transform;
            var grass = Mat("Grass", "#79BC5A");
            var wall = Mat("Wall", "#E6D9C6");
            var white = Mat("ChairWood", "#B5875B");
            var teal = Mat("ChairTeal", "#2BA39C");
            var coral = Mat("Umbrella_Coral", "#F2735E");
            var yellow = Mat("Umbrella_Yellow", "#FFC743");
            var trunk = Mat("Trunk", "#8A5B3D");
            var leaves = Mat("Leaves", "#5DB464");
            var leavesDark = Mat("LeavesDark", "#3F9A57");
            var hill = Mat("Hill", "#7DBB8A");

            Box("Grass_E", root, new Vector3(62f, 0.1f, 6f), new Vector3(100f, 0.2f, 160f), grass);
            Box("Grass_W", root, new Vector3(-62f, 0.1f, 6f), new Vector3(100f, 0.2f, 160f), grass);
            Box("Grass_N", root, new Vector3(0f, 0.1f, 50f), new Vector3(24f, 0.2f, 60f), grass);
            Box("Grass_S", root, new Vector3(0f, 0.1f, -38f), new Vector3(24f, 0.2f, 60f), grass);

            Box("Wall_E", root, new Vector3(12.1f, 0.9f, 6f), new Vector3(0.25f, 1.2f, 28.4f), wall);
            Box("Wall_W", root, new Vector3(-12.1f, 0.9f, 6f), new Vector3(0.25f, 1.2f, 28.4f), wall);
            Box("Wall_N", root, new Vector3(0f, 0.9f, 20.1f), new Vector3(24.4f, 1.2f, 0.25f), wall);
            Box("Wall_S", root, new Vector3(0f, 0.9f, -8.1f), new Vector3(24.4f, 1.2f, 0.25f), wall);

            // The camera films from either side depending on the platform, so both decks get furniture.
            var rng = new System.Random(3);
            foreach (float side in new[] { 1f, -1f })
            {
                // Furniture stays in the far half: the near half of each deck is right under the camera.
                for (int i = 0; i < 3; i++)
                {
                    float z = 8.5f + i * 3.2f;
                    bool odd = (i + (side < 0 ? 1 : 0)) % 2 == 0;
                    Chair(root, new Vector3(8f * side, DeckTop, z), 0f, odd ? teal : white, white);
                    if (i < 2) Umbrella(root, new Vector3(9.3f * side, DeckTop, z + 1.6f), odd ? coral : yellow);
                }
                Chair(root, new Vector3(2.2f * side, DeckTop, 17.8f), 180f, side > 0 ? teal : white, white);
                Umbrella(root, new Vector3(3.8f * side, DeckTop, 18.4f), side > 0 ? yellow : coral);

                // Only trees behind the far walls: anything at the near edge would end up right in front of the lens.
                Vector3[] trees =
                {
                    new(10.8f, 0f, 16.5f), new(16f, 0f, -2f), new(18f, 0f, 9f),
                    new(15.5f, 0f, 19f), new(22f, 0f, 3f), new(24f, 0f, 14f)
                };
                foreach (var p in trees)
                {
                    float s = 0.85f + (float)rng.NextDouble() * 0.45f;
                    var at = new Vector3(p.x * side + (float)rng.NextDouble() - 0.5f, 0f, p.z + (float)rng.NextDouble() * 2f - 1f);
                    Tree(root, at, s, rng.Next(2) == 0 ? leaves : leavesDark, trunk);
                }

                Put(rough, "Hill_1", root, new Vector3(70f * side, -6f, -10f), new Vector3(60f, 26f, 50f), hill);
                Put(rough, "Hill_2", root, new Vector3(85f * side, -10f, 30f), new Vector3(80f, 36f, 60f), hill);
                Put(rough, "Hill_3", root, new Vector3(60f * side, -8f, 55f), new Vector3(50f, 24f, 40f), hill);
            }
        }

        static void Chair(Transform root, Vector3 at, float yaw, Material cushion, Material frame)
        {
            var c = new GameObject("DeckChair").transform;
            c.SetParent(root, false);
            c.localPosition = at;
            c.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Frame", c, new Vector3(0f, 0.22f, 0f), new Vector3(0.7f, 0.08f, 1.9f), frame);
            Box("Seat", c, new Vector3(0f, 0.3f, 0.15f), new Vector3(0.62f, 0.08f, 1.3f), cushion);
            var back = Box("Back", c, new Vector3(0f, 0.55f, -0.7f), new Vector3(0.62f, 0.08f, 0.7f), cushion);
            back.localRotation = Quaternion.Euler(-48f, 0f, 0f);
            foreach (var p in new[] { new Vector3(-0.3f, 0.1f, -0.85f), new Vector3(0.3f, 0.1f, -0.85f), new Vector3(-0.3f, 0.1f, 0.85f), new Vector3(0.3f, 0.1f, 0.85f) })
                Box("Leg", c, p, new Vector3(0.05f, 0.2f, 0.05f), frame);
        }

        static void Umbrella(Transform root, Vector3 at, Material canvas)
        {
            var u = new GameObject("Umbrella").transform;
            u.SetParent(root, false);
            u.localPosition = at;
            Put(hex, "Pole", u, new Vector3(0f, 1.1f, 0f), new Vector3(0.05f, 2.2f, 0.05f), Mat("Rail", "#F7F7F2", smoothness: 0.4f));
            Put(cone, "Canopy", u, new Vector3(0f, 2.25f, 0f), new Vector3(2.3f, 0.5f, 2.3f), canvas);
        }

        static void Tree(Transform root, Vector3 at, float s, Material crown, Material bark)
        {
            var t = new GameObject("Tree").transform;
            t.SetParent(root, false);
            t.localPosition = at;
            t.localScale = Vector3.one * s;
            Put(hex, "Trunk", t, new Vector3(0f, 1.3f, 0f), new Vector3(0.3f, 2.6f, 0.3f), bark);
            Put(rough, "Crown", t, new Vector3(0f, 3.2f, 0f), new Vector3(2.4f, 2.2f, 2.4f), crown);
            Put(rough, "Crown_Top", t, new Vector3(0.3f, 4.1f, -0.2f), new Vector3(1.5f, 1.4f, 1.5f), crown);
        }

        static LandingZone BuildZone()
        {
            var go = new GameObject("LandingZone");
            go.transform.position = new Vector3(Platforms[1].x, 0f, Platforms[1].edgeZ + 4f);

            var red = Mat("BuoyRed", "#F2434F", smoothness: 0.45f);
            var white = Mat("BuoyWhite", "#F7F7F2", smoothness: 0.45f);

            // Lane-rope float: a spindle threaded on the rope.
            var floatMesh = LowPolyMeshes.Lathe("RopeFloat", new[]
            {
                new Vector2(0.012f, 0.088f), new Vector2(0.036f, 0.082f), new Vector2(0.058f, 0.058f),
                new Vector2(0.068f, 0.025f), new Vector2(0.07f, 0f), new Vector2(0.068f, -0.025f),
                new Vector2(0.058f, -0.058f), new Vector2(0.036f, -0.082f), new Vector2(0.012f, -0.088f)
            }, 12, true);

            var ropeGo = new GameObject("Rope");
            ropeGo.transform.SetParent(go.transform, false);
            var rope = ropeGo.AddComponent<LineRenderer>();
            rope.sharedMaterial = Mat("Rope", "#FFFFFF", shader: "Sprites/Default");
            rope.startColor = rope.endColor = Hex("#39404C");
            rope.widthMultiplier = 0.018f;
            rope.loop = true;
            rope.useWorldSpace = true;
            rope.shadowCastingMode = ShadowCastingMode.Off;
            rope.positionCount = 0;

            // Floats are created by the zone itself for each level's radius.
            var zone = go.AddComponent<LandingZone>();
            Link(zone, "rope", rope);
            Link(zone, "floatMesh", floatMesh);
            Link(zone, "floatRed", red);
            Link(zone, "floatWhite", white);
            return zone;
        }

        static Splash BuildSplash()
        {
            var foam = Mat("Foam", "#EEFBFF", smoothness: 0.5f);

            var go = new GameObject("Splash");
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startRotation3D = true;
            main.gravityModifier = 1.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.25f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = drop;
            renderer.sharedMaterial = foam;

            var ripple = Put(disc, "Ripple", null, Vector3.zero, Vector3.one, foam);
            ripple.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            ripple.gameObject.SetActive(false);

            var splash = go.AddComponent<Splash>();
            Link(splash, "ripple", ripple);
            return splash;
        }

        static Diver BuildDiver()
        {
            var skin = Mat("Skin", "#EFBC98", smoothness: 0.3f);
            var trunks = Mat("Trunks", "#283B8F", smoothness: 0.3f);
            var cap = Mat("SwimCap", "#FFC743", smoothness: 0.55f);
            var strap = Mat("Goggles", "#1E2A4A", smoothness: 0.6f);
            var lens = Mat("Lens", "#7FD3F0", smoothness: 0.9f);

            // Smooth rounded body: every joint is covered by the rounded ends of the parts meeting there.
            var torsoMesh = LowPolyMeshes.Lathe("Body_Torso", new[]
            {
                new Vector2(0f, 0.47f), new Vector2(0.07f, 0.465f), new Vector2(0.15f, 0.44f), new Vector2(0.195f, 0.39f),
                new Vector2(0.205f, 0.31f), new Vector2(0.19f, 0.2f), new Vector2(0.165f, 0.1f), new Vector2(0.16f, 0.02f),
                new Vector2(0.17f, -0.06f), new Vector2(0.1f, -0.11f), new Vector2(0f, -0.12f)
            }, 16, true);
            var trunksMesh = LowPolyMeshes.Lathe("Body_Trunks", new[]
            {
                new Vector2(0f, 0.06f), new Vector2(0.172f, 0.06f), new Vector2(0.186f, 0f),
                new Vector2(0.18f, -0.07f), new Vector2(0.15f, -0.13f), new Vector2(0f, -0.15f)
            }, 16, true);
            var head = LowPolyMeshes.Sphere("Body_Head", 0.142f);
            var capMesh = LowPolyMeshes.Sphere("Body_Cap", 0.151f, 16, 8, 0f, 100f);
            var band = LowPolyMeshes.Lathe("Body_GoggleBand", new[] { new Vector2(0.144f, 0.02f), new Vector2(0.149f, 0f), new Vector2(0.144f, -0.02f) }, 16, true);
            var joint = LowPolyMeshes.Sphere("Body_Joint", 0.068f, 12, 8);
            var lensMesh = LowPolyMeshes.Sphere("Body_Lens", 0.038f, 10, 6);
            var hand = LowPolyMeshes.Sphere("Body_Hand", 0.052f, 10, 6);
            var neck = LowPolyMeshes.Capsule("Body_Neck", 0.064f, 0.07f, 0.05f);
            var upperArm = LowPolyMeshes.Capsule("Body_UpperArm", 0.064f, 0.052f, 0.26f);
            var foreArm = LowPolyMeshes.Capsule("Body_ForeArm", 0.051f, 0.042f, 0.23f);
            var thighMesh = LowPolyMeshes.Capsule("Body_Thigh", 0.102f, 0.072f, 0.37f);
            var shinMesh = LowPolyMeshes.Capsule("Body_Shin", 0.073f, 0.05f, 0.36f);
            var footMesh = LowPolyMeshes.Capsule("Body_Foot", 0.05f, 0.042f, 0.17f);

            var root = new GameObject("Diver");
            // Placed on the 5 m platform; GameFlow moves it to the current level's platform.
            root.transform.position = new Vector3(Platforms[1].x, Platforms[1].height + 0.9f, Platforms[1].edgeZ - 0.25f);

            var rig = new GameObject("Rig").transform;
            rig.SetParent(root.transform, false);
            Put(trunksMesh, "Trunks", rig, Vector3.zero, new Vector3(1f, 1f, 0.72f), trunks);

            var spine = Pivot("Spine", rig, new Vector3(0f, 0.1f, 0f));
            Put(torsoMesh, "Torso", spine, Vector3.zero, new Vector3(1f, 1f, 0.66f), skin);
            Put(neck, "Neck", spine, new Vector3(0f, 0.52f, 0f), Vector3.one, skin);

            var headPivot = Pivot("Head", spine, new Vector3(0f, 0.63f, 0.01f));
            Put(head, "Head_Mesh", headPivot, Vector3.zero, new Vector3(0.95f, 1.06f, 1f), skin);
            var capT = Put(capMesh, "Cap", headPivot, new Vector3(0f, 0.012f, -0.006f), new Vector3(1f, 1.04f, 1.02f), cap);
            capT.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            var bandT = Put(band, "GoggleStrap", headPivot, new Vector3(0f, -0.004f, 0f), new Vector3(0.98f, 1f, 1.02f), strap);
            bandT.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            foreach (float x in new[] { -0.05f, 0.05f })
                Put(lensMesh, "Lens", headPivot, new Vector3(x, 0.004f, 0.134f), new Vector3(1.15f, 0.8f, 0.5f), lens);

            (Transform arm, Transform fore) Arm(string side, float x)
            {
                Put(joint, "Shoulder_" + side, spine, new Vector3(x * 0.93f, 0.4f, 0f), Vector3.one, skin);
                var arm = Pivot("Arm_" + side, spine, new Vector3(x, 0.4f, 0f));
                Put(upperArm, "UpperArm_" + side, arm, Vector3.zero, Vector3.one, skin);
                var fore = Pivot("ForeArm_" + side, arm, new Vector3(0f, -0.27f, 0f));
                Put(foreArm, "ForeArm_" + side + "_Mesh", fore, Vector3.zero, Vector3.one, skin);
                Put(hand, "Hand_" + side, fore, new Vector3(0f, -0.29f, 0f), new Vector3(0.9f, 1.3f, 0.6f), skin);
                return (arm, fore);
            }
            var armL = Arm("L", -0.235f);
            var armR = Arm("R", 0.235f);

            (Transform thigh, Transform shin) Leg(string side, float x)
            {
                var thigh = Pivot("Thigh_" + side, rig, new Vector3(x, 0.02f, 0f));
                Put(thighMesh, "Thigh_" + side + "_Mesh", thigh, Vector3.zero, Vector3.one, skin);
                var shin = Pivot("Shin_" + side, thigh, new Vector3(0f, -0.44f, 0f));
                Put(shinMesh, "Shin_" + side + "_Mesh", shin, Vector3.zero, Vector3.one, skin);
                var foot = Put(footMesh, "Foot_" + side, shin, new Vector3(0f, -0.425f, -0.05f), Vector3.one, skin);
                foot.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                return (thigh, shin);
            }
            var legL = Leg("L", -0.1f);
            var legR = Leg("R", 0.1f);

            var pose = root.AddComponent<DiverPose>();
            Link(pose, "rig", rig);
            Link(pose, "spine", spine);
            Link(pose, "armL", armL.arm);
            Link(pose, "armR", armR.arm);
            Link(pose, "foreL", armL.fore);
            Link(pose, "foreR", armR.fore);
            Link(pose, "thighL", legL.thigh);
            Link(pose, "thighR", legR.thigh);
            Link(pose, "kneeL", legL.shin);
            Link(pose, "kneeR", legR.shin);

            var diver = root.AddComponent<Diver>();
            Link(diver, "pose", pose);
            return diver;
        }

        static DiveCamera BuildCamera(Diver diver, LandingZone zone)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 55f;
            cam.farClipPlane = 300f;
            go.AddComponent<AudioListener>();

            var follow = go.AddComponent<DiveCamera>();
            Link(follow, "diver", diver);
            Link(follow, "zone", zone);

            // Put the camera where it will start so the scene view matches the game.
            var focus = Vector3.Lerp(diver.transform.position, zone.transform.position, 0.3f);
            go.transform.position = focus + new Vector3(-7.5f, 2.5f, -1.5f);
            go.transform.LookAt(focus);
            return follow;
        }

        static void BuildHud(Diver diver, GameFlow flow)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var pill = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            var canvasGo = new GameObject("Hud");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            RectTransform Rect(string n, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
            {
                var go = new GameObject(n, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
                return rt;
            }

            Image Dot(string n, Transform parent, Vector2 anchor, Vector2 pos, float size)
            {
                var img = Rect(n, parent, anchor, pos, new Vector2(size, size)).gameObject.AddComponent<Image>();
                img.sprite = knob;
                img.raycastTarget = false;
                var o = img.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.1f, 0.14f, 0.25f, 0.8f);
                o.effectDistance = new Vector2(2f, -2f);
                return img;
            }

            Button Btn(string n, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label, string color, int fontSize)
            {
                var rt = Rect(n, parent, anchor, pos, size);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = pill;
                img.type = Image.Type.Sliced;
                img.color = Hex(color);
                var shadow = rt.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.1f, 0.13f, 0.25f, 0.5f);
                shadow.effectDistance = new Vector2(0f, -5f);
                var b = rt.gameObject.AddComponent<Button>();
                var colors = b.colors;
                colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
                b.colors = colors;
                var t = Label("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size, fontSize, TextAnchor.MiddleCenter);
                t.text = label;
                return b;
            }

            Text Label(string n, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, FontStyle style = FontStyle.Bold)
            {
                var go = new GameObject(n, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
                var t = go.AddComponent<Text>();
                t.font = font;
                t.fontSize = fontSize;
                t.fontStyle = style;
                t.alignment = align;
                t.color = Color.white;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                var outline = go.AddComponent<Outline>();
                outline.effectColor = Hex("#1B2340");
                outline.effectDistance = new Vector2(2.5f, -2.5f);
                var shadow = go.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.1f, 0.13f, 0.25f, 0.45f);
                shadow.effectDistance = new Vector2(0f, -5f);
                return t;
            }

            var root = canvasGo.transform;
            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);
            var center = new Vector2(0.5f, 0.5f);

            // Level selector, top centre.
            var levelTitle = Label("LevelTitle", root, top, new Vector2(0f, -22f), new Vector2(460f, 50f), 34, TextAnchor.MiddleCenter);
            var prev = Btn("Prev", root, top, new Vector2(-260f, -22f), new Vector2(64f, 56f), "<", "#2BA39C", 34);
            var next = Btn("Next", root, top, new Vector2(260f, -22f), new Vector2(64f, 56f), ">", "#2BA39C", 34);

            // Goals and task, top left.
            var dots = new Image[3];
            var goalTexts = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                float x = 28f + i * 112f;
                dots[i] = Dot("Goal_" + i, root, topLeft, new Vector2(x, -24f), 30f);
                goalTexts[i] = Label("GoalText_" + i, root, topLeft, new Vector2(x + 38f, -22f), new Vector2(80f, 34f), 26, TextAnchor.MiddleLeft);
            }
            var taskDot = Dot("TaskDot", root, topLeft, new Vector2(32f, -74f), 22f);
            var taskText = Label("TaskText", root, topLeft, new Vector2(64f, -70f), new Vector2(520f, 34f), 24, TextAnchor.MiddleLeft);
            var best = Label("Best", root, topRight, new Vector2(-28f, -22f), new Vector2(360f, 50f), 28, TextAnchor.MiddleRight);

            var turns = Label("Turns", root, top, new Vector2(0f, -90f), new Vector2(600f, 130f), 96, TextAnchor.UpperCenter);
            var hint = Label("Hint", root, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1100f, 120f), 32, TextAnchor.LowerCenter);

            // Result panel.
            var panelGo = new GameObject("Result", typeof(RectTransform));
            panelGo.transform.SetParent(root, false);
            var panel = (RectTransform)panelGo.transform;
            panel.anchorMin = new Vector2(0f, 0.5f);
            panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = center;
            panel.sizeDelta = new Vector2(0f, 500f);
            panel.anchoredPosition = new Vector2(0f, 0f);
            var band = panelGo.AddComponent<Image>();
            band.color = new Color(0.1f, 0.14f, 0.26f, 0.45f);
            band.raycastTarget = false;

            var title = Label("Title", panel, center, new Vector2(0f, 185f), new Vector2(1000f, 90f), 66, TextAnchor.MiddleCenter);
            var details = Label("Details", panel, center, new Vector2(0f, 120f), new Vector2(1000f, 50f), 30, TextAnchor.MiddleCenter, FontStyle.Normal);
            var score = Label("Score", panel, center, new Vector2(0f, 40f), new Vector2(1000f, 120f), 100, TextAnchor.MiddleCenter);
            var medals = new Image[3];
            for (int i = 0; i < 3; i++)
                medals[i] = Dot("Medal_" + i, panel, center, new Vector2((i - 1) * 74f, -50f), 54f);
            var task = Label("Task", panel, center, new Vector2(0f, -110f), new Vector2(1000f, 40f), 28, TextAnchor.MiddleCenter);
            var retry = Btn("Retry", panel, center, new Vector2(-130f, -185f), new Vector2(220f, 70f), "Ещё раз", "#2BA39C", 30);
            var nextLevel = Btn("NextLevel", panel, center, new Vector2(130f, -185f), new Vector2(220f, 70f), "Дальше", "#EE6A55", 30);

            var hud = canvasGo.AddComponent<DiveHud>();
            Link(hud, "diver", diver);
            Link(hud, "flow", flow);
            Link(hud, "levelTitle", levelTitle);
            Link(hud, "prevButton", prev);
            Link(hud, "nextButton", next);
            LinkArray(hud, "goalDots", dots);
            LinkArray(hud, "goalTexts", goalTexts);
            Link(hud, "taskDot", taskDot);
            Link(hud, "taskText", taskText);
            Link(hud, "best", best);
            Link(hud, "turns", turns);
            Link(hud, "hint", hint);
            Link(hud, "resultPanel", panel);
            Link(hud, "resultTitle", title);
            Link(hud, "resultDetails", details);
            Link(hud, "resultScore", score);
            LinkArray(hud, "resultMedals", medals);
            Link(hud, "resultTask", task);
            Link(hud, "retryButton", retry);
            Link(hud, "nextLevelButton", nextLevel);
        }

        static void LinkArray(Object owner, string field, Object[] values)
        {
            var so = new SerializedObject(owner);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static Transform Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
            => Put(cube, name, parent, pos, size, mat);

        static Transform Put(Mesh mesh, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        static Material Mat(string name, string hex, float smoothness = 0.08f, bool transparent = false, float alpha = 1f, string shader = "Standard")
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader.name != shader)
            {
                if (mat != null) AssetDatabase.DeleteAsset(path);
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }

            if (shader == "Standard")
            {
                var c = Hex(hex);
                c.a = alpha;
                mat.color = c;
                mat.SetFloat("_Glossiness", smoothness);
                if (transparent) MakeTransparent(mat);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static void Link(Object owner, string field, Object value)
        {
            var so = new SerializedObject(owner);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"{owner.GetType().Name} has no field '{field}'");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
