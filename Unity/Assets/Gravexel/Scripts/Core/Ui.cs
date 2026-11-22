using System;
using System.Collections.Generic;

namespace Gravexel.Core
{
    /// <summary>
    /// Small immediate-mode UI toolkit with mouse, keyboard (arrow keys + Enter) and touch support.
    /// Widgets register their rectangle every frame; arrow keys move focus spatially between them.
    /// </summary>
    public sealed class Ui
    {
        struct Widget { public int Id; public Rect R; }

        readonly GameApp _app;
        readonly List<Widget> _now = new List<Widget>(), _last = new List<Widget>();
        readonly Dictionary<int, float> _anim = new Dictionary<int, float>();
        int _focus = -1, _pressedId = -1, _dragId = -1;
        public Col Accent = Col.Hex("#22E8FF");
        public Col Accent2 = Col.Hex("#FF3DDB");

        public Ui(GameApp app) { _app = app; }

        IRenderer R { get { return _app.R; } }
        InputState In { get { return _app.In; } }

        public int Focus { get { return _focus; } set { _focus = value; } }

        public void Begin(float dt)
        {
            _last.Clear(); _last.AddRange(_now); _now.Clear();
            if (_last.Count > 0)
            {
                bool found = false;
                foreach (var w in _last) if (w.Id == _focus) found = true;
                if (!found) _focus = _last[0].Id;
                int dx = (In.Right ? 1 : 0) - (In.Left ? 1 : 0), dy = (In.Down ? 1 : 0) - (In.Up ? 1 : 0);
                if (dx != 0 || dy != 0) Navigate(dx, dy);
            }
            // decay animations
            var keys = new List<int>(_anim.Keys);
            foreach (var k in keys) _anim[k] = Mathx.Approach(_anim[k], k == _focus ? 1f : 0f, dt * 7f);
            if (!In.PointerDown) _dragId = -1;
        }

        void Navigate(int dx, int dy)
        {
            Rect cur = default(Rect); bool have = false;
            foreach (var w in _last) if (w.Id == _focus) { cur = w.R; have = true; }
            if (!have) return;
            float best = float.MaxValue; int bestId = -1;
            foreach (var w in _last)
            {
                if (w.Id == _focus) continue;
                float ox = w.R.CX - cur.CX, oy = w.R.CY - cur.CY;
                float primary = dx != 0 ? ox * dx : oy * dy;
                float secondary = dx != 0 ? Math.Abs(oy) : Math.Abs(ox);
                if (primary <= 4) continue;
                float score = primary + secondary * 2.2f;
                if (score < best) { best = score; bestId = w.Id; }
            }
            if (bestId >= 0) { _focus = bestId; _app.PlaySfx(Sfx.Hover, 0.6f); }
        }

        float Register(int id, Rect r, out bool hovered)
        {
            _now.Add(new Widget { Id = id, R = r });
            hovered = r.Contains(In.PointerX, In.PointerY);
            if (hovered && In.PointerMoved && _focus != id) { _focus = id; _app.PlaySfx(Sfx.Hover, 0.4f); }
            float a; if (!_anim.TryGetValue(id, out a)) { a = 0; _anim[id] = 0; }
            return a;
        }

        bool Activated(int id, bool hovered)
        {
            bool act = false;
            if (hovered && In.PointerPressed) { _pressedId = id; _focus = id; }
            if (In.PointerReleased && _pressedId == id) { if (hovered) act = true; _pressedId = -1; }
            if (_focus == id && In.Confirm) act = true;
            return act;
        }

        // ---------------- widgets ----------------

        public bool Button(int id, Rect r, string label, int icon = 0, bool primary = false, bool enabled = true)
        {
            bool hov; float a = Register(id, r, out hov);
            bool focused = _focus == id;
            bool pressing = _pressedId == id && In.PointerDown && hov;
            float grow = a * 6f - (pressing ? 4f : 0f);
            var rr = r.Inflate(grow * 0.5f);
            Col acc = primary ? Accent2 : Accent;

            Fill(rr, new Col(0.03f, 0.05f, 0.12f, 0.78f));
            if (a > 0.01f)
            {
                R.Sprite(Tx.FadeH, rr.X + rr.W * 0.5f, rr.CY, rr.W, rr.H, 0, acc.WithA(0.32f * a), Blend.Add);
                R.Sprite(Tx.Glow, rr.CX, rr.CY, rr.W * 1.3f, rr.H * 2.6f, 0, acc.WithA(0.18f * a), Blend.Add);
                Fill(new Rect(rr.X, rr.Y, 6, rr.H), acc.WithA(a));
            }
            Border(rr, focused ? acc.WithA(0.95f) : Col.White.WithA(0.16f), focused ? 2.5f : 1.5f);
            Corners(rr, acc.WithA(0.5f + 0.5f * a), 14);

            float size = Math.Min(rr.H * 0.42f, 40f);
            Col tc = enabled ? Col.White : Col.White.WithA(0.35f);
            float tx = rr.X + 34 + a * 8;
            if (icon != 0)
            {
                R.Sprite(icon, tx + size * 0.5f, rr.CY, size * 1.05f, size * 1.05f, 0, (focused ? acc : Col.White).WithA(enabled ? 1 : 0.35f), Blend.Alpha);
                tx += size * 1.6f;
            }
            R.Text(label, tx, rr.CY, size, tc, Align.Left, FontId.Ui);
            return enabled && Activated(id, hov);
        }

