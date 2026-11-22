using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Gravexel.Core;

namespace Gravexel.Desktop
{
    /// <summary>Command-line modes: --shots (screenshots), --bench (render speed), --selftest (plays every level).</summary>
    public static class Headless
    {
        sealed class NullAudio : IAudio
        {
            public void RegisterClip(int id, float[] s, int r) { }
            public void Play(int id, float v, float p) { }
            public void PlayMusic(int id, float v) { }
            public void SetMusicVolume(float v) { }
        }

        sealed class MemStore : ISaveStore
        {
            public string Data;
            public string Load() { return Data; }
            public void Save(string d) { Data = d; }
        }

        sealed class Plat : IPlatform
        {
            public string Name { get { return "Headless"; } }
            public bool IsTouch { get { return false; } }
            public bool SupportsQuit { get { return true; } }
            public bool SupportsFullscreen { get { return true; } }
            public bool SupportsQuality { get { return true; } }
            public void Quit() { }
            public void ApplySettings(Settings s) { }
        }

        sealed class NullRenderer : IRenderer
        {
            public float ViewW { get { return 1920; } }
            public float ViewH { get { return 1080; } }
            public void RegisterTexture(int id, PixelImage image) { }
            public void Background(Col top, Col bottom) { }
            public void Sprite(int tex, float cx, float cy, float w, float h, float rot, Col color, Blend blend) { }
            public void Text(string text, float x, float y, float size, Col color, Align align, FontId font) { }
            public float MeasureText(string text, float size, FontId font) { return text.Length * size * 0.5f; }
            public void BuildStaticLayer(int layerId, DrawCmd[] cmds, int count, float width, float height) { }
            public void DrawStaticLayer(int layerId, float x, float y) { }
            public void Vignette(float strength) { }
        }

        static void Frames(GameApp app, SoftRenderer r, InputState input, int n, float dt)
        {
            for (int i = 0; i < n; i++)
            {
                if (r != null) r.BeginFrame();
                app.Frame(dt, input); input.ClearFrame();
            }
        }

        static void Shot(GameApp app, SoftRenderer r, InputState input, string dir, string name)
        {
            r.BeginFrame(); app.Frame(1 / 60f, input); input.ClearFrame(); r.EndFrame();
            using (var bmp = r.ToBitmap()) bmp.Save(Path.Combine(dir, name), ImageFormat.Png);
            Console.WriteLine("saved " + name);
        }

        public static int Screenshots(string dir, int w, int h)
        {
            Directory.CreateDirectory(dir);
            var r = new SoftRenderer(Program.Fonts());
            r.Resize(w, h);
            var store = new MemStore();
            var app = new GameApp(r, new NullAudio(), store, new Plat(), Program.LevelsText());
            var input = new InputState();
            Frames(app, r, input, 110, 1 / 60f);
            Shot(app, r, input, dir, "00_splash.png");
            app.GoTo(new TitleScreen()); Frames(app, r, input, 90, 1 / 60f);
            Shot(app, r, input, dir, "01_title.png");

            // pretend some progress so menus look lived-in
            for (int n = 1; n <= 11; n++) { var rec = app.Save.Get(n); rec.Completed = true; rec.StarBits = n % 4 == 0 ? 3 : 7; rec.BestTime = 10 + n; rec.BestScore = 1000 + n * 20; }
            app.Save.SeenSkins.Add(1); app.Save.SeenSkins.Add(2); app.Save.SelectedSkin = 1;

            app.GoTo(new LevelSelectScreen(2)); Frames(app, r, input, 60, 1 / 60f);
            Shot(app, r, input, dir, "02_levels.png");
            app.GoTo(new SkinsScreen()); Frames(app, r, input, 60, 1 / 60f);
            Shot(app, r, input, dir, "03_characters.png");
            app.GoTo(new TitleScreen()); Frames(app, r, input, 40, 1 / 60f);
            app.Push(new SettingsScreen()); Frames(app, r, input, 20, 1 / 60f);
            Shot(app, r, input, dir, "04_settings.png");

            int[] levels = { 1, 10, 18, 38, 56 };
            float[] at = { 0.32f, 0.45f, 0.3f, 0.42f, 0.55f };
            for (int i = 0; i < levels.Length; i++)
            {
                var info = app.Level(levels[i]);
                var res = Solver.Solve(new LevelData(info), 180f, 3);
                var gs = new GameScreen(levels[i]);
                app.GoTo(gs); Frames(app, r, input, 90, Physics.Dt);
                if (i == 0) Shot(app, r, input, dir, "05_ready.png");
                input.Action = true; Frames(app, r, input, 1, Physics.Dt);
                var flips = new HashSet<int>(res.FlipFrames);
                int stop = (int)(res.AllStarsTime / Physics.Dt * at[i]);
                for (int f = 0; f < stop && gs.StateName == "Playing"; f++) { input.Action = flips.Contains(f); r.BeginFrame(); app.Frame(Physics.Dt, input); input.ClearFrame(); }
                Shot(app, r, input, dir, string.Format("{0:00}_play_level{1}.png", 6 + i, levels[i]));
                if (i == 1)
                {
                    input.Pause = true; Frames(app, r, input, 1, 1 / 60f); Frames(app, r, input, 30, 1 / 60f);
                    Shot(app, r, input, dir, "11_pause.png");
                    input.Pause = true; Frames(app, r, input, 1, 1 / 60f);
                }
                if (i == 0)
                {
                    int f2 = stop;
                    int end = (int)(res.AllStarsTime / Physics.Dt) + 10;
                    for (; f2 < end && gs.StateName == "Playing"; f2++) { input.Action = flips.Contains(f2); r.BeginFrame(); app.Frame(Physics.Dt, input); input.ClearFrame(); }
                    Frames(app, r, input, 170, 1 / 60f);
                    Shot(app, r, input, dir, "12_complete.png");
                }
            }
            return 0;
        }

