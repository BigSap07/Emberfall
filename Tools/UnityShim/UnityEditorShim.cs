// OFFLINE TYPE-CHECK SHIM - NOT PART OF THE UNITY PROJECT. See UnityCore.cs.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEngine
{
    /// Escape is the Android hardware back button as well as the desktop key - legacy
    /// Input reports both as this - which is why GameApp's one handler covers both.
    public enum KeyCode { Escape = 27, LeftBracket = 91 }

    /// Standalone window mode. Real Unity puts this in UnityEngine, not UnityEditor, and
    /// PlayerSettings.fullScreenMode is typed with it - so it lives here rather than beside the
    /// build enums, to keep EmberfallBuild's `using` list the same offline as it is in the editor.
    public enum FullScreenMode
    {
        ExclusiveFullScreen = 0,
        FullScreenWindow = 1,
        MaximizedWindow = 2,
        Windowed = 3,
    }

    public static class Input
    {
        public static Vector3 mousePosition { get { return Vector3.zero; } }
        public static bool GetMouseButtonDown(int button) { return false; }
        public static bool anyKeyDown { get { return false; } }
        public static int touchCount { get { return 0; } }
        public static bool GetKeyDown(KeyCode key) { return false; }
        public static bool GetKey(KeyCode key) { return false; }
    }

    /// <summary>
    /// Layout options are opaque to a type-check - only their presence in a signature matters -
    /// so one empty marker stands in for every GUILayout.Width/Height/ExpandWidth call.
    /// </summary>
    public sealed class GUILayoutOption { }

    /// <summary>Real Unity puts GUIStyle in UnityEngine, not UnityEditor - runtime IMGUI uses it
    /// too. It sat in the wrong namespace here until something outside UnityEditor needed it.</summary>
    public sealed class GUIStyle { }

    public static class GUILayout
    {
        public static bool Button(string text) { return false; }
        public static bool Button(string text, params GUILayoutOption[] options) { return false; }
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options) { return false; }
        public static void Label(string text, params GUILayoutOption[] options) { }
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options) { }
        public static bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options) { return value; }
        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) { return value; }
        public static void Space(float pixels) { }
        public static void FlexibleSpace() { }
        public static GUILayoutOption Width(float w) { return null; }
        public static GUILayoutOption Height(float h) { return null; }
        public static GUILayoutOption ExpandWidth(bool expand) { return null; }
    }

    public static class GUILayoutUtility
    {
        public static Rect GetRect(float minW, float maxW, float minH, float maxH) { return new Rect(); }
        public static Rect GetRect(float minW, float maxW, float minH, float maxH, params GUILayoutOption[] options) { return new Rect(); }
        public static Rect GetRect(float w, float h) { return new Rect(); }
    }

    /// <summary>The immediate-mode surface, enough to type-check a hand-drawn editor window.</summary>
    public static class GUI
    {
        public static Color backgroundColor { get; set; }
        public static Color color { get; set; }
        public static bool enabled { get; set; }
        public static void Label(Rect rect, string text) { }
        public static void Label(Rect rect, string text, GUIStyle style) { }
        public static bool Button(Rect rect, string text) { return false; }
        public static void DrawTexture(Rect rect, Texture image) { }
        public static void DrawTextureWithTexCoords(Rect rect, Texture image, Rect texCoords) { }
        public static void DrawTextureWithTexCoords(Rect rect, Texture image, Rect texCoords, bool alphaBlend) { }
    }
}

namespace UnityEditor.Compilation
{
    /// Only the one call the capture tooling makes: RefreshOnDemand asks the editor to rebuild
    /// the script assemblies from outside, because Unity's automatic refresh runs on window focus
    /// and an automated capture cannot rely on getting it.
    public static class CompilationPipeline
    {
        public static void RequestScriptCompilation() { }
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

    public enum PlayModeStateChange { EnteredEditMode = 0, ExitingEditMode = 1, EnteredPlayMode = 2, ExitingPlayMode = 3 }

