using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Redline.Editor
{
    public static class RedlineBuilder
    {
        const string ScenePath = "Assets/Redline/Scenes/RedlineRenegades.unity";

        [MenuItem("Redline Renegades/Build Windows Demo")]
        public static void BuildWindows()
        {
            ConfigurePlayer();
            CreateScene();
            string buildDirectory = Path.Combine(Application.dataPath, "..", "Build", "Windows");
            if (Directory.Exists(buildDirectory)) Directory.Delete(buildDirectory, true);
            Directory.CreateDirectory(buildDirectory);
            string executable = Path.Combine(buildDirectory, "RedlineRenegades.exe");
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Redline Renegades build failed: " + report.summary.result + " / " + report.summary.totalErrors + " errors");
            Debug.Log("REDLINE_BUILD_OK: " + executable + " (" + report.summary.totalSize + " bytes)");
        }

        static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Redline Renegades Bootstrap").AddComponent<RedlineGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Aayush Portfolio";
            PlayerSettings.productName = "Redline Renegades";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.aayushportfolio.redlinerenegades");
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Unity_4_8);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        }
    }
}
