using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using Gravexel.Core;

namespace Gravexel.Desktop
{
    /// <summary>
    /// Multi-threaded software renderer: bilinear, rotated, tinted sprites with alpha and additive blending
    /// into a 32-bit premultiplied frame buffer. Needs no GPU driver, so the game runs on any Windows PC.
    /// </summary>
    public sealed unsafe class SoftRenderer : IRenderer
    {
        sealed class Tex { public int W, H; public uint[] Px; public bool Solid; }
        sealed class Layer { public int W, H; public uint[] Px; public float Scale; public DrawCmd[] Source; public float VW, VH; }

        enum Kind : byte { Sprite, Glyph, Layer, Vignette, Gradient }

        struct Cmd
        {
            public Kind Kind; public Tex T; public Glyph G; public Layer L;
            public float CX, CY, HW, HH, Cos, Sin;
            public int MR, MG, MB, MA; public bool Add;
            public int X, Y; public float V;
            public uint C0, C1;
        }

        readonly Dictionary<int, Tex> _tex = new Dictionary<int, Tex>();
        readonly Dictionary<int, Layer> _layers = new Dictionary<int, Layer>();
        readonly List<Cmd> _cmds = new List<Cmd>(8192);
        readonly GlyphCache _glyphs;
        int _pw, _ph; float _scale;
        uint[] _buf = new uint[1];
        int[] _vx = new int[1], _vy = new int[1]; float _vStrength = -1; int _vW, _vH; readonly object _vlock = new object();

        public SoftRenderer(GlyphCache glyphs) { _glyphs = glyphs; Resize(1280, 720); }

        public int PixelW { get { return _pw; } }
        public int PixelH { get { return _ph; } }
        public uint[] Buffer { get { return _buf; } }
        public float Scale { get { return _scale; } }
        public float ViewW { get { return _pw / _scale; } }
        public float ViewH { get { return 1080f; } }

        public void Resize(int w, int h)
        {
            w = Math.Max(64, w); h = Math.Max(64, h);
            if (w == _pw && h == _ph) return;
            _pw = w; _ph = h; _scale = h / 1080f;
            _buf = new uint[w * h];
        }

        // ------------------------------------------------------------ IRenderer

        public void RegisterTexture(int id, PixelImage img)
        {
            var t = new Tex { W = img.W, H = img.H, Px = new uint[img.W * img.H], Solid = true };
            for (int i = 0; i < t.Px.Length; i++)
            {
                uint r = img.Rgba[i * 4], g = img.Rgba[i * 4 + 1], b = img.Rgba[i * 4 + 2], a = img.Rgba[i * 4 + 3];
                if (a != 255 || r != 255 || g != 255 || b != 255) t.Solid = false;
                t.Px[i] = (a << 24) | ((r * a / 255) << 16) | ((g * a / 255) << 8) | (b * a / 255);
            }
            _tex[id] = t;
        }

        public void Background(Col top, Col bottom)
        {
            _cmds.Add(new Cmd { Kind = Kind.Gradient, C0 = Pack(top), C1 = Pack(bottom) });
        }

        public void Sprite(int tex, float cx, float cy, float w, float h, float rot, Col c, Blend blend)
        {
            Tex t; if (!_tex.TryGetValue(tex, out t)) return;
            if (c.A <= 0.002f) return;
            var cmd = MakeSprite(t, cx * _scale, cy * _scale, w * _scale, h * _scale, rot, c, blend);
            if (cmd.HW > 0) _cmds.Add(cmd);
        }

        Cmd MakeSprite(Tex t, float cx, float cy, float w, float h, float rot, Col c, Blend blend)
        {
            float a = Mathx.Clamp01(c.A);
            // keep sub-pixel lines visible: widen to one pixel and fade instead
            if (Math.Abs(rot) < 0.0001f || Math.Abs(Math.Abs(rot) - Mathx.PI) < 0.0001f)
            {
                if (w < 1f && w > 0) { a *= w; w = 1f; }
                if (h < 1f && h > 0) { a *= h; h = 1f; }
            }
            if (w <= 0 || h <= 0) return new Cmd { HW = 0 };
            return new Cmd
            {
                Kind = Kind.Sprite, T = t, CX = cx, CY = cy, HW = w * 0.5f, HH = h * 0.5f,
                Cos = (float)Math.Cos(rot), Sin = (float)Math.Sin(rot),
                MR = (int)(Mathx.Clamp01(c.R) * a * 256), MG = (int)(Mathx.Clamp01(c.G) * a * 256), MB = (int)(Mathx.Clamp01(c.B) * a * 256), MA = (int)(a * 256),
                Add = blend == Gravexel.Core.Blend.Add
            };
        }

