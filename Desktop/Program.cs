using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Gravexel.Core;

namespace Gravexel.Desktop
{
    /// <summary>Windows host: window, input, frame pacing, presentation, saving.</summary>
    public static class Program
    {
        public static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gravexel");

        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--shots") return Headless.Screenshots(args.Length > 1 ? args[1] : "shots", args.Length > 2 ? int.Parse(args[2]) : 1920, args.Length > 3 ? int.Parse(args[3]) : 1080);
                if (args.Length > 0 && args[0] == "--bench") return Headless.Bench(args.Length > 1 ? int.Parse(args[1]) : 1280, args.Length > 2 ? int.Parse(args[2]) : 720);
                if (args.Length > 0 && args[0] == "--selftest") return Headless.SelfTest(!(args.Length > 1 && args[1] == "--quiet"));
                if (args.Length > 1 && args[0] == "--icon") return Headless.Icon(args[1]);

                try { SetProcessDPIAware(); } catch (Exception) { }
                Application.EnableVisualStyles();
                using (var form = new GameForm())
                {
                    form.Show();
                    form.RunLoop();
                }
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(DataDir);
                    File.WriteAllText(Path.Combine(DataDir, "crash.log"), DateTime.Now + "\r\n" + ex);
                }
                catch (Exception) { }
                MessageBox.Show("GRAVEXEL hit an error and has to close.\n\n" + ex.Message + "\n\nDetails were saved to:\n" + Path.Combine(DataDir, "crash.log"),
                    "GRAVEXEL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        public static byte[] Resource(string name)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) throw new FileNotFoundException("Missing embedded resource " + name);
                var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray();
            }
        }

        public static string LevelsText() { return Encoding.UTF8.GetString(Resource("levels.txt")); }

        public static GlyphCache Fonts() { return new GlyphCache(Resource("fonts.bin")); }

        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    }

    /// <summary>Saves progress to %LOCALAPPDATA%\Gravexel\save.txt (written atomically).</summary>
    public sealed class FileSaveStore : ISaveStore
    {
        readonly string _path;
        public FileSaveStore(string dir) { Directory.CreateDirectory(dir); _path = Path.Combine(dir, "save.txt"); }
        public string Load()
        {
            if (File.Exists(_path)) return File.ReadAllText(_path);
            // carry progress over from the earlier "GRAVITY SWITCH" builds
            string old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GravitySwitch", "save.txt");
            return File.Exists(old) ? File.ReadAllText(old) : null;
        }
        public void Save(string data)
        {
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, data);
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }
    }

    public sealed class GameForm : Form, IPlatform
    {
        readonly SoftRenderer _renderer;
        readonly WaveOutAudio _audio;
        readonly GameApp _app;
        readonly InputState _input = new InputState();
        readonly HashSet<Keys> _held = new HashSet<Keys>();
        readonly bool _windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        Settings _settings = new Settings();
        bool _fullscreen; Rectangle _windowedBounds;
        volatile bool _quit;
        float _fpsTimer; int _fpsFrames; public float Fps;

        public GameForm()
        {
            Text = "GRAVEXEL";
            BackColor = Color.Black;
            ClientSize = new Size(1280, 720);
            MinimumSize = new Size(800, 480);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            try { Icon = new Icon(new MemoryStream(Program.Resource("icon.ico"))); } catch (Exception) { }

            _renderer = new SoftRenderer(Program.Fonts());
            _audio = new WaveOutAudio();
            _app = new GameApp(_renderer, _audio, new FileSaveStore(Program.DataDir), this, Program.LevelsText());
            _app.Updater = new GitHubUpdater(Quit);
            _app.Updater.Check();   // silent; works offline too (just fails quietly)
        }

        // ---------------------------------------------------------------- IPlatform

        string IPlatform.Name { get { return "Windows"; } }
        public bool IsTouch { get { return false; } }
        public bool SupportsQuit { get { return true; } }
        public bool SupportsFullscreen { get { return true; } }
        public bool SupportsQuality { get { return true; } }
        public void Quit() { _app.SaveNow(); _quit = true; }

        public void ApplySettings(Settings s)
        {
            _settings = s;
            if (s.Fullscreen != _fullscreen) SetFullscreen(s.Fullscreen);
        }

        void SetFullscreen(bool on)
        {
            if (on == _fullscreen) return;
            _fullscreen = on;
            if (on)
            {
                _windowedBounds = Bounds;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Normal;
                Bounds = System.Windows.Forms.Screen.FromControl(this).Bounds;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                Bounds = _windowedBounds.Width > 0 ? _windowedBounds : new Rectangle(100, 100, 1296, 759);
            }
        }

        // ---------------------------------------------------------------- input

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // keep arrows / tab / enter for the game instead of WinForms focus handling
            return false;
        }

        protected override bool IsInputKey(Keys keyData) { return true; }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            bool repeat = _held.Contains(e.KeyCode);
            _held.Add(e.KeyCode);
            e.Handled = true; e.SuppressKeyPress = true;
            if ((e.KeyCode == Keys.Enter && e.Alt) || e.KeyCode == Keys.F11)
            {
                if (!repeat) { _app.Save.Settings.Fullscreen = !_app.Save.Settings.Fullscreen; _app.ApplySettings(); }
                return;
            }
            switch (e.KeyCode)
            {
                case Keys.Up: case Keys.W: _input.Up = true; break;
                case Keys.Down: case Keys.S: _input.Down = true; break;
                case Keys.Left: case Keys.A: _input.Left = true; break;
                case Keys.Right: case Keys.D: _input.Right = true; break;
            }
            if (repeat) return;
            switch (e.KeyCode)
            {
                case Keys.Space: _input.Action = true; _input.Confirm = true; break;
                case Keys.Enter: _input.Action = true; _input.Confirm = true; break;
                case Keys.Up: case Keys.W: case Keys.Z: case Keys.X: case Keys.J: case Keys.K: _input.Action = true; break;
                case Keys.Escape: _input.Back = true; _input.Pause = true; break;
                case Keys.P: _input.Pause = true; break;
                case Keys.Back: _input.Back = true; break;
                case Keys.R: _input.Restart = true; break;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e) { _held.Remove(e.KeyCode); e.Handled = true; }
        protected override void OnDeactivate(EventArgs e) { _held.Clear(); base.OnDeactivate(e); }

        void Pointer(MouseEventArgs e)
        {
            var cs = ClientSize;
            if (cs.Width <= 0 || cs.Height <= 0) return;
            float vh = 1080f, vw = vh * cs.Width / cs.Height;
            _input.PointerX = e.X * vw / cs.Width; _input.PointerY = e.Y * vh / cs.Height;
        }

        protected override void OnMouseMove(MouseEventArgs e) { Pointer(e); _input.PointerMoved = true; }
        protected override void OnMouseDown(MouseEventArgs e) { Pointer(e); _input.PointerPressed = true; _input.PointerDown = true; }
        protected override void OnMouseUp(MouseEventArgs e) { Pointer(e); _input.PointerReleased = true; _input.PointerDown = false; }
        protected override void OnMouseWheel(MouseEventArgs e) { _input.Scroll += e.Delta / 120f; }

        protected override void OnFormClosing(FormClosingEventArgs e) { _app.SaveNow(); _quit = true; base.OnFormClosing(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        // ---------------------------------------------------------------- loop

        public void RunLoop()
        {
            if (_windows) try { timeBeginPeriod(1); } catch (Exception) { }
            _app.ApplySettings();
            var sw = Stopwatch.StartNew();
            double last = sw.Elapsed.TotalSeconds;
            while (!_quit && !IsDisposed)
            {
                Application.DoEvents();
                if (_quit || IsDisposed) break;
                if (WindowState == FormWindowState.Minimized) { Thread.Sleep(50); last = sw.Elapsed.TotalSeconds; continue; }
                double start = sw.Elapsed.TotalSeconds;
                float dt = (float)(start - last); last = start;
                Frame(dt);
                double target = _settings.FpsCap == 0 ? 1 / 60.0 : _settings.FpsCap == 1 ? 1 / 120.0 : 0;
                while (sw.Elapsed.TotalSeconds - start < target - 0.0012) Thread.Sleep(1);
                while (sw.Elapsed.TotalSeconds - start < target) Thread.SpinWait(50);
            }
            _audio.Dispose();
            if (_windows) try { timeEndPeriod(1); } catch (Exception) { }
            if (!IsDisposed) Close();
        }

        void Frame(float dt)
        {
            var cs = ClientSize;
            if (cs.Width < 16 || cs.Height < 16) return;
            float f = _settings.Quality == 0 ? 0.5f : _settings.Quality == 1 ? 0.75f : 1f;
            int ph = Math.Max(360, (int)(cs.Height * f)), pw = Math.Max(640, (int)(ph * (float)cs.Width / cs.Height));
            if (ph > cs.Height) { ph = cs.Height; pw = cs.Width; }
            _renderer.Resize(pw, ph);

            _renderer.BeginFrame();
            _app.Frame(dt, _input);
            _input.ClearFrame();
            _renderer.EndFrame();
            Present();

            _fpsTimer += dt; _fpsFrames++;
            if (_fpsTimer >= 1) { Fps = _fpsFrames / _fpsTimer; _fpsTimer = 0; _fpsFrames = 0; }
            AutoQuality(dt);
        }

        // If the PC is slow, step the graphics quality down once during the first seconds of play.
        float _autoTime, _autoSum; int _autoFrames; bool _autoDone;
        void AutoQuality(float dt)
        {
            if (_autoDone) return;
            _autoTime += dt;
            if (_autoTime < 1.5f) return;              // skip start-up hitches
            _autoSum += dt; _autoFrames++;
            if (_autoTime < 4.5f) return;
            _autoDone = true;
            float avg = _autoSum / Math.Max(1, _autoFrames);
            if (avg > 1f / 40f && _app.Save.Settings.Quality > 0)
            {
                _app.Save.Settings.Quality--;
                _app.ApplySettings();
            }
        }

        void Present()
        {
            var cs = ClientSize;
            using (var g = CreateGraphics())
            {
                if (_windows)
                {
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        var bmi = new BITMAPINFOHEADER
                        {
                            biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)), biWidth = _renderer.PixelW, biHeight = -_renderer.PixelH,
                            biPlanes = 1, biBitCount = 32, biCompression = 0
                        };
                        var h = GCHandle.Alloc(_renderer.Buffer, GCHandleType.Pinned);
                        try
                        {
                            if (_renderer.PixelW == cs.Width && _renderer.PixelH == cs.Height)
                                SetDIBitsToDevice(hdc, 0, 0, (uint)cs.Width, (uint)cs.Height, 0, 0, 0, (uint)_renderer.PixelH, h.AddrOfPinnedObject(), ref bmi, 0);
                            else
                            {
                                SetStretchBltMode(hdc, _settings.Quality == 0 ? 3 : 4); // COLORONCOLOR or HALFTONE
                                SetBrushOrgEx(hdc, 0, 0, IntPtr.Zero);
                                StretchDIBits(hdc, 0, 0, cs.Width, cs.Height, 0, 0, _renderer.PixelW, _renderer.PixelH, h.AddrOfPinnedObject(), ref bmi, 0, 0x00CC0020);
                            }
                        }
                        finally { h.Free(); }
                    }
                    finally { g.ReleaseHdc(hdc); }
                }
                else
                {
                    using (var bmp = _renderer.ToBitmap())
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                        g.DrawImage(bmp, 0, 0, cs.Width, cs.Height);
                    }
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
        }

        [DllImport("gdi32.dll")] static extern int SetDIBitsToDevice(IntPtr hdc, int x, int y, uint w, uint h, int sx, int sy, uint start, uint lines, IntPtr bits, ref BITMAPINFOHEADER bmi, uint usage);
        [DllImport("gdi32.dll")] static extern int StretchDIBits(IntPtr hdc, int x, int y, int w, int h, int sx, int sy, int sw, int sh, IntPtr bits, ref BITMAPINFOHEADER bmi, uint usage, uint rop);
        [DllImport("gdi32.dll")] static extern int SetStretchBltMode(IntPtr hdc, int mode);
        [DllImport("gdi32.dll")] static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr prev);
        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint p);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint p);
    }
}
