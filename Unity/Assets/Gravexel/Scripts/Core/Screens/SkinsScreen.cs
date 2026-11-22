using System;

namespace Gravexel.Core
{
    public sealed class SkinsScreen : Screen
    {
        const int IdBack = 400, IdCard = 410;
        float _shake; int _shakeId = -1;

        public override void Enter()
        {
            var t = Themes.World[0];
            App.Ui.Accent = t.Edge; App.Ui.Accent2 = t.Accent;
            App.Ui.Focus = IdCard + Mathx.Clamp(App.Save.SelectedSkin, 0, Skins.All.Length - 1);
        }

        public override void Update(float dt)
        {
            if (App.In.Back || App.In.Pause) { App.PlaySfx(Sfx.Back); App.GoTo(new TitleScreen()); }
            _shake = Math.Max(0, _shake - dt * 3);
        }

        public override void Draw(bool interactive)
        {
            var r = App.R; var ui = App.Ui; var theme = Themes.World[3];
            float W = r.ViewW, H = r.ViewH, t = App.Time;
            App.Backdrop.Draw(r, theme, t * 50f, t);
            r.Vignette(0.6f);
            r.Sprite(Tx.Pixel, W / 2, H / 2, W, H, 0, new Col(0, 0, 0.02f, 0.35f), Blend.Alpha);

            ui.Glow("CHARACTERS", W / 2, 92, 64, ui.Accent, Align.Center, FontId.Title);
            if (interactive && ui.IconButton(IdBack, new Rect(50, 52, 84, 84), Tx.IconBack)) { App.PlaySfx(Sfx.Back); App.GoTo(new TitleScreen()); }
            int stars = App.Save.TotalStars;
            r.Sprite(Tx.Star, W / 2 - 70, 168, 34, 34, 0, WorldView.StarCol, Blend.Alpha);
            r.Text(stars + " STARS COLLECTED", W / 2 - 44, 168, 30, Col.White.WithA(0.85f), Align.Left, FontId.Ui);

            int cols = Skins.All.Length > 12 ? 8 : 6; float cw = cols == 8 ? 206 : 250, ch = cols == 8 ? 330 : 320, g = cols == 8 ? 18 : 26;
            float x0 = W / 2 - (cols * cw + (cols - 1) * g) / 2, y0 = 240;
            for (int i = 0; i < Skins.All.Length; i++)
            {
                var sk = Skins.All[i];
                var rect = new Rect(x0 + (i % cols) * (cw + g), y0 + (i / cols) * (ch + g), cw, ch);
                if (_shakeId == i) rect.X += Mathx.Sin(t * 70) * 10 * _shake;
                bool unlocked = sk.StarsNeeded <= stars, selected = App.Save.SelectedSkin == i;
                float a = 0; bool focused = false;
                bool act = interactive && ui.Area(IdCard + i, rect, out a, out focused);
                var rr = rect.Inflate(a * 8);
                Col c = sk.ColorAt(t);
                if (a > 0.01f) r.Sprite(Tx.Glow, rr.CX, rr.CY, rr.W * 1.6f, rr.H * 1.4f, 0, c.WithA(0.2f * a), Blend.Add);
                ui.Fill(rr, new Col(0.03f, 0.04f, 0.11f, 0.88f));
                if (selected) ui.Fill(new Rect(rr.X, rr.Y, rr.W, 6), c);
                ui.Border(rr, focused ? c : Col.White.WithA(selected ? 0.4f : 0.13f), focused ? 3f : 1.5f);
                float bob = Mathx.Sin(t * 3 + i) * 8 * (focused ? 1 : 0.3f);
                if (unlocked)
                {
                    WorldView.DrawCharacter(r, sk, rr.CX, rr.Y + 130 + bob, 96 + a * 12, Mathx.Sin(t * 2 + i) * 0.08f, 1, t + i * 0.7f);
                    r.Text(sk.Name, rr.CX, rr.Bottom - 92, cols == 8 ? 30 : 36, Col.White, Align.Center, FontId.Title);
                    r.Text(Skins.ShapeNames[sk.Shape].ToUpperInvariant(), rr.CX, rr.Bottom - 56, 22, c.WithA(0.9f), Align.Center, FontId.Ui);
                    if (selected)
                    {
                        r.Sprite(Tx.IconCheck, rr.CX - 54, rr.Bottom - 24, 22, 22, 0, c, Blend.Alpha);
                        r.Text("SELECTED", rr.CX - 36, rr.Bottom - 24, 22, c, Align.Left, FontId.Ui);
                    }
                }
                else
                {
                    WorldView.DrawCharacter(r, sk, rr.CX, rr.Y + 130, 96, 0, 1, t, 1, 1, 0.12f);
                    r.Sprite(Tx.Lock, rr.CX, rr.Y + 130, 60, 60, 0, Col.White.WithA(0.6f), Blend.Alpha);
                    r.Text("???", rr.CX, rr.Bottom - 92, 36, Col.White.WithA(0.35f), Align.Center, FontId.Title);
                    r.Sprite(Tx.Star, rr.CX - 40, rr.Bottom - 46, 26, 26, 0, WorldView.StarCol.WithA(0.8f), Blend.Alpha);
                    r.Text(sk.StarsNeeded.ToString(), rr.CX - 20, rr.Bottom - 46, 28, Col.White.WithA(0.7f), Align.Left, FontId.Ui);
                }
                if (act)
                {
                    if (unlocked)
                    {
                        if (!selected) { App.Save.SelectedSkin = i; App.SaveNow(); App.PlaySfx(Sfx.Unlock, 0.5f); }
                        else App.PlaySfx(Sfx.Click, 0.6f);
                    }
                    else { App.PlaySfx(Sfx.Back, 0.8f, 0.7f); _shakeId = i; _shake = 1; }
                }
            }
            string help = App.Platform.IsTouch ? "Collect stars to unlock new characters" : "Collect stars to unlock new characters  ·  Enter to select  ·  Esc to go back";
            r.Text(help, W / 2, H - 50, 26, Col.White.WithA(0.45f), Align.Center, FontId.Ui);
        }
    }

