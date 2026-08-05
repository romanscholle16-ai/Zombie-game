using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>Small shared helpers used across the game.</summary>
    public static class Util
    {
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Frame-rate independent exponential smoothing.</summary>
        public static float Damp(float a, float b, float lambda, float dt)
        {
            return Lerp(a, b, 1f - Mathf.Exp(-lambda * dt));
        }

        public static float Rand(float a, float b) => a + UnityEngine.Random.value * (b - a);
        public static int RandInt(int a, int b) => a + Mathf.FloorToInt(UnityEngine.Random.value * (b - a + 1));

        public static T Pick<T>(IList<T> list)
        {
            if (list == null || list.Count == 0) return default(T);
            return list[Mathf.Min(list.Count - 1, Mathf.FloorToInt(UnityEngine.Random.value * list.Count))];
        }

        public static float WrapAngle(float a)
        {
            while (a > Mathf.PI) a -= Mathf.PI * 2f;
            while (a < -Mathf.PI) a += Mathf.PI * 2f;
            return a;
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int h = s / 3600;
            int m = (s % 3600) / 60;
            int sec = s % 60;
            if (h > 0) return string.Format("{0}:{1:00}:{2:00}", h, m, sec);
            return string.Format("{0:00}:{1:00}", m, sec);
        }

        public static string FormatNumber(int n) => n.ToString("N0");

        /// <summary>Deterministic PRNG so map detail is identical every run.</summary>
        public sealed class Rng
        {
            uint _state;
            public Rng(int seed) { _state = (uint)(seed == 0 ? 1 : seed); }
            public float Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0xFFFFFF) / 16777216f;
            }
            public float Range(float a, float b) => a + Next() * (b - a);
            public int RangeInt(int a, int b) => a + Mathf.FloorToInt(Next() * (b - a));
        }

        public static int WeightedPick(float[] weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            if (total <= 0f) return 0;
            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < weights.Length; i++)
            {
                r -= weights[i];
                if (r <= 0f) return i;
            }
            return weights.Length - 1;
        }

        /// <summary>Scales a colour toward white without blowing out a channel.</summary>
        public static Color Lighten(Color c, float factor)
        {
            return new Color(
                Mathf.Min(1f, c.r * factor),
                Mathf.Min(1f, c.g * factor),
                Mathf.Min(1f, c.b * factor),
                c.a);
        }

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
        }
    }

    /// <summary>Axis-aligned collision box used by the hand-rolled character movement.</summary>
    public struct Aabb
    {
        public float x0, x1, z0, z1, y0, y1;
        public bool platform;

        public Aabb(float ax0, float ax1, float az0, float az1, float ay0, float ay1, bool isPlatform)
        {
            x0 = ax0; x1 = ax1; z0 = az0; z1 = az1; y0 = ay0; y1 = ay1; platform = isPlatform;
        }
    }
}
