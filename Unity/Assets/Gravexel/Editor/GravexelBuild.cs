using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gravexel.EditorTools
{
    /// <summary>One-click project setup and builds: menu "GRAVEXEL" in the Unity editor.</summary>
    public static class GravexelBuild
    {
        const string ScenePath = "Assets/Gravexel/Scenes/Main.unity";
        const string IconPath = "Assets/Gravexel/Icon/icon.png";
        const string WinOut = "Builds/Windows/Gravexel.exe";
        const string ApkOut = "Builds/Android/Gravexel.apk";

        [MenuItem("GRAVEXEL/1. Setup Project", priority = 1)]
        public static void Setup()
        {
            EnsureScene();
            PlayerSettings.productName = "GRAVEXEL";
            PlayerSettings.companyName = "Gravexel";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("[GRAVEXEL] Project ready. Press Play to test, or use 'Build Windows EXE'.");
        }

        static void EnsureScene()
        {
            if (File.Exists(ScenePath)) { AddToBuild(); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true; cam.orthographicSize = 540;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuild();
        }

        static void AddToBuild()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        [MenuItem("GRAVEXEL/2. Build Windows EXE", priority = 20)]
        public static void BuildWindows()
        {
            Setup();
            Directory.CreateDirectory(Path.GetDirectoryName(WinOut));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = WinOut,
                target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone, options = BuildOptions.None
            });
            if (report.summary.result == BuildResult.Succeeded)
            {
                UnityEngine.Debug.Log("[GRAVEXEL] Windows build OK: " + Path.GetFullPath(WinOut));
                EditorUtility.RevealInFinder(Path.GetFullPath(WinOut));
            }
            else UnityEngine.Debug.LogError("[GRAVEXEL] Windows build failed: " + report.summary.result);
        }

        [MenuItem("GRAVEXEL/3. Build Windows Installer (Inno Setup)", priority = 21)]
        public static void BuildInstaller()
        {
            if (!File.Exists(WinOut)) BuildWindows();
            if (!File.Exists(WinOut)) return;
            string iscc = FindIscc();
            if (iscc == null)
            {
                EditorUtility.DisplayDialog("Inno Setup not found",
                    "Install the free Inno Setup 6 (jrsoftware.org), then run this menu again.\n\nThe game itself is already built in Builds/Windows.", "OK");
                return;
            }
            var psi = new ProcessStartInfo(iscc, "\"" + Path.GetFullPath("../Installer/Gravexel_Unity.iss") + "\"")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (var p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode == 0) { UnityEngine.Debug.Log("[GRAVEXEL] Installer created in Builds/Installer"); EditorUtility.RevealInFinder(Path.GetFullPath("Builds/Installer")); }
                else UnityEngine.Debug.LogError("[GRAVEXEL] Inno Setup failed:\n" + output);
            }
        }

        [MenuItem("GRAVEXEL/4. Build Android APK", priority = 40)]
        public static void BuildAndroid()
        {
            Setup();
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.gravexel.game");
            Directory.CreateDirectory(Path.GetDirectoryName(ApkOut));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = ApkOut,
                target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.None
            });
            UnityEngine.Debug.Log("[GRAVEXEL] Android build: " + report.summary.result);
        }

        static string FindIscc()
        {
            foreach (var dir in new[] { System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles) })
            {
                string p = Path.Combine(dir, "Inno Setup 6", "ISCC.exe");
                if (File.Exists(p)) return p;
            }
            return null;
        }
    }
}
