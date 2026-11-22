using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Gravexel.Core;

namespace Gravexel.Tools
{
    /// <summary>
    /// Developer tool:
    ///   validate levels.txt [--write]   prove every level is finishable with all stars, compute par times
    ///   replay   levels.txt             play every level through the real game screen using the solver's inputs
    ///   smoke    levels.txt             drive all menus with random input to catch crashes
    /// </summary>
    public static class LevelTool
    {
        public static int Main(string[] args)
        {
            if (args.Length < 2) { Console.WriteLine("usage: LevelTool validate|replay|smoke <levels.txt> [--write]"); return 2; }
            string text = File.ReadAllText(args[1]);
            switch (args[0])
            {
                case "validate": return Validate(args[1], text, args.Contains("--write"), args.Contains("--fast"));
                case "replay": return Replay(text);
                case "smoke": return Smoke(text);
            }
            return 2;
        }

        static int Validate(string path, string text, bool write, bool fast)
        {
            var levels = LevelFile.Parse(text);
            int bad = 0;
            var sw = Stopwatch.StartNew();
            foreach (var info in levels)
            {
                var L = new LevelData(info);
                var t0 = sw.Elapsed.TotalSeconds;
                var res = Solver.Solve(L, 180f, fast ? 6 : 3);
                string status = res.AllStars ? "OK " : res.Finishable ? "NO-STARS" : "UNSOLVABLE";
                if (!res.AllStars) bad++;
                Console.WriteLine("{0,2} {1,-16} w{2} {3,4}x{4,-2} stars={5} orbs={6,3}  finish={7,6:0.00}s  all={8,6:0.00}s  peak={9,5}  {10}  ({11:0.0}s)",
                    info.Number, info.Name, info.World, L.W, L.H, L.Stars.Count, L.Orbs.Count, res.FinishTime, res.AllStarsTime, res.PeakStates, status,
                    sw.Elapsed.TotalSeconds - t0);
                if (res.AllStars) info.Par = (float)Math.Ceiling(res.AllStarsTime * 1.25f + 2f);
            }
            Console.WriteLine(bad == 0 ? "ALL LEVELS SOLVABLE WITH ALL STARS" : bad + " LEVEL(S) NEED WORK");
            if (write && bad == 0)
            {
                string header = string.Join("\n", text.Replace("\r", "").Split('\n').TakeWhile(l => l.StartsWith("#") || l.Trim().Length == 0)) + "\n";
                File.WriteAllText(path, LevelFile.Write(levels, header));
                Console.WriteLine("par times written to " + path);
            }
            return bad == 0 ? 0 : 1;
        }

        static GameApp NewApp(string text, out TestHost host)
        {
            host = new TestHost();
            return new GameApp(host, host, host, host, text);
        }

        static void Run(GameApp app, InputState input, int frames)
        {
            for (int i = 0; i < frames; i++) { app.Frame(Physics.Dt, input); input.ClearFrame(); }
        }

        static int Replay(string text)
        {
            TestHost host;
            var app = NewApp(text, out host);
            var input = new InputState();
            int fails = 0;
            foreach (var info in app.Levels)
            {
                var L = new LevelData(info);
                var res = Solver.Solve(L, 180f, 3);
                if (!res.AllStars) { Console.WriteLine("level {0}: solver failed", info.Number); fails++; continue; }
                var screen = new GameScreen(info.Number);
                app.GoTo(screen);
                Run(app, input, 90);
                if (screen.StateName != "Ready") { Console.WriteLine("level {0}: not ready ({1})", info.Number, screen.StateName); fails++; continue; }
                input.Action = true; input.Confirm = true; Run(app, input, 1);
                var flips = new HashSet<int>(res.FlipFrames);
                int f = 0, limit = (int)(res.AllStarsTime / Physics.Dt) + 30;
                while (f < limit && screen.StateName == "Playing")
                {
                    input.Action = flips.Contains(f); input.Confirm = input.Action;
                    app.Frame(Physics.Dt, input); input.ClearFrame();
                    f++;
                }
                bool ok = screen.StateName == "Complete" && screen.RunStars == 7 && screen.Deaths == 0;
                var rec = app.Save.Get(info.Number);
                ok &= rec.Completed && rec.StarCount == 3;
                Console.WriteLine("level {0,2} {1,-16} {2}  frames={3} stars={4} orbs={5} score={6}", info.Number, info.Name, ok ? "WIN " : "FAIL", f, screen.RunStars, screen.OrbsCollected, screen.Score);
                if (!ok) fails++;
                Run(app, input, 200); // let the completion panel animate
            }
            Console.WriteLine("saved data: stars={0} unlocked skins={1}", app.Save.TotalStars, app.Save.SeenSkins.Count);
            Console.WriteLine("save text length: " + host.Saved.Length);
            var reloaded = SaveData.Deserialize(host.Saved);
            Console.WriteLine("reload check: stars={0} last={1}", reloaded.TotalStars, reloaded.LastLevel);
            Console.WriteLine(fails == 0 ? "REPLAY: ALL LEVELS WON WITH 3 STARS" : "REPLAY FAILURES: " + fails);
            return fails == 0 ? 0 : 1;
        }

