using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Emberfall.EditorTools
{
    /// <summary>One-click Android build, plus a command-line entry point for CI.</summary>
    public static class EmberfallBuild
    {
        public const string OutputFolder = "Builds";
        public const string ApkName = "Emberfall.apk";

        [MenuItem("Emberfall/Build Android APK", false, 60)]
        public static void BuildAndroid()
        {
            EmberfallProjectSetup.EnsureScene();
            EmberfallProjectSetup.EnsureBuildSettings();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool switchNow = EditorUtility.DisplayDialog(
                    "Switch to Android?",
                    "The active build target is " + EditorUserBuildSettings.activeBuildTarget +
                    ". Switching to Android re-imports assets, which can take a minute.",
                    "Switch and build", "Cancel");
                if (!switchNow) return;
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            EditorUserBuildSettings.buildAppBundle = false; // an APK installs directly on BlueStacks
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Development;

            string path = Run(false);
            if (path == null) return;

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Emberfall/Build Android APK (Release)", false, 61)]
        public static void BuildAndroidRelease()
        {
            EmberfallProjectSetup.EnsureScene();
            EmberfallProjectSetup.EnsureBuildSettings();
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;
            string path = Run(true);
            if (path != null) EditorUtility.RevealInFinder(path);
        }

        /// <summary>Used by the menu items and by -executeMethod builds.</summary>
        public static string Run(bool release)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputDirectory = Path.Combine(projectRoot, OutputFolder);
            if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, ApkName);

            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new string[] { EmberfallProjectSetup.ScenePath };
            options.locationPathName = outputPath;
            options.target = BuildTarget.Android;
            options.targetGroup = BuildTargetGroup.Android;
            options.options = release ? BuildOptions.None : BuildOptions.Development;

            Debug.Log("Emberfall: building " + outputPath + " ...");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null)
            {
                Debug.LogError("Emberfall: build returned no report.");
                return null;
            }

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("Emberfall: build " + summary.result + " with " + summary.totalErrors + " error(s).");
                return null;
            }

            double megabytes = summary.totalSize / (1024.0 * 1024.0);
            Debug.Log("Emberfall: build succeeded in " + Math.Round(summary.totalTime.TotalSeconds) + "s, " +
                      Math.Round(megabytes, 1) + " MB -> " + outputPath);
            return outputPath;
        }

        /// <summary>
        /// Headless entry point:
        /// Unity.exe -quit -batchmode -projectPath "..." -executeMethod Emberfall.EditorTools.EmberfallBuild.BuildFromCommandLine
        /// </summary>
        public static void BuildFromCommandLine()
        {
            EmberfallProjectSetup.EnsureScene();
            EmberfallProjectSetup.EnsureBuildSettings();
            EmberfallProjectSetup.ApplyPlayerSettings();
            string path = Run(false);
            if (path == null) throw new Exception("Emberfall: Android build failed.");
        }
    }
}
