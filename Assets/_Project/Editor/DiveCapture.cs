using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace JumpGame.EditorTools
{
    // Simulates scripted dives in edit mode and renders contact sheets into Logs/ for a quick look at the motion.
    // Also searches for the best dive on every level to set the medal thresholds.
    public static class DiveCapture
    {
        const int FrameW = 480, FrameH = 270, Columns = 4;
        const float Dt = 1f / 60f;

        struct Scenario
        {
            public string name;
            public int level;
            public Func<float, bool> held;
            public int skin;
            public float[] frames;
        }

        static Diver diver;
        static LandingZone zone;
        static DiveCamera follow;
        static Camera cam;
        static GameFlow flow;
        static DiveHud hud;
        static MapScreen map;
        static Splash splash;
        static ShopScreen shop;
        static TurnPopups popups;
        static DiveOutcome? lastOutcome;

        public static void BuildAndCapture()
        {
            PrototypeSceneBuilder.Build();
            Open();
            TuneLevels();
            Capture();
        }

        public static void BuildAndCaptureQuick()
        {
            PrototypeSceneBuilder.Build();
            Open();
            Capture();
        }

        [MenuItem("Jump Game/Capture Dive Frames")]
        public static void Run()
        {
            Open();
            Capture();
        }

        public static void BuildAndMeasure()
        {
            PrototypeSceneBuilder.Build();
            MeasureJumps();
            Capture();
        }

        public static void MeasureJumps()
        {
            Open();
            foreach (int level in new[] { 0, 2, 6 })
            {
                flow.Load(level);
                diver.ResetToStart();
                diver.Step(Dt, false);

                var pose = diver.GetComponent<DiverPose>();
                var parts = new[] { "Spine/Head", "Spine", "Thigh_L/Shin_L/Ankle_L/Foot_L" };
                var rig = diver.transform.Find("Rig");
                var prev = new Vector3[parts.Length];
                var sb = new StringBuilder($"JUMPS level {level + 1}\n");
                var worst = new System.Collections.Generic.List<(float jump, string line)>();

                var tower = flow.Site(flow.Level.location).Tower;
                var contacts = new Vector3[DiverPose.ContactCount];
                for (float t = 0f; t < 3f; t += Dt)
                {
                    var state = diver.Current;
                    if (state == Diver.State.Crouching || (state == Diver.State.Airborne && t < 1.5f))
                    {
                        pose.ContactPoints(contacts);
                        for (int c = 0; c < contacts.Length; c++)
                            if (tower.Penetration(contacts[c], out var n, out float depth) && depth > 0.01f)
                                sb.Append($"  t={t:0.000} {state} contact {c} inside tower by {depth * 100f:0.0} cm, normal {n}\n");
                    }
                    diver.Step(Dt, true);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        var p = rig.Find(parts[i]).position;
                        if (t > 0f)
                        {
                            float d = (p - prev[i]).magnitude;
                            worst.Add((d, $"  t={t:0.000} {state}->{diver.Current} {parts[i]} moved {d * 100f:0.0} cm"));
                        }
                        prev[i] = p;
                    }
                    if (diver.Current == Diver.State.Underwater) break;
                }

                worst.Sort((a, b) => b.jump.CompareTo(a.jump));
                for (int i = 0; i < 8 && i < worst.Count; i++) sb.Append(worst[i].line).Append('\n');
                Debug.Log(sb.ToString());
            }
        }

        [MenuItem("Jump Game/Tune Level Medals")]
        public static void Tune()
        {
            Open();
            TuneLevels();
        }

        static void Open()
        {
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
            diver = UnityEngine.Object.FindAnyObjectByType<Diver>();
            zone = UnityEngine.Object.FindAnyObjectByType<LandingZone>();
            follow = UnityEngine.Object.FindAnyObjectByType<DiveCamera>();
            flow = UnityEngine.Object.FindAnyObjectByType<GameFlow>();
            hud = UnityEngine.Object.FindAnyObjectByType<DiveHud>();
            map = UnityEngine.Object.FindAnyObjectByType<MapScreen>();
            splash = UnityEngine.Object.FindAnyObjectByType<Splash>();
            shop = UnityEngine.Object.FindAnyObjectByType<ShopScreen>();
            popups = UnityEngine.Object.FindAnyObjectByType<TurnPopups>();
            cam = follow.GetComponent<Camera>();

            Directory.CreateDirectory("Logs");
            cam.aspect = 16f / 9f;
            flow.Persist = false;
            flow.Begin();
            flow.Finished += o => lastOutcome = o;
            diver.TurnCompleted += popups.Show;
            diver.Restarted += popups.Clear;
            // DiveJuice listens in OnEnable, which edit mode never calls.
            diver.Entered += r => { if (r.bullseye && !r.Failed) follow.Highlight(diver.EntryPoint); };
        }

        static void Capture()
        {
            // Title and the 3D map of every location.
            flow.Load(0);
            map.Preview(0);
            CaptureUi("Logs/capture_title.png", null, keepState: true);

            // Every character in the shop, in one contact sheet.
            int skins = Skins.All.Length;
            var skinSheet = new Texture2D(FrameW * 5, FrameH * ((skins + 4) / 5), TextureFormat.RGB24, false);
            int skinRows = (skins + 4) / 5;
            cam.aspect = FrameW / (float)FrameH;
            for (int i = 0; i < skins; i++)
            {
                shop.Preview(i);
                Canvas.ForceUpdateCanvases();
                // A close three-quarter view from the front, with the sky behind.
                Vector3 chest = diver.transform.position + Vector3.up * 0.15f;
                Vector3 front = diver.transform.forward * 2.3f + diver.transform.right * 0.8f;
                cam.transform.SetPositionAndRotation(chest + front, Quaternion.LookRotation(-front));
                Render(FrameW, FrameH, skinSheet, (i % 5) * FrameW, (skinRows - 1 - i / 5) * FrameH);
            }
            File.WriteAllBytes("Logs/capture_skins.png", skinSheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(skinSheet);
            map.Preview(0);
            shop.Preview(2);
            CaptureUi("Logs/capture_shop.png", null, keepState: true);
            shop.Hide();
            flow.ApplySkin();
            map.Preview(0);
            for (int loc = 0; loc < flow.LocationCount; loc++)
            {
                flow.Load(flow.LevelsIn(loc)[1]);
                map.Preview(1);
                CaptureUi($"Logs/capture_map_{loc}.png", null, keepState: true);
            }
            // Summary after the last dive of a location.
            var lakeLevels = flow.LevelsIn(1);
            flow.Load(lakeLevels[lakeLevels.Count - 1]);
            map.Preview(1);
            map.PreviewSummary();
            CaptureUi("Logs/capture_summary.png", null, keepState: true);
            map.Preview(2);

            CaptureLevels();

            flow.Load(4);
            CaptureUi("Logs/capture_ui_ready.png", null);

            var scenarios = new[]
            {
                new Scenario
                {
                    name = "good", level = 4,
                    held = t => t < 1.0f || (t > 1.12f && t < 1.95f),
                    frames = new[] { 0.95f, 1.3f, 1.6f, 1.9f, 2.2f, 2.5f, 2.6f, 2.7f, 2.85f, 3.0f, 3.3f, 3.7f }
                },
                new Scenario
                {
                    name = "slip", level = 4,
                    held = t => t < 3.5f,
                    frames = new[] { 0.9f, 1.2f, 1.35f, 1.45f, 1.55f, 1.65f, 1.75f, 1.9f, 2.05f, 2.2f, 2.35f, 2.6f }
                },
                new Scenario
                {
                    name = "flop", level = 6,
                    held = t => t < 0.8f,
                    frames = new[] { 1.8f, 2.0f, 2.1f, 2.2f, 2.3f, 2.4f, 2.5f, 2.7f, 2.9f, 3.2f, 3.6f, 4.0f }
                },
                new Scenario
                {
                    name = "lake", level = 11,
                    held = t => t < 1.05f || (t > 1.17f && t < 2.0f),
                    frames = new[] { 0.9f, 1.3f, 1.6f, 1.9f, 2.2f, 2.5f, 2.7f, 2.9f, 3.1f, 3.3f, 3.6f, 4.0f }
                },
                new Scenario
                {
                    name = "tap", level = 4,
                    held = t => t < 0.12f,
                    frames = new[] { 0.1f, 0.2f, 0.35f, 0.5f, 0.7f, 0.9f, 1.1f, 1.3f, 1.5f, 1.7f, 1.9f, 2.3f }
                },
                new Scenario
                {
                    name = "fat", level = 4, skin = 2,
                    held = t => t < 0.9f || (t > 1.02f && t < 1.9f),
                    frames = new[] { 1.8f, 2.0f, 2.2f, 2.4f, 2.5f, 2.6f, 2.7f, 2.8f, 3.0f, 3.2f, 3.5f, 3.9f }
                },
                new Scenario
                {
                    name = "robot", level = 4, skin = 9,
                    held = t => t < 0.9f,
                    frames = new[] { 1.4f, 1.6f, 1.7f, 1.8f, 1.9f, 2.0f, 2.1f, 2.3f, 2.5f, 2.8f, 3.2f, 3.8f }
                },
                new Scenario
                {
                    name = "aqueduct", level = 13,
                    held = t => t < 1.05f || (t > 1.17f && t < 2.0f),
                    frames = new[] { 0.9f, 1.4f, 1.8f, 2.2f, 2.6f, 3.0f, 3.3f, 3.6f, 3.8f, 4.0f, 4.3f, 4.7f }
                },
                new Scenario
                {
                    name = "roof", level = 17,
                    held = t => t < 1.05f || (t > 1.17f && t < 2.0f),
                    frames = new[] { 0.9f, 1.4f, 1.8f, 2.2f, 2.6f, 3.0f, 3.3f, 3.6f, 3.8f, 4.0f, 4.3f, 4.7f }
                },
                new Scenario
                {
                    name = "yacht", level = 14,
                    held = t => t < 0.8f,
                    frames = new[] { 0.5f, 0.9f, 1.2f, 1.5f, 1.8f, 2.1f, 2.4f, 2.7f, 3.0f, 3.3f, 3.6f, 4.0f }
                },
                new Scenario
                {
                    name = "cliff", level = 19,
                    held = t => t < 1.05f || (t > 1.17f && t < 2.6f),
                    frames = new[] { 0.9f, 1.4f, 1.8f, 2.2f, 2.6f, 3.0f, 3.3f, 3.6f, 3.8f, 4.0f, 4.3f, 4.7f }
                }
            };

            foreach (var s in scenarios)
            {
                if (s.skin > 0 && !flow.OwnsSkin(s.skin)) flow.BuySkin(s.skin);
                else flow.SelectSkin(s.skin);
                RunScenario(s);
            }
            flow.SelectSkin(0);

            // Find any dive that lands in the very centre, then film it to check the close-up.
            flow.Load(4);
            float bh = -1f, bt = 0f;
            for (float h = 0.5f; h <= 1.3f && bh < 0f; h += 0.02f)
                for (float d = 0f; d <= 1.6f; d += 0.04f)
                {
                    float hh = h, dd = d;
                    Simulate(t => t < hh || (dd > 0f && t > hh + 0.1f && t < hh + 0.1f + dd), 6f, true);
                    if (diver.LastResult.bullseye && diver.LastResult.entry == EntryKind.HeadClean) { bh = h; bt = d; break; }
                }
            Debug.Log($"BULLSEYE dive: hold {bh:0.00} tuck {bt:0.00}");
            if (bh > 0f)
                RunScenario(new Scenario
                {
                    name = "bullseye", level = 4,
                    held = t => t < bh || (bt > 0f && t > bh + 0.1f && t < bh + 0.1f + bt),
                    frames = new[] { 2.2f, 2.4f, 2.5f, 2.6f, 2.7f, 2.8f, 3.0f, 3.2f, 3.5f, 3.8f, 4.2f, 4.8f }
                });

            // HUD mid-flight with a turn popup, then the result screen.
            flow.Load(4);
            lastOutcome = null;
            Simulate(scenarios[0].held, 2.0f, false);
            var shown = popups.GetComponentsInChildren<Text>(false);
            Debug.Log($"POPUPS active={shown.Length} rot={diver.Rotation:0} " +
                      (shown.Length > 0 ? $"pos={shown[0].rectTransform.anchoredPosition} a={shown[0].color.a:0.00} text={shown[0].text}" : ""));
            CaptureUi("Logs/capture_ui_flight.png", null, keepState: true);
            Simulate(scenarios[0].held, 3.6f, true);
            CaptureUi("Logs/capture_ui_result.png", lastOutcome);

            Debug.Log("DiveCapture done");
        }

        // Brute force over hold time and tuck length: the best score on each level sets its medals.
        static void TuneLevels()
        {
            var set = LevelSetAsset();
            var sb = new StringBuilder("TUNE\n");

            for (int i = 0; i < set.levels.Length; i++)
            {
                var level = set.levels[i];
                flow.Load(i);
                zone.Configure(zone.transform.position, level.zoneRadius, 0f);

                int best = 0;
                bool taskPossible = false;
                string bestDive = "";
                for (float hold = 0.3f; hold <= 1.38f; hold += 0.04f)
                {
                    for (float tuck = 0f; tuck <= 2.6f; tuck += 0.06f)
                    {
                        float h = hold, d = tuck;
                        Simulate(t => t < h || (d > 0f && t > h + 0.1f && t < h + 0.1f + d), 6f, true);
                        var r = diver.LastResult;
                        if (r.score > best)
                        {
                            best = r.score;
                            bestDive = $"hold {h:0.00} tuck {d:0.00} -> {r.entry} {r.halfTurns / 2f} turns";
                        }
                        if (level.TaskDone(r)) taskPossible = true;
                    }
                }

                level.gold = Round(best * 0.8f);
                level.silver = Round(best * 0.55f);
                level.bronze = Round(best * 0.3f);
                sb.Append($" L{i + 1} {flow.Height}m d{level.zoneDistance} r{level.zoneRadius}: best {best} ({bestDive}) " +
                          $"-> {level.bronze}/{level.silver}/{level.gold}, task '{level.TaskText}' possible={taskPossible}\n");
            }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
        }

        static int Round(float v) => Mathf.Max(50, Mathf.FloorToInt(v / 50f) * 50);

        static LevelSet LevelSetAsset()
        {
            var so = new SerializedObject(flow);
            return (LevelSet)so.FindProperty("levels").objectReferenceValue;
        }

        static float Distance()
        {
            var edge = flow.Site(flow.Level.location).Tower.Edge(flow.Level.platform);
            return Vector3.Dot(diver.EntryPoint - edge.position, edge.forward);
        }

        static void Simulate(Func<float, bool> held, float seconds, bool stopAtEntry)
        {
            diver.ResetToStart();
            diver.Step(Dt, false);
            for (float t = 0f; t < seconds; t += Dt)
            {
                diver.Step(Dt, held(t));
                zone.Step(Dt);
                if (!stopAtEntry)
                {
                    splash.Step(Dt);
                    popups.Step(Dt);
                    follow.Step(Dt);
                }
                if (stopAtEntry && diver.Current == Diver.State.Underwater) break;
            }
        }

        static void CaptureLevels()
        {
            // The view from every jump spot: one sheet per location, five frames each at a size where the diver can be seen.
            const int w = 640, h = 360;
            for (int loc = 0; loc < flow.LocationCount; loc++)
            {
                var list = flow.LevelsIn(loc);
                var sheet = new Texture2D(w * 2, h * 4, TextureFormat.RGB24, false);
                for (int i = 0; i < list.Count; i++)
                {
                    flow.Load(list[i]);
                    follow.Snap();
                    Render(w, h, sheet, (i % 2) * w, (3 - i / 2) * h);
                }
                File.WriteAllBytes($"Logs/capture_loc_{loc}.png", sheet.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(sheet);
            }
        }

        static void RunScenario(Scenario s)
        {
            flow.Load(s.level);
            diver.Step(Dt, false);
            follow.Snap();

            int rows = Mathf.CeilToInt(s.frames.Length / (float)Columns);
            var sheet = new Texture2D(FrameW * Columns, FrameH * rows, TextureFormat.RGB24, false);
            var log = new StringBuilder();
            int next = 0;
            float t = 0f;
            bool reported = false;

            while (next < s.frames.Length)
            {
                if (t >= s.frames[next] - 0.0001f)
                {
                    int col = next % Columns, row = rows - 1 - next / Columns;
                    Render(FrameW, FrameH, sheet, col * FrameW, row * FrameH);
                    log.Append($"  t={t:0.00} {diver.Current} held={s.held(t)} rot={diver.Rotation:0} spin={diver.Spin:0} y={diver.transform.position.y:0.00}\n");
                    next++;
                }

                diver.Step(Dt, s.held(t));
                zone.Step(Dt);
                splash.Step(Dt);
                follow.Step(Dt);
                t += Dt;

                if (!reported && diver.Current == Diver.State.Underwater)
                {
                    var r = diver.LastResult;
                    log.Append($"  ENTRY at t={t:0.00}: {r.entry} turns={r.halfTurns / 2f} inZone={r.inZone} hitBuoys={r.hitBuoys} score={r.score} dist={Distance():0.00}\n");
                    reported = true;
                }
            }

            File.WriteAllBytes($"Logs/capture_{s.name}.png", sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log($"Scenario {s.name}:\n{log}");
        }

        // Renders the scene with the HUD drawn into the same image.
        static void CaptureUi(string path, DiveOutcome? result, bool keepState = false)
        {
            const int w = 1280, h = 720;
            var canvas = hud.GetComponent<Canvas>();
            var scaler = hud.GetComponent<CanvasScaler>();
            var mode = canvas.renderMode;

            cam.aspect = w / (float)h;
            if (!keepState)
            {
                follow.Snap();
                hud.Preview(result);
            }

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            scaler.enabled = false;
            scaler.enabled = true;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = null;

            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            canvas.renderMode = mode;
        }

        static void Render(int w, int h, Texture2D into, int x, int y)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            cam.aspect = w / (float)h;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            into.ReadPixels(new Rect(0, 0, w, h), x, y);
            into.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
