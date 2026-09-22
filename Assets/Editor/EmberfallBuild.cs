using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Emberfall.EditorTools
{
    /// <summary>One-click Android build, plus a command-line entry point for CI.</summary>
    public static class EmberfallBuild
    {
        public const string OutputFolder = "Builds";
        public const string ApkName = "Emberfall.apk";

        // ------------------------------------------------------------------ Windows desktop

        /// <summary>
        /// THE WINDOWS DESKTOP BUILD, for handing the game to somebody on a PC.
        ///
        /// Android is the target this game is designed for and nothing here changes that: the
        /// layout is the same landscape layout, driven by the same UITheme.ReferenceResolution and
        /// the same safe-area rect, and no screen has a desktop variant. What a desktop build adds
        /// is a window whose shape the player chooses, which is why the settings below are about
        /// the window and nothing else.
        ///
        /// Windowed rather than fullscreen on first run, deliberately. A fullscreen player that
        /// takes the whole display on a stranger's machine before they have pressed anything is
        /// hard to get out of if it misbehaves, and this is a development build handed to one
        /// person to try. Alt+Enter is left on (PlayerSettings.allowFullscreenSwitch, which is
        /// Unity's own Windows handler, not something the game binds) so fullscreen is one
        /// keystroke away, and the window is resizable so the landscape layout can be checked at
        /// whatever shape the player drags it to.
        ///
        /// 1600x900 rather than the 1920x1080 ProjectSettings carries for Android: it is the same
        /// 16:9, and it fits inside a 1920x1080 desktop with its title bar and taskbar still
        /// visible, which a 1920x1080 window does not.
        /// </summary>
        public const string WindowsFolderName = "Emberfall-0.1.0-dev-win";
        public const string WindowsExeName = "Emberfall.exe";

        /// <summary>
        /// THE HAND-OFF BUILD, for a headless run that puts the player somewhere other than the
        /// working tree.
        ///
        /// BuildWindows has taken an outputRoot since it was written and its own summary already
        /// describes this caller - "the hand-off build passes a path OUTSIDE the repository so a
        /// multi-hundred-megabyte player never lands in a working tree" - but nothing in the
        /// project actually passed one. BuildWindowsMenu passes null, which means the project's
        /// own Builds folder, and -executeMethod cannot call a method that takes an argument. So
        /// a hand-off build could only ever be done by hand.
        ///
        /// The destination is read from the command line rather than written here, because it is
        /// a property of the machine running the build and not of the game:
        ///
        ///   Unity.exe -quit -batchmode -projectPath "..." -buildTarget Win64
        ///             -executeMethod Emberfall.EditorTools.EmberfallBuild.BuildWindowsHandoff
        ///             -emberfallOutput "<a folder outside every working tree>"
        ///
        /// Missing or empty, it throws rather than falling back to the in-tree Builds folder.
        /// A silent fallback is how a 200 MB player ends up inside a checkout, and the whole
        /// reason this entry point exists is to keep it out of one.
        /// </summary>
        public static void BuildWindowsHandoff()
        {
            string outputRoot = CommandLineValue("-emberfallOutput");
            if (string.IsNullOrEmpty(outputRoot))
            {
                throw new Exception("Emberfall: BuildWindowsHandoff needs -emberfallOutput <folder>; " +
                                    "refusing to fall back to the in-tree Builds folder.");
            }

            string path = BuildWindows(outputRoot);
            if (path == null) throw new Exception("Emberfall: Windows hand-off build failed.");
        }

        /// <summary>The value following <paramref name="flag"/> on Unity's own command line, or
        /// null if the flag is absent or is the last argument.</summary>
        private static string CommandLineValue(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return null;
        }

        [MenuItem("Emberfall/Build Windows (Development)", false, 62)]
        public static void BuildWindowsMenu()
        {
            string path = BuildWindows(null);
            if (path != null) EditorUtility.RevealInFinder(path);
        }

        /// <summary>
        /// Applies the desktop window settings. Separate from the build so the settings land in
        /// ProjectSettings.asset as a reviewable diff rather than only existing inside a build run.
        /// </summary>
        public static void ApplyWindowsPlayerSettings()
        {
            PlayerSettings.companyName = "Emberfall Studio";
            PlayerSettings.productName = "Emberfall";
            PlayerSettings.bundleVersion = "0.1.0-dev";

            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;      // Alt+Enter, handled by the player
            PlayerSettings.forceSingleInstance = true;

            // Mono, not IL2CPP, and the reason is the machine rather than the game: this editor
            // install has no Windows IL2CPP module - PlaybackEngines/windowsstandalonesupport/
            // Variations carries only the six mono variations, while AndroidPlayer/Variations has
            // an il2cpp folder beside its mono one. Asking for IL2CPP here fails the build outright
            // rather than falling back. Mono is also the faster build and the backend the Android
            // fast-iteration path already uses, and a development build is not shipping anywhere.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
        }

        /// <summary>
        /// Builds the Windows x86_64 player. outputRoot may be null, which puts it under the
        /// project's own Builds folder; the hand-off build passes a path OUTSIDE the repository so
        /// a multi-hundred-megabyte player never lands in a working tree.
        /// </summary>
        public static string BuildWindows(string outputRoot)
        {
            EmberfallProjectSetup.EnsureScene();
            EmberfallProjectSetup.EnsureBuildSettings();
            ApplyWindowsPlayerSettings();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            }

            // Development Build ON, Script Debugging OFF. They are separate flags and only the
            // first is wanted: Development gets the profiler, the development console and the
            // DEVELOPMENT_BUILD define that compiles the Dev Arena in, while AllowDebugging opens
            // a managed debugger port and makes the player wait on it. The person receiving this
            // build wants the DEV button, not a debugger socket.
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;

            if (string.IsNullOrEmpty(outputRoot))
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                outputRoot = Path.Combine(projectRoot, OutputFolder);
            }
            string folder = Path.Combine(outputRoot, WindowsFolderName);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            string outputPath = Path.Combine(folder, WindowsExeName);

            BuildPlayerOptions options = new BuildPlayerOptions();

            // THE ARENA SCENE IS NAMED HERE RATHER THAN LEFT TO DevArenaMenu.BuildHook, because
            // the hook cannot actually put it in the player and two builds proved it.
            //
            // The hook amends EditorBuildSettings.scenes from OnPreprocessBuild. That callback
            // fires from INSIDE BuildPipeline.BuildPlayer, after the pipeline has already resolved
            // which scenes this build contains - so the amendment lands in the settings asset and
            // never in the player. It is too late whether or not the caller named its own scenes:
            // the first Windows build named them and got one scene, the second left them null so
            // the list would be read from EditorBuildSettings, and got one scene again even with
            // DevArena.unity sitting enabled in that list. Both players held level0 and no level1,
            // and the DEV button - compiled in, because DEVELOPMENT_BUILD is set - did nothing but
            // log that the scene was missing.
            //
            // So the scene list is stated outright, by ScenesFor below, gated on the same
            // Development condition the hook tests and shared with the Android path so both
            // targets answer it the same way. The hook is left alone: its release-side half
            // (strip the scene if
            // somebody added it by hand) still does something, and the guarantee that no arena
            // code reaches a release player was never this list anyway - it is the
            // `#if UNITY_EDITOR || DEVELOPMENT_BUILD` compiler gate on every file in
            // Assets/Scripts/Dev, which no build script can bypass. See DESIGN-BUILD-ORDER.md.
            options.scenes = ScenesFor(true);

            options.locationPathName = outputPath;
            options.target = BuildTarget.StandaloneWindows64;
            options.targetGroup = BuildTargetGroup.Standalone;
            options.options = BuildOptions.Development;

            Debug.Log("Emberfall: building Windows player -> " + outputPath);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null)
            {
                Debug.LogError("Emberfall: Windows build returned no report.");
                return null;
            }

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("Emberfall: Windows build " + summary.result + " with " +
                               summary.totalErrors + " error(s).");
                return null;
            }

            double megabytes = summary.totalSize / (1024.0 * 1024.0);
            Debug.Log("Emberfall: Windows build succeeded in " +
                      Math.Round(summary.totalTime.TotalSeconds) + "s, " +
                      Math.Round(megabytes, 1) + " MB -> " + outputPath);
            return outputPath;
        }

        /// <summary>
        /// THE SCENE LIST EVERY BUILD USES, and the only place the arena's inclusion is decided.
        ///
        /// It is stated here rather than left to DevArenaMenu.BuildHook, because the hook cannot
        /// actually put the scene in a player and two builds proved it - the reasoning is written
        /// out at the call site in BuildWindows. What this method adds is that the rule is now the
        /// SAME rule for every target: a development build gets the arena, a release build cannot
        /// get it whatever is sitting in EditorBuildSettings, and neither answer depends on the
        /// caller remembering to append a path.
        ///
        /// `development` is the caller's own build flag, not a reading of EditorUserBuildSettings,
        /// so the list and the BuildOptions.Development bit beside it can never disagree.
        ///
        /// The File.Exists guard is not defensive noise: Assets/Dev is the one folder a checkout
        /// can legitimately be missing, and naming a scene that is not on disk fails the build
        /// outright rather than building without it.
        /// </summary>
        public static string[] ScenesFor(bool development)
        {
            List<string> scenes = new List<string>();
            scenes.Add(EmberfallProjectSetup.ScenePath);
            if (development && File.Exists(DevArenaMenu.ScenePath)) scenes.Add(DevArenaMenu.ScenePath);
            return scenes.ToArray();
        }

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
            options.scenes = ScenesFor(!release);
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
