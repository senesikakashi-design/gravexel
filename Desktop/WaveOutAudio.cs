using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Gravexel.Core;

namespace Gravexel.Desktop
{
    /// <summary>
    /// Small real-time mixer on top of the Windows waveOut API (built into every Windows version).
    /// Mixes any number of sound effects plus one looping music track. Silent if no audio device exists.
    /// </summary>
    public sealed class WaveOutAudio : IAudio, IDisposable
    {
        const int Rate = 44100, BufferFrames = 1024, BufferCount = 4;

        sealed class Clip { public float[] Data; public double Step; }
        struct Voice { public Clip Clip; public double Pos, Step; public float Vol; }

        readonly Dictionary<int, Clip> _clips = new Dictionary<int, Clip>();
        readonly List<Voice> _voices = new List<Voice>();
        readonly object _lock = new object();
        Clip _music; double _musicPos; float _musicFade = 1f; Clip _nextMusic;
        float _musicVolumeSetting = 0.7f;

        IntPtr _wave = IntPtr.Zero;
        readonly IntPtr[] _headers = new IntPtr[BufferCount];
        readonly IntPtr[] _data = new IntPtr[BufferCount];
        readonly short[] _pcm = new short[BufferFrames];
        readonly float[] _mix = new float[BufferFrames];
        Thread _thread; volatile bool _running;
        int _flagsOffset, _headerSize;

        public bool Available { get { return _wave != IntPtr.Zero; } }

        public WaveOutAudio()
        {
            try { Open(); }
            catch (Exception) { _wave = IntPtr.Zero; }
        }

        void Open()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            var fmt = new WAVEFORMATEX { wFormatTag = 1, nChannels = 1, nSamplesPerSec = Rate, wBitsPerSample = 16, nBlockAlign = 2, nAvgBytesPerSec = Rate * 2, cbSize = 0 };
            if (waveOutOpen(out _wave, WAVE_MAPPER, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) != 0) { _wave = IntPtr.Zero; return; }
            _headerSize = Marshal.SizeOf(typeof(WAVEHDR));
            _flagsOffset = (int)Marshal.OffsetOf(typeof(WAVEHDR), "dwFlags");
            for (int i = 0; i < BufferCount; i++)
            {
                _data[i] = Marshal.AllocHGlobal(BufferFrames * 2);
                var hdr = new WAVEHDR { lpData = _data[i], dwBufferLength = (uint)(BufferFrames * 2) };
                _headers[i] = Marshal.AllocHGlobal(_headerSize);
                Marshal.StructureToPtr(hdr, _headers[i], false);
                waveOutPrepareHeader(_wave, _headers[i], (uint)_headerSize);
            }
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = "Gravexel Audio" };
            _thread.Start();
        }

        void Loop()
        {
            for (int i = 0; i < BufferCount; i++) Submit(i);
            int next = 0;
            while (_running)
            {
                int flags = Marshal.ReadInt32(_headers[next], _flagsOffset);
                if ((flags & WHDR_DONE) != 0) { Submit(next); next = (next + 1) % BufferCount; }
                else Thread.Sleep(2);
            }
        }

        void Submit(int i)
        {
            Fill();
            Marshal.Copy(_pcm, 0, _data[i], BufferFrames);
            waveOutWrite(_wave, _headers[i], (uint)_headerSize);
        }

        void Fill()
        {
            Array.Clear(_mix, 0, _mix.Length);
            lock (_lock)
            {
                // music with a short crossfade when the track changes
                if (_nextMusic != null)
                {
                    _musicFade -= BufferFrames / (float)Rate * 3f;
                    if (_musicFade <= 0 || _music == null) { _music = _nextMusic; _nextMusic = null; _musicPos = 0; _musicFade = 0; }
                }
                else if (_musicFade < 1) _musicFade = Math.Min(1, _musicFade + BufferFrames / (float)Rate * 2f);
                if (_music != null)
                {
                    var d = _music.Data; float v = _musicVolumeSetting * _musicFade * 0.8f;
                    if (v > 0.0001f)
                        for (int n = 0; n < BufferFrames; n++)
                        {
                            int p = (int)_musicPos; double f = _musicPos - p;
                            float s = (float)(d[p] + (d[(p + 1) % d.Length] - d[p]) * f);
                            _mix[n] += s * v;
                            _musicPos += _music.Step; if (_musicPos >= d.Length) _musicPos -= d.Length;
                        }
                    else { _musicPos = (_musicPos + _music.Step * BufferFrames) % d.Length; }
                }
                for (int i = _voices.Count - 1; i >= 0; i--)
                {
                    var vc = _voices[i]; var d = vc.Clip.Data; bool done = false;
                    for (int n = 0; n < BufferFrames; n++)
                    {
                        int p = (int)vc.Pos;
                        if (p >= d.Length - 1) { done = true; break; }
                        double f = vc.Pos - p;
                        _mix[n] += (float)(d[p] + (d[p + 1] - d[p]) * f) * vc.Vol;
                        vc.Pos += vc.Step;
                    }
                    if (done) _voices.RemoveAt(i); else _voices[i] = vc;
                }
            }
            for (int n = 0; n < BufferFrames; n++)
            {
                float s = _mix[n];
                if (s > 1f || s < -1f) s = (float)Math.Tanh(s); // soft limiter
                _pcm[n] = (short)(s * 32000);
            }
        }

        // ---------------------------------------------------------------- IAudio

        public void RegisterClip(int id, float[] samples, int sampleRate)
        {
            lock (_lock) _clips[id] = new Clip { Data = samples, Step = sampleRate / (double)Rate };
        }

        public void Play(int id, float volume, float pitch)
        {
            if (!Available) return;
            lock (_lock)
            {
                Clip c; if (!_clips.TryGetValue(id, out c)) return;
                if (_voices.Count > 40) _voices.RemoveAt(0);
                _voices.Add(new Voice { Clip = c, Pos = 0, Step = c.Step * pitch, Vol = volume });
            }
        }

        public void PlayMusic(int id, float volume)
        {
            lock (_lock)
            {
                Clip c; if (!_clips.TryGetValue(id, out c)) return;
                _musicVolumeSetting = volume;
                if (_music == null) { _music = c; _musicPos = 0; _musicFade = 0; }
                else if (_music != c) _nextMusic = c;
            }
        }

        public void SetMusicVolume(float volume) { lock (_lock) _musicVolumeSetting = volume; }

        public void Dispose()
        {
            _running = false;
            if (_thread != null) _thread.Join(200);
            if (_wave != IntPtr.Zero)
            {
                waveOutReset(_wave);
                for (int i = 0; i < BufferCount; i++)
                {
                    waveOutUnprepareHeader(_wave, _headers[i], (uint)_headerSize);
                    Marshal.FreeHGlobal(_headers[i]); Marshal.FreeHGlobal(_data[i]);
                }
                waveOutClose(_wave);
                _wave = IntPtr.Zero;
            }
        }

        // ---------------------------------------------------------------- winmm

        const uint WAVE_MAPPER = 0xFFFFFFFF;
        const int WHDR_DONE = 1;

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }

        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr hwo, uint uDeviceID, ref WAVEFORMATEX fmt, IntPtr cb, IntPtr inst, uint flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr hwo, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr hwo);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr hwo);
    }
}
