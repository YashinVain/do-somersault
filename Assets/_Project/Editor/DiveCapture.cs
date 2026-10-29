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
    // Also probes what each platform allows (distance range, turns) to tune the level list.
    public static class DiveCapture
    {
        const int FrameW = 480, FrameH = 270, Columns = 4;
        const float Dt = 1f / 60f;

        struct Scenario
        {
            public string name;
            public int level;
            public Func<float, bool> held;
            public float[] frames;
        }

        static Diver diver;
        static LandingZone zone;
        static DiveCamera follow;
        static Camera cam;
        static GameFlow flow;
        static DiveHud hud;
        static DiveOutcome? lastOutcome;

        public static void BuildAndCapture()
        {
            PrototypeSceneBuilder.Build();
            Run();
        }

        [MenuItem("Jump Game/Capture Dive Frames")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
            diver = UnityEngine.Object.FindAnyObjectByType<Diver>();
            zone = UnityEngine.Object.FindAnyObjectByType<LandingZone>();
            follow = UnityEngine.Object.FindAnyObjectByType<DiveCamera>();
            flow = UnityEngine.Object.FindAnyObjectByType<GameFlow>();
            hud = UnityEngine.Object.FindAnyObjectByType<DiveHud>();
            cam = follow.GetComponent<Camera>();

            Directory.CreateDirectory("Logs");
            cam.aspect = 16f / 9f;
            flow.Persist = false;
            flow.Begin();
            flow.Finished += o => lastOutcome = o;

            Probe();
            CaptureLevels();

            flow.Load(2);
            CaptureUi("Logs/capture_ui_ready.png", null);

            var scenarios = new[]
            {
                new Scenario
                {
                    name = "good", level = 2,
                    held = t => t < 1.0f || (t > 1.12f && t < 1.95f),
                    frames = new[] { 0f, 0.5f, 0.95f, 1.1f, 1.3f, 1.5f, 1.7f, 1.9f, 2.1f, 2.3f, 2.5f, 2.9f }
                },
                new Scenario
                {
                    name = "slip", level = 2,
                    held = t => t < 3.5f,
                    frames = new[] { 0.4f, 0.9f, 1.2f, 1.4f, 1.6f, 1.8f, 2.0f, 2.2f, 2.4f, 2.6f, 2.8f, 3.1f }
                },
                new Scenario
                {
                    name = "tap", level = 2,
                    held = t => t < 0.08f,
                    frames = new[] { 0.1f, 0.25f, 0.4f, 0.55f, 0.7f, 0.85f, 1.0f, 1.2f, 1.4f, 1.7f, 2.0f, 2.4f }
                },
                new Scenario
                {
                    name = "high", level = 6,
                    held = t => t < 1.0f || (t > 1.12f && t < 2.3f),
                    frames = new[] { 0.9f, 1.2f, 1.5f, 1.8f, 2.1f, 2.4f, 2.7f, 3.0f, 3.2f, 3.4f, 3.6f, 3.9f }
                }
            };

            foreach (var s in scenarios)
                RunScenario(s);

            // Result screen of the last good dive.
            flow.Load(2);
            lastOutcome = null;
            Simulate(scenarios[0].held, 3f);
            CaptureUi("Logs/capture_ui_result.png", lastOutcome);

            Debug.Log("DiveCapture done");
        }

        static void Probe()
        {
            var sb = new StringBuilder("PROBE\n");
            for (int p = 0; p < 4; p++)
            {
                int level = Array.FindIndex(LevelsOf(), l => l.platform == p);
                flow.Load(level);

                sb.Append($" platform {flow.Height}m\n  straight:");
                for (float hold = 0.3f; hold <= 1.36f; hold += 0.1f)
                {
                    float h = hold;
                    Simulate(t => t < h, 5f);
                    sb.Append($" {h:0.0}s->{Distance():0.0}m/{diver.LastResult.entry}");
                }

                int maxTurns = 0, maxClean = 0;
                float bestTuck = 0f, bestCleanTuck = 0f;
                for (float tuck = 0.1f; tuck <= 2.6f; tuck += 0.02f)
                {
                    float d = tuck;
                    Simulate(t => t < 1.0f || (t > 1.12f && t < 1.12f + d), 6f);
                    var r = diver.LastResult;
                    if (!r.Failed && r.halfTurns > maxTurns) { maxTurns = r.halfTurns; bestTuck = d; }
                    if (r.entry == EntryKind.HeadClean && r.halfTurns > maxClean) { maxClean = r.halfTurns; bestCleanTuck = d; }
                }
                sb.Append($"\n  max turns {maxTurns / 2f} (tuck {bestTuck:0.00}s), max clean head {maxClean / 2f} (tuck {bestCleanTuck:0.00}s)\n");
            }
            Debug.Log(sb.ToString());
        }

        static LevelInfo[] LevelsOf()
        {
            var so = new SerializedObject(flow);
            return ((LevelSet)so.FindProperty("levels").objectReferenceValue).levels;
        }

        static float Distance()
        {
            var edge = UnityEngine.Object.FindAnyObjectByType<DivingTower>().Edge(flow.Level.platform);
            return Vector3.Dot(diver.EntryPoint - edge.position, edge.forward);
        }

        static void Simulate(Func<float, bool> held, float seconds)
        {
            diver.ResetToStart();
            diver.Step(Dt, false);
            for (float t = 0f; t < seconds; t += Dt)
            {
                diver.Step(Dt, held(t));
                zone.Step(Dt);
                if (diver.Current == Diver.State.Underwater) break;
            }
        }

        static void CaptureLevels()
        {
            var sheet = new Texture2D(FrameW * 2, FrameH * 2, TextureFormat.RGB24, false);
            int[] levels = { 0, 2, 4, 6 };
            for (int i = 0; i < levels.Length; i++)
            {
                flow.Load(levels[i]);
                follow.Snap();
                Render(FrameW, FrameH, sheet, (i % 2) * FrameW, (1 - i / 2) * FrameH);
            }
            File.WriteAllBytes("Logs/capture_levels.png", sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
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
        static void CaptureUi(string path, DiveOutcome? result)
        {
            const int w = 1280, h = 720;
            var canvas = hud.GetComponent<Canvas>();
            var scaler = hud.GetComponent<CanvasScaler>();
            var mode = canvas.renderMode;

            cam.aspect = w / (float)h;
            follow.Snap();
            hud.Preview(result);

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            cam.aspect = w / (float)h;
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
