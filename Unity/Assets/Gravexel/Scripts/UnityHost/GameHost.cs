using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using GS = Gravexel.Core;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Gravexel.UnityHost
{
    /// <summary>
    /// Boots the game automatically in any scene: creates the camera and the host object,
    /// then runs the shared Gravexel.Core game every frame.
    /// </summary>
    public sealed class GameHost : MonoBehaviour, GS.IPlatform
    {
        GS.GameApp _app;
        UnityRenderer _renderer;
        UnityAudio _audio;
        readonly GS.InputState _input = new GS.InputState();
        Vector2 _lastPointer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_2023_1_OR_NEWER
            if (FindFirstObjectByType<GameHost>() != null) return;
#else
            if (FindObjectOfType<GameHost>() != null) return;
#endif
            var go = new GameObject("Gravexel");
            DontDestroyOnLoad(go);
            go.AddComponent<GameHost>();
        }

        void Awake()
        {
            var cam = Camera.main;
            if (cam == null) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            cam.orthographic = true;
            cam.orthographicSize = 540f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 100f;

            var root = new GameObject("Render").transform;
            root.SetParent(transform, false);
            _renderer = new UnityRenderer(root);
            _audio = new UnityAudio(gameObject);
            var levels = Resources.Load<TextAsset>("levels");
            _app = new GS.GameApp(_renderer, _audio, new PlayerPrefsStore(), this, levels.text);
            _app.Updater = new UnityUpdater(this);
            _app.Updater.Check();
        }

        void Update()
        {
            ReadInput();
            _renderer.BeginFrame();
            _app.Frame(Time.unscaledDeltaTime, _input);
            _renderer.EndFrame();
            _input.ClearFrame();
        }

        void OnApplicationPause(bool paused) { if (paused && _app != null) _app.SaveNow(); }
        void OnApplicationQuit() { if (_app != null) _app.SaveNow(); }

        // ---------------------------------------------------------------- input

        void Pointer(Vector2 screen, bool down)
        {
            float vw = _renderer.ViewW;
            _input.PointerX = screen.x / Mathf.Max(1, Screen.width) * vw;
            _input.PointerY = (1f - screen.y / Mathf.Max(1, Screen.height)) * 1080f;
            if ((screen - _lastPointer).sqrMagnitude > 0.5f) _input.PointerMoved = true;
            _lastPointer = screen;
            if (down && !_input.PointerDown) _input.PointerPressed = true;
            if (!down && _input.PointerDown) _input.PointerReleased = true;
            _input.PointerDown = down;
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        void ReadInput()
        {
            var k = Keyboard.current;
            if (k != null)
            {
                _input.Up |= k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame;
                _input.Down |= k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame;
                _input.Left |= k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame;
                _input.Right |= k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame;
                bool confirm = k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame;
                _input.Confirm |= confirm;
                _input.Action |= confirm || k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.zKey.wasPressedThisFrame || k.xKey.wasPressedThisFrame;
                _input.Back |= k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame;
                _input.Pause |= k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame;
                _input.Restart |= k.rKey.wasPressedThisFrame;
                if (k.f11Key.wasPressedThisFrame) ToggleFullscreen();
            }
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed) { Pointer(ts.primaryTouch.position.ReadValue(), true); return; }
            if (ts != null && ts.primaryTouch.press.wasReleasedThisFrame) { Pointer(ts.primaryTouch.position.ReadValue(), false); return; }
            var m = Mouse.current;
            if (m != null) Pointer(m.position.ReadValue(), m.leftButton.isPressed || m.rightButton.isPressed);
        }
#else
        void ReadInput()
        {
            _input.Up |= Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            _input.Down |= Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            _input.Left |= Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            _input.Right |= Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
            bool confirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            _input.Confirm |= confirm;
            _input.Action |= confirm || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.X);
            _input.Back |= Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);
            _input.Pause |= Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
            _input.Restart |= Input.GetKeyDown(KeyCode.R);
            if (Input.GetKeyDown(KeyCode.F11)) ToggleFullscreen();
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                Pointer(t.position, t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled);
                return;
            }
            Pointer(Input.mousePosition, Input.GetMouseButton(0) || Input.GetMouseButton(1));
        }
