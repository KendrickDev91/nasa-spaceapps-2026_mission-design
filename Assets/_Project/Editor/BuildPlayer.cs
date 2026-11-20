using System.IO;
using MissionDesign.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MissionDesign.Editor
{
    /// <summary>
    /// Compila el ejecutable de Windows en Builds/Windows/MissionTradeOff.exe (la carpeta Builds no va a git).
    /// Batch: -executeMethod MissionDesign.Editor.BuildPlayer.BuildWindows
    /// </summary>
    public static class BuildPlayer
    {
        public const string OutputDir = "Builds/Windows";
        public const string ExeName = "MissionTradeOff.exe";

        [MenuItem("NASA SpaceApps/Build Windows Player")]
        public static void BuildWindows()
        {
            PlayerSettings.productName = GameInfo.Title;
            PlayerSettings.companyName = "MissionTradeOff";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;

            Directory.CreateDirectory(OutputDir);
            var options = new BuildPlayerOptions
            {
                scenes = File.Exists(MainSceneBuilder.SpaceCenterScenePath)
                    ? new[] { MainSceneBuilder.ScenePath, MainSceneBuilder.SpaceCenterScenePath }
                    : new[] { MainSceneBuilder.ScenePath },
                locationPathName = Path.Combine(OutputDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Build] {s.result}: {s.outputPath} · {s.totalSize / (1024f * 1024f):F1} MB · {s.totalTime.TotalSeconds:F0} s · {s.totalErrors} errores");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        public const string WebGLOutputDir = "Builds/WebGL";

        /// <summary>
        /// Versión para navegador (itch.io «HTML5»). Gzip con «decompression fallback» para que funcione en cualquier
        /// servidor sin configurar cabeceras. Batch: -buildTarget WebGL -executeMethod MissionDesign.Editor.BuildPlayer.BuildWebGL
        /// </summary>
        [MenuItem("NASA SpaceApps/Build WebGL Player")]
        public static void BuildWebGL()
        {
            PlayerSettings.productName = GameInfo.Title;
            PlayerSettings.companyName = "MissionTradeOff";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            // Plantilla propia (Assets/WebGLTemplates/AstroByte): carga con la marca, lienzo 16:9 que se adapta a la
            // ventana o al iframe de itch.io y botón de pantalla completa.
            PlayerSettings.WebGL.template = "PROJECT:AstroByte";

            if (Directory.Exists(WebGLOutputDir)) Directory.Delete(WebGLOutputDir, true);
            Directory.CreateDirectory(WebGLOutputDir);
            var options = new BuildPlayerOptions
            {
                scenes = File.Exists(MainSceneBuilder.SpaceCenterScenePath)
                    ? new[] { MainSceneBuilder.ScenePath, MainSceneBuilder.SpaceCenterScenePath }
                    : new[] { MainSceneBuilder.ScenePath },
                locationPathName = WebGLOutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Build WebGL] {s.result}: {s.outputPath} · {s.totalSize / (1024f * 1024f):F1} MB · {s.totalTime.TotalSeconds:F0} s · {s.totalErrors} errores");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
