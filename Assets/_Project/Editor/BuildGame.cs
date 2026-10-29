using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // One-click builds. Output goes to Builds/, which git ignores.
    public static class BuildGame
    {
        const string Product = "Nyryay";
        const string Folder = "Builds/Windows";

        [MenuItem("Jump Game/Build Windows")]
        public static void Windows()
        {
            PlayerSettings.productName = "Ныряй";
            PlayerSettings.companyName = "YashinVain";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            Directory.CreateDirectory(Folder);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { PrototypeSceneBuilder.ScenePath },
                locationPathName = $"{Folder}/{Product}.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"BUILD {s.result}: {s.outputPath}, {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:0} s");
            if (s.result != BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
