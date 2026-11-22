using System;

namespace Gravexel.Core
{
    /// <summary>
    /// Brand intro: the two chevrons of the GRAVEXEL X slide in from the ceiling and the floor and lock
    /// together, then the wordmark and the tagline appear. Any key or click skips it.
    /// </summary>
    public sealed class SplashScreen : Screen
    {
        public static readonly Col Cyan = Col.Hex("#22E8FF"), Magenta = Col.Hex("#FF3DDB");
        float _t; bool _left, _locked;
        readonly Particles _fx = new Particles(240);

        public override void Update(float dt)
        {
            _t += dt; _fx.Update(dt);
            if (!_locked && _t > 0.62f)
            {
                _locked = true;
                App.PlaySfx(Sfx.Land, 0.8f, 0.8f);
                _fx.Burst(App.R.ViewW / 2, 430, 46, Col.White, 900f, 22f, 0.7f);
            }
            var i = App.In;
            bool skip = i.Action || i.Confirm || i.Back || i.PointerPressed;
            if (!_left && (_t > 2.7f || (skip && _t > 0.3f))) { _left = true; App.GoTo(new TitleScreen()); }
        }

        /// <summary>
        /// Draws the X centred at (cx, cy); 'height' is the height of the X itself. 'open' pushes the chevrons
        /// apart (0 = the logo), used for the intro animation.
        /// </summary>
        public static void DrawMark(IRenderer r, float cx, float cy, float height, float alpha, float glow, float open = 0f)
        {
            float tex = height * 256f / 178f;       // the artwork spans y 39..217 of the 256 texture
            float d = open * height;
            if (glow > 0)
            {
                r.Sprite(Tx.Glow, cx, cy - height * 0.25f - d, height * 1.5f, height * 1.1f, 0, Cyan.WithA(0.35f * glow * alpha), Blend.Add);
                r.Sprite(Tx.Glow, cx, cy + height * 0.25f + d, height * 1.5f, height * 1.1f, 0, Magenta.WithA(0.35f * glow * alpha), Blend.Add);
            }
            r.Sprite(Tx.LogoTop, cx, cy - d, tex, tex, 0, Cyan.WithA(alpha), Blend.Alpha);
            r.Sprite(Tx.LogoBottom, cx, cy + d, tex, tex, 0, Magenta.WithA(alpha), Blend.Alpha);
        }

        /// <summary>
        /// The GRAVEXEL logo: GRAV (cyan), E (white), the X made of the two chevrons, EL (white).
        /// x is the left edge, y the cap centre. Returns the width.
        /// </summary>
        public static float DrawWordmark(GameApp app, float x, float y, float size, float alpha, float glow)
        {
            var r = app.R; var ui = app.Ui;
            float gw = r.MeasureText("GRAV", size, FontId.Title), ew = r.MeasureText("E", size, FontId.Title), xw = r.MeasureText("X", size, FontId.Title);
            ui.Glow("GRAV", x, y, size, Cyan.WithA(alpha), Align.Left, FontId.Title, glow);
            ui.Glow("E", x + gw, y, size, Col.White.WithA(alpha), Align.Left, FontId.Title, glow * 0.6f);
            // the X: chevrons stretched to the letter's width and cap height
            float cap = size * 0.7f, cx = x + gw + ew + xw / 2;
            float texH = cap * 256f / 178f, texW = xw * 256f / 164f;
            if (glow > 0) r.Sprite(Tx.Glow, cx, y, xw * 1.6f, cap * 1.8f, 0, Magenta.WithA(0.18f * glow * alpha), Blend.Add);
            r.Sprite(Tx.LogoTop, cx, y, texW, texH, 0, Cyan.WithA(alpha), Blend.Alpha);
            r.Sprite(Tx.LogoBottom, cx, y, texW, texH, 0, Magenta.WithA(alpha), Blend.Alpha);
            ui.Glow("EL", x + gw + ew + xw, y, size, Col.White.WithA(alpha), Align.Left, FontId.Title, glow * 0.6f);
            return gw + ew + xw + r.MeasureText("EL", size, FontId.Title);
        }

        public override void Draw(bool interactive)
        {
            var r = App.R; float W = r.ViewW, H = r.ViewH;
            r.Sprite(Tx.Pixel, W / 2, H / 2, W, H, 0, Col.Hex("#050816"), Blend.Alpha);
            r.Sprite(Tx.Glow, W / 2, H * 0.45f, 1800, 1300, 0, Magenta.WithA(0.12f), Blend.Add);
            float k = Mathx.EaseOutCubic(Mathx.Clamp01(_t / 0.62f));
            float pulse = _locked ? Mathx.Clamp01(1.4f - (_t - 0.62f) * 2f) : 0;
            DrawMark(r, W / 2, 430, 300 + pulse * 20, Mathx.Clamp01(_t * 3), 0.6f + pulse, (1 - k) * 1.6f);
            _fx.Draw(r, 0, 0, 1, 0, 0);
            float wa = Mathx.Clamp01((_t - 0.85f) * 2.5f);
            if (wa > 0)
            {
                float size = 120;
                float width = r.MeasureText("GRAVEXEL", size, FontId.Title);
                DrawWordmark(App, W / 2 - width / 2, 720, size, wa, 0.8f);
                float ta = Mathx.Clamp01((_t - 1.25f) * 2.5f);
                r.Text(GameApp.Tagline, W / 2, 830, 46, Magenta.WithA(ta), Align.Center, FontId.Title);
                r.Text(GameApp.Slogan, W / 2, 895, 30, Col.White.WithA(0.75f * ta), Align.Center, FontId.Ui);
            }
        }
    }
}
