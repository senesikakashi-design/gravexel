namespace Gravexel.Core
{
    public enum Blend : byte { Alpha, Add }
    public enum Align : byte { Left, Center, Right }
    public enum FontId : byte { Title, Ui }

    /// <summary>One sprite draw. Coordinates are virtual pixels (y down); X/Y is the sprite centre.</summary>
    public struct DrawCmd
    {
        public int Tex;
        public float X, Y, W, H, Rot;
        public Col Color;
        public Blend Blend;
    }

    /// <summary>
    /// Everything the game needs from a graphics backend. The virtual screen is always
    /// 1080 units tall; its width follows the window aspect ratio.
    /// Implemented by the Windows software renderer and by the Unity sprite renderer.
    /// </summary>
    public interface IRenderer
    {
        float ViewW { get; }
        float ViewH { get; }
        void RegisterTexture(int id, PixelImage image);
        void Background(Col top, Col bottom);
        void Sprite(int tex, float cx, float cy, float w, float h, float rot, Col color, Blend blend);
        /// <summary>Draws text; y is the vertical centre of capital letters.</summary>
        void Text(string text, float x, float y, float size, Col color, Align align, FontId font);
        float MeasureText(string text, float size, FontId font);
        /// <summary>Static layers hold level geometry that never changes, drawn once and scrolled.</summary>
        void BuildStaticLayer(int layerId, DrawCmd[] cmds, int count, float width, float height);
        void DrawStaticLayer(int layerId, float x, float y);
        void Vignette(float strength);
    }

    public interface IAudio
    {
        void RegisterClip(int id, float[] monoSamples, int sampleRate);
        void Play(int id, float volume, float pitch);
        void PlayMusic(int id, float volume);
        void SetMusicVolume(float volume);
    }

    public interface ISaveStore
    {
        string Load();
        void Save(string data);
    }

    public interface IPlatform
    {
        string Name { get; }
        bool IsTouch { get; }
        bool SupportsQuit { get; }
        bool SupportsFullscreen { get; }
        bool SupportsQuality { get; }
        void Quit();
        void ApplySettings(Settings s);
    }

    /// <summary>Input snapshot for one frame, filled by the host. Pointer is in virtual pixels.</summary>
    public sealed class InputState
    {
        public float PointerX, PointerY;
        public bool PointerDown, PointerPressed, PointerReleased, PointerMoved;
        /// <summary>The one-button action: Space, W, Up, Z, X, Enter (also a click/tap in game).</summary>
        public bool Action;
        public bool Confirm, Back, Pause, Restart, Up, Down, Left, Right;
        public float Scroll;

        public void ClearFrame()
        {
            PointerPressed = PointerReleased = PointerMoved = false;
            Action = Confirm = Back = Pause = Restart = Up = Down = Left = Right = false;
            Scroll = 0;
        }
    }
}

namespace Gravexel.Core
{
    public enum UpdateState { Idle, Checking, UpToDate, Available, Downloading, Installing, Failed }

    public sealed class UpdateInfo
    {
        public string Version = "", Notes = "", PageUrl = "", DownloadUrl = "";
    }

    /// <summary>
    /// Optional update service (GitHub Releases). All calls return immediately; the screens poll State.
    /// The game works fully offline - a failed check is silent.
    /// </summary>
    public interface IUpdater
    {
        UpdateState State { get; }
        UpdateInfo Latest { get; }
        float Progress { get; }
        string Error { get; }
        /// <summary>True when the update can be downloaded and installed from inside the game (Windows).</summary>
        bool CanInstallInApp { get; }
        void Check();
        void Install();
    }

    public static class VersionUtil
    {
        /// <summary>Compares dotted versions like "v1.2.0" and "1.1.0".</summary>
        public static bool IsNewer(string remote, string local)
        {
            int[] a = Parse(remote), b = Parse(local);
            for (int i = 0; i < 4; i++) if (a[i] != b[i]) return a[i] > b[i];
            return false;
        }

        static int[] Parse(string v)
        {
            var r = new int[4];
            if (string.IsNullOrEmpty(v)) return r;
            v = v.Trim().TrimStart('v', 'V');
            var parts = v.Split('.', '-', '+');
            for (int i = 0; i < 4 && i < parts.Length; i++) { int n; if (int.TryParse(parts[i], out n)) r[i] = n; }
            return r;
        }
    }
}