        /// <summary>Square icon button (pause, back...).</summary>
        public bool IconButton(int id, Rect r, int icon)
        {
            bool hov; float a = Register(id, r, out hov);
            var rr = r.Inflate(a * 4);
            R.Sprite(Tx.Glow, rr.CX, rr.CY, rr.W * 2, rr.H * 2, 0, Accent.WithA(0.25f * a), Blend.Add);
            R.Sprite(Tx.Circle, rr.CX, rr.CY, rr.W, rr.H, 0, new Col(0.03f, 0.05f, 0.12f, 0.8f), Blend.Alpha);
            R.Sprite(Tx.Ring, rr.CX, rr.CY, rr.W, rr.H, 0, (a > 0.5f ? Accent : Col.White).WithA(0.4f + a * 0.6f), Blend.Alpha);
            R.Sprite(icon, rr.CX, rr.CY, rr.W * 0.5f, rr.H * 0.5f, 0, Col.White, Blend.Alpha);
            return Activated(id, hov);
        }

        public bool Slider(int id, Rect r, string label, ref float value)
        {
            bool hov; float a = Register(id, r, out hov);
            bool focused = _focus == id; bool changed = false;
            Row(r, a, focused);
            R.Text(label, r.X + 30, r.CY, 34, Col.White, Align.Left, FontId.Ui);
            var bar = new Rect(r.X + r.W * 0.48f, r.CY - 5, r.W * 0.38f, 10);
            if (hov && In.PointerPressed) _dragId = id;
            if (_dragId == id && In.PointerDown)
            {
                float v = Mathx.Clamp01((In.PointerX - bar.X) / bar.W);
                if (Math.Abs(v - value) > 0.001f) { value = v; changed = true; }
            }
            if (focused && (In.Left || In.Right))
            {
                value = Mathx.Clamp01((float)Math.Round((value + (In.Right ? 0.1f : -0.1f)) * 10) / 10f); changed = true;
            }
            Fill(bar, Col.White.WithA(0.12f));
            Fill(new Rect(bar.X, bar.Y, bar.W * value, bar.H), Accent);
            R.Sprite(Tx.GlowLine, bar.X + bar.W * value * 0.5f, bar.CY, bar.W * value, 40, 0, Accent.WithA(0.5f), Blend.Add);
            float kx = bar.X + bar.W * value;
            R.Sprite(Tx.Glow, kx, bar.CY, 70, 70, 0, Accent.WithA(0.5f + 0.3f * a), Blend.Add);
            R.Sprite(Tx.Circle, kx, bar.CY, 26 + a * 6, 26 + a * 6, 0, Col.White, Blend.Alpha);
            R.Text(((int)Math.Round(value * 100)).ToString() + "%", r.Right - 26, r.CY, 30, Col.White.WithA(0.8f), Align.Right, FontId.Ui);
            return changed;
        }

        public bool Toggle(int id, Rect r, string label, ref bool value)
        {
            bool hov; float a = Register(id, r, out hov);
            bool focused = _focus == id;
            Row(r, a, focused);
            R.Text(label, r.X + 30, r.CY, 34, Col.White, Align.Left, FontId.Ui);
            bool act = Activated(id, hov) || (focused && (In.Left || In.Right));
            if (act) { value = !value; _app.PlaySfx(Sfx.Click, 0.7f); }
            float sw = 96, sh = 40, sx = r.Right - 30 - sw;
            var track = new Rect(sx, r.CY - sh / 2, sw, sh);
            Fill(track, value ? Accent.WithA(0.85f) : Col.White.WithA(0.14f));
            Border(track, Col.White.WithA(0.3f), 1.5f);
            float kx = value ? track.Right - sh / 2 : track.X + sh / 2;
            if (value) R.Sprite(Tx.Glow, kx, track.CY, 90, 90, 0, Accent.WithA(0.5f), Blend.Add);
            R.Sprite(Tx.Circle, kx, track.CY, sh - 10, sh - 10, 0, Col.White, Blend.Alpha);
            R.Text(value ? "ON" : "OFF", sx - 18, r.CY, 28, Col.White.WithA(0.75f), Align.Right, FontId.Ui);
            return act;
        }

