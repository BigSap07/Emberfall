using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Configures the project through the Unity API instead of hand-written asset files.
    /// Runs once automatically when the project is first opened, and can be re-run from the
    /// Emberfall menu. This is why the repository contains no ProjectSettings YAML beyond the
    /// editor version - everything else is applied here, for whatever Unity version you use.
    /// </summary>
    [InitializeOnLoad]
    public static class EmberfallProjectSetup
    {
        public const string SceneFolder = "Assets/Scenes";
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string BundleId = "com.emberfall.prototype";
        private const string SetupDoneKey = "Emberfall.SetupComplete.v1";

        static EmberfallProjectSetup()
        {
            EditorApplication.delayCall = EditorApplication.delayCall + AutoSetup;
        }

        private static void AutoSetup()
        {
            bool sceneMissing = !File.Exists(ScenePath);
            if (sceneMissing) EnsureScene();
            EnsureBuildSettings();

            if (EditorPrefs.GetBool(SetupDoneKey, false)) return;
            ApplyPlayerSettings();
            EditorPrefs.SetBool(SetupDoneKey, true);
            Debug.Log("Emberfall: project configured. Open " + ScenePath + " and press Play, " +
                      "or use Emberfall > Build Android APK.");
        }

        [MenuItem("Emberfall/Set Up Project", false, 0)]
        public static void SetUpProject()
        {
            EnsureScene();
            EnsureBuildSettings();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Emberfall: setup complete.");
        }

        [MenuItem("Emberfall/Open Main Scene", false, 1)]
        public static void OpenMainScene()
        {
            EnsureScene();
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Emberfall/Delete Local Save", false, 20)]
        public static void DeleteLocalSave()
        {
            Emberfall.Game.SaveService.DeleteSave();
            Debug.Log("Emberfall: local save deleted. The next run starts a fresh account.");
        }

        [MenuItem("Emberfall/Reveal Save Folder", false, 21)]
        public static void RevealSaveFolder()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        // ------------------------------------------------------------------ scene

        /// <summary>
        /// Creates an intentionally empty scene. The game builds its camera, canvas and
        /// battlefield from code at startup, so there is no serialised scene content to break.
        /// </summary>
        public static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;

            if (!Directory.Exists(SceneFolder))
            {
                Directory.CreateDirectory(SceneFolder);
                AssetDatabase.Refresh();
            }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("Emberfall: created " + ScenePath);
        }

        public static void EnsureBuildSettings()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            bool alreadyFirst = existing != null && existing.Length > 0
                                && existing[0].path == ScenePath && existing[0].enabled;
            if (alreadyFirst) return;

            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            Debug.Log("Emberfall: build settings now point at " + ScenePath);
        }

        // ------------------------------------------------------------------ player settings

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Emberfall Studio";
            PlayerSettings.productName = "Emberfall";
            PlayerSettings.bundleVersion = "0.1.0";

            // LANDSCAPE, mirroring ProjectSettings.asset rather than contradicting it.
            //
            // This block used to set Portrait and switch both landscape autorotations OFF, which
            // is the opposite of every other statement the project makes: ProjectSettings.asset
            // carries defaultScreenOrientation 3 (LandscapeLeft) with both landscape rotations
            // allowed and both portrait rotations denied, UITheme.ReferenceResolution is 1920x1080,
            // and the battle line is built to need the width. Nothing had noticed because this
            // method only runs from the Emberfall menu items and the first-open auto-setup, so on
            // an already-configured checkout it never fires - but running Set Up Project on a
            // fresh clone would have quietly flipped a shipped landscape game to portrait.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.bundleVersionCode = 1;

            ApplyFastIterationBackend();
        }

        [MenuItem("Emberfall/Android/Fast Iteration Backend (Mono, ARMv7)", false, 40)]
        public static void ApplyFastIterationBackend()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.Mono2x);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Disabled);
            Debug.Log("Emberfall: Android set to Mono / ARMv7. Fastest builds, no NDK required, " +
                      "runs on BlueStacks through its ARM translation layer.");
        }

        [MenuItem("Emberfall/Android/Release Backend (IL2CPP, ARM64)", false, 41)]
        public static void ApplyReleaseBackend()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            Debug.Log("Emberfall: Android set to IL2CPP / ARM64. Requires the Android NDK module. " +
                      "x86-64 is no longer a supported Android target architecture, so this backend " +
                      "no longer runs natively on 64-bit BlueStacks instances - ARM64 through " +
                      "BlueStacks' translation layer, or a physical/emulated ARM64 device, are the " +
                      "remaining options for this backend.");
        }
    }
}
