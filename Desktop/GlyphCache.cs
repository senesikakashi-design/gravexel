using System;
using System.Collections.Generic;
using System.IO;
using Gravexel.Core;

namespace Gravexel.Desktop
{
    public sealed class Glyph { public int W, H, OX, OY; public float Adv; public byte[] A; }

    /// <summary>
    /// Text rendering from pre-rendered glyph atlases (fonts.bin, made by Tools/make_fonts.py).
    /// The nearest larger atlas size is resampled to the exact pixel size once and cached,
    /// so text looks the same on every Windows machine without installing or loading fonts.
    /// </summary>
    public sealed class GlyphCache
    {
        internal sealed class BaseSize { public int Px; public int Cap; public Dictionary<char, Glyph> Glyphs = new Dictionary<char, Glyph>(); }

        public sealed class FaceCache
        {
            internal BaseSize Base; internal float K;
            internal readonly Dictionary<char, Glyph> Glyphs = new Dictionary<char, Glyph>();
            public float CapHeight;
            static readonly Glyph Empty = new Glyph();

            public Glyph Get(char c)
            {
                Glyph g;
                if (Glyphs.TryGetValue(c, out g)) return g;
                Glyph src;
                if (!Base.Glyphs.TryGetValue(c, out src) && !Base.Glyphs.TryGetValue(char.ToUpperInvariant(c), out src) && !Base.Glyphs.TryGetValue('?', out src)) src = Empty;
                g = Scale(src, K);
                Glyphs[c] = g;
                return g;
            }

            public float Measure(string s) { float w = 0; foreach (char c in s) w += Get(c).Adv; return w; }
        }

        readonly List<BaseSize>[] _fonts = new List<BaseSize>[2];
        readonly Dictionary<int, FaceCache> _faces = new Dictionary<int, FaceCache>();

        public GlyphCache(byte[] fontsBin)
        {
            using (var br = new BinaryReader(new MemoryStream(fontsBin)))
            {
                if (new string(br.ReadChars(4)) != "GSF1") throw new InvalidDataException("bad font atlas");
                int fonts = br.ReadInt32();
                for (int f = 0; f < fonts && f < 2; f++)
                {
                    var list = new List<BaseSize>();
                    int sizes = br.ReadInt32();
                    for (int s = 0; s < sizes; s++)
                    {
                        var b = new BaseSize { Px = br.ReadInt32(), Cap = br.ReadInt32() };
                        int n = br.ReadInt32();
                        for (int i = 0; i < n; i++)
                        {
                            char ch = (char)br.ReadUInt16();
                            var g = new Glyph { Adv = br.ReadSingle(), OX = br.ReadInt16(), OY = br.ReadInt16(), W = br.ReadInt16(), H = br.ReadInt16() };
                            g.A = br.ReadBytes(g.W * g.H);
                            b.Glyphs[ch] = g;
                        }
                        list.Add(b);
                    }
                    _fonts[f] = list;
                }
            }
        }

        public FaceCache Face(FontId id, int px)
        {
            int key = ((int)id << 16) | px;
            FaceCache f;
            if (_faces.TryGetValue(key, out f)) return f;
            var list = _fonts[id == FontId.Title ? 0 : 1];
            BaseSize best = list[list.Count - 1];
            foreach (var b in list) if (b.Px >= px) { best = b; break; }
            f = new FaceCache { Base = best, K = px / (float)best.Px };
            f.CapHeight = best.Cap * f.K;
            if (_faces.Count > 400) _faces.Clear();
            _faces[key] = f;
            return f;
        }

        /// <summary>Area-filtered resample of a glyph bitmap.</summary>
        static Glyph Scale(Glyph src, float k)
        {
            if (Math.Abs(k - 1f) < 0.001f || src.W == 0) return new Glyph { W = src.W, H = src.H, OX = src.OX, OY = src.OY, Adv = src.Adv * k, A = src.A };
            float x0 = src.OX * k, y0 = src.OY * k;
            int ox = (int)Math.Floor(x0), oy = (int)Math.Floor(y0);
            int w = (int)Math.Ceiling(x0 + src.W * k) - ox, h = (int)Math.Ceiling(y0 + src.H * k) - oy;
            var a = new byte[w * h];
            const int SS = 3;
            float inv = 1f / k;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sum = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float px = (ox + x + (sx + 0.5f) / SS) * inv - src.OX - 0.5f;
                            float py = (oy + y + (sy + 0.5f) / SS) * inv - src.OY - 0.5f;
                            sum += Sample(src, px, py);
                        }
                    a[y * w + x] = (byte)Math.Min(255, sum / (SS * SS) + 0.5f);
                }
            return new Glyph { W = w, H = h, OX = ox, OY = oy, Adv = src.Adv * k, A = a };
        }

        static float Sample(Glyph g, float x, float y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            return Px(g, ix, iy) * (1 - fx) * (1 - fy) + Px(g, ix + 1, iy) * fx * (1 - fy) + Px(g, ix, iy + 1) * (1 - fx) * fy + Px(g, ix + 1, iy + 1) * fx * fy;
        }

        static float Px(Glyph g, int x, int y) { return (x < 0 || y < 0 || x >= g.W || y >= g.H) ? 0 : g.A[y * g.W + x]; }
    }
}