    public static class EditorApplication
    {
        public static event Action<PlayModeStateChange> playModeStateChanged;
        /// <summary>Test seam, not in the real API: raises the event the way the editor does, so
        /// Tools/OfflineSwitchCheck can drive EditorCaptureGuard's play-mode handler.</summary>
        public static void RaisePlayModeStateChanged(PlayModeStateChange change)
        {
            if (playModeStateChanged != null) playModeStateChanged(change);
        }

        public static CallbackFunction delayCall { get; set; }
        public static CallbackFunction update { get; set; }
        /// <summary>Seconds since the editor started, as a double in the real API. Any editor tool
        /// that animates reads it every frame, so the width is part of the contract.</summary>
        public static double timeSinceStartup { get { return 0.0; } }
        // Real Unity: settable, and driving it is exactly what the Play-Mode automation under
        // Assets/Editor does (EditorApplication.isPlaying = true/false). A getter-only stub compiles
        // fine for code that only reads it, but throws CS0200 on every assignment - which is most of
        // what these tools do with it.
        public static bool isPlaying { get; set; }
        public static bool isPlayingOrWillChangePlaymode { get { return false; } }
        /// <summary>Real API: asks for one player-loop update on the next editor tick. VfxShots uses
        /// it so an unfocused editor still advances game frames.</summary>
        public static void QueuePlayerLoopUpdate() { }
        public static void Exit(int returnValue) { }
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

    /// <summary>Real API: a [Flags] enum, copied whole rather than trimmed to the one member the
    /// baker uses - a shim that renumbers an engine enum is a shim that compiles code the engine
    /// will not.</summary>
    [Flags]
    public enum ImportAssetOptions
    {
        Default = 0,
        ForceUpdate = 1,
        ForceSynchronousImport = 8,
        ImportRecursive = 256,
        DontDownloadFromCacheServer = 8192,
        ForceUncompressedImport = 16384
    }

    public static class AssetDatabase
    {
        public static void Refresh() { }
        public static void Refresh(ImportAssetOptions options) { }
        public static void ImportAsset(string path) { }
        public static void ImportAsset(string path, ImportAssetOptions options) { }
        public static bool DeleteAsset(string path) { return true; }
        public static void StartAssetEditing() { }
        public static void StopAssetEditing() { }
        public static void SaveAssets() { }
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object { return null; }
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string path) { return new UnityEngine.Object[0]; }
        public static string[] FindAssets(string filter) { return new string[0]; }
        public static string GUIDToAssetPath(string guid) { return ""; }
        public static bool IsValidFolder(string path) { return true; }
        public static string CreateFolder(string parent, string newFolderName) { return ""; }
    }

    // ------------------------------------------------------------------ the sprite importer
    //
    // Signature-only, like the rest of this shim, and present for one reason: EmberforgeBaker
    // sets a nine-slice border on every sprite it writes, and a border that never gets set is the
    // one failure in that pipeline that is completely silent - the sprite imports, loads and
    // draws, and only stretches its corners at a size nobody photographed. Type-checking that
    // call offline is cheap insurance for it.

    public enum TextureImporterType { Default = 0, NormalMap = 1, GUI = 2, Sprite = 8, Cursor = 7, Cookie = 4, Lightmap = 6, SingleChannel = 10 }
    public enum SpriteImportMode { None = 0, Single = 1, Multiple = 2, Polygon = 3 }
    public enum TextureImporterCompression { Uncompressed = 0, Compressed = 1, CompressedHQ = 2, CompressedLQ = 3 }
    public enum SpriteAlignment { Center = 0, TopLeft = 1, TopCenter = 2, TopRight = 3, LeftCenter = 4, RightCenter = 5, BottomLeft = 6, BottomCenter = 7, BottomRight = 8, Custom = 9 }

    public class AssetImporter : UnityEngine.Object
    {
        public string assetPath { get; set; }
        public static AssetImporter GetAtPath(string path) { return null; }
        public void SaveAndReimport() { }
    }

