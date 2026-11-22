using System;

namespace Gravexel.Core
{
    /// <summary>RGBA8 image, straight alpha, row 0 at the top.</summary>
    public sealed class PixelImage
    {
        public readonly int W, H;
        public readonly byte[] Rgba;
        public PixelImage(int w, int h) { W = w; H = h; Rgba = new byte[w * h * 4]; }

        public void Set(int x, int y, Col c)
        {
            int i = (y * W + x) * 4;
            Rgba[i] = (byte)(Mathx.Clamp01(c.R) * 255 + 0.5f);
            Rgba[i + 1] = (byte)(Mathx.Clamp01(c.G) * 255 + 0.5f);
            Rgba[i + 2] = (byte)(Mathx.Clamp01(c.B) * 255 + 0.5f);
            Rgba[i + 3] = (byte)(Mathx.Clamp01(c.A) * 255 + 0.5f);
        }
    }

    /// <summary>Texture ids. Every texture is generated in code, so the game ships with no image files.</summary>
    public static class Tx
    {
        public const int Pixel = 1, Glow = 2, GlowLine = 3, Circle = 4, Ring = 5, RingSoft = 6, Spike = 7, Saw = 8,
            Star = 9, StarOutline = 10, Chevron = 11, Key = 12, Lock = 13,
            BodyCube = 14, BodyOrb = 15, BodyPrism = 16, BodyHex = 17, BodyStar = 18,
            Eye = 19, Skyline1 = 20, Skyline2 = 21, HexOutline = 22, Swirl = 23, Beacon = 24, Switch = 25, Gate = 26,
            ToggleBlock = 27, Emitter = 28, FadeH = 29,
            IconPause = 40, IconPlay = 41, IconRetry = 42, IconHome = 43, IconGear = 44, IconGrid = 45, IconBack = 46,
            IconCheck = 47, IconPower = 48, IconSound = 49, IconUser = 50,
            LogoTop = 60, LogoBottom = 61;

        public static int BodyFor(int shape)
        {
            switch (shape) { case 1: return BodyOrb; case 2: return BodyPrism; case 3: return BodyHex; case 4: return BodyStar; default: return BodyCube; }
        }
    }

    public static class TextureGen
    {
        delegate Col PixelFn(float x, float y);

