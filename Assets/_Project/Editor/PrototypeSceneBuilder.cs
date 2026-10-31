using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JumpGame.EditorTools
{
    // Builds the whole game scene: every location, the diver, camera and UI. Safe to rerun: it overwrites the scene.
    // The pool and shared parts live here, the other locations and the level map in PrototypeSceneBuilder.Sites.cs.
    public static partial class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Pool_Prototype.unity";
        const string MaterialDir = "Assets/_Project/Materials/Prototype";

        const string LevelsPath = "Assets/_Project/Levels/PoolLevels.asset";

        const float DeckTop = 0.3f;
        const float PoolDepth = 3f;
        static readonly Vector2 PoolX = new(-5f, 5f);
        static readonly Vector2 PoolZ = new(-3f, 15f);
        const float TowerBack = -4.8f;

        // Each location sits on its own block; they stand side by side along X.
        const float PoolBlockHalf = 32f, PoolBlockZ = 6f, PoolBlockTop = -3.1f;
        // Placed towards -X: the map camera faces back at the towers, so -X is on the right of the screen and "next" comes in from the right.
        static readonly Vector3[] SiteOffsets = { Vector3.zero, new(-230f, 0f, 0f), new(-460f, 0f, 0f), new(-690f, 0f, 0f), new(-920f, 0f, 0f), new(-1150f, 0f, 0f), new(-1380f, 0f, 0f), new(-1610f, 0f, 0f) };

        // Platform heights, sideways position and how far the front edge reaches over the water.
        static readonly (float height, float x, float edgeZ)[] Platforms =
        {
            (3f, -2.6f, -0.9f),
            (5f, 2.6f, -0.4f),
            (7.5f, -2.6f, 0f),
            (10f, 0f, 0.3f)
        };

        // Meshes
        static Mesh cube, hex, rough, cone, drop;

        [MenuItem("Jump Game/Build Prototype Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            MakeMeshes();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            needsDiver.Clear();

            var sun = BuildAtmosphere();
            var pool = BuildPoolSite();
            var lake = BuildLakeSite();
            var cliffs = BuildCliffSite();
            var port = BuildPortSite();
            var arctic = BuildArcticSite();
            var town = BuildTownSite();
            var jungle = BuildJungleSite();
            var rig = BuildRigSite();

            var zone = BuildZone();
            var splash = BuildSplash();
            var diver = BuildDiver();
            Link(diver, "zone", zone);
            Link(diver, "splash", splash);
            Link(diver, "tower", pool.Tower);
            foreach (var (owner, field, asTransform) in needsDiver)
                Link(owner, field, asTransform ? diver.transform : (Object)diver);

            var cam = BuildCamera(diver, zone);

            var flow = new GameObject("Game").AddComponent<GameFlow>();
            Link(flow, "levels", LevelAsset());
            LinkArray(flow, "sites", new Object[] { pool, lake, cliffs, port, arctic, town, jungle, rig });
            Link(flow, "sun", sun);
            Link(flow, "diver", diver);
            Link(flow, "zone", zone);
            Link(flow, "cam", cam);

            BuildSounds(diver, flow);
            new GameObject("YandexGames").AddComponent<YandexGames>();

            var juice = flow.gameObject.AddComponent<DiveJuice>();
            Link(juice, "diver", diver);
            Link(juice, "view", cam);

            // All location blocks stay in the world side by side; the map flies between them.
            var flowSo = new SerializedObject(flow);
            flowSo.FindProperty("keepAllSitesActive").boolValue = true;
            flowSo.ApplyModifiedPropertiesWithoutUndo();

            var ui = BuildHud(diver, flow);
            BuildMap(ui, flow, diver, cam);

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
            cone = LowPolyMeshes.Prism("Cone", 8, 0.5f, 0f, 1f);
            rough = LowPolyMeshes.Icosphere("Rough", 1, 0.5f, 0.14f, 7);
            drop = LowPolyMeshes.Icosphere("Drop", 0, 0.5f);
        }

        static Light BuildAtmosphere()
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
            return sun;
        }

        static void BuildPool(Transform parent)
        {
            var root = Child("Pool", parent);
            var deck = Mat("Deck", "#D8C29E");
            var tile = Mat("PoolTile", "#6CC4D4");
            var line = Mat("PoolLine", "#245F92");
            var coping = Mat("Coping", "#F4F2EC");
            var water = Mat("Water", "#1C98B8", transparent: true, alpha: 0.82f, smoothness: 0.6f);

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

        static DivingTower BuildTower(Transform parent)
        {
            var root = Child("Tower", parent);
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

            // The deck around the pool is solid too, for a dive that falls short.
            void DeckSolid(float x0, float x1, float z0, float z1)
                => solids.Add(new Bounds(new Vector3((x0 + x1) * 0.5f, (DeckTop - PoolDepth) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, DeckTop + PoolDepth, z1 - z0)));
            DeckSolid(-12f, PoolX.x, -8f, 20f);
            DeckSolid(PoolX.y, 12f, -8f, 20f);
            DeckSolid(PoolX.x, PoolX.y, -8f, PoolZ.x);
            DeckSolid(PoolX.x, PoolX.y, PoolZ.y, 20f);

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

            // A starting block on the near end of the pool: the lowest, first dive of the game.
            var block = new Vector3(3.9f, (DeckTop + 0.8f) * 0.5f, PoolZ.x - 0.35f);
            var blockSize = new Vector3(0.6f, 0.8f - DeckTop, 0.7f);
            Box("StartingBlock", root, block, blockSize, accent);
            Box("StartingBlock_Top", root, new Vector3(3.9f, 0.805f, PoolZ.x - 0.35f), new Vector3(0.62f, 0.01f, 0.72f), metal);
            solids.Add(new Bounds(block, blockSize));
            var blockEdge = Mark("StartingBlock_Mark", root, new Vector3(3.9f, 0.8f, PoolZ.x));

            // A springboard on the west deck, jumping east across the pool.
            var boardWhite = Mat("Board_White", "#F4F2EC");
            var boardAqua = Mat("Board_Aqua", "#2BA39C", smoothness: 0.4f);
            Solid("BoardBase", root, new Vector3(-7.6f, 0.65f, 6f), new Vector3(1.2f, 0.7f, 0.8f), boardWhite, solids);
            Solid("Springboard", root, new Vector3(-5.9f, 0.95f, 6f), new Vector3(3.4f, 0.1f, 0.8f), boardAqua, solids);
            Box("BoardGrip", root, new Vector3(-5.9f, 1.006f, 6f), new Vector3(3.2f, 0.012f, 0.7f), Mat("Board_Grip", "#1E7F79"));
            var boardEdge = Mark("Springboard_Mark", root, new Vector3(-4.3f, 1f, 6f));
            boardEdge.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // A lifeguard chair on the east deck, jumping west.
            var chairWood = Mat("Chair_Wood", "#C9C2B2");
            var chairRed = Mat("Chair_Red", "#D9503F");
            foreach (float cx in new[] { 5.75f, 6.95f })
                foreach (float cz in new[] { 11.3f, 12.7f })
                    Solid("ChairLeg", root, new Vector3(cx, 1.35f, cz), new Vector3(0.14f, 2.1f, 0.14f), chairWood, solids);
            Solid("ChairSeat", root, new Vector3(6.35f, 2.3f, 12f), new Vector3(1.7f, 0.12f, 1.8f), chairRed, solids);
            Solid("ChairBack", root, new Vector3(7.1f, 2.9f, 12f), new Vector3(0.12f, 1.2f, 1.8f), chairRed, solids);
            Box("ChairRoofPost", root, new Vector3(7.0f, 3.2f, 11.2f), new Vector3(0.08f, 1.6f, 0.08f), chairWood);
            Box("ChairRoofPost", root, new Vector3(7.0f, 3.2f, 12.8f), new Vector3(0.08f, 1.6f, 0.08f), chairWood);
            Box("ChairRoof", root, new Vector3(6.6f, 4.05f, 12f), new Vector3(2.2f, 0.1f, 2.4f), chairRed);
            for (float y = 0.55f; y < 2.3f; y += 0.4f)
                Box("ChairRung", root, new Vector3(5.7f, y, 12f), new Vector3(0.05f, 0.04f, 0.8f), chairWood).localRotation = Quaternion.identity;
            var chairEdge = Mark("Chair_Mark", root, new Vector3(5.5f, 2.36f, 12f));
            chairEdge.localRotation = Quaternion.Euler(0f, -90f, 0f);

            var spots = new Transform[edges.Length + 3];
            spots[0] = blockEdge;
            for (int i = 0; i < edges.Length; i++) spots[i + 1] = edges[i];
            spots[edges.Length + 1] = boardEdge;
            spots[edges.Length + 2] = chairEdge;
            return MakeTower(root, spots, solids);
        }

        static DivingTower MakeTower(Transform root, Transform[] edges, System.Collections.Generic.List<Bounds> solids, float[] sides = null, float[] leans = null)
        {
            var tower = root.gameObject.AddComponent<DivingTower>();
            var so = new SerializedObject(tower);
            if (sides != null)
            {
                var sp = so.FindProperty("cameraSides");
                sp.arraySize = sides.Length;
                for (int i = 0; i < sides.Length; i++) sp.GetArrayElementAtIndex(i).floatValue = sides[i];
            }
            if (leans != null)
            {
                var lp = so.FindProperty("cameraLean");
                lp.arraySize = leans.Length;
                for (int i = 0; i < leans.Length; i++) lp.GetArrayElementAtIndex(i).floatValue = leans[i];
            }
            var arr = so.FindProperty("edges");
            arr.arraySize = edges.Length;
            for (int i = 0; i < edges.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = edges[i];
            var boxes = so.FindProperty("solids");
            boxes.arraySize = solids.Count;
            for (int i = 0; i < solids.Count; i++) boxes.GetArrayElementAtIndex(i).boundsValue = solids[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return tower;
        }

        static Transform Child(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Transform Mark(string name, Transform parent, Vector3 at)
        {
            var t = Child(name, parent);
            t.localPosition = at;
            return t;
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

            LevelInfo L(int location, int platform, float distance, float radius, float drift, TaskKind task, float turns = 0f) => new LevelInfo
            {
                location = location, platform = platform, zoneDistance = distance, zoneRadius = radius, zoneDrift = drift,
                task = task, taskTurns = turns
            };

            set.locations = new[]
            {
                "Городской бассейн", "Лесное озеро", "Скалы у моря", "Ночной порт",
                "Северная бухта", "Старый город", "Джунгли и водопад", "Нефтяная платформа"
            };
            // The last two are bought with coins.
            set.locationPrices = new[] { 0, 0, 0, 0, 0, 0, 1500, 2500 };

            // Five dives per location, each from its own spot and each higher than the last.
            // Pool: starting block 0.8 / 3 / 5 / 7.5 / 10 m. Lake: jetty 0.8 / tower arm 5 / tree 7 / tower top 9 / bridge 12 m.
            // Cliffs: yacht 3 / ledge 8 / house roof 11.5 / ledge 15 / lighthouse 18 m.
            // Medal thresholds are filled in by DiveCapture.TuneLevels from the best dive it can find.
            var layout = new[]
            {
                // Pool: block 0.8 / springboard 1 / lifeguard chair 2.4 / tower 3, 5, 7.5, 10 m. Platform numbers are the order the spots were built in.
                L(0, 0, 2.0f, 0.9f, 0f, TaskKind.HeadFirst),
                L(0, 5, 2.6f, 1.1f, 0f, TaskKind.FeetFirst),
                L(0, 6, 3.2f, 1.1f, 0f, TaskKind.Turns, 1f),
                L(0, 1, 3.0f, 1.3f, 0f, TaskKind.CannonballInZone),
                L(0, 2, 3.5f, 1.2f, 0f, TaskKind.Turns, 1.5f),
                L(0, 3, 4.0f, 1.1f, 0f, TaskKind.ZoneNoBuoys),
                L(0, 4, 4.6f, 1.1f, 0f, TaskKind.Turns, 2.5f),

                // Lake: rowboat 0.35 / jetty 0.8 / tower arm 5 / boulder 6 / tree 7 / tower top 9 / aqueduct 12 m.
                L(1, 5, 2.4f, 1.2f, 0f, TaskKind.FeetFirst),
                L(1, 0, 2.2f, 1.1f, 0f, TaskKind.NoTuck),
                L(1, 1, 3.8f, 1.2f, 0.9f, TaskKind.Bullseye),
                L(1, 6, 4.6f, 1.1f, 0f, TaskKind.HeadFirst),
                L(1, 2, 4.6f, 1.0f, 0f, TaskKind.Turns, 1.5f),
                L(1, 3, 5.0f, 1.1f, 0f, TaskKind.ZoneNoBuoys),
                L(1, 4, 6.2f, 1.0f, 1.2f, TaskKind.CleanTurns, 2.5f),

                // Cliffs: yacht 3 / sea stack 7 / ledge 8 / house roof 11.5 / tall stack 12 / ledge 15 / lighthouse 18 m.
                L(2, 0, 3.2f, 1.2f, 0f, TaskKind.Turns, 1f),
                L(2, 5, 4.8f, 1.1f, 0f, TaskKind.FeetFirst),
                L(2, 1, 5.0f, 1.1f, 0f, TaskKind.CannonballInZone),
                L(2, 2, 5.6f, 1.0f, 1.0f, TaskKind.CleanTurns, 2.5f),
                L(2, 6, 6.0f, 1.0f, 0f, TaskKind.Turns, 2f),
                L(2, 3, 6.0f, 1.0f, 0f, TaskKind.Turns, 3f),
                L(2, 4, 7.5f, 0.9f, 1.3f, TaskKind.Bullseye),

                // Night port: quay 1.2 / barge 2.2 / tug 4.8 / containers 9 / harbour light 12 / crane catwalk 15 / crane boom 23.
                L(3, 0, 2.6f, 1.1f, 0f, TaskKind.HeadFirst),
                L(3, 5, 3.2f, 1.1f, 0f, TaskKind.FeetFirst),
                L(3, 1, 5.4f, 1.2f, 0f, TaskKind.Turns, 1f),
                L(3, 2, 5.6f, 1.1f, 0f, TaskKind.CannonballInZone),
                L(3, 6, 6.2f, 1.0f, 0f, TaskKind.ZoneNoBuoys),
                L(3, 3, 6.8f, 1.0f, 1.0f, TaskKind.CleanTurns, 2f),
                L(3, 4, 8.6f, 0.95f, 0f, TaskKind.Bullseye),

                // Northern bay: floe 0.6 / icebreaker 3.4 / small berg 5.5 / berg 8 / glacier ledge 14 / ice tower 16 / ice arch 20 m.
                L(4, 0, 2.4f, 1.1f, 0f, TaskKind.FeetFirst),
                L(4, 1, 4.8f, 1.1f, 0.8f, TaskKind.ZoneNoBuoys),
                L(4, 5, 4.8f, 1.1f, 0f, TaskKind.Turns, 1.5f),
                L(4, 2, 5.4f, 1.0f, 0f, TaskKind.NoTuck),
                L(4, 3, 6.4f, 1.0f, 1.1f, TaskKind.CleanTurns, 2f),
                L(4, 6, 7.0f, 1.0f, 0f, TaskKind.CannonballInZone),
                L(4, 4, 7.8f, 0.95f, 0f, TaskKind.Turns, 3f),

                // Old town: steps 0.9 / bridge 5 / far balcony 7 / balcony 9 / bell tower 15 / clock tower 18 / dome 22 m.
                L(5, 0, 2.4f, 1.0f, 0f, TaskKind.HeadFirst),
                L(5, 1, 4.4f, 1.1f, 0f, TaskKind.Turns, 1.5f),
                L(5, 5, 4.8f, 1.1f, 0f, TaskKind.FeetFirst),
                L(5, 2, 5.4f, 1.0f, 0.9f, TaskKind.CannonballInZone),
                L(5, 3, 6.6f, 1.0f, 0f, TaskKind.CleanTurns, 2.5f),
                L(5, 6, 7.0f, 1.0f, 0f, TaskKind.Turns, 3f),
                L(5, 4, 8.2f, 0.95f, 1.2f, TaskKind.Bullseye),

                // Jungle falls (paid): dock 1.2 / totem rock 4.1 / ledge 6 / idol island 9 / treehouse 11 / temple 16 / falls 24 m.
                L(6, 0, 2.8f, 1.1f, 0f, TaskKind.Turns, 1f),
                L(6, 6, 3.6f, 1.1f, 0f, TaskKind.HeadFirst),
                L(6, 1, 5.0f, 1.1f, 0f, TaskKind.FeetFirst),
                L(6, 5, 5.4f, 1.0f, 0f, TaskKind.Turns, 2f),
                L(6, 2, 6.4f, 1.0f, 0.8f, TaskKind.CleanTurns, 1.5f),
                L(6, 3, 7.2f, 1.0f, 0f, TaskKind.CannonballInZone),
                L(6, 4, 8.8f, 0.95f, 1.2f, TaskKind.Bullseye),

                // Offshore rig (paid): landing 2.5 / supply vessel 3.2 / walkway 8 / deck 13 / nav tower 14 / helideck 19 / crane boom 27 m.
                L(7, 0, 3.2f, 1.1f, 0f, TaskKind.HeadFirst),
                L(7, 5, 4.0f, 1.1f, 0f, TaskKind.FeetFirst),
                L(7, 1, 5.0f, 1.1f, 0f, TaskKind.CleanTurns, 1.5f),
                L(7, 2, 6.4f, 1.0f, 0.8f, TaskKind.Turns, 2.5f),
                L(7, 6, 6.6f, 1.0f, 0f, TaskKind.CannonballInZone),
                L(7, 3, 8.0f, 1.0f, 0f, TaskKind.Turns, 3f),
                L(7, 4, 9.8f, 0.9f, 1.0f, TaskKind.Bullseye)
            };

            // Keep tuned thresholds for levels whose layout did not change.
            var old = set.levels;
            for (int i = 0; i < layout.Length; i++)
            {
                var o = old != null && i < old.Length ? old[i] : null;
                bool same = o != null && o.location == layout[i].location && o.platform == layout[i].platform && o.task == layout[i].task
                    && Mathf.Approximately(o.zoneDistance, layout[i].zoneDistance)
                    && Mathf.Approximately(o.zoneRadius, layout[i].zoneRadius)
                    && Mathf.Approximately(o.zoneDrift, layout[i].zoneDrift);
                if (same)
                {
                    layout[i].bronze = o.bronze;
                    layout[i].silver = o.silver;
                    layout[i].gold = o.gold;
                }
            }

            set.levels = layout;
            EditorUtility.SetDirty(set);
            return set;
        }

        static void BuildSurroundings(Transform parent)
        {
            var root = Child("Surroundings", parent);
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

            // Lawn out to the edges of the location block, solid down to the block's base.
            float lawnY = (0.2f + PoolBlockTop) * 0.5f, lawnH = 0.2f - PoolBlockTop;
            float bx0 = -PoolBlockHalf, bx1 = PoolBlockHalf, bz0 = PoolBlockZ - PoolBlockHalf, bz1 = PoolBlockZ + PoolBlockHalf;
            Box("Grass_E", root, new Vector3((12f + bx1) * 0.5f, lawnY, PoolBlockZ), new Vector3(bx1 - 12f, lawnH, bz1 - bz0), grass);
            Box("Grass_W", root, new Vector3((bx0 - 12f) * 0.5f, lawnY, PoolBlockZ), new Vector3(-12f - bx0, lawnH, bz1 - bz0), grass);
            Box("Grass_N", root, new Vector3(0f, lawnY, (20f + bz1) * 0.5f), new Vector3(24f, lawnH, bz1 - 20f), grass);
            Box("Grass_S", root, new Vector3(0f, lawnY, (bz0 - 8f) * 0.5f), new Vector3(24f, lawnH, -8f - bz0), grass);

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

                // Gentle mounds in the far corners of the block instead of distant hills.
                Put(rough, "Mound", root, new Vector3(25f * side, -1.2f, 31f), new Vector3(13f, 5f, 11f), hill);
                Put(rough, "Mound", root, new Vector3(26f * side, -1.4f, -18f), new Vector3(10f, 4.4f, 12f), hill);
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
            var model = Prop("Trees", "Tree_" + "ABC"[Variant(at, 3)], root, at, Vector3.one * s, Variant(at, 360) * 1f,
                             ("Crown", crown), ("Bark", bark));
            if (model != null) return;
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

            // A faint warm ring marks the centre bonus; the zone scales it to the level's radius.
            var markMat = Mat("CenterMark", "#FFFFFF", shader: "Legacy Shaders/Particles/Alpha Blended");
            markMat.SetColor("_TintColor", new Color(0.6f, 0.52f, 0.18f, 0.3f));
            var mark = Put(LowPolyMeshes.SoftRing("CenterRing", 0.28f, 0.5f, 40, 4), "CenterMark", go.transform,
                           new Vector3(0f, 0.012f, 0f), Vector3.one, markMat);
            mark.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Floats are created by the zone itself for each level's radius.
            var zone = go.AddComponent<LandingZone>();
            Link(zone, "rope", rope);
            Link(zone, "floatMesh", floatMesh);
            Link(zone, "floatRed", red);
            Link(zone, "floatWhite", white);
            Link(zone, "centerMark", mark);
            return zone;
        }

        static Splash BuildSplash()
        {
            var spray = Mat("Spray", "#FFFFFF", shader: "Legacy Shaders/Particles/Alpha Blended");
            spray.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));

            var go = new GameObject("Splash");

            ParticleSystem Particles(string n, float angle, float radius, Vector2 life, Vector2 speed, Vector2 size, float gravity, Color color)
            {
                var child = new GameObject(n);
                child.transform.SetParent(go.transform, false);
                child.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                var ps = child.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = ps.main;
                main.loop = false;
                main.playOnAwake = false;
                main.duration = 1f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
                main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
                main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
                main.startRotation3D = true;
                main.startColor = color;
                main.gravityModifier = gravity;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 300;

                var emission = ps.emission;
                emission.rateOverTime = 0f;

                var shape = ps.shape;
                if (angle > 0f)
                {
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = angle;
                    shape.radius = radius;
                }
                else
                {
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = radius;
                }

                var fade = ps.colorOverLifetime;
                fade.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
                fade.color = g;

                var shrink = ps.sizeOverLifetime;
                shrink.enabled = true;
                shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));

                var renderer = child.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = drop;
                renderer.sharedMaterial = spray;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                return ps;
            }

            var white = new Color(0.95f, 0.99f, 1f, 0.95f);
            var crown = Particles("Crown", 32f, 0.2f, new Vector2(0.5f, 1f), new Vector2(0.55f, 1f), new Vector2(0.6f, 1.4f), 1.4f, white);
            var jet = Particles("Jet", 7f, 0.08f, new Vector2(0.6f, 1f), new Vector2(0.7f, 1f), new Vector2(0.06f, 0.12f), 1.2f, white);
            var bubbles = Particles("Bubbles", 0f, 0.35f, new Vector2(0.8f, 1.8f), new Vector2(0.1f, 0.35f), new Vector2(0.04f, 0.1f), -0.12f, new Color(0.85f, 0.97f, 1f, 0.7f));

            var foamMesh = LowPolyMeshes.SoftRing("SoftFoam", 0f, 0.5f, 40, 6);
            var ringMesh = LowPolyMeshes.SoftRing("SoftRipple", 0.38f, 0.5f, 48, 4);

            Renderer Flat(Mesh mesh, string n, float y)
            {
                var t = Put(mesh, n, go.transform, new Vector3(0f, y, 0f), Vector3.one, spray);
                var r = t.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.enabled = false;
                return r;
            }

            var foam = Flat(foamMesh, "Foam", 0.012f);
            var ripples = new Renderer[3];
            for (int i = 0; i < ripples.Length; i++) ripples[i] = Flat(ringMesh, "Ripple_" + i, 0.016f + i * 0.002f);

            var splash = go.AddComponent<Splash>();
            Link(splash, "crown", crown);
            Link(splash, "jet", jet);
            Link(splash, "bubbles", bubbles);
            Link(splash, "foam", foam);
            LinkArray(splash, "ripples", ripples);
            return splash;
        }

        static Diver BuildDiver()
        {
            // The materials only set the first look; DiverSkin tints private copies of them per character.
            var skin = Mat("Skin", "#EFBC98", smoothness: 0.3f);
            var trunks = Mat("Trunks", "#283B8F", smoothness: 0.3f);
            var cap = Mat("SwimCap", "#FFC743", smoothness: 0.55f);
            var strap = Mat("Goggles", "#1E2A4A", smoothness: 0.6f);
            var lens = Mat("Lens", "#7FD3F0", smoothness: 0.9f);
            var shirtMat = Mat("Shirt", "#F4F1EA", smoothness: 0.25f);
            var hairMat = Mat("Hair", "#3A2A20", smoothness: 0.2f);
            var accentMat = Mat("Accent", "#C8553D", smoothness: 0.4f);
            var metalMat = Mat("Metal", "#BFC6CE", smoothness: 0.8f);

            var tinted = new System.Collections.Generic.List<(Renderer, SkinRole)>();
            var worn = new System.Collections.Generic.List<(GameObject, Gear)>();
            var widen = new System.Collections.Generic.List<Transform>();
            var lensParts = new System.Collections.Generic.List<Transform>();
            Transform Tint(Transform t, SkinRole role)
            {
                tinted.Add((t.GetComponent<Renderer>(), role));
                return t;
            }
            Transform Wear(Transform t, Gear g)
            {
                worn.Add((t.gameObject, g));
                return t;
            }

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
            var hairMesh = LowPolyMeshes.Sphere("Body_Hair", 0.156f, 16, 8, 0f, 118f);
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
            var tankMesh = LowPolyMeshes.Capsule("Body_Tank", 0.07f, 0.07f, 0.34f);
            var ringMesh = LowPolyMeshes.Torus("Body_Ring", 0.31f, 0.075f, 20, 8);

            var root = new GameObject("Diver");
            // Placed on the 5 m platform; GameFlow moves it to the current level's platform.
            root.transform.position = new Vector3(Platforms[1].x, Platforms[1].height + 0.9f, Platforms[1].edgeZ - 0.25f);

            var rig = new GameObject("Rig").transform;
            rig.SetParent(root.transform, false);
            widen.Add(Tint(Put(trunksMesh, "Trunks", rig, Vector3.zero, new Vector3(1f, 1f, 0.72f), trunks), SkinRole.Outfit));
            var ring = Put(ringMesh, "SwimRing", rig, new Vector3(0f, 0.03f, 0f), new Vector3(1f, 1f, 0.85f), accentMat);
            ring.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Wear(Tint(ring, SkinRole.Accent), Gear.SwimRing);

            var spine = Pivot("Spine", rig, new Vector3(0f, 0.1f, 0f));
            widen.Add(Tint(Put(torsoMesh, "Torso", spine, Vector3.zero, new Vector3(1f, 1f, 0.66f), skin), SkinRole.Skin));
            widen.Add(Wear(Tint(Put(torsoMesh, "Shirt", spine, Vector3.zero, new Vector3(1.05f, 1.0f, 0.7f), shirtMat), SkinRole.Shirt), Gear.Shirt));
            Tint(Put(neck, "Neck", spine, new Vector3(0f, 0.52f, 0f), Vector3.one, skin), SkinRole.Face);
            Wear(Tint(Put(cube, "Backpack", spine, new Vector3(0f, 0.2f, -0.2f), new Vector3(0.3f, 0.34f, 0.13f), accentMat), SkinRole.Accent), Gear.Backpack);
            foreach (float x in new[] { -0.085f, 0.085f })
                Wear(Tint(Put(tankMesh, "Tank", spine, new Vector3(x, 0.18f, -0.2f), Vector3.one, accentMat), SkinRole.Accent), Gear.Tank);

            var headPivot = Pivot("Head", spine, new Vector3(0f, 0.63f, 0.01f));
            Tint(Put(head, "Head_Mesh", headPivot, Vector3.zero, new Vector3(0.95f, 1.06f, 1f), skin), SkinRole.Face);
            var capT = Put(capMesh, "Cap", headPivot, new Vector3(0f, 0.012f, -0.006f), new Vector3(1f, 1.04f, 1.02f), cap);
            capT.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            Wear(Tint(capT, SkinRole.Cap), Gear.SwimCap);
            var bandT = Put(band, "GoggleStrap", headPivot, new Vector3(0f, -0.004f, 0f), new Vector3(0.98f, 1f, 1.02f), strap);
            bandT.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            Wear(Tint(bandT, SkinRole.Strap), Gear.Goggles | Gear.Mask);
            foreach (float x in new[] { -0.05f, 0.05f })
            {
                var l = Put(lensMesh, "Lens", headPivot, new Vector3(x, 0.004f, 0.134f), new Vector3(1.15f, 0.8f, 0.5f), lens);
                Wear(Tint(l, SkinRole.Lens), Gear.Goggles | Gear.Eyes);
                lensParts.Add(l);
            }

            // Hair, beard and hats.
            var hairT = Put(hairMesh, "Hair", headPivot, new Vector3(0f, 0.004f, -0.01f), new Vector3(0.97f, 1.05f, 1.02f), hairMat);
            hairT.localRotation = Quaternion.Euler(-34f, 0f, 0f);
            Wear(Tint(hairT, SkinRole.Hair), Gear.Hair);
            Wear(Tint(Put(joint, "Beard", headPivot, new Vector3(0f, -0.1f, 0.065f), new Vector3(1.75f, 1.2f, 1.35f), hairMat), SkinRole.Hair), Gear.Beard);
            var tail = Put(upperArm, "Ponytail", headPivot, new Vector3(0f, -0.05f, -0.16f), new Vector3(0.65f, 0.85f, 0.65f), hairMat);
            tail.localRotation = Quaternion.Euler(25f, 0f, 0f);
            Wear(Tint(tail, SkinRole.Hair), Gear.Ponytail);

            Wear(Tint(Put(hex, "HatBrim", headPivot, new Vector3(0f, 0.115f, 0f), new Vector3(0.5f, 0.022f, 0.5f), accentMat), SkinRole.Accent), Gear.PirateHat);
            Wear(Tint(Put(cone, "HatCrown", headPivot, new Vector3(0f, 0.2f, 0f), new Vector3(0.32f, 0.2f, 0.32f), accentMat), SkinRole.Accent), Gear.PirateHat);
            Wear(Tint(Put(joint, "Eyepatch", headPivot, new Vector3(0.05f, 0.016f, 0.128f), new Vector3(0.95f, 0.75f, 0.3f), strap), SkinRole.Strap), Gear.Eyepatch);
            var patchBand = Put(band, "EyepatchBand", headPivot, new Vector3(0f, 0.004f, 0f), new Vector3(0.98f, 0.6f, 1.02f), strap);
            patchBand.localRotation = Quaternion.Euler(-18f, 0f, 22f);
            Wear(Tint(patchBand, SkinRole.Strap), Gear.Eyepatch);
            Wear(Tint(Put(cube, "Sunglasses", headPivot, new Vector3(0f, 0.012f, 0.14f), new Vector3(0.17f, 0.045f, 0.03f), strap), SkinRole.Strap), Gear.Sunglasses);
            Wear(Tint(Put(cube, "MaskFrame", headPivot, new Vector3(0f, 0.008f, 0.136f), new Vector3(0.18f, 0.09f, 0.05f), strap), SkinRole.Strap), Gear.Mask);
            Wear(Tint(Put(cube, "MaskGlass", headPivot, new Vector3(0f, 0.008f, 0.162f), new Vector3(0.145f, 0.06f, 0.012f), lens), SkinRole.Lens), Gear.Mask);
            Wear(Tint(Put(hex, "AntennaPole", headPivot, new Vector3(0f, 0.2f, 0f), new Vector3(0.016f, 0.12f, 0.016f), metalMat), SkinRole.Metal), Gear.Antenna);
            Wear(Tint(Put(lensMesh, "AntennaTip", headPivot, new Vector3(0f, 0.27f, 0f), new Vector3(1.3f, 1.3f, 1.3f), accentMat), SkinRole.Accent), Gear.Antenna);

            (Transform arm, Transform fore) Arm(string side, float x)
            {
                Tint(Put(joint, "Shoulder_" + side, spine, new Vector3(x * 0.93f, 0.4f, 0f), Vector3.one, skin), SkinRole.Skin);
                var arm = Pivot("Arm_" + side, spine, new Vector3(x, 0.4f, 0f));
                Tint(Put(upperArm, "UpperArm_" + side, arm, Vector3.zero, Vector3.one, skin), SkinRole.Skin);
                var fore = Pivot("ForeArm_" + side, arm, new Vector3(0f, -0.27f, 0f));
                Tint(Put(foreArm, "ForeArm_" + side + "_Mesh", fore, Vector3.zero, Vector3.one, skin), SkinRole.Skin);
                Tint(Put(hand, "Hand_" + side, fore, new Vector3(0f, -0.29f, 0f), new Vector3(0.9f, 1.3f, 0.6f), skin), SkinRole.Skin);
                return (arm, fore);
            }
            var armL = Arm("L", -0.235f);
            var armR = Arm("R", 0.235f);

            Transform shinRight = null;
            (Transform thigh, Transform shin, Transform ankle) Leg(string side, float x)
            {
                var thigh = Pivot("Thigh_" + side, rig, new Vector3(x, 0.02f, 0f));
                Tint(Put(thighMesh, "Thigh_" + side + "_Mesh", thigh, Vector3.zero, Vector3.one, skin), SkinRole.Skin);
                var shin = Pivot("Shin_" + side, thigh, new Vector3(0f, -0.44f, 0f));
                var shinVisual = Put(shinMesh, "Shin_" + side + "_Mesh", shin, Vector3.zero, Vector3.one, skin);
                Tint(shinVisual, side == "R" ? SkinRole.ShinR : SkinRole.Skin);
                if (side == "R") shinRight = shinVisual;
                var ankle = Pivot("Ankle_" + side, shin, new Vector3(0f, -0.425f, 0f));
                var foot = Put(footMesh, "Foot_" + side, ankle, new Vector3(0f, 0f, -0.05f), Vector3.one, skin);
                foot.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                Tint(foot, side == "R" ? SkinRole.ShinR : SkinRole.Skin);
                return (thigh, shin, ankle);
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
            Link(pose, "ankleL", legL.ankle);
            Link(pose, "ankleR", legR.ankle);

            var dress = root.AddComponent<DiverSkin>();
            var dressSo = new SerializedObject(dress);
            var tintedProp = dressSo.FindProperty("tinted");
            tintedProp.arraySize = tinted.Count;
            for (int i = 0; i < tinted.Count; i++)
            {
                var e = tintedProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("renderer").objectReferenceValue = tinted[i].Item1;
                e.FindPropertyRelative("role").enumValueIndex = (int)tinted[i].Item2;
            }
            var gearProp = dressSo.FindProperty("gear");
            gearProp.arraySize = worn.Count;
            for (int i = 0; i < worn.Count; i++)
            {
                var e = gearProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("part").objectReferenceValue = worn[i].Item1;
                e.FindPropertyRelative("gear").intValue = (int)worn[i].Item2;
                worn[i].Item1.SetActive((Skins.All[0].gear & worn[i].Item2) != 0);
            }
            dressSo.ApplyModifiedPropertiesWithoutUndo();
            LinkArray(dress, "widen", widen.ToArray());
            LinkArray(dress, "lenses", lensParts.ToArray());
            Link(dress, "shinR", shinRight);

            // Authored models from Blender, when they exist: they take over the look while the joints above keep driving them.
            var modelHost = root.AddComponent<DiverModel>();
            Link(modelHost, "pose", pose);
            var prefabs = new System.Collections.Generic.List<Object>();
            foreach (var n in new[] { "Diver", "Robot" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Models/Characters/{n}.fbx");
                if (prefab != null) prefabs.Add(prefab);
            }
            LinkArray(modelHost, "models", prefabs.ToArray());
            var placeholders = new System.Collections.Generic.List<Object>();
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true)) placeholders.Add(r);
            LinkArray(modelHost, "placeholder", placeholders.ToArray());
            Link(dress, "model", modelHost);

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
            cam.nearClipPlane = 0.9f;
            cam.farClipPlane = 320f;
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

        // A browser has no system fonts, so the text carries its own (Inter, SIL OFL) for Cyrillic.
        static Font UiFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Fonts/Inter-Medium.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static UiKit BuildHud(Diver diver, GameFlow flow)
        {
            var font = UiFont();
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
                rt.gameObject.AddComponent<ButtonPop>();
                var b = rt.gameObject.AddComponent<Button>();
                var colors = b.colors;
                colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
                colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f);
                colors.fadeDuration = 0.12f;
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

            RectTransform Stretch(string n, Transform parent)
            {
                var go = new GameObject(n, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                return rt;
            }

            var root = Stretch("HudRoot", canvasGo.transform);
            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);
            var center = new Vector2(0.5f, 0.5f);

            // Level selector, top centre.
            var levelTitle = Label("LevelTitle", root, top, new Vector2(0f, -22f), new Vector2(560f, 50f), 28, TextAnchor.MiddleCenter);
            var prev = Btn("Prev", root, top, new Vector2(-70f, -74f), new Vector2(56f, 46f), "<", "#2BA39C", 30);
            var next = Btn("Next", root, top, new Vector2(70f, -74f), new Vector2(56f, 46f), ">", "#2BA39C", 30);

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
            var taskText = Label("TaskText", root, topLeft, new Vector2(64f, -70f), new Vector2(440f, 34f), 22, TextAnchor.MiddleLeft);
            var menuButton = Btn("Menu", root, topRight, new Vector2(-24f, -18f), new Vector2(120f, 56f), "Меню", "#2BA39C", 26);
            var restart = Btn("Restart", root, topRight, new Vector2(-154f, -18f), new Vector2(150f, 56f), "Заново", "#EE6A55", 26);
            var best = Label("Best", root, topRight, new Vector2(-28f, -84f), new Vector2(360f, 40f), 26, TextAnchor.MiddleRight);
            var coinDot = Dot("CoinDot", root, topRight, new Vector2(-24f, -124f), 26f);
            coinDot.color = new Color(1f, 0.8f, 0.26f);
            var coinCounter = Label("CoinCounter", root, topRight, new Vector2(-58f, -120f), new Vector2(150f, 34f), 26, TextAnchor.MiddleRight);

            var turns = Label("Turns", root, top, new Vector2(0f, -90f), new Vector2(600f, 130f), 96, TextAnchor.UpperCenter);
            var hint = Label("Hint", root, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1100f, 120f), 32, TextAnchor.LowerCenter);

            // Result panel.
            var panelGo = new GameObject("Result", typeof(RectTransform));
            panelGo.transform.SetParent(root, false);
            var panel = (RectTransform)panelGo.transform;
            panel.anchorMin = new Vector2(0f, 0.5f);
            panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = center;
            panel.sizeDelta = new Vector2(0f, 520f);
            panel.anchoredPosition = new Vector2(0f, 0f);
            var band = panelGo.AddComponent<Image>();
            band.color = new Color(0.1f, 0.14f, 0.26f, 0.45f);
            band.raycastTarget = false;

            var title = Label("Title", panel, center, new Vector2(0f, 205f), new Vector2(1000f, 80f), 62, TextAnchor.MiddleCenter);
            var details = Label("Details", panel, center, new Vector2(0f, 148f), new Vector2(1000f, 44f), 28, TextAnchor.MiddleCenter, FontStyle.Normal);
            var score = Label("Score", panel, center, new Vector2(0f, 78f), new Vector2(1000f, 110f), 92, TextAnchor.MiddleCenter);

            // Medals on the left, the judges' cards on the right.
            var medals = new Image[3];
            for (int i = 0; i < 3; i++)
                medals[i] = Dot("Medal_" + i, panel, center, new Vector2(-250f + i * 62f, -2f), 48f);
            var judgesLabel = Label("JudgesLabel", panel, center, new Vector2(185f, 42f), new Vector2(300f, 30f), 20, TextAnchor.MiddleCenter, FontStyle.Normal);
            judgesLabel.text = "оценки судей";
            var cards = new RectTransform[3];
            var cardTexts = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                cards[i] = Rect("Judge_" + i, panel, center, new Vector2(95f + i * 90f, -2f), new Vector2(76f, 58f));
                var bg = cards[i].gameObject.AddComponent<Image>();
                bg.sprite = pill;
                bg.type = Image.Type.Sliced;
                bg.color = Hex("#FBF8F0");
                bg.raycastTarget = false;
                var sh = cards[i].gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0.1f, 0.13f, 0.25f, 0.45f);
                sh.effectDistance = new Vector2(0f, -4f);
                cardTexts[i] = Label("Value", cards[i], center, Vector2.zero, new Vector2(76f, 58f), 30, TextAnchor.MiddleCenter);
                Object.DestroyImmediate(cardTexts[i].GetComponent<Outline>());
                Object.DestroyImmediate(cardTexts[i].GetComponent<Shadow>());
            }

            var task = Label("Task", panel, center, new Vector2(0f, -92f), new Vector2(1000f, 40f), 26, TextAnchor.MiddleCenter);
            var coinsEarned = Label("CoinsEarned", panel, center, new Vector2(0f, -52f), new Vector2(900f, 34f), 24, TextAnchor.MiddleCenter);
            coinsEarned.color = new Color(1f, 0.82f, 0.3f);
            var retry = Btn("Retry", panel, center, new Vector2(-130f, -150f), new Vector2(220f, 70f), "Ещё раз", "#2BA39C", 30);
            var nextLevel = Btn("NextLevel", panel, center, new Vector2(130f, -150f), new Vector2(220f, 70f), "Дальше", "#EE6A55", 30);
            var doubleCoins = Btn("DoubleCoins", panel, center, new Vector2(0f, -224f), new Vector2(430f, 56f), "Монеты ×2 за видео", "#C98A1E", 26);

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
            LinkArray(hud, "judgeCards", cards);
            LinkArray(hud, "judgeTexts", cardTexts);
            Link(hud, "resultTask", task);
            Link(hud, "resultCoins", coinsEarned);
            Link(hud, "coinCounter", coinCounter);
            Link(hud, "restartButton", restart);
            Link(hud, "doubleCoinsButton", doubleCoins);
            Link(hud, "retryButton", retry);
            Link(hud, "nextLevelButton", nextLevel);

            // "+200 / Оборот!" popups.
            var popupArea = Stretch("Popups", root);
            var popupTemplate = Label("Popup", popupArea, center, Vector2.zero, new Vector2(320f, 110f), 28, TextAnchor.MiddleCenter);
            popupTemplate.lineSpacing = 0.85f;
            var popups = popupArea.gameObject.AddComponent<TurnPopups>();
            Link(popups, "diver", diver);
            Link(popups, "view", Object.FindAnyObjectByType<Camera>());
            Link(popups, "template", popupTemplate);

            return new UiKit
            {
                canvas = canvasGo.transform, hud = root.gameObject, menuButton = menuButton,
                Stretch = Stretch, Rect = Rect, Btn = Btn, Label = Label, knob = knob, pill = pill
            };
        }

        delegate RectTransform StretchFn(string n, Transform parent);
        delegate RectTransform RectFn(string n, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size);
        delegate Button ButtonFn(string n, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label, string color, int fontSize);
        delegate Text LabelFn(string n, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, FontStyle style = FontStyle.Bold);

        // What the level map needs from the HUD build: where to put things and the same widget makers.
        struct UiKit
        {
            public Transform canvas;
            public GameObject hud;
            public Button menuButton;
            public StretchFn Stretch;
            public RectFn Rect;
            public ButtonFn Btn;
            public LabelFn Label;
            public Sprite knob, pill;
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