#endif

        void ToggleFullscreen()
        {
            _app.Save.Settings.Fullscreen = !_app.Save.Settings.Fullscreen;
            _app.ApplySettings();
        }

        // ---------------------------------------------------------------- IPlatform

        public string Name { get { return Application.platform.ToString(); } }
        public bool IsTouch { get { return Application.isMobilePlatform; } }
        public bool SupportsQuit { get { return Application.platform != RuntimePlatform.IPhonePlayer && Application.platform != RuntimePlatform.WebGLPlayer; } }
        public bool SupportsFullscreen { get { return !Application.isMobilePlatform; } }
        public bool SupportsQuality { get { return false; } }
        public void Quit() { Application.Quit(); }

        public void ApplySettings(GS.Settings s)
        {
            if (Application.isMobilePlatform) { Application.targetFrameRate = 60; return; }
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            if (Screen.fullScreen != s.Fullscreen)
            {
                if (s.Fullscreen) Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
                else Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            }
        }
    }

    public sealed class PlayerPrefsStore : GS.ISaveStore
    {
        const string Key = "gravexel_save";
        public string Load() { return PlayerPrefs.GetString(Key, ""); }
        public void Save(string data) { PlayerPrefs.SetString(Key, data); PlayerPrefs.Save(); }
    }

    /// <summary>Plays generated clips through a pool of AudioSources; music crossfades between two sources.</summary>
    public sealed class UnityAudio : GS.IAudio
    {
        readonly Dictionary<int, AudioClip> _clips = new Dictionary<int, AudioClip>();
        readonly AudioSource[] _sfx = new AudioSource[16];
        readonly AudioSource[] _music = new AudioSource[2];
        int _next, _active;
        float _musicVolume = 0.7f;

        public UnityAudio(GameObject host)
        {
            for (int i = 0; i < _sfx.Length; i++) { _sfx[i] = host.AddComponent<AudioSource>(); _sfx[i].playOnAwake = false; }
            for (int i = 0; i < 2; i++) { _music[i] = host.AddComponent<AudioSource>(); _music[i].loop = true; _music[i].playOnAwake = false; }
        }

        public void RegisterClip(int id, float[] samples, int rate)
        {
            var c = AudioClip.Create("gs" + id, samples.Length, 1, rate, false);
            c.SetData(samples, 0);
            _clips[id] = c;
        }

        public void Play(int id, float volume, float pitch)
        {
            AudioClip c; if (!_clips.TryGetValue(id, out c)) return;
            var src = _sfx[_next]; _next = (_next + 1) % _sfx.Length;
            src.clip = c; src.volume = volume; src.pitch = pitch; src.Play();
        }

        public void PlayMusic(int id, float volume)
        {
            AudioClip c; if (!_clips.TryGetValue(id, out c)) return;
            _musicVolume = volume;
            if (_music[_active].clip == c && _music[_active].isPlaying) return;
            _music[_active].Stop();
            _active = 1 - _active;
            _music[_active].clip = c; _music[_active].volume = volume * 0.8f; _music[_active].Play();
        }

        public void SetMusicVolume(float volume) { _musicVolume = volume; _music[_active].volume = volume * 0.8f; }
    }

    /// <summary>
    /// Checks GitHub Releases for a newer version. On every platform "Install" opens the release page
    /// (on mobile, point UnityUpdater.PageOverride at the store listing once the app is published).
    /// </summary>
    public sealed class UnityUpdater : GS.IUpdater
    {
        public const string Repo = "senesikakashi-design/gravexel";
        public static string PageOverride = "";

        [System.Serializable] class Release { public string tag_name = "", body = "", html_url = ""; }

        readonly MonoBehaviour _host;
        GS.UpdateState _state = GS.UpdateState.Idle;
        GS.UpdateInfo _latest = new GS.UpdateInfo();
        string _error = "";

        public UnityUpdater(MonoBehaviour host) { _host = host; }

        public GS.UpdateState State { get { return _state; } }
        public GS.UpdateInfo Latest { get { return _latest; } }
        public float Progress { get { return 0; } }
        public string Error { get { return _error; } }
        public bool CanInstallInApp { get { return false; } }

        public void Check()
        {
            if (_state == GS.UpdateState.Checking) return;
            _state = GS.UpdateState.Checking;
            _host.StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            using (var req = UnityWebRequest.Get("https://api.github.com/repos/" + Repo + "/releases/latest"))
            {
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { _error = req.error; _state = GS.UpdateState.Failed; yield break; }
                var rel = JsonUtility.FromJson<Release>(req.downloadHandler.text);
                _latest = new GS.UpdateInfo { Version = rel.tag_name, Notes = rel.body, PageUrl = string.IsNullOrEmpty(PageOverride) ? rel.html_url : PageOverride };
                _state = GS.VersionUtil.IsNewer(rel.tag_name, GS.GameApp.Version) ? GS.UpdateState.Available : GS.UpdateState.UpToDate;
            }
        }

        public void Install() { if (!string.IsNullOrEmpty(_latest.PageUrl)) Application.OpenURL(_latest.PageUrl); }
    }
}