        public void Text(string text, float x, float y, float size, Col c, Align align, FontId font)
        {
            if (string.IsNullOrEmpty(text) || c.A <= 0.002f) return;
            var face = _glyphs.Face(font, Math.Max(6, (int)Math.Round(size * _scale)));
            float width = face.Measure(text);
            float pen = x * _scale;
            if (align == Align.Center) pen -= width / 2; else if (align == Align.Right) pen -= width;
            float baseline = y * _scale + face.CapHeight / 2f;
            float a = Mathx.Clamp01(c.A);
            int mr = (int)(Mathx.Clamp01(c.R) * a * 256), mg = (int)(Mathx.Clamp01(c.G) * a * 256), mb = (int)(Mathx.Clamp01(c.B) * a * 256), ma = (int)(a * 256);
            foreach (char ch in text)
            {
                var g = face.Get(ch);
                if (g.W > 0)
                    _cmds.Add(new Cmd { Kind = Kind.Glyph, G = g, X = (int)Math.Round(pen + g.OX), Y = (int)Math.Round(baseline + g.OY), MR = mr, MG = mg, MB = mb, MA = ma });
                pen += g.Adv;
            }
        }

        public float MeasureText(string text, float size, FontId font)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var face = _glyphs.Face(font, Math.Max(6, (int)Math.Round(size * _scale)));
            return face.Measure(text) / _scale;
        }

        public void BuildStaticLayer(int layerId, DrawCmd[] cmds, int count, float width, float height)
        {
            var src = new DrawCmd[count];
            Array.Copy(cmds, src, count);
            var l = new Layer { Source = src, VW = width, VH = height, Scale = -1 };
            _layers[layerId] = l;
        }

