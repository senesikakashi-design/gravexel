using System;

namespace Gravexel.Core
{
    public sealed class TitleScreen : Screen
    {
        const int IdPlay = 1, IdLevels = 2, IdSkins = 3, IdSettings = 4, IdQuit = 5, IdUpdate = 6;
        bool _updateShown;
        readonly Particles _fx = new Particles(400);
        int _lastSide = -1;
        float _intro;

        public override void Enter()
        {
            App.PlayMusic(Sfx.MusicMenu);
            var t = Themes.World[0];
            App.Ui.Accent = t.Edge; App.Ui.Accent2 = t.Accent;
            App.Ui.Focus = IdPlay;
        }

        public override void Update(float dt)
        {
            _intro += dt;
            _fx.Update(dt);
        }

        public override void Draw(bool interactive)
        {
            var r = App.R; var ui = App.Ui; var theme = Themes.World[0];
            float W = r.ViewW, H = r.ViewH, t = App.Time;
            App.Backdrop.Draw(r, theme, t * 140f, t);

            DrawDemo(r, theme, W, H, t);
            r.Vignette(0.5f);

            // title
            float k = Mathx.EaseOutCubic(Mathx.Clamp01(_intro * 1.6f));
            float tx = 130 - (1 - k) * 60;
            // the logo: GRAVEXEL with the two chevrons as its X
            float fullW = SplashScreen.DrawWordmark(App, tx, 200, 150, k, 1.2f);
            r.Text(GameApp.Tagline, tx + 6, 318, 46, SplashScreen.Magenta.WithA(0.95f * k), Align.Left, FontId.Title);
            r.Sprite(Tx.Pixel, tx + fullW / 2, 368, fullW, 3, 0, theme.Edge.WithA(0.6f * k), Blend.Alpha);
            r.Sprite(Tx.GlowLine, tx + fullW / 2, 368, fullW + 40, 40, 0, theme.Edge.WithA(0.5f * k), Blend.Add);
            r.Text(GameApp.Slogan, tx + 6, 408, 30, theme.Edge.WithA(0.85f * k), Align.Left, FontId.Ui);

            if (interactive && _intro > 0.25f)
            {
                float bx = tx, by = 470, bw = 520, bh = 80, gap = 92;
                bool progress = App.Save.TotalStars > 0 || App.Save.Get(1).Completed;
                int next = NextLevel();
                string playLabel = progress ? "CONTINUE  ·  LEVEL " + Label(next) : "PLAY";
                if (ui.Button(IdPlay, new Rect(bx, by, bw, bh), playLabel, Tx.IconPlay, true)) { App.PlaySfx(Sfx.Click); App.GoTo(new GameScreen(next)); }
                if (ui.Button(IdLevels, new Rect(bx, by + gap, bw, bh), "LEVELS", Tx.IconGrid)) { App.PlaySfx(Sfx.Click); App.GoTo(new LevelSelectScreen(Math.Max(1, App.Level(next) != null ? App.Level(next).World : 1))); }
                if (ui.Button(IdSkins, new Rect(bx, by + gap * 2, bw, bh), "CHARACTERS", Tx.IconUser)) { App.PlaySfx(Sfx.Click); App.GoTo(new SkinsScreen()); }
                if (ui.Button(IdSettings, new Rect(bx, by + gap * 3, bw, bh), "SETTINGS", Tx.IconGear)) { App.PlaySfx(Sfx.Click); App.Push(new SettingsScreen()); }
                if (App.Platform.SupportsQuit && ui.Button(IdQuit, new Rect(bx, by + gap * 4, bw, bh), "QUIT", Tx.IconPower)) { App.SaveNow(); App.Platform.Quit(); }
            }

            // update banner (only when a newer version exists online)
            var up = App.Updater;
            if (up != null && up.State == UpdateState.Available)
            {
                var br = new Rect(W - 600, 40, 560, 82);
                float pulse = 0.5f + 0.5f * Mathx.Sin(t * 4);
                r.Sprite(Tx.Glow, br.CX, br.CY, br.W * 1.3f, br.H * 2.4f, 0, theme.Accent.WithA(0.2f + 0.15f * pulse), Blend.Add);
                string ver = up.Latest.Version.StartsWith("v") ? up.Latest.Version : "v" + up.Latest.Version;
                if (interactive && ui.Button(IdUpdate, br, "UPDATE AVAILABLE  " + ver, Tx.IconCheck, true)) { App.PlaySfx(Sfx.Click); App.Push(new UpdateScreen()); }
                if (!_updateShown) { _updateShown = true; App.PlaySfx(Sfx.Unlock, 0.4f); }
            }

            // footer
            int stars = App.Save.TotalStars, max = App.Levels.Count * 3;
            r.Sprite(Tx.Star, W - 300, H - 52, 34, 34, 0, WorldView.StarCol, Blend.Alpha);
            r.Text(stars + " / " + max, W - 270, H - 52, 30, Col.White, Align.Left, FontId.Ui);
            r.Text("v" + GameApp.Version, W - 40, H - 52, 24, Col.White.WithA(0.45f), Align.Right, FontId.Ui);
            string help = App.Platform.IsTouch ? "Tap to select" : "Arrow keys + Enter, or the mouse";
            r.Text(help, 130, H - 52, 24, Col.White.WithA(0.45f), Align.Left, FontId.Ui);
        }