    public sealed class TextureImporterSettings
    {
        public Vector4 spriteBorder { get; set; }
        public SpriteMeshType spriteMeshType { get; set; }
        public int spriteAlignment { get; set; }
        public Vector2 spritePivot { get; set; }
    }

    public sealed class TextureImporter : AssetImporter
    {
        public TextureImporterType textureType { get; set; }
        public SpriteImportMode spriteImportMode { get; set; }
        public float spritePixelsPerUnit { get; set; }
        public bool mipmapEnabled { get; set; }
        public bool alphaIsTransparency { get; set; }
        public bool isReadable { get; set; }
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
        public TextureImporterCompression textureCompression { get; set; }
        public int maxTextureSize { get; set; }
        public void ReadTextureSettings(TextureImporterSettings dest) { }
        public void SetTextureSettings(TextureImporterSettings src) { }
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok) { return true; }
        public static bool DisplayDialog(string title, string message, string ok, string cancel) { return true; }
        public static void RevealInFinder(string path) { }
        public static string SaveFilePanel(string title, string directory, string defaultName, string extension) { return ""; }
        public static string OpenFilePanel(string title, string directory, string extension) { return ""; }
        public static void DisplayProgressBar(string title, string info, float progress) { }
        public static void ClearProgressBar() { }
        public static void SetDirty(UnityEngine.Object target) { }
    }

    /// <summary>Real API: persists per-session (survives domain reload, not the process), which is
    /// exactly why the Play-Mode automation under Assets/Editor uses it to carry state across the
    /// reload that entering/exiting Play Mode triggers.</summary>
    public static class SessionState
    {
        // Backed by a dictionary so Tools/OfflineSwitchCheck can execute the real forced-offline
        // logic in SupabaseConfig. Nothing else runs against the shim, so the other stubs here
        // keep returning their defaults.
        private static readonly System.Collections.Generic.Dictionary<string, object> _values =
            new System.Collections.Generic.Dictionary<string, object>();

        private static T Get<T>(string key, T defaultValue)
        {
            object value;
            return _values.TryGetValue(key, out value) && value is T ? (T)value : defaultValue;
        }

        public static bool GetBool(string key, bool defaultValue) { return Get(key, defaultValue); }
        public static void SetBool(string key, bool value) { _values[key] = value; }
        public static int GetInt(string key, int defaultValue) { return Get(key, defaultValue); }
        public static void SetInt(string key, int value) { _values[key] = value; }
        public static float GetFloat(string key, float defaultValue) { return Get(key, defaultValue); }
        public static void SetFloat(string key, float value) { _values[key] = value; }
        public static string GetString(string key, string defaultValue) { return Get(key, defaultValue); }
        public static void SetString(string key, string value) { _values[key] = value; }
    }

    /// <summary>Enough of the real type for the verification tools' GameView-focus helper:
    /// EditorWindow.GetWindow(gameViewType, false, null, false).Focus()/.Repaint().</summary>
    /// <summary>Derives from ScriptableObject, exactly as Unity's does. It was free-standing,
    /// which was harmless until something walked Resources.FindObjectsOfTypeAll's Object[] and
    /// cast an element to it - a cast the real engine allows and the shim rejected.</summary>
    public class EditorWindow : ScriptableObject
    {
        public static EditorWindow GetWindow(Type t) { return null; }
        public static EditorWindow GetWindow(Type t, bool utility) { return null; }
        public static EditorWindow GetWindow(Type t, bool utility, string title) { return null; }
        public static EditorWindow GetWindow(Type t, bool utility, string title, bool focus) { return null; }
        public static T GetWindow<T>(string title) where T : EditorWindow { return null; }
        public void Focus() { }
        public void Show() { }
        public void Repaint() { }
        public Vector2 minSize { get; set; }
        public Vector2 maxSize { get; set; }
        /// <summary>The window's screen rect. A hand-laid-out window reads its width to size
        /// columns, so it is part of the surface a drawing tool type-checks against.</summary>
        public Rect position { get; set; }
    }

