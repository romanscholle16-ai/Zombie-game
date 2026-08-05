using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Procedural textures and shared materials.
    ///
    /// Every texture is drawn into a Texture2D at boot from noise and simple
    /// shapes, so the project ships with no imported art at all.
    /// </summary>
    public static class Art
    {
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Material> _mat = new Dictionary<string, Material>();
        static Shader _lit, _unlit;

        public static Shader LitShader
        {
            get
            {
                if (_lit == null)
                {
                    _lit = Shader.Find("Universal Render Pipeline/Lit");
                    if (_lit == null) _lit = Shader.Find("Standard");
                    if (_lit == null) _lit = Shader.Find("Diffuse");
                }
                return _lit;
            }
        }

        public static Shader UnlitShader
        {
            get
            {
                if (_unlit == null)
                {
                    _unlit = Shader.Find("Universal Render Pipeline/Unlit");
                    if (_unlit == null) _unlit = Shader.Find("Unlit/Texture");
                    if (_unlit == null) _unlit = LitShader;
                }
                return _unlit;
            }
        }

        public static void Clear()
        {
            _tex.Clear();
            _mat.Clear();
        }

        // ------------------------------------------------------------ materials
        /// <summary>Creates (or fetches) a lit material. Works in built-in RP and URP.</summary>
        public static Material Lit(string key, Color color, float smoothness = 0.15f, float metallic = 0f,
                                   Texture2D map = null, float tiling = 1f)
        {
            Material m;
            if (_mat.TryGetValue(key, out m)) return m;
            m = new Material(LitShader);
            m.name = key;
            SetColor(m, color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (map != null)
            {
                m.mainTexture = map;
                m.mainTextureScale = new Vector2(tiling, tiling);
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", map);
            }
            _mat[key] = m;
            return m;
        }

        /// <summary>Emissive panel material used for signage, strips and machines.</summary>
        public static Material Emissive(string key, Color color, float strength = 2f, Texture2D map = null)
        {
            Material m;
            if (_mat.TryGetValue(key, out m)) return m;
            m = new Material(LitShader);
            m.name = key;
            SetColor(m, color * 0.25f);
            if (map != null)
            {
                m.mainTexture = map;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", map);
            }
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * strength);
            if (m.HasProperty("_EmissionMap") && map != null) m.SetTexture("_EmissionMap", map);
            _mat[key] = m;
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            m.color = c;
        }

        public static void SetEmission(Material m, Color c)
        {
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
        }

        // ------------------------------------------------------------ textures
        static Texture2D Make(string key, int size, System.Action<Color[], int, Util.Rng> fill, int seed)
        {
            Texture2D t;
            if (_tex.TryGetValue(key, out t)) return t;
            t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t.name = key;
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            var px = new Color[size * size];
            fill(px, size, new Util.Rng(seed));
            t.SetPixels(px);
            t.Apply(true);
            _tex[key] = t;
            return t;
        }

        static void NoiseFill(Color[] px, int size, Util.Rng rng, Color baseCol, float spread)
        {
            for (int i = 0; i < px.Length; i++)
            {
                float n = (rng.Next() - 0.5f) * spread;
                px[i] = new Color(
                    Mathf.Clamp01(baseCol.r + n),
                    Mathf.Clamp01(baseCol.g + n),
                    Mathf.Clamp01(baseCol.b + n), 1f);
            }
        }

        /// <summary>Soft radial blotches for grime, rust and damp patches.</summary>
        static void Blotches(Color[] px, int size, Util.Rng rng, int count, Color col,
                             float rMin, float rMax, float alphaMax)
        {
            for (int b = 0; b < count; b++)
            {
                float cx = rng.Next() * size, cy = rng.Next() * size;
                float r = rng.Range(rMin, rMax);
                float a = alphaMax * rng.Range(0.4f, 1f);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r)), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + r));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r)), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(cy + r));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (d > r) continue;
                        float k = (1f - d / r) * a;
                        int i = y * size + x;
                        px[i] = Color.Lerp(px[i], col, k);
                    }
                }
            }
        }

        static void Streaks(Color[] px, int size, Util.Rng rng, int count, Color col, float alpha)
        {
            for (int s = 0; s < count; s++)
            {
                int x = Mathf.FloorToInt(rng.Next() * size);
                int len = Mathf.FloorToInt(rng.Range(size * 0.2f, size * 0.9f));
                int y0 = Mathf.FloorToInt(rng.Next() * size);
                int wdt = 1 + Mathf.FloorToInt(rng.Next() * 3);
                for (int k = 0; k < len; k++)
                {
                    int y = (y0 + k) % size;
                    for (int q = 0; q < wdt; q++)
                    {
                        int xx = (x + q) % size;
                        int i = y * size + xx;
                        px[i] = Color.Lerp(px[i], col, alpha * (1f - (float)k / len));
                    }
                }
            }
        }

        public static Texture2D Concrete()
        {
            return Make("concrete", 256, (px, s, rng) =>
            {
                NoiseFill(px, s, rng, Util.Hex(0x56584f), 0.14f);
                Blotches(px, s, rng, 26, Util.Hex(0x282a24), 12, 60, 0.35f);
                Blotches(px, s, rng, 12, Util.Hex(0x787a72), 10, 40, 0.18f);
                Streaks(px, s, rng, 10, Util.Hex(0x26281f), 0.4f);
            }, 1001);
        }

        public static Texture2D ConcreteFloor()
        {
            return Make("concreteFloor", 256, (px, s, rng) =>
            {
                NoiseFill(px, s, rng, Util.Hex(0x464845), 0.11f);
                Blotches(px, s, rng, 30, Util.Hex(0x20221e), 14, 70, 0.4f);
                Blotches(px, s, rng, 8, Util.Hex(0x60563c), 18, 50, 0.14f);
                // Expansion joints.
                for (int i = 0; i < s; i++)
                {
                    px[(s / 2) * s + i] = Util.Hex(0x1e201c);
                    px[i * s + (s / 2)] = Util.Hex(0x1e201c);
                }
            }, 1002);
        }

        public static Texture2D Tile()
        {
            return Make("tile", 256, (px, s, rng) =>
            {
                NoiseFill(px, s, rng, Util.Hex(0x969c96), 0.07f);
                int n = 4, cellPx = s / n;
                for (int gy = 0; gy < n; gy++)
                {
                    for (int gx = 0; gx < n; gx++)
                    {
                        Color c = Util.Hex(0xaab0a8) * rng.Range(0.92f, 1.08f);
                        for (int y = gy * cellPx + 2; y < (gy + 1) * cellPx - 2; y++)
                            for (int x = gx * cellPx + 2; x < (gx + 1) * cellPx - 2; x++)
                                px[y * s + x] = c;
                    }
                }
                Blotches(px, s, rng, 20, Util.Hex(0x3c483c), 8, 34, 0.3f);
                Blotches(px, s, rng, 6, Util.Hex(0x6e140e), 6, 26, 0.35f);
            }, 1003);
        }

        public static Texture2D MetalPanel()
        {
            return Make("metalPanel", 256, (px, s, rng) =>
            {
                NoiseFill(px, s, rng, Util.Hex(0x4a4e52), 0.08f);
                Color seam = Util.Hex(0x1c1e21);
                for (int i = 0; i < s; i++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        px[i * s + k] = seam; px[i * s + (s - 1 - k)] = seam;
                        px[k * s + i] = seam; px[(s - 1 - k) * s + i] = seam;
                        px[i * s + (s / 2 + k)] = seam;
                    }
                }
                // Rivets.
                for (int ry = 0; ry < 4; ry++)
                {
                    for (int rx = 0; rx < 4; rx++)
                    {
                        int cx = 18 + rx * (s - 36) / 3, cy = 18 + ry * (s - 36) / 3;
                        for (int y = -3; y <= 3; y++)
                            for (int x = -3; x <= 3; x++)
                                if (x * x + y * y <= 7)
                                {
                                    int i = ((cy + y + s) % s) * s + ((cx + x + s) % s);
                                    px[i] = Util.Hex(0x8c9296);
                                }
                    }
                }
                Blotches(px, s, rng, 18, Util.Hex(0x784018), 6, 30, 0.4f);
            }, 1004);
        }

        public static Texture2D RustMetal()
        {
            return Make("rustMetal", 256, (px, s, rng) =>
            {
                NoiseFill(px, s, rng, Util.Hex(0x5c3e28), 0.13f);
                Blotches(px, s, rng, 40, Util.Hex(0x964e1c), 8, 44, 0.45f);
                Blotches(px, s, rng, 18, Util.Hex(0x30221a), 10, 40, 0.5f);
            }, 1005);
        }

        public static Texture2D Wood()
        {
            return Make("wood", 256, (px, s, rng) =>
            {
                NoiseFill(px, s, rng, Util.Hex(0x684a2c), 0.08f);
                for (int g = 0; g < 90; g++)
                {
                    int y = Mathf.FloorToInt(rng.Next() * s);
                    Color c = Util.Hex(0x3a2614) * rng.Range(0.7f, 1.4f);
                    float a = rng.Range(0.25f, 0.65f);
                    int wdt = 1 + Mathf.FloorToInt(rng.Next() * 3);
                    for (int x = 0; x < s; x++)
                    {
                        int yy = (y + Mathf.RoundToInt(Mathf.Sin(x * 0.06f + g) * 2.4f) + s) % s;
                        for (int q = 0; q < wdt; q++)
                        {
                            int i = ((yy + q) % s) * s + x;
                            px[i] = Color.Lerp(px[i], c, a);
                        }
                    }
                }
                Blotches(px, s, rng, 10, Util.Hex(0x281a0e), 8, 30, 0.3f);
            }, 1006);
        }

        public static Texture2D Dirt()
        {
            return Make("dirt", 256, (px, s, rng) =>
            {
                NoiseFill(px, s, rng, Util.Hex(0x3a3228), 0.13f);
                Blotches(px, s, rng, 40, Util.Hex(0x1e1a14), 10, 50, 0.5f);
                Blotches(px, s, rng, 14, Util.Hex(0x524a38), 8, 32, 0.25f);
            }, 1007);
        }

        public static Texture2D Hazard()
        {
            return Make("hazard", 128, (px, s, rng) =>
            {
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                        px[y * s + x] = ((x + y) / 16) % 2 == 0 ? Util.Hex(0xc9a227) : Util.Hex(0x1b1d1a);
            }, 1008);
        }

        public static Texture2D ZombieSkin(int variant)
        {
            int[] tints = { 0x5c6a4e, 0x686250, 0x4e6058, 0x6e6054, 0x565c60 };
            return Make("zskin" + variant, 128, (px, s, rng) =>
            {
                NoiseFill(px, s, rng, Util.Hex(tints[variant % 5]), 0.11f);
                Blotches(px, s, rng, 26, Util.Hex(0x361a16), 6, 30, 0.55f);
                Blotches(px, s, rng, 14, Util.Hex(0x788460), 8, 26, 0.3f);
                Blotches(px, s, rng, 8, Util.Hex(0x14100e), 5, 18, 0.6f);
            }, 2000 + variant);
        }

        public static Texture2D Cloth(int variant)
        {
            int[] tints = { 0x34383c, 0x40342c, 0x2a322e, 0x3a2e38 };
            return Make("zcloth" + variant, 128, (px, s, rng) =>
            {
                NoiseFill(px, s, rng, Util.Hex(tints[variant % 4]), 0.09f);
                for (int y = 0; y < s; y += 6)
                    for (int x = 0; x < s; x++)
                        px[y * s + x] = Color.Lerp(px[y * s + x], Color.black, 0.35f);
                Blotches(px, s, rng, 16, Util.Hex(0x5a1410), 5, 22, 0.5f);
                Blotches(px, s, rng, 10, Util.Hex(0x12100e), 8, 26, 0.5f);
            }, 3000 + variant);
        }

        /// <summary>Soft radial sprite used for particles and glows.</summary>
        public static Texture2D Particle()
        {
            return Make("particle", 64, (px, s, rng) =>
            {
                float c = (s - 1) * 0.5f;
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                        float a = Mathf.Clamp01(1f - d);
                        px[y * s + x] = new Color(1f, 1f, 1f, a * a);
                    }
                }
            }, 1009);
        }

        /// <summary>Flat 1x1 texture, handy for IMGUI panels and bars.</summary>
        public static Texture2D Solid(Color c)
        {
            string key = "solid" + c.r + "_" + c.g + "_" + c.b + "_" + c.a;
            Texture2D t;
            if (_tex.TryGetValue(key, out t)) return t;
            t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixels(new[] { c });
            t.Apply();
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            _tex[key] = t;
            return t;
        }

        /// <summary>Vertical gradient strip, used for HUD panels and menu chrome.</summary>
        public static Texture2D Gradient(string key, Color top, Color bottom)
        {
            Texture2D t;
            if (_tex.TryGetValue(key, out t)) return t;
            t = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            var px = new Color[64];
            for (int i = 0; i < 64; i++) px[i] = Color.Lerp(bottom, top, i / 63f);
            t.SetPixels(px);
            t.Apply();
            t.wrapMode = TextureWrapMode.Clamp;
            _tex[key] = t;
            return t;
        }
    }
}