        int NextLevel()
        {
            int n = App.Save.LastLevel;
            if (App.Level(n) == null || !App.Save.IsUnlocked(n)) n = 1;
            return n;
        }

        string Label(int number)
        {
            var info = App.Level(number); if (info == null) return number.ToString();
            int idx = 1; foreach (var l in App.LevelsInWorld(info.World)) { if (l.Number == number) break; idx++; }
            return info.World + "-" + idx;
        }

        /// <summary>A looping demo: the hero runs between two rails and flips over incoming spikes.</summary>
        void DrawDemo(IRenderer r, Theme theme, float W, float H, float t)
        {
            float x0 = Math.Max(W * 0.5f, 820), x1 = W - 70;
            float top = 470, bottom = 880;
            const float spacing = 560f, speed = 430f;
            float charX = x0 + 230;
            float s = t * speed;

            // rails
            foreach (float ry in new[] { top, bottom })
            {
                r.Sprite(Tx.GlowLine, (x0 + x1) / 2, ry, x1 - x0, 40, 0, theme.Edge.WithA(0.55f), Blend.Add);
                r.Sprite(Tx.Pixel, (x0 + x1) / 2, ry, x1 - x0, 4, 0, theme.Edge.Brighten(0.4f), Blend.Alpha);
                for (float d = Mathx.Repeat(-s, 80f); d < x1 - x0; d += 80f)
                    r.Sprite(Tx.Pixel, x0 + d, ry + (ry == top ? -14 : 14), 30, 3, 0, theme.Edge.WithA(0.25f), Blend.Alpha);
            }
            r.Sprite(Tx.FadeH, x0 + 90, (top + bottom) / 2, 180, bottom - top + 80, Mathx.PI, theme.SkyTop.WithA(0.0f), Blend.Alpha);

            // spikes
            int kStart = (int)Math.Floor((s - (x1 - charX)) / spacing) - 1;
            for (int k = Math.Max(0, kStart); k < kStart + 8; k++)
            {
                float sx = charX + (k * spacing - s) + 500;
                if (sx < x0 + 10 || sx > x1 - 10) continue;
                bool ceil = (k & 1) == 1;
                float sy = ceil ? top + 26 : bottom - 26;
                r.Sprite(Tx.Glow, sx, sy, 90, 90, 0, theme.Hazard.WithA(0.4f), Blend.Add);
                r.Sprite(Tx.Spike, sx, sy, 50, 50, ceil ? Mathx.PI : 0, theme.Hazard.Brighten(0.25f), Blend.Alpha);
            }

            // hero: side flips shortly after each spike passes
            float u = (s - 500) / spacing - 0.18f;
            int kk = (int)Math.Floor(u); float frac = u - kk;
            int side = ((kk % 2) + 2) % 2;             // 0 floor, 1 ceiling
            float prog = Mathx.EaseOutCubic(Mathx.Clamp01(frac / 0.32f));
            float yFloor = bottom - 27, yCeil = top + 27;
            float yFrom = side == 0 ? yCeil : yFloor, yTo = side == 0 ? yFloor : yCeil;
            float y = Mathx.Lerp(yFrom, yTo, prog);
            float rot = side == 1 ? Mathx.Lerp(0, Mathx.PI, prog) : Mathx.Lerp(Mathx.PI, Mathx.PI * 2, prog);
            if (side != _lastSide)
            {
                if (_lastSide >= 0) { App.PlaySfx(Sfx.Flip, 0.12f, side == 1 ? 1.15f : 0.95f); _fx.Burst(charX, yFrom, 12, theme.Edge, 260f, 22f, 0.5f); }
                _lastSide = side;
            }
            var skin = Skins.All[Mathx.Clamp(App.Save.SelectedSkin, 0, Skins.All.Length - 1)];
            for (int i = 1; i < 9; i++)
            {
                float ty = Mathx.Lerp(yFrom, yTo, Mathx.EaseOutCubic(Mathx.Clamp01((frac - i * 0.012f) / 0.32f)));
                r.Sprite(Tx.BodyFor(skin.Shape), charX - i * 22, ty, 52 * (1 - i / 10f), 52 * (1 - i / 10f), rot, skin.TrailAt(t).WithA(0.18f * (1 - i / 9f)), Blend.Add);
            }
            WorldView.DrawCharacter(r, skin, charX, y, 54, rot, 1, t, 1, 1, 1, Mathx.Cos(rot));
            if (App.Time % 0.06f < 0.017f) _fx.Spawn(charX - 26, y, -200, 0, 0.4f, 14, skin.TrailAt(t).WithA(0.6f));
            _fx.Draw(r, 0, 0, 1, 0, 0);
        }
    }
}