        public static void RegisterAll(IRenderer r)
        {
            r.RegisterTexture(Tx.Pixel, Gen(4, 4, (x, y) => Col.White));
            r.RegisterTexture(Tx.Glow, Gen(128, 128, (x, y) =>
            {
                float d = Mathx.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64)) / 64f;
                float a = (float)Math.Pow(Mathx.Clamp01(1 - d), 2.2);
                return Col.White.WithA(a);
            }));
            r.RegisterTexture(Tx.GlowLine, Gen(8, 64, (x, y) =>
            {
                float d = Math.Abs(y - 32) / 32f;
                return Col.White.WithA((float)Math.Pow(Mathx.Clamp01(1 - d), 2.4));
            }));
            r.RegisterTexture(Tx.FadeH, Gen(64, 4, (x, y) => Col.White.WithA(1 - x / 64f)));
            r.RegisterTexture(Tx.Circle, Shape(128, (x, y) => Circle(x, y, 64, 64, 62)));
            r.RegisterTexture(Tx.Ring, Shape(128, (x, y) => Math.Abs(Circle(x, y, 64, 64, 56)) - 5));
            r.RegisterTexture(Tx.RingSoft, Gen(128, 128, (x, y) =>
            {
                float d = Math.Abs(Mathx.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64)) - 46) / 18f;
                return Col.White.WithA((float)Math.Pow(Mathx.Clamp01(1 - d), 2));
            }));
            r.RegisterTexture(Tx.Spike, Gen(64, 64, (x, y) =>
            {
                float d = Poly(x, y, new float[] { 32, 3, 61, 62, 3, 62 });
                float a = Mathx.Clamp01(0.5f - d);
                float shade = Mathx.Lerp(1f, 0.62f, y / 64f);
                float edge = Mathx.Clamp01(1 - Math.Abs(d + 3) / 2.5f) * 0.5f;
                return new Col(shade + edge, shade + edge, shade + edge, a);
            }));
            r.RegisterTexture(Tx.Saw, Gen(128, 128, (x, y) =>
            {
                float dx = x - 64, dy = y - 64; float rr = Mathx.Sqrt(dx * dx + dy * dy);
                float ang = (float)Math.Atan2(dy, dx);
                float tooth = Mathx.Repeat(ang / (2 * Mathx.PI) * 14f, 1f);
                float outer = 50 + 12 * (1 - tooth);
                float d = Math.Max(rr - outer, 14 - rr);
                float a = Mathx.Clamp01(0.5f - d);
                float groove = Mathx.Clamp01(1 - Math.Abs(rr - 32) / 3f);
                float shade = 1f - groove * 0.45f - Mathx.Clamp01((rr - 40) / 30f) * 0.25f;
                return new Col(shade, shade, shade, a);
            }));
            float[] star = StarPts(64, 64, 60, 25, 5);
            r.RegisterTexture(Tx.Star, Shape(128, (x, y) => Poly(x, y, star) - 1));
            r.RegisterTexture(Tx.StarOutline, Shape(128, (x, y) => Math.Abs(Poly(x, y, star) + 4) - 3.5f));
            r.RegisterTexture(Tx.Chevron, Shape(64, (x, y) =>
                Math.Min(Seg(x, y, 20, 10, 44, 32), Seg(x, y, 44, 32, 20, 54)) - 6.5f));
            r.RegisterTexture(Tx.Key, Shape(64, (x, y) =>
            {
                float ring = Math.Abs(Circle(x, y, 20, 32, 12)) - 4.5f;
                float shaft = Seg(x, y, 32, 32, 58, 32) - 4.5f;
                float t1 = Seg(x, y, 50, 32, 50, 44) - 4f;
                float t2 = Seg(x, y, 40, 32, 40, 41) - 4f;
                return Math.Min(Math.Min(ring, shaft), Math.Min(t1, t2));
            }));
            r.RegisterTexture(Tx.Lock, Shape(64, (x, y) =>
            {
                float body = RoundBox(x, y, 32, 40, 20, 15, 4);
                float shackle = Math.Max(Math.Abs(Circle(x, y, 32, 24, 12)) - 4, y - 28);
                float sides = Math.Min(Seg(x, y, 20, 24, 20, 30), Seg(x, y, 44, 24, 44, 30)) - 4;
                float hole = Circle(x, y, 32, 38, 4);
                return Math.Max(Math.Min(Math.Min(body, shackle), sides), -hole);
            }));

            // Character bodies: white with soft shading; tinted at draw time.
            r.RegisterTexture(Tx.BodyCube, Body(128, (x, y) => RoundBox(x, y, 64, 64, 56, 56, 22)));
            r.RegisterTexture(Tx.BodyOrb, Body(128, (x, y) => Circle(x, y, 64, 64, 60)));
            r.RegisterTexture(Tx.BodyPrism, Body(128, (x, y) => Poly(x, y, new float[] { 64, 2, 126, 64, 64, 126, 2, 64 }) + 4 - 8));
            float[] hex = StarPts(64, 64, 62, 62, 3, Mathx.PI / 6);
            r.RegisterTexture(Tx.BodyHex, Body(128, (x, y) => Poly(x, y, hex) + 6 - 6));
            float[] bstar = StarPts(64, 68, 64, 36, 5);
            r.RegisterTexture(Tx.BodyStar, Body(128, (x, y) => Poly(x, y, bstar) + 5 - 5));
            r.RegisterTexture(Tx.Eye, Shape(32, (x, y) => RoundBox(x, y, 16, 16, 12, 15, 10)));
            r.RegisterTexture(Tx.HexOutline, Shape(128, (x, y) => Math.Abs(Poly(x, y, hex)) - 3));

            r.RegisterTexture(Tx.Skyline1, Skyline(512, 220, 11, 0.55f));
            r.RegisterTexture(Tx.Skyline2, Skyline(512, 220, 29, 0.85f));
            r.RegisterTexture(Tx.Swirl, Gen(128, 128, (x, y) =>
            {
                float dx = x - 64, dy = y - 64; float rr = Mathx.Sqrt(dx * dx + dy * dy) / 62f;
                if (rr > 1) return Col.Clear;
                float ang = (float)Math.Atan2(dy, dx);
                float arm = 0.5f + 0.5f * Mathx.Sin(ang * 3 + rr * 9f);
                float a = (float)Math.Pow(arm, 3) * Mathx.Clamp01((1 - rr) * 3f) * Mathx.Clamp01(rr * 4);
                return Col.White.WithA(a);
            }));
            r.RegisterTexture(Tx.Beacon, Gen(64, 128, (x, y) =>
            {
                float pole = Seg(x, y, 32, 40, 32, 126) - 3;
                float gem = Poly(x, y, new float[] { 32, 4, 50, 22, 32, 40, 14, 22 });
                float d = Math.Min(pole, gem);
                return Col.White.WithA(Mathx.Clamp01(0.5f - d) * (gem < pole ? 1f : 0.7f));
            }));
            r.RegisterTexture(Tx.Switch, Shape(64, (x, y) =>
            {
                float frame = Math.Abs(RoundBox(x, y, 32, 32, 26, 26, 8)) - 3.5f;
                float dot = Circle(x, y, 32, 32, 11);
                return Math.Min(frame, dot);
            }));
            r.RegisterTexture(Tx.Gate, Gen(64, 64, (x, y) =>
            {
                float stripe = Mathx.Repeat((x + y) / 16f, 1f) < 0.5f ? 1f : 0.35f;
                float frame = (x < 4 || y < 4 || x > 59 || y > 59) ? 1f : 0f;
                return Col.White.WithA(Math.Max(stripe * 0.75f, frame));
            }));
            r.RegisterTexture(Tx.ToggleBlock, Gen(64, 64, (x, y) =>
            {
                float frame = (x < 5 || y < 5 || x > 58 || y > 58) ? 1f : 0f;
                float inner = (x > 12 && y > 12 && x < 51 && y < 51) ? 0.45f : 0.22f;
                return Col.White.WithA(Math.Max(frame, inner));
            }));
            r.RegisterTexture(Tx.Emitter, Shape(32, (x, y) => RoundBox(x, y, 16, 16, 14, 10, 4)));

            // UI icons (64x64, white)
            r.RegisterTexture(Tx.IconPause, Shape(64, (x, y) => Math.Min(RoundBox(x, y, 22, 32, 6, 20, 2), RoundBox(x, y, 42, 32, 6, 20, 2))));
            r.RegisterTexture(Tx.IconPlay, Shape(64, (x, y) => Poly(x, y, new float[] { 20, 10, 54, 32, 20, 54 }) + 1));
            r.RegisterTexture(Tx.IconRetry, Shape(64, (x, y) =>
            {
                float dx = x - 32, dy = y - 34; float ang = (float)Math.Atan2(dy, dx);
                float arc = Math.Abs(Mathx.Sqrt(dx * dx + dy * dy) - 18) - 4.5f;
                if (ang < -1.1f && ang > -2.3f) arc = 99;
                float head = Poly(x, y, new float[] { 34, 4, 48, 15, 34, 26 });
                return Math.Min(arc, head);
            }));
            r.RegisterTexture(Tx.IconHome, Shape(64, (x, y) =>
            {
                float roof = Poly(x, y, new float[] { 32, 6, 58, 30, 6, 30 });
                float body = RoundBox(x, y, 32, 42, 17, 16, 2);
                float door = RoundBox(x, y, 32, 50, 5, 9, 1);
                return Math.Max(Math.Min(roof, body), -door);
            }));
            r.RegisterTexture(Tx.IconGear, Shape(64, (x, y) =>
            {
                float dx = x - 32, dy = y - 32; float rr = Mathx.Sqrt(dx * dx + dy * dy);
                float ang = (float)Math.Atan2(dy, dx);
                float tooth = Mathx.Repeat(ang / (2 * Mathx.PI) * 8f, 1f) < 0.5f ? 27f : 20f;
                return Math.Max(rr - tooth, 9 - rr);
            }));
            r.RegisterTexture(Tx.IconGrid, Shape(64, (x, y) =>
            {
                float d = 99;
                for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++) d = Math.Min(d, RoundBox(x, y, 20 + i * 24, 20 + j * 24, 9, 9, 3));
                return d;
            }));
            r.RegisterTexture(Tx.IconBack, Shape(64, (x, y) =>
                Math.Min(Math.Min(Seg(x, y, 14, 32, 30, 16), Seg(x, y, 14, 32, 30, 48)), Seg(x, y, 14, 32, 52, 32)) - 5f));
            r.RegisterTexture(Tx.IconCheck, Shape(64, (x, y) => Math.Min(Seg(x, y, 12, 34, 26, 48), Seg(x, y, 26, 48, 52, 18)) - 5.5f));
            r.RegisterTexture(Tx.IconPower, Shape(64, (x, y) =>
            {
                float dx = x - 32, dy = y - 34; float ang = (float)Math.Atan2(dy, dx);
                float arc = Math.Abs(Mathx.Sqrt(dx * dx + dy * dy) - 20) - 4.5f;
                if (ang < -1.0f && ang > -2.14f) arc = 99;
                return Math.Min(arc, Seg(x, y, 32, 8, 32, 30) - 4.5f);
            }));
            r.RegisterTexture(Tx.IconSound, Shape(64, (x, y) =>
            {
                float spk = Poly(x, y, new float[] { 8, 24, 20, 24, 34, 10, 34, 54, 20, 40, 8, 40 });
                float dx = x - 34, dy = y - 32; float ang = (float)Math.Atan2(dy, dx);
                float w1 = Math.Abs(Mathx.Sqrt(dx * dx + dy * dy) - 12) - 3;
                float w2 = Math.Abs(Mathx.Sqrt(dx * dx + dy * dy) - 22) - 3;
                float waves = Math.Abs(ang) < 0.8f ? Math.Min(w1, w2) : 99;
                return Math.Min(spk, waves);
            }));
            // GRAVEXEL logo: the X made of two chevrons (split so each half can be coloured and animated)
            r.RegisterTexture(Tx.LogoTop, Shape(256, (x, y) => Poly(x, y, new float[] { 46, 39, 88, 39, 128, 79, 168, 39, 210, 39, 128, 121 })));
            r.RegisterTexture(Tx.LogoBottom, Shape(256, (x, y) => Poly(x, y, new float[] { 46, 217, 88, 217, 128, 175, 168, 217, 210, 217, 128, 135 })));
            r.RegisterTexture(Tx.IconUser, Shape(64, (x, y) => Math.Min(Circle(x, y, 32, 22, 12), RoundBox(x, y, 32, 52, 20, 12, 10))));
        }

        // ---------- helpers ----------

        static PixelImage Gen(int w, int h, PixelFn f)
        {
            var img = new PixelImage(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    img.Set(x, y, f(x + 0.5f, y + 0.5f));
            return img;
        }

        /// <summary>Image from a signed-distance function (pixels, negative inside), anti-aliased.</summary>
        static PixelImage Shape(int size, Func<float, float, float> sdf)
        {
            return Gen(size, size, (x, y) => Col.White.WithA(Mathx.Clamp01(0.5f - sdf(x, y))));
        }

        static PixelImage Body(int size, Func<float, float, float> sdf)
        {
            return Gen(size, size, (x, y) =>
            {
                float d = sdf(x, y);
                float a = Mathx.Clamp01(0.5f - d);
                float rim = Mathx.Clamp01(1 + d / 7f);           // 1 near the edge
                float top = 1 - y / size * 0.35f;                 // lighter at the top
                float v = Mathx.Clamp01(top * (1 - rim * 0.22f) + Mathx.Clamp01(1 - Math.Abs(d + 4) / 2f) * 0.25f);
                return new Col(v, v, v, a);
            });
        }

        static PixelImage Skyline(int w, int h, int seed, float density)
        {
            var rng = new Rng(seed);
            var heights = new float[w];
            var windows = new bool[w];
            int x = 0;
            while (x < w)
            {
                int bw = rng.Range(18, 54);
                float bh = rng.Range(0.25f, 0.95f) * h * density + h * 0.1f;
                bool antenna = rng.Chance(0.25f);
                for (int i = 0; i < bw && x + i < w; i++)
                {
                    heights[x + i] = bh;
                    windows[x + i] = (i % 6) >= 2 && i > 2 && i < bw - 2;
                    if (antenna && i == bw / 2) heights[x + i] = bh + 22;
                }
                x += bw + rng.Range(0, 6);
            }
            var winRng = new Rng(seed * 7 + 3);
            var lit = new bool[w * h / 36 + 64];
            for (int i = 0; i < lit.Length; i++) lit[i] = winRng.Chance(0.35f);
            return Gen(w, h, (px, py) =>
            {
                int ix = (int)px; float top = h - heights[ix];
                if (py < top) return Col.Clear;
                bool win = windows[ix] && ((int)py % 7) >= 3 && py > top + 6;
                int cell = ((int)py / 7) * (w / 6) + ix / 6;
                if (win && lit[cell % lit.Length]) return new Col(1, 1, 1, 0.95f);
                float fade = Mathx.Lerp(0.55f, 0.18f, (py - top) / Math.Max(1f, h - top));
                return new Col(0.55f, 0.55f, 0.55f, fade);
            });
        }

        static float Circle(float x, float y, float cx, float cy, float r)
        {
            float dx = x - cx, dy = y - cy; return Mathx.Sqrt(dx * dx + dy * dy) - r;
        }

        static float RoundBox(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            float qx = Math.Abs(x - cx) - hw + r, qy = Math.Abs(y - cy) - hh + r;
            float ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
            return Mathx.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - r;
        }

        static float Seg(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathx.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathx.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Signed distance to a polygon given as x,y pairs.</summary>
        static float Poly(float px, float py, float[] v)
        {
            int n = v.Length / 2;
            float d = float.MaxValue; bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float ax = v[j * 2], ay = v[j * 2 + 1], bx = v[i * 2], by = v[i * 2 + 1];
                d = Math.Min(d, Seg(px, py, ax, ay, bx, by));
                if (((by > py) != (ay > py)) && (px < (ax - bx) * (py - by) / (ay - by) + bx)) inside = !inside;
            }
            return inside ? -d : d;
        }

        static float[] StarPts(float cx, float cy, float ro, float ri, int points, float rot = 0)
        {
            var pts = new float[points * 4];
            for (int i = 0; i < points * 2; i++)
            {
                float r = (i % 2 == 0) ? ro : ri;
                float a = -Mathx.PI / 2 + rot + i * Mathx.PI / points;
                pts[i * 2] = cx + Mathx.Cos(a) * r; pts[i * 2 + 1] = cy + Mathx.Sin(a) * r;
            }
            return pts;
        }
    }
}
