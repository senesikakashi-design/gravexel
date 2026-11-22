using System;
using System.Collections.Generic;

namespace Gravexel.Core
{
    public sealed class LevelSelectScreen : Screen
    {
        const int IdBack = 200, IdTab = 210, IdCard = 230;
        int _world;
        readonly bool _finishedAll;
        float _shakeCard; int _shakeId = -1;

        public LevelSelectScreen(int world, bool finishedAll = false) { _world = Math.Max(1, world); _finishedAll = finishedAll; }

        public override void Enter()
        {
            App.PlayMusic(Sfx.MusicMenu);
            SetTheme();
            // focus the first unlocked, unfinished level of this world
            var list = App.LevelsInWorld(_world);
            for (int i = 0; i < list.Count; i++)
                if (App.Save.IsUnlocked(list[i].Number) && !App.Save.Get(list[i].Number).Completed) { App.Ui.Focus = IdCard + i; return; }
            App.Ui.Focus = IdCard;
        }

        void SetTheme() { var t = Themes.For(_world); App.Ui.Accent = t.Edge; App.Ui.Accent2 = t.Accent; }

        public override void Update(float dt)
        {
            if (App.In.Back || App.In.Pause) { App.PlaySfx(Sfx.Back); App.GoTo(new TitleScreen()); }
            _shakeCard = Math.Max(0, _shakeCard - dt * 3);
        }

        bool WorldUnlocked(int w)
        {
            var list = App.LevelsInWorld(w);
            return list.Count > 0 && App.Save.IsUnlocked(list[0].Number);
        }

