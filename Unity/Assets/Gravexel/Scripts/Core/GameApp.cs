using System;
using System.Collections.Generic;
using System.Threading;

namespace Gravexel.Core
{
    public abstract class Screen
    {
        public GameApp App;
        /// <summary>Overlays let the screen underneath stay visible.</summary>
        public virtual bool IsOverlay { get { return false; } }
        public virtual void Enter() { }
        public abstract void Update(float dt);
        /// <summary>Draws the screen; widgets are only created when 'interactive' (top of the stack).</summary>
        public abstract void Draw(bool interactive);
    }

    /// <summary>
    /// The whole game, independent of any engine. A host (Unity or the Windows build) creates it with
    /// its renderer, audio, save store and platform, then calls Frame() once per rendered frame.
    /// </summary>
    public sealed class GameApp
    {
        public const string Version = "1.1.0";
        public const string ProductName = "GRAVEXEL";
        public const string Tagline = "DEFY THE GRAVITY";
        public const string Slogan = "A NEW DIMENSION OF MOVEMENT";

        public readonly IRenderer R;
        public readonly IAudio Audio;
        public readonly IPlatform Platform;
        readonly ISaveStore _store;
        public readonly SaveData Save;
        public readonly List<LevelInfo> Levels;
        public InputState In = new InputState();
        public readonly Ui Ui;
        public readonly Backdrop Backdrop = new Backdrop();
        public float Time;
        public int Worlds { get; private set; }
        /// <summary>Optional: set by hosts that can check GitHub for new versions.</summary>
        public IUpdater Updater { get; set; }

        readonly List<Screen> _stack = new List<Screen>();
        Screen _pending; float _fade = 1f; int _fadeDir = -1;

        int _musicWanted = -1, _musicPlaying = -1;
        readonly HashSet<int> _musicReady = new HashSet<int>(), _musicGenerating = new HashSet<int>();
        readonly Queue<KeyValuePair<int, float[]>> _musicDone = new Queue<KeyValuePair<int, float[]>>();
        readonly InputState _blocked = new InputState();
        float _hoverCooldown;

        public GameApp(IRenderer renderer, IAudio audio, ISaveStore store, IPlatform platform, string levelsText)
        {
            R = renderer; Audio = audio; _store = store; Platform = platform;
            TextureGen.RegisterAll(R);
            AudioGen.RegisterSfx(Audio);
            Save = SaveData.Deserialize(SafeLoad());
            Levels = LevelFile.Parse(levelsText);
            foreach (var l in Levels) Worlds = Math.Max(Worlds, l.World);
            Ui = new Ui(this);
            Platform.ApplySettings(Save.Settings);
            Audio.SetMusicVolume(Save.Settings.MusicVolume);
            _stack.Add(Attach(new SplashScreen()));
            _stack[0].Enter();
            PrepareMusic(Sfx.MusicMenu);
            for (int w = 1; w <= Math.Min(Worlds, 6); w++) PrepareMusic(Sfx.MusicWorld1 + w - 1);
            PlayMusic(Sfx.MusicMenu);
        }

        string SafeLoad() { try { return _store.Load(); } catch (Exception) { return null; } }

        public void SaveNow()
        {
            try { _store.Save(Save.Serialize()); } catch (Exception) { /* never crash the game on a save error */ }
        }

        Screen Attach(Screen s) { s.App = this; return s; }

        public LevelInfo Level(int number)
        {
            foreach (var l in Levels) if (l.Number == number) return l;
            return null;
        }

        public List<LevelInfo> LevelsInWorld(int world)
        {
            var list = new List<LevelInfo>();
            foreach (var l in Levels) if (l.World == world) list.Add(l);
            return list;
        }

        // ---------------- screens ----------------

        /// <summary>Fade out, replace the whole stack with this screen, fade in.</summary>
        public void GoTo(Screen s) { if (_pending != null) return; _pending = Attach(s); _fadeDir = 1; }
        public void Push(Screen s) { _stack.Add(Attach(s)); s.Enter(); }
        public void Pop() { if (_stack.Count > 1) _stack.RemoveAt(_stack.Count - 1); }
        public bool Transitioning { get { return _pending != null; } }

        // ---------------- audio ----------------

        public void PlaySfx(int id, float vol = 1f, float pitch = 1f)
        {
            if (id == Sfx.Hover) { if (_hoverCooldown > 0) return; _hoverCooldown = 0.05f; }
            float v = vol * Save.Settings.SfxVolume;
            if (v > 0.001f) Audio.Play(id, v, pitch);
        }

        void PrepareMusic(int id)
        {
            if (_musicReady.Contains(id) || _musicGenerating.Contains(id)) return;
            _musicGenerating.Add(id);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                float[] data = null;
                try { data = AudioGen.Music(id); } catch (Exception) { }
                lock (_musicDone) _musicDone.Enqueue(new KeyValuePair<int, float[]>(id, data));
            });
        }

        public void PlayMusic(int id)
        {
            _musicWanted = id;
            PrepareMusic(id);
            TryStartMusic();
        }

        void TryStartMusic()
        {
            if (_musicWanted < 0 || _musicPlaying == _musicWanted || !_musicReady.Contains(_musicWanted)) return;
            Audio.PlayMusic(_musicWanted, Save.Settings.MusicVolume);
            _musicPlaying = _musicWanted;
        }

        public void ApplySettings()
        {
            Audio.SetMusicVolume(Save.Settings.MusicVolume);
            Platform.ApplySettings(Save.Settings);
            SaveNow();
        }

        // ---------------- frame ----------------

        public void Frame(float dt, InputState input)
        {
            dt = Math.Min(dt, 0.1f);
            Time += dt;
            _hoverCooldown -= dt;
            lock (_musicDone)
                while (_musicDone.Count > 0)
                {
                    var kv = _musicDone.Dequeue();
                    _musicGenerating.Remove(kv.Key);
                    if (kv.Value != null) { Audio.RegisterClip(kv.Key, kv.Value, Sfx.MusicRate); _musicReady.Add(kv.Key); }
                }
            TryStartMusic();

            // fade transition
            if (_fadeDir != 0)
            {
                _fade += _fadeDir * dt * 4.2f;
                if (_fadeDir > 0 && _fade >= 1f)
                {
                    _fade = 1f; _fadeDir = -1;
                    if (_pending != null) { _stack.Clear(); _stack.Add(_pending); _pending.Enter(); _pending = null; }
                }
                else if (_fadeDir < 0 && _fade <= 0f) { _fade = 0f; _fadeDir = 0; }
            }

            _blocked.PointerX = input.PointerX; _blocked.PointerY = input.PointerY;
            In = _pending != null ? _blocked : input;

            Ui.Begin(dt);
            var top = _stack[_stack.Count - 1];
            top.Update(dt);

            int first = _stack.Count - 1;
            while (first > 0 && _stack[first].IsOverlay) first--;
            for (int i = first; i < _stack.Count; i++) _stack[i].Draw(i == _stack.Count - 1);

            if (_fade > 0.001f) R.Sprite(Tx.Pixel, R.ViewW / 2, R.ViewH / 2, R.ViewW + 8, R.ViewH + 8, 0, new Col(0.01f, 0.01f, 0.03f, Mathx.SmoothStep(_fade)), Blend.Alpha);
            In = input;
        }
    }
}