    public enum MessageType { None, Info, Warning, Error }

    public static class EditorStyles
    {
        public static GUIStyle boldLabel { get { return null; } }
        public static GUIStyle label { get { return null; } }
        public static GUIStyle miniLabel { get { return null; } }
        public static GUIStyle miniBoldLabel { get { return null; } }
        public static GUIStyle whiteLabel { get { return null; } }
        public static GUIStyle miniButton { get { return null; } }
        public static GUIStyle toolbar { get { return null; } }
        public static GUIStyle toolbarButton { get { return null; } }
        public static GUIStyle helpBox { get { return null; } }
        public static GUIStyle textField { get { return null; } }
    }

    public static class EditorGUI
    {
        public static int indentLevel { get; set; }
        public static void DrawRect(Rect rect, Color color) { }
        public static bool Foldout(Rect rect, bool foldout, string content, bool toggleOnLabelClick) { return foldout; }
    }

    public static class EditorGUILayout
    {
        public static void LabelField(string label, GUIStyle style) { }
        public static void LabelField(string label, string value) { }
        public static string TextField(string label, string value) { return value; }
        public static int IntSlider(string label, int value, int min, int max) { return value; }
        public static void HelpBox(string message, MessageType type) { }
        public static Vector2 BeginScrollView(Vector2 scroll) { return scroll; }
        public static void EndScrollView() { }
        public static void BeginHorizontal() { }
        public static void BeginHorizontal(GUIStyle style, params GUILayoutOption[] options) { }
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical() { }
        public static void BeginVertical(GUIStyle style, params GUILayoutOption[] options) { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void EndVertical() { }
        public static void Space() { }
        public static void Space(float pixels) { }
        public static Vector2 BeginScrollView(Vector2 scroll, params GUILayoutOption[] options) { return scroll; }
        public static void LabelField(string label) { }
        public static void LabelField(string label, params GUILayoutOption[] options) { }
        public static void LabelField(string label, string value, GUIStyle style) { }
        public static void LabelField(string label, GUIStyle style, params GUILayoutOption[] options) { }
        public static void LabelField(string label, string value, params GUILayoutOption[] options) { }
        public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick) { return foldout; }
        public static bool Toggle(string label, bool value) { return value; }
        public static bool Toggle(bool value, params GUILayoutOption[] options) { return value; }
        public static float Slider(string label, float value, float min, float max) { return value; }
        public static int IntField(int value, params GUILayoutOption[] options) { return value; }
        public static int IntField(string label, int value) { return value; }
        public static string TextField(string label, string value, params GUILayoutOption[] options) { return value; }
        public static int Popup(string label, int selected, string[] displayed) { return selected; }
        public static int Popup(int selected, string[] displayed, params GUILayoutOption[] options) { return selected; }
        public static int IntPopup(string label, int selected, string[] displayed, int[] values) { return selected; }
    }

    /// <summary>
    /// Signature-only, like the rest of this shim. GameViewSize reflects against internal editor
    /// types and needs typeof(Editor).Assembly as its handle on the UnityEditor assembly, plus
    /// ScriptableSingleton&lt;T&gt; to reach GameViewSizes.instance. Neither is ever called here -
    /// they exist so build-check type-checks the file instead of skipping it.
    /// </summary>
    public class Editor : ScriptableObject
    {
        public UnityEngine.Object target { get; set; }
        public virtual void OnInspectorGUI() { }
    }

    public class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        public static T instance { get { return null; } }
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
        // Separate from `development` in Unity as well as here, which is the whole point of the
        // pair: Development Build without script debugging is a real and wanted combination.
        public static bool allowDebugging { get; set; }
        public static bool connectProfiler { get; set; }
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