        static int Smoke(string text)
        {
            TestHost host;
            var app = NewApp(text, out host);
            var input = new InputState();
            var rng = new Random(5);
            int frames = 0;
            // walk through every menu with keyboard and random clicks
            for (int round = 0; round < 40; round++)
            {
                for (int i = 0; i < 240; i++)
                {
                    int k = rng.Next(40);
                    input.Up = k == 0; input.Down = k == 1; input.Left = k == 2; input.Right = k == 3;
                    input.Confirm = k == 4; input.Action = k == 5 || k == 4; input.Back = k == 6 && rng.Next(4) == 0;
                    input.Pause = k == 7 && rng.Next(3) == 0;
                    if (k == 8) { input.PointerX = rng.Next(1920); input.PointerY = rng.Next(1080); input.PointerMoved = true; input.PointerPressed = true; input.PointerDown = true; }
                    if (k == 9) { input.PointerReleased = true; input.PointerDown = false; }
                    app.Frame(1 / 60f, input); input.ClearFrame(); frames++;
                }
            }
            Console.WriteLine("SMOKE OK: {0} frames, sprites drawn {1}, texts {2}", frames, host.Sprites, host.Texts);
            return 0;
        }
    }

    /// <summary>Headless host used by the tests.</summary>
    sealed class TestHost : IRenderer, IAudio, ISaveStore, IPlatform
    {
        public long Sprites, Texts; public string Saved = "";
        public float ViewW { get { return 1920; } }
        public float ViewH { get { return 1080; } }
        public void RegisterTexture(int id, PixelImage image) { }
        public void Background(Col top, Col bottom) { }
        public void Sprite(int tex, float cx, float cy, float w, float h, float rot, Col color, Blend blend)
        {
            if (float.IsNaN(cx) || float.IsNaN(cy) || float.IsNaN(w) || float.IsNaN(h)) throw new Exception("NaN sprite");
            Sprites++;
        }
        public void Text(string text, float x, float y, float size, Col color, Align align, FontId font) { if (text == null) throw new Exception("null text"); Texts++; }
        public float MeasureText(string text, float size, FontId font) { return text.Length * size * 0.5f; }
        public void BuildStaticLayer(int layerId, DrawCmd[] cmds, int count, float width, float height) { }
        public void DrawStaticLayer(int layerId, float x, float y) { }
        public void Vignette(float strength) { }
        public void RegisterClip(int id, float[] monoSamples, int sampleRate) { }
        public void Play(int id, float volume, float pitch) { }
        public void PlayMusic(int id, float volume) { }
        public void SetMusicVolume(float volume) { }
        public string Load() { return Saved; }
        public void Save(string data) { Saved = data; }
        public string Name { get { return "test"; } }
        public bool IsTouch { get { return false; } }
        public bool SupportsQuit { get { return false; } }
        public bool SupportsFullscreen { get { return true; } }
        public bool SupportsQuality { get { return true; } }
        public void Quit() { }
        public void ApplySettings(Settings s) { }
    }
}
