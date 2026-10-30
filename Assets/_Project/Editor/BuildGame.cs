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

        const string WebFolder = "Builds/WebGL";

        // The build for Yandex Games: a WebGL player with the page template that loads the platform SDK.
        // Upload the zip it writes next to the folder (index.html must sit at the top of the archive).
        [MenuItem("Jump Game/Build WebGL (Yandex)")]
        public static void WebGL()
        {
            PlayerSettings.productName = "Ныряй";
            PlayerSettings.companyName = "YashinVain";
            PlayerSettings.WebGL.template = "PROJECT:Yandex";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;

            if (Directory.Exists(WebFolder)) Directory.Delete(WebFolder, true);
            Directory.CreateDirectory(WebFolder);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { PrototypeSceneBuilder.ScenePath },
                locationPathName = WebFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"BUILD {s.result}: {s.outputPath}, {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:0} s");
            if (s.result == BuildResult.Succeeded)
            {
                string zip = "Builds/Nyryay_WebGL.zip";
                if (File.Exists(zip)) File.Delete(zip);
                System.IO.Compression.ZipFile.CreateFromDirectory(WebFolder, zip);
                Debug.Log("ZIP " + zip);
            }
            else if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
