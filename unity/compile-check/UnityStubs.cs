// Compile-time stand-in for the slice of the Unity API this project uses.
//
// This file is NOT part of the Unity project (it lives outside Assets/). It
// exists so the game scripts can be type-checked with a plain C# compiler on a
// machine that has no Unity install:
//
//     mcs -target:library -out:/tmp/rotgrid.dll -recurse:'unity/Rotgrid/Assets/Scripts/*.cs' \
//         unity/compile-check/UnityStubs.cs
//
// Signatures mirror the real UnityEngine ones. Bodies are deliberately empty.
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y); } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public Vector2 normalized { get { return this; } }
        public void Normalize() { }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator *(Vector2 a, float b) { return new Vector2(a.x * b, a.y * b); }
        public static Vector2 operator *(float b, Vector2 a) { return new Vector2(a.x * b, a.y * b); }
        public static Vector2 operator /(Vector2 a, float b) { return new Vector2(a.x / b, a.y / b); }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; this.z = 0; }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y + z * z); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public Vector3 normalized { get { return this; } }
        public void Normalize() { }
        public void Set(float a, float b, float c) { x = a; y = b; z = c; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 down { get { return new Vector3(0, -1, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 right { get { return new Vector3(1, 0, 0); } }
        public static float Distance(Vector3 a, Vector3 b) { return 0f; }
        public static float Dot(Vector3 a, Vector3 b) { return 0f; }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return zero; }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { return a; }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) { return a; }
        public static Vector3 Normalize(Vector3 a) { return a; }
        public static Vector3 Scale(Vector3 a, Vector3 b) { return a; }
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d) { return a; }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float b) { return new Vector3(a.x * b, a.y * b, a.z * b); }
        public static Vector3 operator *(float b, Vector3 a) { return new Vector3(a.x * b, a.y * b, a.z * b); }
        public static Vector3 operator /(Vector3 a, float b) { return new Vector3(a.x / b, a.y / b, a.z / b); }
        public static bool operator ==(Vector3 a, Vector3 b) { return false; }
        public static bool operator !=(Vector3 a, Vector3 b) { return true; }
        public override bool Equals(object o) { return false; }
        public override int GetHashCode() { return 0; }
        public override string ToString() { return ""; }
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity { get { return new Quaternion(0, 0, 0, 1); } }
        public Vector3 eulerAngles { get { return Vector3.zero; } set { } }
        public static Quaternion Euler(float x, float y, float z) { return identity; }
        public static Quaternion Euler(Vector3 v) { return identity; }
        public static Quaternion AngleAxis(float a, Vector3 axis) { return identity; }
        public static Quaternion LookRotation(Vector3 f) { return identity; }
        public static Quaternion LookRotation(Vector3 f, Vector3 u) { return identity; }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return a; }
        public static Vector3 operator *(Quaternion q, Vector3 v) { return v; }
        public static Quaternion operator *(Quaternion a, Quaternion b) { return a; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1, 1, 1); } }
        public static Color black { get { return new Color(0, 0, 0); } }
        public static Color clear { get { return new Color(0, 0, 0, 0); } }
        public static Color red { get { return new Color(1, 0, 0); } }
        public static Color green { get { return new Color(0, 1, 0); } }
        public static Color gray { get { return new Color(.5f, .5f, .5f); } }
        public static Color Lerp(Color a, Color b, float t) { return a; }
        public static Color operator *(Color a, float b) { return new Color(a.r * b, a.g * b, a.b * b, a.a * b); }
        public static Color operator *(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); }
        public static Color operator +(Color a, Color b) { return new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a); }
        public static implicit operator Vector4(Color c) { return new Vector4(c.r, c.g, c.b, c.a); }
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color(Color32 c) { return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); }
        public static implicit operator Color32(Color c) { return new Color32(0, 0, 0, 255); }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public float xMin { get { return x; } }
        public float yMin { get { return y; } }
        public float xMax { get { return x + width; } }
        public float yMax { get { return y + height; } }
        public Vector2 center { get { return new Vector2(x + width / 2, y + height / 2); } }
        public bool Contains(Vector2 p) { return false; }
    }

    public struct Bounds
    {
        public Vector3 center, extents, size, min, max;
        public Bounds(Vector3 c, Vector3 s) { center = c; size = s; extents = s; min = c; max = c; }
        public void Encapsulate(Bounds b) { }
        public void Encapsulate(Vector3 p) { }
    }

    public struct Ray
    {
        public Vector3 origin, direction;
        public Ray(Vector3 o, Vector3 d) { origin = o; direction = d; }
        public Vector3 GetPoint(float d) { return origin; }
    }

    public struct RaycastHit
    {
        public Vector3 point { get { return Vector3.zero; } }
        public Vector3 normal { get { return Vector3.zero; } }
        public float distance { get { return 0f; } }
        public Collider collider { get { return null; } }
        public Transform transform { get { return null; } }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public const float Infinity = float.PositiveInfinity;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
        public static float Abs(float f) { return Math.Abs(f); }
        public static int Abs(int f) { return Math.Abs(f); }
        public static float Sin(float f) { return (float)Math.Sin(f); }
        public static float Cos(float f) { return (float)Math.Cos(f); }
        public static float Tan(float f) { return (float)Math.Tan(f); }
        public static float Asin(float f) { return (float)Math.Asin(f); }
        public static float Atan(float f) { return (float)Math.Atan(f); }
        public static float Atan2(float a, float b) { return (float)Math.Atan2(a, b); }
        public static float Exp(float f) { return (float)Math.Exp(f); }
        public static float Log(float f) { return (float)Math.Log(f); }
        public static float Pow(float a, float b) { return (float)Math.Pow(a, b); }
        public static float Sign(float f) { return f < 0 ? -1f : 1f; }
        public static float Min(float a, float b) { return Math.Min(a, b); }
        public static int Min(int a, int b) { return Math.Min(a, b); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
        public static int Max(int a, int b) { return Math.Max(a, b); }
        public static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
        public static int Clamp(int v, int a, int b) { return v < a ? a : (v > b ? b : v); }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        public static float LerpUnclamped(float a, float b, float t) { return a + (b - a) * t; }
        public static float InverseLerp(float a, float b, float v) { return 0f; }
        public static float MoveTowards(float a, float b, float d) { return b; }
        public static float SmoothStep(float a, float b, float t) { return a; }
        public static float Repeat(float t, float len) { return t; }
        public static float PingPong(float t, float len) { return t; }
        public static int FloorToInt(float f) { return (int)Math.Floor(f); }
        public static int CeilToInt(float f) { return (int)Math.Ceiling(f); }
        public static int RoundToInt(float f) { return (int)Math.Round(f); }
        public static float Floor(float f) { return (float)Math.Floor(f); }
        public static float Ceil(float f) { return (float)Math.Ceiling(f); }
        public static float Round(float f) { return (float)Math.Round(f); }
        public static float PerlinNoise(float x, float y) { return 0f; }
        public static float DeltaAngle(float a, float b) { return 0f; }
    }

    public class Object
    {
        public string name { get; set; }
        public HideFlags hideFlags { get; set; }
        public static void Destroy(Object o) { }
        public static void Destroy(Object o, float t) { }
        public static void DestroyImmediate(Object o) { }
        public static void DontDestroyOnLoad(Object o) { }
        public static T Instantiate<T>(T o) where T : Object { return o; }
        public static bool operator ==(Object a, Object b) { return ReferenceEquals(a, b); }
        public static bool operator !=(Object a, Object b) { return !ReferenceEquals(a, b); }
        public override bool Equals(object o) { return ReferenceEquals(this, o); }
        public override int GetHashCode() { return 0; }
        public static implicit operator bool(Object o) { return !ReferenceEquals(o, null); }
    }

    public enum HideFlags { None = 0, HideAndDontSave = 61 }
    public enum Space { World, Self }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public enum CursorLockMode { None, Locked, Confined }
    public enum FogMode { Linear = 1, Exponential = 2, ExponentialSquared = 3 }
    public enum LightType { Spot, Directional, Point, Area }
    public enum LightShadows { None, Hard, Soft }
    public enum ShadowQuality { Disable, HardOnly, All }
    public enum TextureFormat { RGBA32 = 4, RGB24 = 3 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp, Mirror }
    public enum AudioRolloffMode { Logarithmic, Linear, Custom }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum ScaleMode { StretchToFill, ScaleAndCrop, ScaleToFit }
    public enum EventType { MouseDown, MouseUp, MouseMove, KeyDown, KeyUp, ScrollWheel, Repaint, Layout, Ignore, Used }
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }

    public class Component : Object
    {
        public Transform transform { get; set; }
        public GameObject gameObject { get; set; }
        public T GetComponent<T>() where T : class { return null; }
        public T GetComponentInChildren<T>() where T : class { return null; }
        public T[] GetComponentsInChildren<T>() where T : class { return new T[0]; }
        public T AddComponent<T>() where T : Component, new() { return new T(); }
    }

    public class Behaviour : Component { public bool enabled { get; set; } }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator r) { return null; }
        public void StopCoroutine(Coroutine c) { }
        public void StopAllCoroutines() { }
        public void Invoke(string m, float t) { }
        public void CancelInvoke() { }
    }

    public class Coroutine { }
    public class YieldInstruction { }
    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } }
    public class WaitForSecondsRealtime : YieldInstruction { public WaitForSecondsRealtime(float s) { } }
    public class WaitForEndOfFrame : YieldInstruction { }

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
        public Vector3 up { get; set; }
        public Vector3 right { get; set; }
        public Transform parent { get; set; }
        public int childCount { get { return 0; } }
        public Transform GetChild(int i) { return null; }
        public Transform Find(string n) { return null; }
        public void SetParent(Transform p) { }
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void LookAt(Vector3 t) { }
        public void Translate(Vector3 v) { }
        public void Rotate(Vector3 v) { }
        public void Rotate(float x, float y, float z) { }
        public Vector3 TransformPoint(Vector3 p) { return p; }
        public Vector3 InverseTransformPoint(Vector3 p) { return p; }
        public Vector3 TransformDirection(Vector3 p) { return p; }
        public IEnumerator GetEnumerator() { return null; }
    }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public Transform transform { get; set; }
        public bool activeSelf { get { return true; } }
        public int layer { get; set; }
        public string tag { get; set; }
        public void SetActive(bool a) { }
        public T AddComponent<T>() where T : Component, new() { return new T(); }
        public T GetComponent<T>() where T : class { return null; }
        public T GetComponentInChildren<T>() where T : class { return null; }
        public static GameObject CreatePrimitive(PrimitiveType t) { return new GameObject(); }
        public static GameObject Find(string n) { return null; }
    }

    public class Renderer : Component
    {
        public Material material { get; set; }
        public Material sharedMaterial { get; set; }
        public Material[] materials { get; set; }
        public bool enabled { get; set; }
        public Bounds bounds { get { return new Bounds(Vector3.zero, Vector3.one); } }
        public bool receiveShadows { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
    }

    public class MeshRenderer : Renderer { }
    public class MeshFilter : Component { public Mesh mesh { get; set; } public Mesh sharedMesh { get; set; } }

    public class Mesh : Object
    {
        public Vector3[] vertices { get; set; }
        public int[] triangles { get; set; }
        public Vector3[] normals { get; set; }
        public Vector2[] uv { get; set; }
        public Bounds bounds { get; set; }
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
        public void Clear() { }
    }

    public class Texture : Object
    {
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
        public int anisoLevel { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }

    public class Texture2D : Texture
    {
        public Texture2D(int w, int h) { }
        public Texture2D(int w, int h, TextureFormat f, bool mip) { }
        public void SetPixel(int x, int y, Color c) { }
        public void SetPixels(Color[] c) { }
        public void SetPixels32(Color32[] c) { }
        public Color GetPixel(int x, int y) { return Color.black; }
        public void Apply() { }
        public void Apply(bool mip) { }
        public static Texture2D whiteTexture { get { return null; } }
    }

    public class Shader : Object
    {
        public static Shader Find(string name) { return null; }
    }

    public class Material : Object
    {
        public Material(Shader s) { }
        public Material(Material m) { }
        public Color color { get; set; }
        public Shader shader { get; set; }
        public Texture mainTexture { get; set; }
        public Vector2 mainTextureScale { get; set; }
        public int renderQueue { get; set; }
        public bool HasProperty(string n) { return false; }
        public void SetFloat(string n, float v) { }
        public void SetColor(string n, Color c) { }
        public void SetTexture(string n, Texture t) { }
        public void SetInt(string n, int v) { }
        public void SetVector(string n, Vector4 v) { }
        public void EnableKeyword(string k) { }
        public void DisableKeyword(string k) { }
        public float GetFloat(string n) { return 0f; }
        public Color GetColor(string n) { return Color.black; }
    }

    public class Camera : Behaviour
    {
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public Color backgroundColor { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public int cullingMask { get; set; }
        public float depth { get; set; }
        public static Camera main { get { return null; } }
        public Ray ScreenPointToRay(Vector3 p) { return new Ray(); }
        public Vector3 WorldToScreenPoint(Vector3 p) { return Vector3.zero; }
    }

    public enum CameraClearFlags { Skybox = 1, Color = 2, Depth = 3, Nothing = 4 }

    public class Light : Behaviour
    {
        public LightType type { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public float range { get; set; }
        public float spotAngle { get; set; }
        public LightShadows shadows { get; set; }
        public float shadowBias { get; set; }
        public int cullingMask { get; set; }
    }

    public class AudioClip : Object
    {
        public float length { get { return 0f; } }
        public static AudioClip Create(string n, int lengthSamples, int channels, int freq, bool stream) { return null; }
        public bool SetData(float[] data, int offset) { return true; }
    }

    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public float volume { get; set; }
        public float pitch { get; set; }
        public float spatialBlend { get; set; }
        public float minDistance { get; set; }
        public float maxDistance { get; set; }
        public float dopplerLevel { get; set; }
        public AudioRolloffMode rolloffMode { get; set; }
        public bool isPlaying { get { return false; } }
        public void Play() { }
        public void Stop() { }
        public void PlayOneShot(AudioClip c, float v) { }
    }

    public class AudioListener : Behaviour { public static float volume { get; set; } }

    public class Collider : Component
    {
        public bool isTrigger { get; set; }
        public bool enabled { get; set; }
        public Bounds bounds { get { return new Bounds(Vector3.zero, Vector3.one); } }
    }

    public class BoxCollider : Collider { public Vector3 center { get; set; } public Vector3 size { get; set; } }
    public class SphereCollider : Collider { public Vector3 center { get; set; } public float radius { get; set; } }
    public class CapsuleCollider : Collider { public Vector3 center { get; set; } public float radius { get; set; } public float height { get; set; } }
    public class Rigidbody : Component { public bool isKinematic { get; set; } public bool useGravity { get; set; } }

    public static class Physics
    {
        public static bool Raycast(Ray r, out RaycastHit hit, float dist) { hit = new RaycastHit(); return false; }
        public static bool Raycast(Vector3 o, Vector3 d, out RaycastHit hit, float dist) { hit = new RaycastHit(); return false; }
        public static RaycastHit[] RaycastAll(Ray r, float dist) { return new RaycastHit[0]; }
        public static RaycastHit[] RaycastAll(Vector3 o, Vector3 d, float dist) { return new RaycastHit[0]; }
        public static int RaycastNonAlloc(Ray r, RaycastHit[] results, float dist) { return 0; }
        public static bool queriesHitTriggers { get; set; }
    }

    public static class Time
    {
        public static float deltaTime { get { return 0f; } }
        public static float unscaledDeltaTime { get { return 0f; } }
        public static float time { get { return 0f; } }
        public static float unscaledTime { get { return 0f; } }
        public static float timeScale { get; set; }
        public static int frameCount { get { return 0; } }
    }

    public static class Debug
    {
        public static void Log(object o) { }
        public static void LogWarning(object o) { }
        public static void LogError(object o) { }
        public static void DrawLine(Vector3 a, Vector3 b, Color c) { }
    }

    public static class Random
    {
        public static float value { get { return 0f; } }
        public static float Range(float a, float b) { return a; }
        public static int Range(int a, int b) { return a; }
        public static Vector3 insideUnitSphere { get { return Vector3.zero; } }
        public static void InitState(int seed) { }
    }

    public static class Application
    {
        public static bool isPlaying { get { return true; } }
        public static bool isEditor { get { return false; } }
        public static string persistentDataPath { get { return ""; } }
        public static int targetFrameRate { get; set; }
        public static void Quit() { }
    }

    public static class Screen
    {
        public static int width { get { return 1920; } }
        public static int height { get { return 1080; } }
        public static bool fullScreen { get; set; }
        public static void SetResolution(int w, int h, bool fs) { }
    }

    public static class Cursor
    {
        public static CursorLockMode lockState { get; set; }
        public static bool visible { get; set; }
    }

    public static class PlayerPrefs
    {
        public static void SetInt(string k, int v) { }
        public static void SetFloat(string k, float v) { }
        public static void SetString(string k, string v) { }
        public static int GetInt(string k, int d) { return d; }
        public static float GetFloat(string k, float d) { return d; }
        public static string GetString(string k, string d) { return d; }
        public static bool HasKey(string k) { return false; }
        public static void DeleteAll() { }
        public static void Save() { }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object { return null; }
        public static T GetBuiltinResource<T>(string name) where T : Object { return null; }
    }

    public static class QualitySettings
    {
        public static ShadowQuality shadows { get; set; }
        public static float shadowDistance { get; set; }
        public static int vSyncCount { get; set; }
        public static int antiAliasing { get; set; }
    }

    public static class RenderSettings
    {
        public static bool fog { get; set; }
        public static FogMode fogMode { get; set; }
        public static Color fogColor { get; set; }
        public static float fogDensity { get; set; }
        public static Color ambientLight { get; set; }
        public static Color ambientSkyColor { get; set; }
        public static Color ambientEquatorColor { get; set; }
        public static Color ambientGroundColor { get; set; }
        public static float ambientIntensity { get; set; }
        public static AmbientMode ambientMode { get; set; }
        public static Material skybox { get; set; }
    }

    public static class JsonUtility
    {
        public static string ToJson(object o) { return ""; }
        public static T FromJson<T>(string s) { return default(T); }
    }

    public static class SystemInfo { public static string deviceName { get { return ""; } } }

    public enum KeyCode
    {
        None = 0, Backspace = 8, Tab = 9, Return = 13, Escape = 27, Space = 32,
        Alpha0 = 48, Alpha1 = 49, Alpha2 = 50, Alpha3 = 51, Alpha4 = 52,
        Alpha5 = 53, Alpha6 = 54, Alpha7 = 55, Alpha8 = 56, Alpha9 = 57,
        A = 97, B = 98, C = 99, D = 100, E = 101, F = 102, G = 103, H = 104,
        I = 105, J = 106, K = 107, L = 108, M = 109, N = 110, O = 111, P = 112,
        Q = 113, R = 114, S = 115, T = 116, U = 117, V = 118, W = 119, X = 120,
        Y = 121, Z = 122,
        UpArrow = 273, DownArrow = 274, RightArrow = 275, LeftArrow = 276,
        LeftShift = 304, RightShift = 303, LeftControl = 306, RightControl = 305,
        LeftAlt = 308, RightAlt = 307,
        Mouse0 = 323, Mouse1 = 324, Mouse2 = 325,
        JoystickButton0 = 330, JoystickButton1 = 331, JoystickButton2 = 332,
        JoystickButton3 = 333, JoystickButton6 = 336, JoystickButton7 = 337,
        JoystickButton8 = 338, JoystickButton9 = 339,
    }

    public static class Input
    {
        public static bool GetKey(KeyCode k) { return false; }
        public static bool GetKeyDown(KeyCode k) { return false; }
        public static bool GetKeyUp(KeyCode k) { return false; }
        public static bool GetMouseButton(int b) { return false; }
        public static bool GetMouseButtonDown(int b) { return false; }
        public static float GetAxis(string n) { return 0f; }
        public static float GetAxisRaw(string n) { return 0f; }
        public static Vector3 mousePosition { get { return Vector3.zero; } }
        public static bool anyKeyDown { get { return false; } }
        public static string inputString { get { return ""; } }
    }

    public class Font : Object
    {
        public int fontSize { get; set; }
    }

    public class GUIStyleState
    {
        public Color textColor { get; set; }
        public Texture2D background { get; set; }
    }

    public class RectOffset
    {
        public RectOffset() { }
        public RectOffset(int l, int r, int t, int b) { }
        public int left { get; set; }
        public int right { get; set; }
        public int top { get; set; }
        public int bottom { get; set; }
    }

    public class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }
        public GUIStyleState normal { get; set; }
        public GUIStyleState hover { get; set; }
        public GUIStyleState active { get; set; }
        public GUIStyleState focused { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public RectOffset padding { get; set; }
        public RectOffset margin { get; set; }
        public RectOffset border { get; set; }
        public bool wordWrap { get; set; }
        public bool richText { get; set; }
        public float fixedHeight { get; set; }
        public float fixedWidth { get; set; }
        public Vector2 CalcSize(GUIContent c) { return Vector2.zero; }
        public void Draw(Rect r, GUIContent c, bool a, bool b, bool cc, bool d) { }
    }

    public class GUIContent
    {
        public GUIContent() { }
        public GUIContent(string text) { }
        public GUIContent(string text, string tooltip) { }
        public string text { get; set; }
    }

    public class GUISkin : Object
    {
        public GUIStyle box { get; set; }
        public GUIStyle label { get; set; }
        public GUIStyle button { get; set; }
    }

    public class Event
    {
        public static Event current { get { return null; } }
        public EventType type { get { return EventType.Repaint; } }
        public KeyCode keyCode { get { return KeyCode.None; } }
        public bool isKey { get { return false; } }
        public bool isMouse { get { return false; } }
        public int button { get { return 0; } }
        public Vector2 mousePosition { get { return Vector2.zero; } }
        public void Use() { }
    }

    public static class GUI
    {
        public static Color color { get; set; }
        public static Color contentColor { get; set; }
        public static Color backgroundColor { get; set; }
        public static int depth { get; set; }
        public static Matrix4x4 matrix { get; set; }
        public static GUISkin skin { get; set; }
        public static bool enabled { get; set; }
        public static void Label(Rect r, string t) { }
        public static void Label(Rect r, string t, GUIStyle s) { }
        public static void Box(Rect r, string t) { }
        public static void Box(Rect r, string t, GUIStyle s) { }
        public static bool Button(Rect r, string t) { return false; }
        public static bool Button(Rect r, string t, GUIStyle s) { return false; }
        public static void DrawTexture(Rect r, Texture t) { }
        public static void DrawTexture(Rect r, Texture t, ScaleMode m) { }
        public static void DrawTexture(Rect r, Texture t, ScaleMode m, bool alphaBlend) { }
        public static string TextField(Rect r, string text, int maxLength) { return text; }
        public static string TextField(Rect r, string text, int maxLength, GUIStyle s) { return text; }
        public static float HorizontalSlider(Rect r, float v, float a, float b) { return v; }
        public static bool Toggle(Rect r, bool v, string t) { return v; }
        public static void BeginGroup(Rect r) { }
        public static void EndGroup() { }
        public static Vector2 BeginScrollView(Rect pos, Vector2 scroll, Rect view) { return scroll; }
        public static void EndScrollView() { }
        public static void SetNextControlName(string n) { }
        public static void FocusControl(string n) { }
    }

    public struct Matrix4x4
    {
        public static Matrix4x4 identity { get { return new Matrix4x4(); } }
        public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s) { return identity; }
    }

    namespace Rendering
    {
        public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    }

    namespace SceneManagement
    {
        public static class SceneManager
        {
            public static void LoadScene(string name) { }
            public static void LoadScene(int index) { }
        }
    }
}

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public class AddComponentMenu : Attribute { public AddComponentMenu(string s) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public class DisallowMultipleComponent : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int o) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
}