        public bool Choice(int id, Rect r, string label, string[] options, ref int index)
        {
            bool hov; float a = Register(id, r, out hov);
            bool focused = _focus == id; bool changed = false;
            Row(r, a, focused);
            R.Text(label, r.X + 30, r.CY, 34, Col.White, Align.Left, FontId.Ui);
            if (Activated(id, hov) || (focused && In.Right)) { index = (index + 1) % options.Length; changed = true; }
            else if (focused && In.Left) { index = (index + options.Length - 1) % options.Length; changed = true; }
            if (changed) _app.PlaySfx(Sfx.Click, 0.7f);
            float cx = r.Right - 200;
            R.Sprite(Tx.Chevron, cx - 150, r.CY, 26, 26, Mathx.PI, Col.White.WithA(0.5f + a * 0.5f), Blend.Alpha);
            R.Sprite(Tx.Chevron, cx + 150, r.CY, 26, 26, 0, Col.White.WithA(0.5f + a * 0.5f), Blend.Alpha);
            R.Text(options[index], cx, r.CY, 32, focused ? Accent : Col.White, Align.Center, FontId.Ui);
            return changed;
        }

        /// <summary>Invisible focusable area (cards). Returns true when activated; 'anim' is the focus amount.</summary>
        public bool Area(int id, Rect r, out float anim, out bool focused)
        {
            bool hov; anim = Register(id, r, out hov);
            focused = _focus == id;
            return Activated(id, hov);
        }

        void Row(Rect r, float a, bool focused)
        {
            Fill(r, new Col(0.03f, 0.05f, 0.12f, 0.7f));
            if (a > 0.01f)
            {
                R.Sprite(Tx.FadeH, r.CX, r.CY, r.W, r.H, 0, Accent.WithA(0.22f * a), Blend.Add);
                Fill(new Rect(r.X, r.Y, 5, r.H), Accent.WithA(a));
            }
            Border(r, focused ? Accent.WithA(0.8f) : Col.White.WithA(0.12f), focused ? 2f : 1.2f);
        }

        // ---------------- drawing helpers ----------------

        public void Fill(Rect r, Col c) { R.Sprite(Tx.Pixel, r.CX, r.CY, r.W, r.H, 0, c, Blend.Alpha); }

        public void Border(Rect r, Col c, float t)
        {
            Fill(new Rect(r.X, r.Y, r.W, t), c);
            Fill(new Rect(r.X, r.Bottom - t, r.W, t), c);
            Fill(new Rect(r.X, r.Y + t, t, r.H - 2 * t), c);
            Fill(new Rect(r.Right - t, r.Y + t, t, r.H - 2 * t), c);
        }

        public void Corners(Rect r, Col c, float len)
        {
            float t = 3;
            Fill(new Rect(r.X - 4, r.Y - 4, len, t), c); Fill(new Rect(r.X - 4, r.Y - 4, t, len), c);
            Fill(new Rect(r.Right + 4 - len, r.Bottom + 4 - t, len, t), c); Fill(new Rect(r.Right + 4 - t, r.Bottom + 4 - len, t, len), c);
        }

        public void Panel(Rect r, Col accent)
        {
            R.Sprite(Tx.Glow, r.CX, r.CY, r.W * 1.4f, r.H * 1.5f, 0, accent.WithA(0.12f), Blend.Add);
            Fill(r, new Col(0.02f, 0.03f, 0.09f, 0.92f));
            Fill(new Rect(r.X, r.Y, r.W, 4), accent);
            R.Sprite(Tx.GlowLine, r.CX, r.Y + 2, r.W, 40, 0, accent.WithA(0.6f), Blend.Add);
            Border(r, Col.White.WithA(0.1f), 1.5f);
            Corners(r, accent.WithA(0.8f), 26);
        }

        /// <summary>Text with a neon halo.</summary>
        public void Glow(string text, float x, float y, float size, Col c, Align al, FontId f, float glow = 1f)
        {
            if (glow > 0)
            {
                float rad = size * 0.06f;
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathx.PI / 4;
                    R.Text(text, x + Mathx.Cos(ang) * rad, y + Mathx.Sin(ang) * rad, size, c.WithA(0.16f * glow), al, f);
                }
                R.Sprite(Tx.Glow, al == Align.Center ? x : (al == Align.Left ? x + R.MeasureText(text, size, f) / 2 : x - R.MeasureText(text, size, f) / 2),
                    y, R.MeasureText(text, size, f) * 1.4f, size * 2.6f, 0, c.WithA(0.12f * glow), Blend.Add);
            }
            R.Text(text, x, y, size, Col.Lerp(c, Col.White, 0.55f), al, f);
        }

        public void Stars(float cx, float cy, float size, int bits, float gap, float t)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = cx + (i - 1) * gap;
                bool on = (bits & (1 << i)) != 0;
                if (on)
                {
                    R.Sprite(Tx.Glow, x, cy, size * 2.2f, size * 2.2f, 0, Col.Hex("#FFC93A", 0.45f), Blend.Add);
                    R.Sprite(Tx.Star, x, cy, size, size, 0, Col.Hex("#FFE04A"), Blend.Alpha);
                }
                else R.Sprite(Tx.StarOutline, x, cy, size, size, 0, Col.White.WithA(0.28f), Blend.Alpha);
            }
        }
    }
}