        void Rasterize(Layer l)
        {
            l.Scale = _scale;
            l.W = Math.Max(1, (int)Math.Ceiling(l.VW * _scale)); l.H = Math.Max(1, (int)Math.Ceiling(l.VH * _scale));
            l.Px = new uint[l.W * l.H];
            var list = new List<Cmd>(l.Source.Length);
            foreach (var d in l.Source)
            {
                Tex t; if (!_tex.TryGetValue(d.Tex, out t)) continue;
                var cmd = MakeSprite(t, d.X * _scale, d.Y * _scale, d.W * _scale, d.H * _scale, d.Rot, d.Color, d.Blend);
                if (cmd.HW > 0) list.Add(cmd);
            }
            int bands = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));
            int bh = (l.H + bands - 1) / bands;
            Parallel.For(0, bands, b =>
            {
                int y0 = b * bh, y1 = Math.Min(l.H, y0 + bh);
                fixed (uint* dst = l.Px)
                    for (int i = 0; i < list.Count; i++) { var c = list[i]; DrawSprite(dst, l.W, 0, l.W, y0, y1, ref c, true); }
            });
        }

        public void DrawStaticLayer(int layerId, float x, float y)
        {
            Layer l; if (!_layers.TryGetValue(layerId, out l)) return;
            if (Math.Abs(l.Scale - _scale) > 1e-5f) Rasterize(l);
            _cmds.Add(new Cmd { Kind = Kind.Layer, L = l, X = (int)Math.Round(x * _scale), Y = (int)Math.Round(y * _scale) });
        }

        public void Vignette(float strength) { _cmds.Add(new Cmd { Kind = Kind.Vignette, V = strength }); }

        // ------------------------------------------------------------ frame

        public void BeginFrame() { _cmds.Clear(); }

        public void EndFrame()
        {
            int bands = Math.Max(1, Math.Min(Environment.ProcessorCount, 12)) * 2;
            int bh = (_ph + bands - 1) / bands;
            Parallel.For(0, bands, b =>
            {
                int y0 = b * bh, y1 = Math.Min(_ph, y0 + bh);
                if (y0 >= y1) return;
                fixed (uint* dst = _buf)
                {
                    for (int i = y0 * _pw, e = y1 * _pw; i < e; i++) dst[i] = 0xFF000000u;
                    for (int i = 0; i < _cmds.Count; i++)
                    {
                        var c = _cmds[i];
                        switch (c.Kind)
                        {
                            case Kind.Sprite: DrawSprite(dst, _pw, 0, _pw, y0, y1, ref c, false); break;
                            case Kind.Glyph: DrawGlyph(dst, y0, y1, ref c); break;
                            case Kind.Layer: DrawLayer(dst, y0, y1, ref c); break;
                            case Kind.Gradient: DrawGradient(dst, y0, y1, c.C0, c.C1); break;
                            case Kind.Vignette: DrawVignette(dst, y0, y1, c.V); break;
                        }
                    }
                }
            });
        }

        static uint Pack(Col c)
        {
            return 0xFF000000u | ((uint)(Mathx.Clamp01(c.R) * 255) << 16) | ((uint)(Mathx.Clamp01(c.G) * 255) << 8) | (uint)(Mathx.Clamp01(c.B) * 255);
        }

        // ------------------------------------------------------------ rasterizers

        static void Limit(float a, float d, float max, ref float kmin, ref float kmax)
        {
            if (Math.Abs(d) < 1e-7f) { if (a < 0 || a >= max) kmax = kmin - 1; return; }
            float lo, hi;
            if (d > 0) { lo = -a / d; hi = (max - a) / d; } else { lo = (max - a) / d; hi = -a / d; }
            if (lo > kmin) kmin = lo;
            if (hi < kmax) kmax = hi;
        }

        static void DrawSprite(uint* dst, int stride, int cx0, int cx1, int cy0, int cy1, ref Cmd c, bool layerMode)
        {
            float ex = Math.Abs(c.Cos) * c.HW + Math.Abs(c.Sin) * c.HH, ey = Math.Abs(c.Sin) * c.HW + Math.Abs(c.Cos) * c.HH;
            int x0 = Math.Max(cx0, (int)Math.Floor(c.CX - ex)), x1 = Math.Min(cx1, (int)Math.Ceiling(c.CX + ex));
            int y0 = Math.Max(cy0, (int)Math.Floor(c.CY - ey)), y1 = Math.Min(cy1, (int)Math.Ceiling(c.CY + ey));
            if (x0 >= x1 || y0 >= y1) return;
            var t = c.T;
            int W = t.W, H = t.H;
            float sx = W / (2 * c.HW), sy = H / (2 * c.HH);
            float dtx = c.Cos * sx, dty = -c.Sin * sy;
            int mr = c.MR, mg = c.MG, mb = c.MB, ma = c.MA;
            bool add = c.Add;
            bool solid = t.Solid && Math.Abs(c.Sin) < 1e-5f;

            fixed (uint* tp = t.Px)
            {
                for (int y = y0; y < y1; y++)
                {
                    float dx0 = x0 + 0.5f - c.CX, dy = y + 0.5f - c.CY;
                    float u = dx0 * c.Cos + dy * c.Sin, v = -dx0 * c.Sin + dy * c.Cos;
                    float tx = u * sx + W * 0.5f, ty = v * sy + H * 0.5f;
                    float kmin = 0, kmax = x1 - x0 - 1;
                    Limit(tx, dtx, W, ref kmin, ref kmax);
                    Limit(ty, dty, H, ref kmin, ref kmax);
                    int ks = (int)Math.Ceiling(kmin - 1e-4f), ke = (int)Math.Floor(kmax + 1e-4f);
                    if (ks < 0) ks = 0; if (ke > x1 - x0 - 1) ke = x1 - x0 - 1;
                    if (ks > ke) continue;
                    uint* row = dst + y * stride + x0;

                    if (solid)
                    {
                        uint sr = (uint)(255 * mr >> 8), sg = (uint)(255 * mg >> 8), sb = (uint)(255 * mb >> 8), sa = (uint)(255 * ma >> 8);
                        for (int k = ks; k <= ke; k++) Blend(row + k, sa, sr, sg, sb, add, layerMode);
                        continue;
                    }

                    int ftx = (int)((tx + dtx * ks - 0.5f) * 65536f), fty = (int)((ty + dty * ks - 0.5f) * 65536f);
                    int dfx = (int)(dtx * 65536f), dfy = (int)(dty * 65536f);
                    for (int k = ks; k <= ke; k++, ftx += dfx, fty += dfy)
                    {
                        int ix = ftx >> 16, iy = fty >> 16;
                        uint wx = (uint)((ftx >> 8) & 255), wy = (uint)((fty >> 8) & 255);
                        int ix0 = ix < 0 ? 0 : (ix >= W ? W - 1 : ix), ix1 = ix + 1 < 0 ? 0 : (ix + 1 >= W ? W - 1 : ix + 1);
                        int iy0 = iy < 0 ? 0 : (iy >= H ? H - 1 : iy), iy1 = iy + 1 < 0 ? 0 : (iy + 1 >= H ? H - 1 : iy + 1);
                        uint p00 = tp[iy0 * W + ix0], p10 = tp[iy0 * W + ix1], p01 = tp[iy1 * W + ix0], p11 = tp[iy1 * W + ix1];
                        if ((p00 | p10 | p01 | p11) == 0) continue;
                        uint iwx = 256 - wx, iwy = 256 - wy;
                        uint rb0 = (((p00 & 0xFF00FF) * iwx + (p10 & 0xFF00FF) * wx) >> 8) & 0xFF00FF;
                        uint ag0 = ((((p00 >> 8) & 0xFF00FF) * iwx + ((p10 >> 8) & 0xFF00FF) * wx) >> 8) & 0xFF00FF;
                        uint rb1 = (((p01 & 0xFF00FF) * iwx + (p11 & 0xFF00FF) * wx) >> 8) & 0xFF00FF;
                        uint ag1 = ((((p01 >> 8) & 0xFF00FF) * iwx + ((p11 >> 8) & 0xFF00FF) * wx) >> 8) & 0xFF00FF;
                        uint rb = ((rb0 * iwy + rb1 * wy) >> 8) & 0xFF00FF;
                        uint ag = ((ag0 * iwy + ag1 * wy) >> 8) & 0xFF00FF;
                        uint a = (ag >> 16) * (uint)ma >> 8;
                        if (a == 0 && !add) continue;
                        uint r = (rb >> 16) * (uint)mr >> 8, g = (ag & 0xFF) * (uint)mg >> 8, b = (rb & 0xFF) * (uint)mb >> 8;
                        Blend(row + k, a, r, g, b, add, layerMode);
                    }
                }
            }
        }

        static void Blend(uint* p, uint a, uint r, uint g, uint b, bool add, bool layerMode)
        {
            uint d = *p;
            uint dr = (d >> 16) & 255, dg = (d >> 8) & 255, db = d & 255, da = d >> 24;
            if (add)
            {
                dr += r; dg += g; db += b;
                if (dr > 255) dr = 255; if (dg > 255) dg = 255; if (db > 255) db = 255;
            }
            else
            {
                uint inv = 256 - a - (a >> 7);
                dr = r + (dr * inv >> 8); dg = g + (dg * inv >> 8); db = b + (db * inv >> 8);
                if (layerMode) da = a + (da * inv >> 8);
                if (dr > 255) dr = 255; if (dg > 255) dg = 255; if (db > 255) db = 255; if (da > 255) da = 255;
            }
            *p = (layerMode ? da << 24 : 0xFF000000u) | (dr << 16) | (dg << 8) | db;
        }

        void DrawGlyph(uint* dst, int cy0, int cy1, ref Cmd c)
        {
            var g = c.G;
            int x0 = Math.Max(0, c.X), x1 = Math.Min(_pw, c.X + g.W);
            int y0 = Math.Max(cy0, c.Y), y1 = Math.Min(cy1, c.Y + g.H);
            if (x0 >= x1 || y0 >= y1) return;
            uint mr = (uint)c.MR, mg = (uint)c.MG, mb = (uint)c.MB, ma = (uint)c.MA;
            fixed (byte* ap = g.A)
                for (int y = y0; y < y1; y++)
                {
                    byte* arow = ap + (y - c.Y) * g.W - c.X;
                    uint* row = dst + y * _pw;
                    for (int x = x0; x < x1; x++)
                    {
                        uint av = arow[x];
                        if (av == 0) continue;
                        Blend(row + x, av * ma >> 8, av * mr >> 8, av * mg >> 8, av * mb >> 8, false, false);
                    }
                }
        }

        void DrawLayer(uint* dst, int cy0, int cy1, ref Cmd c)
        {
            var l = c.L;
            int x0 = Math.Max(0, c.X), x1 = Math.Min(_pw, c.X + l.W);
            int y0 = Math.Max(cy0, c.Y), y1 = Math.Min(cy1, c.Y + l.H);
            if (x0 >= x1 || y0 >= y1) return;
            fixed (uint* lp = l.Px)
                for (int y = y0; y < y1; y++)
                {
                    uint* src = lp + (y - c.Y) * l.W - c.X;
                    uint* row = dst + y * _pw;
                    for (int x = x0; x < x1; x++)
                    {
                        uint s = src[x];
                        if (s == 0) continue;
                        uint a = s >> 24;
                        if (a == 255) { row[x] = s; continue; }
                        uint d = row[x];
                        uint inv = 256 - a - (a >> 7);
                        uint dr = ((s >> 16) & 255) + (((d >> 16) & 255) * inv >> 8);
                        uint dg = ((s >> 8) & 255) + (((d >> 8) & 255) * inv >> 8);
                        uint db = (s & 255) + ((d & 255) * inv >> 8);
                        if (dr > 255) dr = 255; if (dg > 255) dg = 255; if (db > 255) db = 255;
                        row[x] = 0xFF000000u | (dr << 16) | (dg << 8) | db;
                    }
                }
        }

        void DrawGradient(uint* dst, int y0, int y1, uint top, uint bottom)
        {
            for (int y = y0; y < y1; y++)
            {
                float t = (float)y / Math.Max(1, _ph - 1);
                uint r = (uint)Mathx.Lerp((top >> 16) & 255, (bottom >> 16) & 255, t);
                uint g = (uint)Mathx.Lerp((top >> 8) & 255, (bottom >> 8) & 255, t);
                uint b = (uint)Mathx.Lerp(top & 255, bottom & 255, t);
                uint v = 0xFF000000u | (r << 16) | (g << 8) | b;
                uint* row = dst + y * _pw;
                for (int x = 0; x < _pw; x++) row[x] = v;
            }
        }

        void DrawVignette(uint* dst, int y0, int y1, float strength)
        {
            lock (_vlock)
            {
                if (_vStrength != strength || _vW != _pw || _vH != _ph)
                {
                    var vx = new int[_pw]; var vy = new int[_ph];
                    for (int x = 0; x < _pw; x++) { float n = Math.Abs(x * 2f / _pw - 1); vx[x] = (int)(256 * (1 - strength * 0.55f * Math.Pow(n, 2.6))); }
                    for (int y = 0; y < _ph; y++) { float n = Math.Abs(y * 2f / _ph - 1); vy[y] = (int)(256 * (1 - strength * 0.65f * Math.Pow(n, 2.2))); }
                    _vx = vx; _vy = vy; _vStrength = strength; _vW = _pw; _vH = _ph;
                }
            }
            int[] fx = _vx, fy = _vy;
            for (int y = y0; y < y1; y++)
            {
                uint* row = dst + y * _pw; int ky = fy[y];
                for (int x = 0; x < _pw; x++)
                {
                    uint k = (uint)(fx[x] * ky >> 8);
                    if (k >= 255) continue;
                    uint d = row[x];
                    row[x] = 0xFF000000u | ((((d >> 16) & 255) * k >> 8) << 16) | ((((d >> 8) & 255) * k >> 8) << 8) | ((d & 255) * k >> 8);
                }
            }
        }

        /// <summary>Copies the frame into a bitmap (used for screenshots and as the non-Windows display path).</summary>
        public Bitmap ToBitmap()
        {
            var bmp = new Bitmap(_pw, _ph, PixelFormat.Format32bppRgb);
            var data = bmp.LockBits(new Rectangle(0, 0, _pw, _ph), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            fixed (uint* src = _buf)
                for (int y = 0; y < _ph; y++)
                    System.Buffer.MemoryCopy(src + y * _pw, (byte*)data.Scan0 + y * data.Stride, data.Stride, _pw * 4);
            bmp.UnlockBits(data);
            return bmp;
        }
    }
}