        // Standalone window settings, for EmberfallBuild's Windows desktop player. Plain
        // properties like the rest of this class: the shim exists so editor-facing code COMPILES
        // without Unity, not so it behaves, and nothing offline reads these back.
        public static bool defaultIsNativeResolution { get; set; }
        public static int defaultScreenWidth { get; set; }
        public static int defaultScreenHeight { get; set; }
        public static FullScreenMode fullScreenMode { get; set; }
        public static bool resizableWindow { get; set; }
        public static bool allowFullscreenSwitch { get; set; }
        public static bool forceSingleInstance { get; set; }
        public static UnityEngine.Texture2D[] GetIconsForTargetGroup(BuildTargetGroup group) { return new UnityEngine.Texture2D[0]; }
        public static void SetIconsForTargetGroup(BuildTargetGroup group, UnityEngine.Texture2D[] icons) { }
        public static void SetApiCompatibilityLevel(BuildTargetGroup group, ApiCompatibilityLevel level) { }
        public static ColorSpace colorSpace { get; set; }

        // NamedBuildTarget-based overloads - the current (non-obsolete) way to set these as of Unity 6.
        public static void SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget target, string identifier) { }
        public static string GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget target) { return ""; }
        public static void SetScriptingBackend(UnityEditor.Build.NamedBuildTarget target, ScriptingImplementation backend) { }
        public static ScriptingImplementation GetScriptingBackend(UnityEditor.Build.NamedBuildTarget target) { return ScriptingImplementation.Mono2x; }
        public static void SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget target, ManagedStrippingLevel level) { }

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

namespace UnityEditor.Build
{
    /// <summary>Ordering for a build callback. Real Unity puts it on IOrderedCallback, which both
    /// build hooks derive from.</summary>
    public interface IOrderedCallback
    {
        int callbackOrder { get; }
    }

    public interface IPreprocessBuildWithReport : IOrderedCallback
    {
        void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report);
    }

    public interface IPostprocessBuildWithReport : IOrderedCallback
    {
        void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report);
    }

    /// <summary>Replaces BuildTargetGroup in the newer PlayerSettings overloads.</summary>
    public struct NamedBuildTarget
    {
        private readonly string _name;
        private NamedBuildTarget(string name) { _name = name; }
        public string TargetName { get { return _name; } }

        public static NamedBuildTarget Standalone { get { return new NamedBuildTarget("Standalone"); } }
        public static NamedBuildTarget Android { get { return new NamedBuildTarget("Android"); } }
        public static NamedBuildTarget iOS { get { return new NamedBuildTarget("iOS"); } }
    }
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
        /// <summary>What the build was asked for. A build hook reads Development off this to
        /// decide whether a development-only scene belongs in the scene list.</summary>
        public BuildOptions options { get { return BuildOptions.None; } }
    }
    public sealed class BuildReport : UnityEngine.Object
    {
        public BuildSummary summary { get { return null; } }
        /// <summary>Every serialized file the build wrote, with what went into it. EmberfallBuild
        /// reads it for the size report beside each Windows player.</summary>
        public PackedAssets[] packedAssets { get { return new PackedAssets[0]; } }
    }
    public sealed class PackedAssets : UnityEngine.Object
    {
        public string shortPath { get { return ""; } }
        public ulong overhead { get { return 0; } }
        public PackedAssetInfo[] contents { get { return new PackedAssetInfo[0]; } }
    }
    public struct PackedAssetInfo
    {
        public Type type { get { return null; } }
        public ulong packedSize { get { return 0; } }
        public string sourceAssetPath { get { return ""; } }
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum OpenSceneMode { Single, Additive, AdditiveWithoutLoading }
    public enum NewSceneMode { Single, Additive }

    public static class EditorSceneManager
    {
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) { return new Scene(); }
        public static Scene OpenScene(string path, OpenSceneMode mode) { return new Scene(); }
        public static void MarkSceneDirty(Scene scene) { }
        public static bool SaveScene(Scene scene, string path) { return true; }
        public static bool SaveScene(Scene scene) { return true; }
        public static Scene OpenScene(string path) { return new Scene(); }
        public static Scene GetActiveScene() { return new Scene(); }
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() { return true; }
    }
}
