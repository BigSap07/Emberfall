// OFFLINE TYPE-CHECK SHIM - NOT PART OF THE UNITY PROJECT. See UnityCore.cs.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEngine
{
    public static class Input
    {
        public static Vector3 mousePosition { get { return Vector3.zero; } }
        public static bool GetMouseButtonDown(int button) { return false; }
        public static bool anyKeyDown { get { return false; } }
        public static int touchCount { get { return 0; } }
    }
}

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InitializeOnLoadAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class InitializeOnLoadMethodAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
    }

    public delegate void CallbackFunction();

    public static class EditorApplication
    {
        public static CallbackFunction delayCall { get; set; }
        public static CallbackFunction update { get; set; }
        public static bool isPlaying { get { return false; } }
        public static bool isPlayingOrWillChangePlaymode { get { return false; } }
        public static bool isCompiling { get { return false; } }
        public static bool isUpdating { get { return false; } }
    }

    public sealed class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { this.path = path; this.enabled = enabled; }
        public string path { get; set; }
        public bool enabled { get; set; }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
    }

    public static class AssetDatabase
    {
        public static void Refresh() { }
        public static void SaveAssets() { }
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object { return null; }
        public static string[] FindAssets(string filter) { return new string[0]; }
        public static string GUIDToAssetPath(string guid) { return ""; }
        public static bool IsValidFolder(string path) { return true; }
        public static string CreateFolder(string parent, string newFolderName) { return ""; }
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok) { return true; }
        public static bool DisplayDialog(string title, string message, string ok, string cancel) { return true; }
        public static void RevealInFinder(string path) { }
        public static void DisplayProgressBar(string title, string info, float progress) { }
        public static void ClearProgressBar() { }
        public static void SetDirty(UnityEngine.Object target) { }
    }

    public static class EditorPrefs
    {
        public static bool GetBool(string key, bool defaultValue) { return defaultValue; }
        public static void SetBool(string key, bool value) { }
        public static string GetString(string key, string defaultValue) { return defaultValue; }
        public static void SetString(string key, string value) { }
    }

    public enum BuildTarget { Android = 13, StandaloneWindows64 = 19, iOS = 9 }
    public enum BuildTargetGroup { Standalone = 1, Android = 7, iOS = 4 }
    [Flags] public enum BuildOptions { None = 0, Development = 1, AutoRunPlayer = 4, ShowBuiltPlayer = 8, ConnectWithProfiler = 256 }

    public sealed class BuildPlayerOptions
    {
        public string[] scenes;
        public string locationPathName;
        public BuildTarget target;
        public BuildTargetGroup targetGroup;
        public BuildOptions options;
    }

    public static class BuildPipeline
    {
        public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions options) { return null; }
    }

    public static class EditorUserBuildSettings
    {
        public static BuildTarget activeBuildTarget { get { return BuildTarget.StandaloneWindows64; } }
        public static bool buildAppBundle { get; set; }
        public static bool development { get; set; }
        public static bool exportAsGoogleAndroidProject { get; set; }
        public static AndroidBuildType androidBuildType { get; set; }
        public static bool SwitchActiveBuildTarget(BuildTargetGroup group, BuildTarget target) { return true; }
    }

    public enum AndroidSdkVersions { AndroidApiLevel23 = 23, AndroidApiLevel24 = 24, AndroidApiLevel26 = 26, AndroidApiLevel29 = 29, AndroidApiLevel31 = 31, AndroidApiLevel33 = 33 }
    public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, X86_64 = 8, All = 0xFFFFFFF }
    public enum ScriptingImplementation { Mono2x = 0, IL2CPP = 1 }
    public enum ApiCompatibilityLevel { NET_Standard_2_0 = 6, NET_Unity_4_8 = 3, NET_Standard = 6 }
    public enum UIOrientation { Portrait = 0, PortraitUpsideDown = 1, LandscapeRight = 2, LandscapeLeft = 3, AutoRotation = 4 }
    public enum AndroidBuildType { Debug, Development, Release }
    public enum ManagedStrippingLevel { Disabled, Low, Medium, High }

    public static class PlayerSettings
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static string bundleVersion { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; }
        public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }
        public static bool useAnimatedAutorotation { get; set; }
        public static UnityEngine.Texture2D[] GetIconsForTargetGroup(BuildTargetGroup group) { return new UnityEngine.Texture2D[0]; }
        public static void SetIconsForTargetGroup(BuildTargetGroup group, UnityEngine.Texture2D[] icons) { }
        public static void SetApplicationIdentifier(BuildTargetGroup group, string identifier) { }
        public static string GetApplicationIdentifier(BuildTargetGroup group) { return ""; }
        public static void SetScriptingBackend(BuildTargetGroup group, ScriptingImplementation backend) { }
        public static ScriptingImplementation GetScriptingBackend(BuildTargetGroup group) { return ScriptingImplementation.Mono2x; }
        public static void SetApiCompatibilityLevel(BuildTargetGroup group, ApiCompatibilityLevel level) { }
        public static void SetManagedStrippingLevel(BuildTargetGroup group, ManagedStrippingLevel level) { }
        public static ColorSpace colorSpace { get; set; }

        public static class Android
        {
            public static AndroidSdkVersions minSdkVersion { get; set; }
            public static AndroidSdkVersions targetSdkVersion { get; set; }
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static bool useAPKExpansionFiles { get; set; }
            public static int bundleVersionCode { get; set; }
        }
    }

    public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 }
}

namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Unknown, Succeeded, Failed, Cancelled }
    public sealed class BuildSummary
    {
        public BuildResult result { get { return BuildResult.Succeeded; } }
        public ulong totalSize { get { return 0; } }
        public string outputPath { get { return ""; } }
        public TimeSpan totalTime { get { return TimeSpan.Zero; } }
        public int totalErrors { get { return 0; } }
    }
    public sealed class BuildReport : UnityEngine.Object
    {
        public BuildSummary summary { get { return null; } }
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }

    public static class EditorSceneManager
    {
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) { return new Scene(); }
        public static bool SaveScene(Scene scene, string path) { return true; }
        public static bool SaveScene(Scene scene) { return true; }
        public static Scene OpenScene(string path) { return new Scene(); }
        public static Scene GetActiveScene() { return new Scene(); }
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() { return true; }
    }
}
