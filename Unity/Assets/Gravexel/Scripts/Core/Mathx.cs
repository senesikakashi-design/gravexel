using System;

namespace Gravexel.Core
{
    /// <summary>Engine-independent RGBA colour (0..1 floats, straight alpha).</summary>
    public struct Col
    {
        public float R, G, B, A;

        public Col(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }

        public static Col Hex(string hex, float a = 1f)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            int v = Convert.ToInt32(hex, 16);
            return new Col(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, a);
        }

        public Col WithA(float a) { return new Col(R, G, B, a); }
        public Col MulA(float k) { return new Col(R, G, B, A * k); }

        public static Col Lerp(Col a, Col b, float t)
        {
            t = Mathx.Clamp01(t);
            return new Col(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        public Col Brighten(float k) { return Lerp(this, new Col(1, 1, 1, A), k); }
        public Col Darken(float k) { return Lerp(this, new Col(0, 0, 0, A), k); }

        public static Col operator *(Col c, float k) { return new Col(c.R * k, c.G * k, c.B * k, c.A); }

        public static Col FromHsv(float h, float s, float v, float a = 1f)
        {
            h = Mathx.Repeat(h, 1f) * 6f;
            int i = (int)h; float f = h - i;
            float p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
            switch (i)
            {
                case 0: return new Col(v, t, p, a);
                case 1: return new Col(q, v, p, a);
                case 2: return new Col(p, v, t, a);
                case 3: return new Col(p, q, v, a);
                case 4: return new Col(t, p, v, a);
                default: return new Col(v, p, q, a);
            }
        }

        public static readonly Col White = new Col(1, 1, 1, 1);
        public static readonly Col Black = new Col(0, 0, 0, 1);
        public static readonly Col Clear = new Col(0, 0, 0, 0);
    }

    public static class Mathx
    {
        public const float PI = 3.14159265f;
        public static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
        public static int Clamp(int v, int a, int b) { return v < a ? a : (v > b ? b : v); }
        public static float Clamp01(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        public static float InvLerp(float a, float b, float v) { return Math.Abs(b - a) < 1e-6f ? 0 : Clamp01((v - a) / (b - a)); }
        public static float SmoothStep(float t) { t = Clamp01(t); return t * t * (3 - 2 * t); }
        public static float EaseOutCubic(float t) { t = Clamp01(t); float u = 1 - t; return 1 - u * u * u; }
        public static float EaseInCubic(float t) { t = Clamp01(t); return t * t * t; }
        public static float EaseOutBack(float t)
        {
            t = Clamp01(t); const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1; return 1 + c3 * u * u * u + c1 * u * u;
        }
        public static float EaseOutElastic(float t)
        {
            t = Clamp01(t);
            if (t == 0 || t == 1) return t;
            return (float)(Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * (2 * Math.PI / 3)) + 1);
        }
        /// <summary>Frame-rate independent exponential smoothing.</summary>
        public static float Damp(float a, float b, float sharpness, float dt)
        {
            return Lerp(a, b, 1f - (float)Math.Exp(-sharpness * dt));
        }
        public static float Repeat(float t, float len) { return t - (float)Math.Floor(t / len) * len; }
        public static float PingPong(float t, float len)
        {
            if (len <= 0) return 0;
            t = Repeat(t, len * 2f);
            return len - Math.Abs(t - len);
        }
        public static float Sin(float x) { return (float)Math.Sin(x); }
        public static float Cos(float x) { return (float)Math.Cos(x); }
        public static float Abs(float x) { return x < 0 ? -x : x; }
        public static float Sqrt(float x) { return (float)Math.Sqrt(x); }
        public static int FloorToInt(float x) { return (int)Math.Floor(x); }
        public static float Approach(float v, float target, float step)
        {
            if (v < target) return Math.Min(v + step, target);
            return Math.Max(v - step, target);
        }
    }

    /// <summary>Small deterministic random generator (xorshift32).</summary>
    public sealed class Rng
    {
        uint _s;
        public Rng(int seed) { _s = (uint)seed * 2654435761u + 1013904223u; if (_s == 0) _s = 1; }
        public uint NextU() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
        public float Next() { return (NextU() >> 8) / 16777216f; }
        public float Range(float a, float b) { return a + (b - a) * Next(); }
        public int Range(int a, int bExclusive) { return a + (int)(NextU() % (uint)Math.Max(1, bExclusive - a)); }
        public bool Chance(float p) { return Next() < p; }
    }

    public struct Rect
    {
        public float X, Y, W, H;
        public Rect(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        public float CX { get { return X + W * 0.5f; } }
        public float CY { get { return Y + H * 0.5f; } }
        public float Right { get { return X + W; } }
        public float Bottom { get { return Y + H; } }
        public bool Contains(float px, float py) { return px >= X && px <= X + W && py >= Y && py <= Y + H; }
        public Rect Inflate(float d) { return new Rect(X - d, Y - d, W + 2 * d, H + 2 * d); }
        public static Rect Centered(float cx, float cy, float w, float h) { return new Rect(cx - w / 2, cy - h / 2, w, h); }
    }
}
