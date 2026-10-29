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

        const float PlatformTop = 5f;
        const float DeckTop = 0.3f;
        const float PoolDepth = 3f;
        static readonly Vector2 PoolX = new(-5f, 5f);
        static readonly Vector2 PoolZ = new(-3f, 15f);
        static readonly Vector3 ZoneCenter = new(0f, 0f, 3.9f);
        const float ZoneRadius = 1.3f;

        // Meshes
        static Mesh cube, hex, oct, disc, ball, rough, cone, drop;

        [MenuItem("Jump Game/Build Prototype Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            MakeMeshes();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildAtmosphere();
            BuildPool();
            BuildTower();
            BuildSurroundings();

            var zone = BuildZone();
            var splash = BuildSplash();
            var diver = BuildDiver();
            Link(diver, "zone", zone);
            Link(diver, "splash", splash);

            BuildCamera(diver, zone);
            BuildHud(diver);

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
            oct = LowPolyMeshes.Prism("Oct", 8, 0.5f, 0.5f, 1f);
            disc = LowPolyMeshes.Prism("Disc", 20, 0.5f, 0.5f, 0.02f);
            cone = LowPolyMeshes.Prism("Cone", 8, 0.5f, 0f, 1f);
            ball = LowPolyMeshes.Icosphere("Ball", 1, 0.5f);
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
            sun.transform.rotation = Quaternion.Euler(42f, 40f, 0f);

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

        static void BuildTower()
        {
            var root = new GameObject("Tower").transform;
            var concrete = Mat("Concrete", "#E4DED2");
            var accent = Mat("TowerAccent", "#EE6A55");
            var metal = Mat("Rail", "#F7F7F2", smoothness: 0.4f);

            float pillarH = PlatformTop - DeckTop - 0.25f;
            Box("Pillar", root, new Vector3(0f, DeckTop + pillarH * 0.5f, -3.9f), new Vector3(1.6f, pillarH, 1.4f), concrete);
            Box("Platform", root, new Vector3(0f, PlatformTop - 0.125f, -2.3f), new Vector3(2f, 0.25f, 4.6f), accent);
            Box("EdgeStripe", root, new Vector3(0f, PlatformTop + 0.005f, -0.08f), new Vector3(2f, 0.01f, 0.16f), metal);

            foreach (float x in new[] { -0.95f, 0.95f })
            {
                foreach (float z in new[] { -4.4f, -3.1f, -1.8f })
                    Put(hex, "Post", root, new Vector3(x, PlatformTop + 0.5f, z), new Vector3(0.06f, 1f, 0.06f), metal);
                Box("Rail", root, new Vector3(x, PlatformTop + 1f, -3.1f), new Vector3(0.06f, 0.06f, 2.7f), metal);
                Box("Rail_Mid", root, new Vector3(x, PlatformTop + 0.5f, -3.1f), new Vector3(0.04f, 0.04f, 2.7f), metal);
                Box("Ladder_Side", root, new Vector3(x * 0.35f, 2.6f, -4.7f), new Vector3(0.06f, 4.8f, 0.06f), metal);
            }
            for (float y = 0.7f; y < PlatformTop; y += 0.38f)
                Box("Rung", root, new Vector3(0f, y, -4.7f), new Vector3(0.66f, 0.04f, 0.05f), metal);
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

            for (int i = 0; i < 4; i++)
            {
                float z = 0.5f + i * 3.4f;
                Chair(root, new Vector3(8f, DeckTop, z), i % 2 == 0 ? teal : white, white);
                if (i < 3) Umbrella(root, new Vector3(9.3f, DeckTop, z + 1.7f), i % 2 == 0 ? coral : yellow);
            }

            var rng = new System.Random(3);
            Vector3[] trees =
            {
                new(10.6f, 0f, -6f), new(10.8f, 0f, 16.5f), new(-10.5f, 0f, -5f), new(-10.7f, 0f, 9f),
                new(16f, 0f, -2f), new(18f, 0f, 9f), new(15.5f, 0f, 19f), new(22f, 0f, 3f), new(24f, 0f, 14f)
            };
            foreach (var p in trees)
            {
                float s = 0.85f + (float)rng.NextDouble() * 0.45f;
                Tree(root, p, s, rng.Next(2) == 0 ? leaves : leavesDark, trunk);
            }

            Put(rough, "Hill_1", root, new Vector3(70f, -6f, -10f), new Vector3(60f, 26f, 50f), hill);
            Put(rough, "Hill_2", root, new Vector3(85f, -10f, 30f), new Vector3(80f, 36f, 60f), hill);
            Put(rough, "Hill_3", root, new Vector3(60f, -8f, 55f), new Vector3(50f, 24f, 40f), hill);
        }

        static void Chair(Transform root, Vector3 at, Material cushion, Material frame)
        {
            var c = new GameObject("DeckChair").transform;
            c.SetParent(root, false);
            c.localPosition = at;
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
            go.transform.position = ZoneCenter;

            var red = Mat("BuoyRed", "#FF4F5E", smoothness: 0.35f);
            var white = Mat("BuoyWhite", "#FFFFFF", smoothness: 0.35f);

            const int count = 14;
            var buoys = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * ZoneRadius, 0.05f, Mathf.Sin(a) * ZoneRadius);
                buoys[i] = Put(ball, "Buoy_" + i, go.transform, p, new Vector3(0.26f, 0.2f, 0.26f), i % 2 == 0 ? red : white);
            }

            var zone = go.AddComponent<LandingZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("radius").floatValue = ZoneRadius;
            var arr = so.FindProperty("buoys");
            arr.arraySize = count;
            for (int i = 0; i < count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = buoys[i];
            so.ApplyModifiedPropertiesWithoutUndo();
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
            var skin = Mat("Skin", "#F2C2A0");
            var trunks = Mat("Trunks", "#283B8F");
            var cap = Mat("SwimCap", "#FFC743", smoothness: 0.45f);
            var goggles = Mat("Goggles", "#1E2A4A", smoothness: 0.7f);

            var limbArm = LowPolyMeshes.Prism("Limb_Arm", 6, 0.055f, 0.072f, 0.56f, pivotTop: true);
            var limbThigh = LowPolyMeshes.Prism("Limb_Thigh", 6, 0.085f, 0.105f, 0.45f, pivotTop: true);
            var limbShin = LowPolyMeshes.Prism("Limb_Shin", 6, 0.065f, 0.085f, 0.45f, pivotTop: true);
            var torsoMesh = LowPolyMeshes.Prism("Torso", 8, 0.21f, 0.27f, 0.5f);

            var root = new GameObject("Diver");
            root.transform.position = new Vector3(0f, PlatformTop + 0.9f, -0.25f);

            var rig = new GameObject("Rig").transform;
            rig.SetParent(root.transform, false);

            Put(oct, "Hips", rig, new Vector3(0f, 0.04f, 0f), new Vector3(0.48f, 0.26f, 0.3f), trunks);

            var spine = Pivot("Spine", rig, new Vector3(0f, 0.12f, 0f));
            Put(torsoMesh, "Torso", spine, new Vector3(0f, 0.25f, 0f), new Vector3(1f, 1f, 0.64f), skin);
            Put(hex, "Neck", spine, new Vector3(0f, 0.54f, 0f), new Vector3(0.14f, 0.1f, 0.14f), skin);
            Put(ball, "Head", spine, new Vector3(0f, 0.73f, 0f), Vector3.one * 0.36f, skin);
            Put(ball, "Cap", spine, new Vector3(0f, 0.765f, -0.006f), new Vector3(0.38f, 0.3f, 0.38f), cap);
            Box("Goggles", spine, new Vector3(0f, 0.745f, 0.165f), new Vector3(0.27f, 0.055f, 0.05f), goggles);

            Transform Arm(string n, float x)
            {
                var p = Pivot(n, spine, new Vector3(x, 0.47f, 0f));
                Put(limbArm, n + "_Mesh", p, Vector3.zero, Vector3.one, skin);
                Put(ball, n + "_Hand", p, new Vector3(0f, -0.59f, 0f), Vector3.one * 0.13f, skin);
                return p;
            }
            var armL = Arm("Arm_L", -0.31f);
            var armR = Arm("Arm_R", 0.31f);

            (Transform thigh, Transform shin) Leg(string side, float x)
            {
                var thigh = Pivot("Thigh_" + side, rig, new Vector3(x, 0f, 0f));
                Put(limbThigh, "Thigh_" + side + "_Mesh", thigh, Vector3.zero, Vector3.one, skin);
                var shin = Pivot("Shin_" + side, thigh, new Vector3(0f, -0.45f, 0f));
                Put(limbShin, "Shin_" + side + "_Mesh", shin, Vector3.zero, Vector3.one, skin);
                Box("Foot_" + side, shin, new Vector3(0f, -0.47f, 0.06f), new Vector3(0.12f, 0.07f, 0.25f), skin);
                return (thigh, shin);
            }
            var legL = Leg("L", -0.12f);
            var legR = Leg("R", 0.12f);

            var pose = root.AddComponent<DiverPose>();
            Link(pose, "rig", rig);
            Link(pose, "spine", spine);
            Link(pose, "armL", armL);
            Link(pose, "armR", armR);
            Link(pose, "thighL", legL.thigh);
            Link(pose, "thighR", legR.thigh);
            Link(pose, "kneeL", legL.shin);
            Link(pose, "kneeR", legR.shin);

            var diver = root.AddComponent<Diver>();
            Link(diver, "pose", pose);
            return diver;
        }

        static void BuildCamera(Diver diver, LandingZone zone)
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
        }

        static void BuildHud(Diver diver)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("Hud");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

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
            var best = Label("Best", root, new Vector2(0f, 1f), new Vector2(28f, -22f), new Vector2(400f, 50f), 30, TextAnchor.UpperLeft);
            var turns = Label("Turns", root, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(600f, 130f), 96, TextAnchor.UpperCenter);
            var hint = Label("Hint", root, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1100f, 120f), 32, TextAnchor.LowerCenter);

            var panelGo = new GameObject("Result", typeof(RectTransform));
            panelGo.transform.SetParent(root, false);
            var panel = (RectTransform)panelGo.transform;
            panel.anchorMin = new Vector2(0f, 0.5f);
            panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(0f, 400f);
            panel.anchoredPosition = new Vector2(0f, 20f);
            var band = panelGo.AddComponent<Image>();
            band.color = new Color(0.1f, 0.14f, 0.26f, 0.42f);
            band.raycastTarget = false;

            var title = Label("Title", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1000f, 90f), 70, TextAnchor.MiddleCenter);
            var details = Label("Details", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(1000f, 50f), 32, TextAnchor.MiddleCenter, FontStyle.Normal);
            var score = Label("Score", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -45f), new Vector2(1000f, 120f), 104, TextAnchor.MiddleCenter);
            var tap = Label("Tap", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1000f, 40f), 28, TextAnchor.MiddleCenter, FontStyle.Normal);
            tap.text = "Нажми, чтобы прыгнуть ещё раз";

            var hud = canvasGo.AddComponent<DiveHud>();
            Link(hud, "diver", diver);
            Link(hud, "best", best);
            Link(hud, "turns", turns);
            Link(hud, "hint", hint);
            Link(hud, "resultPanel", panel);
            Link(hud, "resultTitle", title);
            Link(hud, "resultDetails", details);
            Link(hud, "resultScore", score);
            Link(hud, "resultTap", tap);
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