        /// <summary>Renders the application icon (256x256 PNG).</summary>
        public static int Icon(string path)
        {
            var r = new SoftRenderer(Program.Fonts());
            r.Resize(256, 256);
            TextureGen.RegisterAll(r);
            r.BeginFrame();
            float S = 1080f; // virtual units across the 256px square
            var navy = Col.Hex("#0A1030"); var cyan = Col.Hex("#22E8FF"); var pink = Col.Hex("#FF3DDB");
            r.Background(Col.Hex("#050A1C"), Col.Hex("#1A0B33"));
            r.Sprite(Tx.Glow, S * 0.5f, S * 0.5f, S * 1.1f, S * 1.1f, 0, pink.WithA(0.35f), Blend.Add);
            foreach (float y in new[] { S * 0.16f, S * 0.84f })
            {
                r.Sprite(Tx.GlowLine, S * 0.5f, y, S, 160, 0, cyan.WithA(0.8f), Blend.Add);
                r.Sprite(Tx.Pixel, S * 0.5f, y, S, 26, 0, cyan.Brighten(0.5f), Blend.Alpha);
            }
            var skin = Skins.All[0];
            for (int i = 1; i < 5; i++)
                r.Sprite(Tx.BodyCube, S * 0.5f, S * 0.62f - i * 70, 420 - i * 50, 420 - i * 50, 0, cyan.WithA(0.18f), Blend.Add);
            WorldView.DrawCharacter(r, skin, S * 0.5f, S * 0.6f, 430, 0, 1, 0.5f);
            r.Sprite(Tx.Chevron, S * 0.5f, S * 0.27f, 170, 170, -Mathx.PI / 2, pink, Blend.Alpha);
            r.EndFrame();
            using (var bmp = r.ToBitmap()) bmp.Save(path, ImageFormat.Png);
            return 0;
        }

        public static int Bench(int w, int h)
        {
            var r = new SoftRenderer(Program.Fonts());
            r.Resize(w, h);
            var app = new GameApp(r, new NullAudio(), new MemStore(), new Plat(), Program.LevelsText());
            var input = new InputState();
            var sb = new StringBuilder();
            foreach (var scene in new[] { "title", "level16", "level32" })
            {
                GameScreen gs = null;
                if (scene == "level16" || scene == "level32") { gs = new GameScreen(scene == "level16" ? 16 : 32); app.GoTo(gs); }
                Frames(app, r, input, 80, Physics.Dt);
                List<int> flips = null; LevelData L = null;
                if (gs != null)
                {
                    L = new LevelData(app.Level(scene == "level16" ? 16 : 32));
                    flips = Solver.Solve(L, 180, 3).FlipFrames;
                    input.Action = true; Frames(app, r, input, 1, Physics.Dt);
                }
                var set = flips == null ? new HashSet<int>() : new HashSet<int>(flips);
                var sw = Stopwatch.StartNew(); int n = 0; double logic = 0, raster = 0;
                for (int f = 0; f < 240; f++)
                {
                    input.Action = set.Contains(f);
                    var t0 = sw.Elapsed.TotalMilliseconds;
                    r.BeginFrame(); app.Frame(Physics.Dt, input); input.ClearFrame();
                    var t1 = sw.Elapsed.TotalMilliseconds;
                    r.EndFrame();
                    var t2 = sw.Elapsed.TotalMilliseconds;
                    logic += t1 - t0; raster += t2 - t1; n++;
                }
                sb.AppendFormat("{0,-8} {1}x{2}: logic {3:0.00} ms, raster {4:0.00} ms, total {5:0.00} ms/frame\n", scene, w, h, logic / n, raster / n, (logic + raster) / n);
            }
            Console.Write(sb.ToString());
            return 0;
        }

        /// <summary>Plays every level with the solver's inputs through the real game code.</summary>
        public static int SelfTest(bool showDialog)
        {
            var store = new MemStore();
            var app = new GameApp(new NullRenderer(), new NullAudio(), store, new Plat(), Program.LevelsText());
            var input = new InputState();
            var sb = new StringBuilder();
            int fails = 0;
            foreach (var info in app.Levels)
            {
                var res = Solver.Solve(new LevelData(info), 180f, 3);
                var gs = new GameScreen(info.Number);
                app.GoTo(gs); Frames(app, null, input, 90, Physics.Dt);
                input.Action = true; input.Confirm = true; Frames(app, null, input, 1, Physics.Dt);
                var flips = new HashSet<int>(res.FlipFrames);
                int f = 0, limit = (int)(res.AllStarsTime / Physics.Dt) + 30;
                for (; f < limit && gs.StateName == "Playing"; f++) { input.Action = flips.Contains(f); input.Confirm = input.Action; app.Frame(Physics.Dt, input); input.ClearFrame(); }
                bool ok = res.AllStars && gs.StateName == "Complete" && gs.RunStars == 7;
                if (!ok) fails++;
                sb.AppendFormat("Level {0,2} {1,-16} {2}\r\n", info.Number, info.Name, ok ? "OK (3 stars)" : "FAILED");
            }
            sb.Append(fails == 0 ? "\r\nAll levels passed.\r\n" : "\r\n" + fails + " level(s) failed.\r\n");
            Console.Write(sb.ToString());
            if (showDialog)
            {
                try { Directory.CreateDirectory(Program.DataDir); File.WriteAllText(Path.Combine(Program.DataDir, "selftest.txt"), sb.ToString()); } catch (Exception) { }
                MessageBox.Show(sb.ToString(), "GRAVEXEL self-test", MessageBoxButtons.OK, fails == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            return fails == 0 ? 0 : 1;
        }
    }
}
