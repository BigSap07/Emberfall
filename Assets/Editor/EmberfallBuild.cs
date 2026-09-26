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

        /// <summary>
        /// THE VERSION, stated once. The Windows settings below, the Android settings in
        /// EmberfallProjectSetup and the folder names all read this, and ProjectSettings.asset
        /// carries the same value as bundleVersion - so applying the settings for a build leaves
        /// the tree clean, which Tools/build-windows.ps1 checks after every build.
        ///
        /// No "-dev" suffix. Whether a player is a development build is a property of that
        /// player, not of the version: Game.BuildStamp prints DEV after the version on a
        /// development player, and the folder name says it too.
        /// </summary>
        public const string Version = "0.3.0";

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
        /// hard to get out of if it misbehaves, and every build here is a build handed to one
        /// person to try. Alt+Enter is left on (PlayerSettings.allowFullscreenSwitch, which is
        /// Unity's own Windows handler, not something the game binds) so fullscreen is one
        /// keystroke away, and the window is resizable so the landscape layout can be checked at
        /// whatever shape the player drags it to.
        ///
        /// 1600x900 rather than the 1920x1080 ProjectSettings carries for Android: it is the same
        /// 16:9, and it fits inside a 1920x1080 desktop with its title bar and taskbar still
        /// visible, which a 1920x1080 window does not.
        /// </summary>
        public const string WindowsFolderName = "Emberfall-" + Version + "-dev-win";
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

        /// <summary>
        /// THE PLAYTEST BUILDS, for people who are not developers: the same Windows path as the
        /// two entry points above, in one of four variants, and nothing else differs between them.
        ///
        ///   release      Development OFF. No Dev Arena scene (ScenesFor), no Dev Arena or Design
        ///                Gallery classes (the compiler guard on Assets/Scripts/Dev and on the
        ///                overflow menu's rows), no telemetry lines, no development console.
        ///   development  Development ON, which is exactly what brings all of those in. The Dev
        ///                Arena is a row of the Hub's overflow menu, and it fields any hero in the
        ///                content and plays any of its clips, owned or not.
        ///
        ///   offline      Assets/Resources/Config/supabase.json is NOT in the project, so the
        ///                player has no account service and says so on every cloud screen.
        ///   cloud        It is, so the player signs in to that project.
        ///
        /// Tools/build-windows.ps1 is the only caller, and it is the one that makes the build
        /// honest: it refuses a dirty tree, passes the commit it checked, moves the cloud config
        /// out for an offline build and back afterwards, and checks the tree is still clean. This
        /// method does not repeat those checks, with one exception: the variant has to agree with
        /// the file on disk, because a player named "offline" that signs in somewhere is the one
        /// mistake here nobody would find by looking. It refuses to run without all three values:
        ///
        ///   -emberfallOutput  "<a folder outside every working tree>"
        ///   -emberfallCommit  "<short hash>"
        ///   -emberfallVariant release-offline | release-cloud | development-offline | development-cloud
        ///
        /// The player lands in Emberfall-VERSION-HASH-VARIANT-win under the output folder, so no
        /// two of them can be mistaken for each other without opening either.
        /// </summary>
        public static void BuildWindowsPlaytest()
        {
            string outputRoot = CommandLineValue("-emberfallOutput");
            string commit = CommandLineValue("-emberfallCommit");
            string variant = CommandLineValue("-emberfallVariant");
            if (string.IsNullOrEmpty(outputRoot) || string.IsNullOrEmpty(commit) || string.IsNullOrEmpty(variant))
            {
                throw new Exception("Emberfall: BuildWindowsPlaytest needs -emberfallOutput <folder>, " +
                                    "-emberfallCommit <hash> and -emberfallVariant <variant>. " +
                                    "Run it through Tools/build-windows.ps1.");
            }

            string[] parts = variant.Split('-');
            if (parts.Length != 2 || (parts[0] != "release" && parts[0] != "development")
                || (parts[1] != "offline" && parts[1] != "cloud"))
            {
                throw new Exception("Emberfall: '" + variant + "' is not a playtest variant. It is " +
                                    "release or development, then offline or cloud: release-offline.");
            }
            bool development = parts[0] == "development";
            bool cloud = parts[1] == "cloud";

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            bool configPresent = File.Exists(Path.Combine(projectRoot, CloudConfigPath));
            if (configPresent != cloud)
            {
                throw new Exception("Emberfall: a " + variant + " build with " + CloudConfigPath +
                                    (configPresent ? " present" : " absent") + ". The script moves it " +
                                    "for an offline build and needs it for a cloud one.");
            }

            string path = BuildWindows(outputRoot, development, commit, PlaytestFolderName(commit, variant));
            if (path == null) throw new Exception("Emberfall: Windows playtest build failed.");
        }

        /// <summary>The ignored file that decides whether a player has accounts at all.</summary>
        public const string CloudConfigPath = "Assets/Resources/Config/supabase.json";

        /// <summary>Emberfall-VERSION-HASH-VARIANT-win. Tools/build-windows.ps1 looks for exactly
        /// this.</summary>
        public static string PlaytestFolderName(string commit, string variant)
        {
            return "Emberfall-" + Version + "-" + commit + "-" + variant + "-win";
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
            PlayerSettings.bundleVersion = Version;

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
            // fast-iteration path already uses, and a playtest build is not shipping to a store.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
        }

        /// <summary>
        /// Builds the Windows x86_64 DEVELOPMENT player. outputRoot may be null, which puts it
        /// under the project's own Builds folder; the hand-off build passes a path OUTSIDE the
        /// repository so a multi-hundred-megabyte player never lands in a working tree.
        ///
        /// Stamped with whatever git says HEAD is, with "-dirty" after it when tracked files
        /// differ from it - a development build is allowed to be made from a working tree, but it
        /// is not allowed to claim a commit that does not describe it.
        /// </summary>
        public static string BuildWindows(string outputRoot)
        {
            return BuildWindows(outputRoot, true, WorkingTreeCommit(), WindowsFolderName);
        }

        /// <summary>
        /// Builds the Windows x86_64 player, development or not. The one Windows build path:
        /// the menu, the hand-off build and the playtest build all come through here, and differ
        /// only in the development flag, the commit they stamp and the folder they land in.
        /// </summary>
        public static string BuildWindows(string outputRoot, bool development, string commit, string folderName)
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
            // build wants the DEV button, not a debugger socket. The playtest build turns
            // Development off, and everything the first flag brings in goes with it.
            EditorUserBuildSettings.development = development;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;

            if (string.IsNullOrEmpty(outputRoot))
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                outputRoot = Path.Combine(projectRoot, OutputFolder);
            }
            string folder = Path.Combine(outputRoot, folderName);
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
            options.scenes = ScenesFor(development);

            options.locationPathName = outputPath;
            options.target = BuildTarget.StandaloneWindows64;
            options.targetGroup = BuildTargetGroup.Standalone;
            options.options = development ? BuildOptions.Development : BuildOptions.None;

            Debug.Log("Emberfall: building Windows " + (development ? "development" : "release") +
                      " player " + Version + " (" + (commit ?? "unstamped") + ") -> " + outputPath);
            BuildReport report;
            WriteStamp(commit);
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                RemoveStamp();
            }
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
            WriteSizeReport(report, Path.Combine(outputRoot, folderName + ".sizes.txt"));
            return outputPath;
        }

        // ------------------------------------------------------------------ what the player weighs

        /// <summary>
        /// WHERE THE PLAYER'S BYTES WENT, read from the build report rather than guessed from the
        /// folder, and written beside the player as FOLDER.sizes.txt (outside the zip).
        ///
        /// It exists to answer one question the folder size cannot: whether a small player is
        /// small because the art is compressed or because art is missing. So each texture group
        /// says how many PNGs are on disk, how many were packed, what they packed to, what the
        /// same pixels would weigh as raw RGBA, and which formats they were packed in. Packed at
        /// about raw RGBA means uncompressed; well under it means compressed; fewer packed than
        /// on disk means missing. Everything else in the player is one line.
        /// </summary>
        private static void WriteSizeReport(BuildReport report, string path)
        {
            try
            {
                string[] groups = { "Assets/Resources/Sprites/Units/", "Assets/Resources/Sprites/",
                                    "Assets/Resources/Backgrounds/", "Assets/Resources/Art/" };
                string[] names = { "battle sprite sheets", "other sprites (portraits, badges, HUD)",
                                   "backgrounds", "other Resources art" };
                ulong[] packed = new ulong[groups.Length];
                ulong[] raw = new ulong[groups.Length];
                HashSet<string>[] sources = new HashSet<string>[groups.Length];
                Dictionary<string, int>[] formats = new Dictionary<string, int>[groups.Length];
                for (int g = 0; g < groups.Length; g++)
                {
                    sources[g] = new HashSet<string>();
                    formats[g] = new Dictionary<string, int>();
                }
                ulong total = 0, otherTextures = 0, rest = 0;

                foreach (PackedAssets file in report.packedAssets)
                {
                    total += file.overhead;
                    foreach (PackedAssetInfo info in file.contents)
                    {
                        total += info.packedSize;
                        string source = info.sourceAssetPath ?? "";
                        bool texture = info.type == typeof(Texture2D);
                        int group = -1;
                        for (int g = 0; g < groups.Length && texture; g++)
                        {
                            if (source.StartsWith(groups[g], StringComparison.Ordinal)) { group = g; break; }
                        }
                        if (group < 0)
                        {
                            if (texture) otherTextures += info.packedSize; else rest += info.packedSize;
                            continue;
                        }

                        packed[group] += info.packedSize;
                        if (!sources[group].Add(source)) continue;
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(source);
                        if (tex == null) continue;
                        raw[group] += (ulong)tex.width * (ulong)tex.height * 4UL;
                        string format = tex.format.ToString();
                        int seen;
                        formats[group][format] = formats[group].TryGetValue(format, out seen) ? seen + 1 : 1;
                    }
                }

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                List<string> lines = new List<string>();
                lines.Add("packed into the player (build report): " + Mb(total));
                for (int g = 0; g < groups.Length; g++)
                {
                    string folder = Path.Combine(projectRoot, groups[g].TrimEnd('/'));
                    int onDisk = 0;
                    if (Directory.Exists(folder))
                    {
                        // The groups nest (Units is inside Sprites), so the outer group counts
                        // only what no inner group already claims.
                        foreach (string png in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
                        {
                            string rel = png.Substring(projectRoot.Length + 1).Replace('\\', '/');
                            int owner = -1;
                            for (int h = 0; h < groups.Length && owner < 0; h++)
                                if (rel.StartsWith(groups[h], StringComparison.Ordinal)) owner = h;
                            if (owner == g) onDisk++;
                        }
                    }
                    List<string> formatList = new List<string>();
                    foreach (KeyValuePair<string, int> pair in formats[g]) formatList.Add(pair.Key + " x" + pair.Value);
                    lines.Add(names[g] + " (" + groups[g] + "): " + sources[g].Count + " of " + onDisk +
                              " PNGs packed, " + Mb(packed[g]) + " packed, " + Mb(raw[g]) +
                              " as raw RGBA, formats " + (formatList.Count > 0 ? string.Join(", ", formatList.ToArray()) : "none"));
                }
                lines.Add("other textures: " + Mb(otherTextures));
                lines.Add("everything else (meshes, shaders, fonts, audio, text, scenes): " + Mb(rest));
                File.WriteAllLines(path, lines.ToArray());
                Debug.Log("Emberfall: size report -> " + path + "\n" + string.Join("\n", lines.ToArray()));
            }
            catch (Exception ex)
            {
                // A report that failed to write must not fail a build that succeeded; the script
                // says the report is missing, which is the truth.
                Debug.LogWarning("Emberfall: could not write the size report: " + ex);
            }
        }

        private static string Mb(ulong bytes)
        {
            return (bytes / (1024.0 * 1024.0)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
        }

        // ------------------------------------------------------------------ the build stamp

        /// <summary>Where the stamp lives for the length of one BuildPlayer call. Gitignored,
        /// with its .meta, so writing it never dirties the tree it describes.</summary>
        private const string StampAssetPath = "Assets/Resources/" + Game.BuildStamp.ResourceName + ".txt";

        /// <summary>
        /// Writes the commit where Game.BuildStamp reads it, and imports it synchronously so the
        /// BuildPlayer call on the next line packs it. A null or empty commit writes nothing,
        /// and the player then says "unstamped", which is the truth about it.
        /// </summary>
        private static void WriteStamp(string commit)
        {
            RemoveStamp();
            if (string.IsNullOrEmpty(commit)) return;
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            File.WriteAllText(Path.Combine(projectRoot, StampAssetPath), commit.Trim());
            AssetDatabase.ImportAsset(StampAssetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>Deletes the stamp and its .meta. Runs after every build, failed ones
        /// included, so an editor session never reads a stale commit off disk.</summary>
        private static void RemoveStamp()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            if (File.Exists(Path.Combine(projectRoot, StampAssetPath))) AssetDatabase.DeleteAsset(StampAssetPath);
        }

        /// <summary>
        /// HEAD's short hash, with "-dirty" after it when a tracked file differs from HEAD, or
        /// null when git cannot be asked. For the development builds only: the playtest build
        /// never uses this, because Tools/build-windows.ps1 has already refused a dirty tree
        /// and passes the hash it checked.
        /// </summary>
        private static string WorkingTreeCommit()
        {
            string hash = Git("rev-parse --short HEAD");
            if (string.IsNullOrEmpty(hash)) return null;
            string changes = Git("status --porcelain --untracked-files=no");
            return changes == null || changes.Length > 0 ? hash + "-dirty" : hash;
        }

        /// <summary>One git command in the project folder, its trimmed output, or null.</summary>
        private static string Git(string arguments)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo("git", arguments);
                start.WorkingDirectory = Directory.GetParent(Application.dataPath).FullName;
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.CreateNoWindow = true;
                using (System.Diagnostics.Process git = System.Diagnostics.Process.Start(start))
                {
                    string output = git.StandardOutput.ReadToEnd();
                    git.WaitForExit();
                    return git.ExitCode == 0 ? output.Trim() : null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall: could not run git " + arguments + " - " + ex.Message);
                return null;
            }
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
