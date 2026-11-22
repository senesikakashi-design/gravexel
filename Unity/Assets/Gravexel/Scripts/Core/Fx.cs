using System;

namespace Gravexel.Core
{
    /// <summary>Pooled particle system. Coordinates are in whatever space the caller uses (tiles or pixels).</summary>
    public sealed class Particles
    {
        struct P
        {
            public float X, Y, VX, VY, Life, Max, Size, Rot, VR, Drag, Grav;
            public Col C; public int Tex; public Blend B; public bool Shrink;
        }

        readonly P[] _p;
        int _count;
        readonly Rng _rng = new Rng(1234);

        public Particles(int capacity) { _p = new P[capacity]; }
        public Rng Rng { get { return _rng; } }
        public void Clear() { _count = 0; }

        public void Spawn(float x, float y, float vx, float vy, float life, float size, Col c, int tex = Tx.Glow,
            Blend b = Blend.Add, float drag = 2f, float grav = 0f, float vr = 0f, bool shrink = true)
        {
            if (_count >= _p.Length) return;
            _p[_count++] = new P { X = x, Y = y, VX = vx, VY = vy, Life = life, Max = life, Size = size, C = c, Tex = tex, B = b, Drag = drag, Grav = grav, VR = vr, Rot = _rng.Range(0, 6.28f), Shrink = shrink };
        }

        public void Burst(float x, float y, int n, Col c, float speed, float size, float life, float grav = 0f, int tex = Tx.Glow)
        {
            for (int i = 0; i < n; i++)
            {
                float a = _rng.Range(0, 6.2832f), s = speed * _rng.Range(0.3f, 1f);
                Spawn(x, y, Mathx.Cos(a) * s, Mathx.Sin(a) * s, life * _rng.Range(0.6f, 1.2f), size * _rng.Range(0.6f, 1.2f), c, tex, Blend.Add, 3f, grav, _rng.Range(-8f, 8f));
            }
        }

        public void Update(float dt)
        {
            for (int i = 0; i < _count; i++)
            {
                _p[i].Life -= dt;
                if (_p[i].Life <= 0) { _p[i] = _p[--_count]; i--; continue; }
                float k = (float)Math.Exp(-_p[i].Drag * dt);
                _p[i].VX *= k; _p[i].VY = _p[i].VY * k + _p[i].Grav * dt;
                _p[i].X += _p[i].VX * dt; _p[i].Y += _p[i].VY * dt;
                _p[i].Rot += _p[i].VR * dt;
            }
        }

        /// <summary>Draws with screen = (pos - origin) * scale + offset.</summary>
        public void Draw(IRenderer r, float ox, float oy, float scale, float offX, float offY)
        {
            for (int i = 0; i < _count; i++)
            {
                var p = _p[i];
                float t = p.Life / p.Max;
                float s = p.Size * scale * (p.Shrink ? (0.3f + 0.7f * t) : 1f);
                float sx = (p.X - ox) * scale + offX, sy = (p.Y - oy) * scale + offY;
                if (sx < -s || sy < -s || sx > r.ViewW + s || sy > r.ViewH + s) continue;
                r.Sprite(p.Tex, sx, sy, s, s, p.Rot, p.C.WithA(p.C.A * Math.Min(1f, t * 1.6f)), p.B);
            }
        }
    }

    /// <summary>Neon sky, parallax skyline, stars and floating shapes shared by menus and gameplay.</summary>
    public sealed class Backdrop
    {
        public const int LayerFar = 2, LayerNear = 3, LayerSky = 4;
        const float StripW = 2048f, StripH = 440f;
        readonly float[] _sx = new float[90], _sy = new float[90], _ss = new float[90];
        Theme _built, _skyTheme;
        float _skyW; bool _skySun;

        public Backdrop()
        {
            var rng = new Rng(77);
            for (int i = 0; i < _sx.Length; i++) { _sx[i] = rng.Range(0, 1f); _sy[i] = rng.Range(0, 0.7f); _ss[i] = rng.Range(0.5f, 1.6f); }
        }

        public void Prepare(IRenderer r, Theme theme)
        {
            if (_built == theme) return;
            _built = theme;
            var cmds = new DrawCmd[2];
            for (int i = 0; i < 2; i++)
                cmds[i] = new DrawCmd { Tex = Tx.Skyline1, X = 512 + i * 1024, Y = StripH / 2, W = 1024, H = StripH, Color = theme.Far.Brighten(0.15f).WithA(0.85f), Blend = Blend.Alpha };
            r.BuildStaticLayer(LayerFar, cmds, 2, StripW, StripH);
            for (int i = 0; i < 2; i++)
                cmds[i] = new DrawCmd { Tex = Tx.Skyline2, X = 512 + i * 1024, Y = StripH / 2, W = 1024, H = StripH, Color = theme.Near.WithA(0.95f), Blend = Blend.Alpha };
            r.BuildStaticLayer(LayerNear, cmds, 2, StripW, StripH);
        }

