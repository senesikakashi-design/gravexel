using System;
using System.Collections.Generic;

namespace Gravexel.Core
{
    /// <summary>Shows a new version's notes and installs it (Windows) or opens its download page.</summary>
    public sealed class UpdateScreen : Screen
    {
        const int IdInstall = 600, IdLater = 601, IdRetry = 602;
        public override bool IsOverlay { get { return true; } }

        public override void Enter() { App.Ui.Focus = IdInstall; }

        public override void Update(float dt)
        {
            var u = App.Updater;
            bool busy = u != null && (u.State == UpdateState.Downloading || u.State == UpdateState.Installing);
            if ((App.In.Back || App.In.Pause) && !busy) { App.PlaySfx(Sfx.Back); App.Pop(); }
        }

        List<string> Wrap(string text, float size, float width)
        {
            var lines = new List<string>();
            foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                string para = raw.Trim().TrimStart('#', '*', '-', ' ');
                if (para.Length == 0) continue;
                string cur = "";
                foreach (var word in para.Split(' '))
                {
                    string test = cur.Length == 0 ? word : cur + " " + word;
                    if (App.R.MeasureText(test, size, FontId.Ui) > width && cur.Length > 0) { lines.Add(cur); cur = word; }
                    else cur = test;
                }
                if (cur.Length > 0) lines.Add(cur);
            }
            return lines;
        }

        public override void Draw(bool interactive)
        {
            var r = App.R; var ui = App.Ui; var u = App.Updater;
            float W = r.ViewW, H = r.ViewH;
            r.Sprite(Tx.Pixel, W / 2, H / 2, W, H, 0, new Col(0.01f, 0.01f, 0.04f, 0.78f), Blend.Alpha);
            var panel = Rect.Centered(W / 2, H / 2, 960, 760);
            ui.Panel(panel, ui.Accent2);
            if (u == null) return;
            var info = u.Latest;
            ui.Glow("UPDATE AVAILABLE", W / 2, panel.Y + 74, 56, ui.Accent2, Align.Center, FontId.Title);
            r.Text("v" + GameApp.Version + "   >   " + (info.Version.StartsWith("v") ? info.Version : "v" + info.Version), W / 2, panel.Y + 140, 36, Col.White, Align.Center, FontId.Ui);

            var lines = Wrap(info.Notes, 28, panel.W - 160);
            if (lines.Count == 0) lines.Add("New levels, fixes and improvements.");
            float y = panel.Y + 210;
            for (int i = 0; i < lines.Count && i < 9; i++)
            {
                r.Sprite(Tx.Circle, panel.X + 92, y, 8, 8, 0, ui.Accent, Blend.Alpha);
                r.Text(lines[i], panel.X + 110, y, 28, Col.White.WithA(0.85f), Align.Left, FontId.Ui);
                y += 42;
            }

            float by = panel.Bottom - 120;
            if (u.State == UpdateState.Downloading || u.State == UpdateState.Installing)
            {
                var bar = new Rect(panel.X + 80, by + 10, panel.W - 160, 18);
                ui.Fill(bar, Col.White.WithA(0.12f));
                ui.Fill(new Rect(bar.X, bar.Y, bar.W * Mathx.Clamp01(u.Progress), bar.H), ui.Accent);
                r.Sprite(Tx.GlowLine, bar.X + bar.W * u.Progress / 2, bar.CY, bar.W * u.Progress, 60, 0, ui.Accent.WithA(0.5f), Blend.Add);
                string msg = u.State == UpdateState.Installing ? "INSTALLING... THE GAME WILL RESTART BY ITSELF" : "DOWNLOADING  " + (int)(u.Progress * 100) + "%";
                r.Text(msg, W / 2, by + 62, 30, Col.White, Align.Center, FontId.Ui);
                return;
            }
            if (u.State == UpdateState.Failed)
                r.Text("Could not update: " + u.Error, W / 2, by - 30, 26, Col.Hex("#FF6B8A"), Align.Center, FontId.Ui);
            if (!interactive) return;
            float bw = 360, gap = 30;
            string label = u.State == UpdateState.Failed ? "TRY AGAIN" : (u.CanInstallInApp ? "UPDATE NOW" : "DOWNLOAD");
            if (ui.Button(IdInstall, new Rect(W / 2 - bw - gap / 2, by, bw, 80), label, Tx.IconCheck, true)) { App.PlaySfx(Sfx.Click); u.Install(); }
            if (ui.Button(IdLater, new Rect(W / 2 + gap / 2, by, bw, 80), "LATER", Tx.IconBack)) { App.PlaySfx(Sfx.Back); App.Pop(); }
        }
    }
}
