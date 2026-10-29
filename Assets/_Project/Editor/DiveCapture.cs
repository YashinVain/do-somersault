using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // Simulates scripted dives in edit mode and renders contact sheets into Logs/ for a quick look at the motion.
    public static class DiveCapture
    {
        const int FrameW = 480, FrameH = 270, Columns = 4;
        const float Dt = 1f / 60f;

        struct Scenario
        {
            public string name;
            public Func<float, bool> held;
            public float[] frames;
        }

        public static void BuildAndCapture()
        {
            PrototypeSceneBuilder.Build();
            Run();
        }

        [MenuItem("Jump Game/Capture Dive Frames")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
            var diver = UnityEngine.Object.FindAnyObjectByType<Diver>();
            var zone = UnityEngine.Object.FindAnyObjectByType<LandingZone>();
            var follow = UnityEngine.Object.FindAnyObjectByType<DiveCamera>();
            var cam = follow.GetComponent<Camera>();
            diver.Init();

            Directory.CreateDirectory("Logs");

            follow.Snap();
            SaveFrame(cam, 1280, 720, "Logs/capture_overview.png");

            // Close-ups of the body: standing, and a few poses forced through the simulation.
            var body = diver.transform.position;
            cam.transform.position = body + new Vector3(-1.9f, 0.35f, 0.9f);
            cam.transform.LookAt(body + new Vector3(0f, 0.15f, 0f));
            SaveFrame(cam, 960, 720, "Logs/capture_body.png");
            cam.transform.position = body + new Vector3(-1.3f, 0.6f, 1.3f);
            cam.transform.LookAt(body + new Vector3(0f, 0.3f, 0f));
            SaveFrame(cam, 960, 720, "Logs/capture_face.png");
            follow.Snap();

            var scenarios = new[]
            {
                new Scenario
                {
                    name = "good",
                    held = t => t < 1.0f || (t > 1.12f && t < 1.95f),
                    frames = new[] { 0f, 0.5f, 0.95f, 1.1f, 1.3f, 1.5f, 1.7f, 1.9f, 2.1f, 2.3f, 2.5f, 2.9f }
                },
                new Scenario
                {
                    name = "slip",
                    held = t => t < 3.5f,
                    frames = new[] { 0.4f, 0.9f, 1.2f, 1.4f, 1.6f, 1.8f, 2.0f, 2.2f, 2.4f, 2.6f, 2.8f, 3.1f }
                },
                new Scenario
                {
                    name = "cannonball",
                    held = t => t < 0.9f || t > 1.05f,
                    frames = new[] { 0.3f, 0.85f, 1.0f, 1.2f, 1.4f, 1.6f, 1.8f, 2.0f, 2.2f, 2.4f, 2.6f, 2.9f }
                },
                new Scenario
                {
                    name = "rope",
                    held = t => t < 0.62f,
                    frames = new[] { 1.9f, 2.0f, 2.1f, 2.2f, 2.3f, 2.4f, 2.5f, 2.6f, 2.8f, 3.0f, 3.3f, 3.6f }
                }
            };

            foreach (var s in scenarios)
                RunScenario(s, diver, zone, follow, cam);

            Debug.Log("DiveCapture done");
        }

        static void RunScenario(Scenario s, Diver diver, LandingZone zone, DiveCamera follow, Camera cam)
        {
            diver.ResetToStart();
            diver.Step(Dt, false);
            follow.Snap();

            int rows = Mathf.CeilToInt(s.frames.Length / (float)Columns);
            var sheet = new Texture2D(FrameW * Columns, FrameH * rows, TextureFormat.RGB24, false);
            string log = "";
            int next = 0;
            float t = 0f;
            bool reported = false;

            while (next < s.frames.Length)
            {
                if (t >= s.frames[next] - 0.0001f)
                {
                    int col = next % Columns, row = rows - 1 - next / Columns;
                    Render(cam, FrameW, FrameH, sheet, col * FrameW, row * FrameH);
                    log += $"  t={t:0.00} {diver.Current} held={s.held(t)} rot={diver.Rotation:0} spin={diver.Spin:0} y={diver.transform.position.y:0.00} z={diver.transform.position.z:0.00}\n";
                    next++;
                }

                diver.Step(Dt, s.held(t));
                zone.Step(Dt);
                follow.Step(Dt);
                t += Dt;

                if (!reported && diver.Current == Diver.State.Underwater)
                {
                    var r = diver.LastResult;
                    log += $"  ENTRY at t={t:0.00}: {r.entry} turns={r.halfTurns / 2f} inZone={r.inZone} hitBuoys={r.hitBuoys} score={r.score} z={diver.transform.position.z:0.00}\n";
                    reported = true;
                }
            }

            File.WriteAllBytes($"Logs/capture_{s.name}.png", sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log($"Scenario {s.name}:\n{log}");
        }

        static void SaveFrame(Camera cam, int w, int h, string path)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            Render(cam, w, h, tex, 0, 0);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static void Render(Camera cam, int w, int h, Texture2D into, int x, int y)
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
