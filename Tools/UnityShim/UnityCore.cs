// ---------------------------------------------------------------------------
// OFFLINE TYPE-CHECK SHIM - NOT PART OF THE UNITY PROJECT.
// Lives outside Assets/ so Unity never sees it. Declares just enough of the
// UnityEngine API surface (matching real signatures) that the game's engine
// code can be compiled and error-checked without the editor installed.
// ---------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name = "";
        public HideFlags hideFlags;

        public static void Destroy(Object obj) { }
        public static void Destroy(Object obj, float delay) { }
        public static void DestroyImmediate(Object obj) { }
        public static void DontDestroyOnLoad(Object obj) { }
        public static T Instantiate<T>(T original) where T : Object { return original; }
        public static T FindObjectOfType<T>() where T : Object { return null; }
        public static T[] FindObjectsOfType<T>() where T : Object { return new T[0]; }

        public static implicit operator bool(Object exists) { return !ReferenceEquals(exists, null); }
        public static bool operator ==(Object a, Object b) { return ReferenceEquals(a, b); }
        public static bool operator !=(Object a, Object b) { return !ReferenceEquals(a, b); }
        public override bool Equals(object other) { return ReferenceEquals(this, other); }
        public override int GetHashCode() { return base.GetHashCode(); }
        public override string ToString() { return name; }
    }

    public enum HideFlags { None = 0, HideInHierarchy = 1, HideAndDontSave = 61 }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public enum Space { World, Self }
    public enum RuntimePlatform { WindowsPlayer, WindowsEditor, Android, IPhonePlayer, OSXEditor, OSXPlayer, WebGLPlayer, LinuxPlayer, LinuxEditor }
    public enum ScreenOrientation { Portrait = 1, PortraitUpsideDown = 2, LandscapeLeft = 3, LandscapeRight = 4, AutoRotation = 5 }
    public enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }
    public enum LightType { Spot, Directional, Point, Area }
    public enum LightShadows { None, Hard, Soft }
    public enum TextureFormat { Alpha8 = 1, RGB24 = 3, RGBA32 = 4, ARGB32 = 5 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp, Mirror }
    public enum SpriteMeshType { FullRect, Tight }
    public enum AudioClipLoadType { DecompressOnLoad }

    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponent : Attribute { public RequireComponent(Type t) { } public RequireComponent(Type a, Type b) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, BeforeSplashScreen, SubsystemRegistration, AfterAssembliesLoaded }

    // ------------------------------------------------------------------ math

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
        public static Vector2 up { get { return new Vector2(0, 1); } }
        public static Vector2 right { get { return new Vector2(1, 0); } }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y); } }
        public Vector2 normalized { get { float m = magnitude; return m > 0f ? new Vector2(x / m, y / m) : zero; } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator *(Vector2 a, float d) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator *(float d, Vector2 a) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator /(Vector2 a, float d) { return new Vector2(a.x / d, a.y / d); }
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { return new Vector2(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t)); }
        public static float Distance(Vector2 a, Vector2 b) { return (a - b).magnitude; }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); }
        public override string ToString() { return x + "," + y; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y) { this.x = x; this.y = y; this.z = 0f; }
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 down { get { return new Vector3(0, -1, 0); } }
        public static Vector3 right { get { return new Vector3(1, 0, 0); } }
        public static Vector3 left { get { return new Vector3(-1, 0, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 back { get { return new Vector3(0, 0, -1); } }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y + z * z); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public Vector3 normalized { get { float m = magnitude; return m > 0f ? new Vector3(x / m, y / m, z / m) : zero; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float d) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator *(float d, Vector3 a) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator /(Vector3 a, float d) { return new Vector3(a.x / d, a.y / d, a.z / d); }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { return new Vector3(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.z, b.z, t)); }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) { return a + (b - a) * t; }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
        public static Vector3 Normalize(Vector3 v) { return v.normalized; }
        public override string ToString() { return x + "," + y + "," + z; }
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Vector4 zero { get { return new Vector4(0, 0, 0, 0); } }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity { get { return new Quaternion(0, 0, 0, 1); } }
        public static Quaternion Euler(float x, float y, float z) { return identity; }
        public static Quaternion Euler(Vector3 e) { return identity; }
        public static Quaternion AngleAxis(float angle, Vector3 axis) { return identity; }
        public static Quaternion LookRotation(Vector3 forward) { return identity; }
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) { return identity; }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return a; }
        public Vector3 eulerAngles { get { return Vector3.zero; } set { } }
        public static Quaternion operator *(Quaternion a, Quaternion b) { return a; }
        public static Vector3 operator *(Quaternion q, Vector3 v) { return v; }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public Vector2 min { get { return new Vector2(x, y); } }
        public Vector2 size { get { return new Vector2(width, height); } }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color black { get { return new Color(0, 0, 0, 1); } }
        public static Color clear { get { return new Color(0, 0, 0, 0); } }
        public static Color red { get { return new Color(1, 0, 0, 1); } }
        public static Color green { get { return new Color(0, 1, 0, 1); } }
        public static Color blue { get { return new Color(0, 0, 1, 1); } }
        public static Color yellow { get { return new Color(1, 0.92f, 0.016f, 1); } }
        public static Color gray { get { return new Color(0.5f, 0.5f, 0.5f, 1); } }
        public static Color magenta { get { return new Color(1, 0, 1, 1); } }
        public static Color cyan { get { return new Color(0, 1, 1, 1); } }
        public static Color Lerp(Color a, Color b, float t)
        {
            return new Color(Mathf.Lerp(a.r, b.r, t), Mathf.Lerp(a.g, b.g, t), Mathf.Lerp(a.b, b.b, t), Mathf.Lerp(a.a, b.a, t));
        }
        public static Color operator *(Color a, float d) { return new Color(a.r * d, a.g * d, a.b * d, a.a * d); }
        public static Color operator *(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); }
        public static Color operator +(Color a, Color b) { return new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a); }
        public static implicit operator Color(Color32 c) { return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); }
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color32(Color c)
        {
            return new Color32((byte)Mathf.Clamp(c.r * 255f, 0, 255), (byte)Mathf.Clamp(c.g * 255f, 0, 255),
                               (byte)Mathf.Clamp(c.b * 255f, 0, 255), (byte)Mathf.Clamp(c.a * 255f, 0, 255));
        }
    }

    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string html, out Color color) { color = Color.white; return true; }
        public static string ToHtmlStringRGB(Color color) { return "FFFFFF"; }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public const float Infinity = float.PositiveInfinity;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public const float Epsilon = 1.401298E-45f;
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
        public static float Abs(float f) { return Math.Abs(f); }
        public static int Abs(int f) { return Math.Abs(f); }
        public static float Min(float a, float b) { return Math.Min(a, b); }
        public static int Min(int a, int b) { return Math.Min(a, b); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
        public static int Max(int a, int b) { return Math.Max(a, b); }
        public static float Clamp(float v, float min, float max) { return v < min ? min : (v > max ? max : v); }
        public static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float LerpUnclamped(float a, float b, float t) { return a + (b - a) * t; }
        public static float MoveTowards(float a, float b, float d) { return Math.Abs(b - a) <= d ? b : a + Math.Sign(b - a) * d; }
        public static float InverseLerp(float a, float b, float v) { return a == b ? 0f : Clamp01((v - a) / (b - a)); }
        public static float Sin(float f) { return (float)Math.Sin(f); }
        public static float Cos(float f) { return (float)Math.Cos(f); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Pow(float f, float p) { return (float)Math.Pow(f, p); }
        public static float Floor(float f) { return (float)Math.Floor(f); }
        public static float Ceil(float f) { return (float)Math.Ceiling(f); }
        public static int FloorToInt(float f) { return (int)Math.Floor(f); }
        public static int CeilToInt(float f) { return (int)Math.Ceiling(f); }
        public static int RoundToInt(float f) { return (int)Math.Round(f, MidpointRounding.AwayFromZero); }
        public static float Round(float f) { return (float)Math.Round(f, MidpointRounding.AwayFromZero); }
        public static float Repeat(float t, float length) { return Clamp(t - Floor(t / length) * length, 0f, length); }
        public static float PingPong(float t, float length) { t = Repeat(t, length * 2f); return length - Abs(t - length); }
        public static float SmoothStep(float from, float to, float t) { t = Clamp01(t); t = t * t * (3f - 2f * t); return from + (to - from) * t; }
        public static float Sign(float f) { return f >= 0f ? 1f : -1f; }
        public static bool Approximately(float a, float b) { return Math.Abs(b - a) < 1e-5f; }
    }

    public static class Random
    {
        private static readonly System.Random Rng = new System.Random(12345);
        public static float value { get { return (float)Rng.NextDouble(); } }
        public static float Range(float min, float max) { return min + (float)Rng.NextDouble() * (max - min); }
        public static int Range(int min, int max) { return Rng.Next(min, max); }
        public static void InitState(int seed) { }
        public static Vector3 insideUnitSphere { get { return Vector3.zero; } }
    }

    // ------------------------------------------------------------------ scene graph

    public class Component : Object
    {
        public Transform transform { get { return null; } }
        public GameObject gameObject { get { return null; } }
        public T GetComponent<T>() where T : class { return null; }
        public Component GetComponent(Type t) { return null; }
        public T GetComponentInChildren<T>() where T : class { return null; }
        public T[] GetComponentsInChildren<T>() where T : class { return new T[0]; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class { return new T[0]; }
        public T GetComponentInParent<T>() where T : class { return null; }
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
        public bool isActiveAndEnabled { get { return enabled; } }
    }

    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 lossyScale { get { return Vector3.one; } }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 eulerAngles { get; set; }
        public Vector3 localEulerAngles { get; set; }
        public Vector3 forward { get; set; }
        public Vector3 right { get { return Vector3.right; } }
        public Vector3 up { get { return Vector3.up; } }
        public Transform parent { get; set; }
        public Transform root { get { return this; } }
        public int childCount { get { return 0; } }
        public void SetParent(Transform p) { }
        public void SetParent(Transform p, bool worldPositionStays) { }
        public Transform GetChild(int index) { return null; }
        public Transform Find(string n) { return null; }
        public void SetAsLastSibling() { }
        public void SetAsFirstSibling() { }
        public void SetSiblingIndex(int index) { }
        public int GetSiblingIndex() { return 0; }
        public void LookAt(Vector3 worldPosition) { }
        public void LookAt(Transform target) { }
        public void Translate(Vector3 translation, Space space) { }
        public void Rotate(Vector3 euler) { }
        public void DetachChildren() { }
        public IEnumerator GetEnumerator() { yield break; }
    }

    public sealed class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Rect rect { get { return new Rect(0, 0, 100, 100); } }
        public void SetInsetAndSizeFromParentEdge(Edge edge, float inset, float size) { }
        public enum Edge { Left, Right, Top, Bottom }
        public enum Axis { Horizontal, Vertical }
        public void SetSizeWithCurrentAnchors(Axis axis, float size) { }
    }

    public sealed class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public GameObject(string name, params Type[] components) { this.name = name; }

        public Transform transform { get { return null; } }
        public bool activeSelf { get { return true; } }
        public bool activeInHierarchy { get { return true; } }
        public int layer { get; set; }
        public string tag { get; set; }
        public void SetActive(bool value) { }
        public T AddComponent<T>() where T : Component { return null; }
        public Component AddComponent(Type t) { return null; }
        public T GetComponent<T>() where T : class { return null; }
        public T GetComponentInChildren<T>() where T : class { return null; }
        public T[] GetComponentsInChildren<T>() where T : class { return new T[0]; }
        public static GameObject CreatePrimitive(PrimitiveType type) { return null; }
        public static GameObject Find(string name) { return null; }
    }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) { return null; }
        public void StopCoroutine(IEnumerator routine) { }
        public void StopCoroutine(Coroutine routine) { }
        public void StopAllCoroutines() { }
        public void Invoke(string method, float time) { }
        public void CancelInvoke() { }
        public bool IsInvoking() { return false; }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject { return null; }
    }

    public sealed class Coroutine : Object { }
    public class YieldInstruction { }
    public sealed class WaitForSeconds : YieldInstruction { public WaitForSeconds(float seconds) { } }
    public sealed class WaitForSecondsRealtime : CustomYieldInstruction
    {
        public WaitForSecondsRealtime(float seconds) { }
        public override bool keepWaiting { get { return false; } }
    }
    public sealed class WaitForEndOfFrame : YieldInstruction { }
    public sealed class WaitForFixedUpdate : YieldInstruction { }
    public abstract class CustomYieldInstruction : IEnumerator
    {
        public abstract bool keepWaiting { get; }
        public object Current { get { return null; } }
        public bool MoveNext() { return keepWaiting; }
        public void Reset() { }
    }

    // ------------------------------------------------------------------ engine services

    public static class Debug
    {
        public static void Log(object message) { }
        public static void Log(object message, Object context) { }
        public static void LogWarning(object message) { }
        public static void LogWarning(object message, Object context) { }
        public static void LogError(object message) { }
        public static void LogError(object message, Object context) { }
        public static void LogException(Exception e) { }
    }

    public static class Time
    {
        public static float deltaTime { get { return 0.016f; } }
        public static float unscaledDeltaTime { get { return 0.016f; } }
        public static float time { get { return 0f; } }
        public static float unscaledTime { get { return 0f; } }
        public static float timeScale { get; set; }
        public static int frameCount { get { return 0; } }
    }

    public static class Screen
    {
        public static int width { get { return 1080; } }
        public static int height { get { return 1920; } }
        public static ScreenOrientation orientation { get; set; }
        public static int sleepTimeout { get; set; }
        public static void SetResolution(int w, int h, bool fullscreen) { }
    }

    public static class SleepTimeout { public const int NeverSleep = -1; public const int SystemSetting = -2; }

    public static class Application
    {
        public static int targetFrameRate { get; set; }
        public static string persistentDataPath { get { return ""; } }
        public static string dataPath { get { return ""; } }
        public static bool isEditor { get { return true; } }
        public static bool isPlaying { get { return true; } }
        public static RuntimePlatform platform { get { return RuntimePlatform.WindowsEditor; } }
        public static string version { get { return "0.1"; } }
        public static void Quit() { }
    }

    public static class PlayerPrefs
    {
        public static string GetString(string key, string defaultValue) { return defaultValue; }
        public static void SetString(string key, string value) { }
        public static int GetInt(string key, int defaultValue) { return defaultValue; }
        public static void SetInt(string key, int value) { }
        public static bool HasKey(string key) { return false; }
        public static void DeleteKey(string key) { }
        public static void DeleteAll() { }
        public static void Save() { }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object { return null; }
        public static Object Load(string path) { return null; }
        public static T GetBuiltinResource<T>(string path) where T : Object { return null; }
    }

    public static class Physics { public static bool autoSimulation { get; set; } }
    public static class QualitySettings { public static int vSyncCount { get; set; } public static int antiAliasing { get; set; } }

    public sealed class TextAsset : Object
    {
        public string text { get { return ""; } }
        public byte[] bytes { get { return new byte[0]; } }
    }

    // ------------------------------------------------------------------ rendering

    public class Texture : Object
    {
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
        public int width { get { return 0; } }
        public int height { get { return 0; } }
        public int anisoLevel { get; set; }
    }

    public sealed class Texture2D : Texture
    {
        public Texture2D(int width, int height) { }
        public Texture2D(int width, int height, TextureFormat format, bool mipChain) { }
        public void SetPixel(int x, int y, Color color) { }
        public void SetPixels(Color[] colors) { }
        public void SetPixels32(Color32[] colors) { }
        public Color GetPixel(int x, int y) { return Color.white; }
        public void Apply() { }
        public void Apply(bool updateMipmaps) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
        public static Texture2D whiteTexture { get { return null; } }
    }

    public sealed class Sprite : Object
    {
        public Texture2D texture { get { return null; } }
        public Rect rect { get { return new Rect(0, 0, 1, 1); } }
        public Vector4 border { get { return Vector4.zero; } }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot) { return null; }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) { return null; }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType) { return null; }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border) { return null; }
    }

    public sealed class Shader : Object
    {
        public static Shader Find(string name) { return null; }
        public static int PropertyToID(string name) { return 0; }
    }

    public class Material : Object
    {
        public Material(Shader shader) { }
        public Material(Material source) { }
        public Color color { get; set; }
        public Shader shader { get; set; }
        public int renderQueue { get; set; }
        public Texture mainTexture { get; set; }
        public void SetColor(string name, Color value) { }
        public void SetColor(int id, Color value) { }
        public void SetFloat(string name, float value) { }
        public void SetFloat(int id, float value) { }
        public void SetVector(string name, Vector4 value) { }
        public void SetTexture(string name, Texture value) { }
        public void SetInt(string name, int value) { }
        public Color GetColor(string name) { return Color.white; }
        public float GetFloat(string name) { return 0f; }
        public bool HasProperty(string name) { return true; }
        public void EnableKeyword(string keyword) { }
        public void DisableKeyword(string keyword) { }
    }

    public sealed class Mesh : Object
    {
        public Vector3[] vertices { get; set; }
        public int[] triangles { get; set; }
        public Vector3[] normals { get; set; }
        public Vector2[] uv { get; set; }
        public Color[] colors { get; set; }
        public Color32[] colors32 { get; set; }
        public void Clear() { }
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
        public void MarkDynamic() { }
    }

    public class Renderer : Component
    {
        public bool enabled { get; set; }
        public Material material { get; set; }
        public Material sharedMaterial { get; set; }
        public Material[] materials { get; set; }
        public int sortingOrder { get; set; }
        public bool receiveShadows { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
    }

    public sealed class MeshRenderer : Renderer { }
    public sealed class MeshFilter : Component { public Mesh mesh { get; set; } public Mesh sharedMesh { get; set; } }
    public class Collider : Component { public bool enabled { get; set; } public bool isTrigger { get; set; } }
    public sealed class BoxCollider : Collider { }
    public sealed class SphereCollider : Collider { }
    public sealed class CapsuleCollider : Collider { }
    public sealed class MeshCollider : Collider { }

    public sealed class Camera : Behaviour
    {
        public static Camera main { get { return null; } }
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; }
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public float depth { get; set; }
        public float aspect { get { return 0.5625f; } }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public int cullingMask { get; set; }
        public Vector3 WorldToScreenPoint(Vector3 position) { return Vector3.zero; }
        public Vector3 ScreenToWorldPoint(Vector3 position) { return Vector3.zero; }
        public bool allowHDR { get; set; }
        public bool allowMSAA { get; set; }
    }

    public sealed class Light : Behaviour
    {
        public LightType type { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public float range { get; set; }
        public LightShadows shadows { get; set; }
        public float shadowStrength { get; set; }
    }

    public static class RenderSettings
    {
        public static Color ambientLight { get; set; }
        public static Material skybox { get; set; }
        public static bool fog { get; set; }
        public static Color fogColor { get; set; }
        public static float fogStartDistance { get; set; }
        public static float fogEndDistance { get; set; }
        public static Rendering.AmbientMode ambientMode { get; set; }
    }

    public sealed class Font : Object
    {
        public int fontSize { get; set; }
        public static Font CreateDynamicFontFromOSFont(string fontname, int size) { return null; }
        public static string[] GetOSInstalledFontNames() { return new string[0]; }
    }

    // ------------------------------------------------------------------ audio

    public sealed class AudioClip : Object
    {
        public float length { get { return 0f; } }
        public int samples { get { return 0; } }
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) { return null; }
        public bool SetData(float[] data, int offsetSamples) { return true; }
    }

    public sealed class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public float volume { get; set; }
        public float pitch { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public void Play() { }
        public void Stop() { }
        public void PlayOneShot(AudioClip clip) { }
        public void PlayOneShot(AudioClip clip, float volumeScale) { }
    }

    public sealed class AudioListener : Behaviour { public static float volume { get; set; } }
}

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }
    public static class GraphicsSettings
    {
        public static Object currentRenderPipeline { get { return null; } }
        public static Object defaultRenderPipeline { get { return null; } }
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name { get { return "Main"; } }
        public string path { get { return ""; } }
        public bool IsValid() { return true; }
        public bool isLoaded { get { return true; } }
    }

    public static class SceneManager
    {
        public static Scene GetActiveScene() { return new Scene(); }
        public static void LoadScene(string name) { }
        public static int sceneCount { get { return 1; } }
    }
}

namespace UnityEngine.Events
{
    public class UnityEventBase { public void RemoveAllListeners() { } }
    public class UnityEvent : UnityEventBase
    {
        public void AddListener(UnityAction call) { }
        public void RemoveListener(UnityAction call) { }
        public void Invoke() { }
    }
    public class UnityEvent<T> : UnityEventBase
    {
        public void AddListener(UnityAction<T> call) { }
        public void RemoveListener(UnityAction<T> call) { }
        public void Invoke(T arg) { }
    }
    public delegate void UnityAction();
    public delegate void UnityAction<T>(T arg0);
}