        public override void Draw(bool interactive)
        {
            var r = App.R; var ui = App.Ui; var theme = Themes.For(_world);
            float W = r.ViewW, H = r.ViewH, t = App.Time;
            App.Backdrop.Draw(r, theme, t * 60f, t);
            r.Vignette(0.6f);
            r.Sprite(Tx.Pixel, W / 2, H / 2, W, H, 0, new Col(0, 0, 0.02f, 0.35f), Blend.Alpha);

            ui.Glow("SELECT LEVEL", W / 2, 92, 64, theme.Edge, Align.Center, FontId.Title);
            if (interactive && ui.IconButton(IdBack, new Rect(50, 52, 84, 84), Tx.IconBack)) { App.PlaySfx(Sfx.Back); App.GoTo(new TitleScreen()); }

            // world tabs
            int worlds = Math.Max(1, App.Worlds);
            float tg = 16, tw = Math.Min(330, (W - 220 - tg * (worlds - 1)) / worlds), th = 92, tx0 = W / 2 - (tw * worlds + tg * (worlds - 1)) / 2;
            float nameSize = tw < 300 ? 22 : 32;
            for (int w = 1; w <= worlds; w++)
            {
                var wt = Themes.For(w);
                var rect = new Rect(tx0 + (w - 1) * (tw + tg), 170, tw, th);
                float a = 0; bool focused = false;
                bool act = interactive && ui.Area(IdTab + w, rect, out a, out focused);
                bool sel = w == _world; bool unlocked = WorldUnlocked(w);
                ui.Fill(rect, new Col(0.03f, 0.05f, 0.12f, sel ? 0.92f : 0.7f));
                if (sel) { ui.Fill(new Rect(rect.X, rect.Bottom - 5, rect.W, 5), wt.Edge); r.Sprite(Tx.GlowLine, rect.CX, rect.Bottom - 3, rect.W, 40, 0, wt.Edge.WithA(0.6f), Blend.Add); }
                ui.Border(rect, focused ? wt.Edge : Col.White.WithA(sel ? 0.3f : 0.12f), focused ? 2.5f : 1.5f);
                r.Text("WORLD " + w, rect.X + 22, rect.Y + 30, 24, wt.Edge.WithA(unlocked ? 1 : 0.5f), Align.Left, FontId.Ui);
                r.Text(wt.Name, rect.X + 22, rect.Y + 64, nameSize, Col.White.WithA(unlocked ? 1 : 0.45f), Align.Left, FontId.Title);
                int got = 0, max = 0; foreach (var l in App.LevelsInWorld(w)) { got += App.Save.Get(l.Number).StarCount; max += 3; }
                if (unlocked)
                {
                    r.Sprite(Tx.Star, rect.Right - 92, rect.Y + 30, 24, 24, 0, WorldView.StarCol, Blend.Alpha);
                    r.Text(got + "/" + max, rect.Right - 20, rect.Y + 30, 24, Col.White.WithA(0.85f), Align.Right, FontId.Ui);
                }
                else r.Sprite(Tx.Lock, rect.Right - 40, rect.Y + 34, 32, 32, 0, Col.White.WithA(0.5f), Blend.Alpha);
                if (act && w != _world) { _world = w; SetTheme(); App.PlaySfx(Sfx.Click, 0.7f); }
            }

            // level cards
            var levels = App.LevelsInWorld(_world);
            bool many = levels.Count > 8;
            int cols = many ? 6 : 4; float cw = many ? 262 : 300, ch = many ? 232 : 250, gx = many ? 24 : 34, gy = many ? 26 : 34;
            int rows = (levels.Count + cols - 1) / cols;
            float gx0 = W / 2 - (cols * cw + (cols - 1) * gx) / 2, gy0 = 320;
            for (int i = 0; i < levels.Count; i++)
            {
                var info = levels[i];
                int col = i % cols, row = i / cols;
                var rect = new Rect(gx0 + col * (cw + gx), gy0 + row * (ch + gy), cw, ch);
                if (_shakeId == i) rect.X += Mathx.Sin(t * 70) * 10 * _shakeCard;
                bool unlocked = App.Save.IsUnlocked(info.Number);
                var rec = App.Save.Get(info.Number);
                float a = 0; bool focused = false;
                bool act = interactive && ui.Area(IdCard + i, rect, out a, out focused);
                var rr = rect.Inflate(a * 8);
                if (a > 0.01f) r.Sprite(Tx.Glow, rr.CX, rr.CY, rr.W * 1.5f, rr.H * 1.5f, 0, theme.Edge.WithA(0.2f * a), Blend.Add);
                ui.Fill(rr, new Col(0.03f, 0.05f, 0.12f, unlocked ? 0.88f : 0.6f));
                if (rec.Completed) ui.Fill(new Rect(rr.X, rr.Y, rr.W, 6), theme.Edge);
                ui.Border(rr, focused ? theme.Edge : Col.White.WithA(0.14f), focused ? 3f : 1.5f);
                ui.Corners(rr, theme.Edge.WithA(0.4f + 0.6f * a), 20);
                if (unlocked)
                {
                    ui.Glow(info.World + "-" + (i + 1), rr.CX, rr.Y + 78, many ? 60 : 70, focused ? theme.Edge : Col.White, Align.Center, FontId.Title, focused ? 0.8f : 0.2f);
                    r.Text(info.Name.ToUpperInvariant(), rr.CX, rr.Y + 142, 26, Col.White.WithA(0.75f), Align.Center, FontId.Ui);
                    ui.Stars(rr.CX, rr.Y + 192, 38, rec.StarBits, 50, t);
                    if (rec.BestTime > 0) r.Text(rec.BestTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s", rr.Right - 16, rr.Bottom - 20, 20, Col.White.WithA(0.45f), Align.Right, FontId.Ui);
                }
                else
                {
                    r.Sprite(Tx.Lock, rr.CX, rr.CY - 10, 70, 70, 0, Col.White.WithA(0.35f), Blend.Alpha);
                    r.Text(info.World + "-" + (i + 1), rr.CX, rr.Bottom - 40, 30, Col.White.WithA(0.3f), Align.Center, FontId.Title);
                }
                if (act)
                {
                    if (unlocked) { App.PlaySfx(Sfx.Click); App.GoTo(new GameScreen(info.Number)); }
                    else { App.PlaySfx(Sfx.Back, 0.8f, 0.7f); _shakeId = i; _shakeCard = 1; }
                }
            }

            if (_finishedAll)
            {
                float y = gy0 + rows * (ch + gy) + 40;
                r.Sprite(Tx.GlowLine, W / 2, y, 1100, 110, 0, theme.Accent.WithA(0.35f), Blend.Add);
                ui.Glow("YOU FINISHED EVERY LEVEL  ·  COLLECT ALL THE STARS!", W / 2, y, 38, Col.White, Align.Center, FontId.Ui, 0.5f);
            }
            else
            {
                string help = App.Platform.IsTouch ? "Tap a level to play" : "Arrow keys to move  ·  Enter to play  ·  Esc to go back";
                r.Text(help, W / 2, H - 50, 26, Col.White.WithA(0.45f), Align.Center, FontId.Ui);
            }
        }
    }
}