        void BuildSky(IRenderer r, Theme theme, bool sun)
        {
            float W = r.ViewW, H = r.ViewH;
            var list = new System.Collections.Generic.List<DrawCmd>();
            int bands = 108;
            for (int i = 0; i < bands; i++)
            {
                float t = (i + 0.5f) / bands;
                list.Add(new DrawCmd { Tex = Tx.Pixel, X = W / 2, Y = (i + 0.5f) * H / bands, W = W + 4, H = H / bands + 1, Color = Col.Lerp(theme.SkyTop, theme.SkyBottom, t), Blend = Blend.Alpha });
            }
            list.Add(new DrawCmd { Tex = Tx.Glow, X = W * 0.75f, Y = H * 0.35f, W = 1500, H = 1100, Color = theme.Accent.WithA(0.12f), Blend = Blend.Add });
            list.Add(new DrawCmd { Tex = Tx.Glow, X = W * 0.2f, Y = H * 0.8f, W = 1400, H = 900, Color = theme.Edge.WithA(0.08f), Blend = Blend.Add });
            float sunX = W * 0.72f, sunY = H * 0.6f;
            if (!sun) { r.BuildStaticLayer(LayerSky, list.ToArray(), list.Count, W, H); return; }
            list.Add(new DrawCmd { Tex = Tx.Glow, X = sunX, Y = sunY, W = 760, H = 760, Color = theme.Accent.WithA(0.35f), Blend = Blend.Add });
            list.Add(new DrawCmd { Tex = Tx.Circle, X = sunX, Y = sunY, W = 330, H = 330, Color = Col.Lerp(theme.Accent, Col.White, 0.25f).WithA(0.55f), Blend = Blend.Add });
            for (int i = 0; i < 5; i++)
                list.Add(new DrawCmd { Tex = Tx.Pixel, X = sunX, Y = sunY + 40 + i * 26, W = 340, H = 6 + i * 3, Color = Col.Lerp(theme.SkyTop, theme.SkyBottom, (sunY + 40 + i * 26) / H), Blend = Blend.Alpha });
            r.BuildStaticLayer(LayerSky, list.ToArray(), list.Count, W, H);
        }

        /// <summary>calm = gameplay: no sun and quieter decorations, so nothing distracts from the level.</summary>
        public void Draw(IRenderer r, Theme theme, float scroll, float time, float vertical = 0f, bool calm = false)
        {
            Prepare(r, theme);
            if (_skyTheme != theme || _skySun == calm || Math.Abs(_skyW - r.ViewW) > 0.5f) { _skyTheme = theme; _skySun = !calm; _skyW = r.ViewW; BuildSky(r, theme, !calm); }
            float W = r.ViewW, H = r.ViewH;
            r.DrawStaticLayer(LayerSky, 0, 0);
            // stars
            for (int i = 0; i < _sx.Length; i++)
            {
                float x = Mathx.Repeat(_sx[i] * W * 1.3f - scroll * 0.04f * _ss[i], W * 1.3f) - W * 0.15f;
                float y = _sy[i] * H + vertical * 0.03f;
                float tw = 0.5f + 0.5f * Mathx.Sin(time * (1.5f + _ss[i]) + i);
                r.Sprite(Tx.Glow, x, y, 10 * _ss[i] + 6, 10 * _ss[i] + 6, 0, Col.White.WithA(0.35f + 0.5f * tw), Blend.Add);
            }
            // floating hexes
            for (int i = 0; i < 7; i++)
            {
                float x = Mathx.Repeat(i * 431f - scroll * 0.18f + time * 12f, W + 400) - 200;
                float y = H * (0.15f + 0.1f * (i % 5)) + Mathx.Sin(time * 0.4f + i) * 30 + vertical * 0.12f;
                float s = 90 + (i % 3) * 70;
                r.Sprite(Tx.HexOutline, x, y, s, s, time * 0.15f * ((i & 1) == 0 ? 1 : -1) + i, (i % 2 == 0 ? theme.Edge : theme.Accent).WithA(calm ? 0.04f : 0.1f), Blend.Add);
            }
            // skylines
            DrawStrip(r, LayerFar, scroll * 0.12f, H - StripH - 90 + vertical * 0.08f);
            DrawStrip(r, LayerNear, scroll * 0.3f, H - StripH + 40 + vertical * 0.15f);
            // horizon haze
            r.Sprite(Tx.GlowLine, W / 2, H - 120 + vertical * 0.15f, W * 1.2f, 260, 0, theme.Edge.WithA(0.1f), Blend.Add);
        }

        static void DrawStrip(IRenderer r, int layer, float scroll, float y)
        {
            float x = -Mathx.Repeat(scroll, StripW);
            while (x < r.ViewW) { r.DrawStaticLayer(layer, x, y); x += StripW; }
        }
    }
}