    public sealed class SettingsScreen : Screen
    {
        const int IdMusic = 500, IdSfx = 501, IdFull = 502, IdQuality = 503, IdFps = 504, IdShake = 505, IdTimer = 506, IdReset = 507, IdBack = 508, IdUpdates = 509;
        static readonly string[] Qualities = { "PERFORMANCE", "BALANCED", "ULTRA" };
        static readonly string[] Fps = { "60 FPS", "120 FPS", "UNLIMITED" };
        float _resetArm; float _sfxPreview;

        public override bool IsOverlay { get { return true; } }

        public override void Enter() { App.Ui.Focus = IdMusic; }

        public override void Update(float dt)
        {
            _resetArm = Math.Max(0, _resetArm - dt);
            _sfxPreview -= dt;
            if (App.In.Back || App.In.Pause) Close();
        }

        void Close() { App.PlaySfx(Sfx.Back); App.SaveNow(); App.Pop(); }

        public override void Draw(bool interactive)
        {
            var r = App.R; var ui = App.Ui; var s = App.Save.Settings;
            float W = r.ViewW, H = r.ViewH;
            r.Sprite(Tx.Pixel, W / 2, H / 2, W, H, 0, new Col(0.01f, 0.01f, 0.04f, 0.72f), Blend.Alpha);
            var plat = App.Platform;
            int rows = 5 + (plat.SupportsFullscreen ? 1 : 0) + (plat.SupportsQuality ? 2 : 0) + (App.Updater != null ? 1 : 0);
            float rh = 66, gap = 10, pw = 900, ph = 170 + rows * (rh + gap) + 110;
            var panel = Rect.Centered(W / 2, H / 2, pw, ph);
            ui.Panel(panel, ui.Accent);
            ui.Glow("SETTINGS", W / 2, panel.Y + 70, 58, ui.Accent, Align.Center, FontId.Title);
            if (!interactive) return;

            float x = panel.X + 50, w = pw - 100, y = panel.Y + 140;
            bool changed = false;
            float mv = s.MusicVolume;
            if (ui.Slider(IdMusic, new Rect(x, y, w, rh), "MUSIC", ref mv)) { s.MusicVolume = mv; App.Audio.SetMusicVolume(mv); changed = true; }
            y += rh + gap;
            float sv = s.SfxVolume;
            if (ui.Slider(IdSfx, new Rect(x, y, w, rh), "SOUND EFFECTS", ref sv))
            {
                s.SfxVolume = sv; changed = true;
                if (_sfxPreview <= 0) { App.PlaySfx(Sfx.Orb, 0.8f); _sfxPreview = 0.15f; }
            }
            y += rh + gap;
            if (plat.SupportsFullscreen)
            {
                bool f = s.Fullscreen;
                if (ui.Toggle(IdFull, new Rect(x, y, w, rh), "FULLSCREEN  (F11)", ref f)) { s.Fullscreen = f; changed = true; }
                y += rh + gap;
            }
            if (plat.SupportsQuality)
            {
                int q = s.Quality;
                if (ui.Choice(IdQuality, new Rect(x, y, w, rh), "GRAPHICS", Qualities, ref q)) { s.Quality = q; changed = true; }
                y += rh + gap;
                int fps = s.FpsCap;
                if (ui.Choice(IdFps, new Rect(x, y, w, rh), "FRAME RATE", Fps, ref fps)) { s.FpsCap = fps; changed = true; }
                y += rh + gap;
            }
            bool sh = s.ScreenShake;
            if (ui.Toggle(IdShake, new Rect(x, y, w, rh), "SCREEN SHAKE", ref sh)) { s.ScreenShake = sh; changed = true; }
            y += rh + gap;
            bool tm = s.ShowTimer;
            if (ui.Toggle(IdTimer, new Rect(x, y, w, rh), "SHOW TIMER", ref tm)) { s.ShowTimer = tm; changed = true; }
            y += rh + gap;
            var up = App.Updater;
            if (up != null)
            {
                string ul;
                switch (up.State)
                {
                    case UpdateState.Checking: ul = "CHECKING FOR UPDATES..."; break;
                    case UpdateState.UpToDate: ul = "UP TO DATE  (v" + GameApp.Version + ")"; break;
                    case UpdateState.Available: ul = "UPDATE AVAILABLE  " + up.Latest.Version + "  -  INSTALL"; break;
                    case UpdateState.Failed: ul = "COULD NOT CHECK (OFFLINE?)  -  TRY AGAIN"; break;
                    default: ul = "CHECK FOR UPDATES  (v" + GameApp.Version + ")"; break;
                }
                if (ui.Button(IdUpdates, new Rect(x, y, w, rh), ul, Tx.IconCheck, up.State == UpdateState.Available))
                {
                    App.PlaySfx(Sfx.Click);
                    if (up.State == UpdateState.Available) App.Push(new UpdateScreen());
                    else if (up.State != UpdateState.Checking) up.Check();
                }
                y += rh + gap;
            }
            string resetLabel = _resetArm > 0 ? "PRESS AGAIN TO ERASE ALL PROGRESS" : "RESET PROGRESS";
            if (ui.Button(IdReset, new Rect(x, y, w, rh), resetLabel, Tx.IconRetry))
            {
                if (_resetArm > 0)
                {
                    App.Save.Levels.Clear(); App.Save.SeenSkins.Clear(); App.Save.SeenSkins.Add(0);
                    App.Save.SelectedSkin = 0; App.Save.LastLevel = 1; App.Save.TotalDeaths = App.Save.TotalOrbs = App.Save.TotalFlips = 0;
                    App.SaveNow(); App.PlaySfx(Sfx.Death, 0.6f); _resetArm = 0;
                }
                else { _resetArm = 3f; App.PlaySfx(Sfx.Click); }
            }
            y += rh + gap + 18;
            if (ui.Button(IdBack, new Rect(W / 2 - 180, y, 360, 78), "BACK", Tx.IconBack, true)) Close();
            if (changed) App.ApplySettings();
        }
    }
}
