using System;

namespace Gravexel.Core
{
    public static class Sfx
    {
        public const int Flip = 1, Land = 2, Orb = 3, Star = 4, Death = 5, Checkpoint = 6, Portal = 7, Key = 8, Gate = 9,
            Switch = 10, Turn = 11, Hover = 12, Click = 13, Back = 14, Win = 15, Unlock = 16, Start = 17, Respawn = 18;
        public const int MusicMenu = 100, MusicWorld1 = 101; // worlds 1..6 => 101..106
        public const int SfxRate = 44100, MusicRate = 32000;
    }

    /// <summary>Synthesises every sound effect and music loop in code (no audio files needed).</summary>
    public static class AudioGen
    {
        const float TAU = 6.2831853f;

        public static void RegisterSfx(IAudio a)
        {
            int sr = Sfx.SfxRate;
            a.RegisterClip(Sfx.Flip, Flip(sr), sr);
            a.RegisterClip(Sfx.Land, Land(sr), sr);
            a.RegisterClip(Sfx.Orb, Orb(sr), sr);
            a.RegisterClip(Sfx.Star, Star(sr), sr);
            a.RegisterClip(Sfx.Death, Death(sr), sr);
            a.RegisterClip(Sfx.Checkpoint, Checkpoint(sr), sr);
            a.RegisterClip(Sfx.Portal, Portal(sr), sr);
            a.RegisterClip(Sfx.Key, Bell(sr, 1760, 0.6f), sr);
            a.RegisterClip(Sfx.Gate, Gate(sr), sr);
            a.RegisterClip(Sfx.Switch, SwitchSnd(sr), sr);
            a.RegisterClip(Sfx.Turn, Blip(sr, 620, 520, 0.06f, 0.25f, 1), sr);
            a.RegisterClip(Sfx.Hover, Blip(sr, 1900, 1900, 0.035f, 0.18f, 0), sr);
            a.RegisterClip(Sfx.Click, Blip(sr, 880, 1400, 0.11f, 0.5f, 1), sr);
            a.RegisterClip(Sfx.Back, Blip(sr, 1100, 640, 0.11f, 0.45f, 1), sr);
            a.RegisterClip(Sfx.Win, Win(sr), sr);
            a.RegisterClip(Sfx.Unlock, Unlock(sr), sr);
            a.RegisterClip(Sfx.Start, StartSnd(sr), sr);
            a.RegisterClip(Sfx.Respawn, Respawn(sr), sr);
        }

        // ---------------- sound effects ----------------

        static float[] Buf(int sr, float sec) { return new float[(int)(sr * sec)]; }

        static float[] Flip(int sr)
        {
            var b = Buf(sr, 0.2f); float ph = 0; var rng = new Rng(3); float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / sr;
                float f = 380 * (float)Math.Pow(2.9, Math.Min(1, t / 0.14f));
                ph += TAU * f / sr;
                float tone = Mathx.Sin(ph) * 0.7f + Tri(ph) * 0.3f;
                float n = rng.Range(-1f, 1f); lp += 0.25f * (n - lp);
                float env = (1 - (float)Math.Exp(-t * 400)) * (float)Math.Exp(-t * 16);
                b[i] = (tone * 0.55f + (n - lp) * 0.35f * (float)Math.Exp(-t * 30)) * env;
            }
            return b;
        }

        static float[] Land(int sr)
        {
            var b = Buf(sr, 0.12f); float ph = 0; var rng = new Rng(5); float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / sr; float f = Mathx.Lerp(150, 50, Math.Min(1, t / 0.08f));
                ph += TAU * f / sr;
                lp += 0.08f * (rng.Range(-1f, 1f) - lp);
                b[i] = (Mathx.Sin(ph) * 0.8f + lp * 0.9f) * (float)Math.Exp(-t * 38);
            }
            return b;
        }

        static float[] Orb(int sr)
        {
            var b = Buf(sr, 0.22f); float p1 = 0, p2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / sr;
                p1 += TAU * 1046 / sr; p2 += TAU * 1568 / sr;
                float fm = Mathx.Sin(p1 * 2) * 0.6f * (float)Math.Exp(-t * 30);
                b[i] = (Mathx.Sin(p1 + fm) * 0.5f + Mathx.Sin(p2) * 0.35f * Mathx.Clamp01(t * 40 - 1.5f)) * (float)Math.Exp(-t * 16);
            }
            return b;
        }

        static float[] Star(int sr)
        {
            var b = Buf(sr, 0.9f);
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(k * 0.065f * sr); float ph = 0;
                for (int i = start; i < b.Length; i++)
                {
                    float t = (float)(i - start) / sr; ph += TAU * notes[k] / sr;
                    b[i] += (Mathx.Sin(ph) * 0.6f + Tri(ph * 2) * 0.15f) * (float)Math.Exp(-t * 6) * 0.45f;
                }
            }
            float sp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / sr; sp += TAU * 4186 / sr;
                b[i] += Mathx.Sin(sp) * 0.08f * (0.5f + 0.5f * Mathx.Sin(t * 60)) * (float)Math.Exp(-t * 4);
            }
            return b;
        }

        static float[] Death(int sr)
        {
            var b = Buf(sr, 0.55f); var rng = new Rng(9); float lp = 0, ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / sr;
                float cut = Mathx.Lerp(0.6f, 0.03f, Math.Min(1, t / 0.4f));
                lp += cut * (rng.Range(-1f, 1f) - lp);
                float f = Mathx.Lerp(320, 40, (float)Math.Sqrt(Math.Min(1, t / 0.45f)));
                ph += TAU * f / sr;
                float sq = Math.Sign(Mathx.Sin(ph)) * 0.35f;
                float v = (lp * 1.1f * (float)Math.Exp(-t * 6) + sq * (float)Math.Exp(-t * 5));
                b[i] = (float)Math.Round(v * 10) / 10f * 0.8f; // bit-crush
            }
            return b;
        }

        static float[] Checkpoint(int sr)
        {
            var b = Buf(sr, 0.45f);
            AddTone(b, sr, 0f, 0.3f, 660, 0.4f, 9, 2);
            AddTone(b, sr, 0.09f, 0.35f, 990, 0.45f, 7, 2);
            return b;
        }

        static float[] Portal(int sr)
        {
            var b = Buf(sr, 0.45f); float p1 = 0, p2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / sr;
                float f = 220 + 600 * Mathx.Sin(Mathx.Clamp01(t / 0.4f) * Mathx.PI) + Mathx.Sin(t * 70) * 30;
                p1 += TAU * f / sr; p2 += TAU * f * 1.503f / sr;
                b[i] = (Mathx.Sin(p1) * 0.45f + Mathx.Sin(p2) * 0.25f) * Mathx.Sin(Mathx.Clamp01(t / 0.45f) * Mathx.PI);
            }
            return b;
        }

        static float[] Bell(int sr, float f, float sec)
        {
            var b = Buf(sr, sec);
            float[] partial = { 1f, 2.76f, 5.4f }; float[] amp = { 0.55f, 0.25f, 0.12f }; float[] dec = { 5, 9, 14 };
            for (int k = 0; k < 3; k++)
            {
                float ph = 0;
                for (int i = 0; i < b.Length; i++) { ph += TAU * f * partial[k] / sr; b[i] += Mathx.Sin(ph) * amp[k] * (float)Math.Exp(-(float)i / sr * dec[k]); }
            }
            return b;
        }

        static float[] Gate(int sr)
        {
            var b = Buf(sr, 0.5f); float ph = 0, lp = 0; var rng = new Rng(4);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / sr; float f = Mathx.Lerp(110, 45, t / 0.5f);
                ph += f / sr; float saw = (ph % 1f) * 2 - 1;
                lp += 0.05f * (saw + rng.Range(-0.4f, 0.4f) - lp);
                b[i] = lp * 1.4f * (float)Math.Exp(-t * 5) * Mathx.Clamp01(t * 80);
            }
            AddTone(b, sr, 0.0f, 0.4f, 523, 0.25f, 8, 0);
            AddTone(b, sr, 0.08f, 0.4f, 784, 0.25f, 8, 0);
            return b;
        }

        static float[] SwitchSnd(int sr)
        {
            var b = Buf(sr, 0.2f); var rng = new Rng(2);
            for (int i = 0; i < sr * 0.006f; i++) b[i] += rng.Range(-1f, 1f) * 0.6f;
            AddTone(b, sr, 0.0f, 0.15f, 440, 0.3f, 25, 1);
            AddTone(b, sr, 0.05f, 0.15f, 880, 0.3f, 25, 1);
            return b;
        }

        static float[] Blip(int sr, float f0, float f1, float sec, float vol, int wave)
        {
            var b = Buf(sr, sec); float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / sr; float k = t / sec;
                ph += TAU * Mathx.Lerp(f0, f1, k) / sr;
                float w = wave == 0 ? Mathx.Sin(ph) : (Mathx.Sin(ph) * 0.7f + Tri(ph) * 0.3f);
                b[i] = w * vol * (1 - k) * Mathx.Clamp01(t * 600);
            }
            return b;
        }

        static float[] Win(int sr)
        {
            var b = Buf(sr, 1.8f);
            float[] arp = { 523.3f, 659.3f, 784f, 1046.5f, 1318.5f, 1568f };
            for (int k = 0; k < arp.Length; k++) AddTone(b, sr, k * 0.075f, 0.5f, arp[k], 0.28f, 6, 2);
            float[] chord = { 523.3f, 659.3f, 784f, 1046.5f };
            foreach (var f in chord) AddTone(b, sr, 0.5f, 1.3f, f, 0.16f, 2.2f, 2);
            return b;
        }

        static float[] Unlock(int sr)
        {
            var b = Buf(sr, 1.0f); var rng = new Rng(17);
            for (int k = 0; k < 9; k++) AddTone(b, sr, k * 0.06f, 0.4f, 900 + k * 180 + rng.Range(0, 120), 0.18f, 8, 0);
            return b;
        }

        static float[] StartSnd(int sr)
        {
            var b = Buf(sr, 0.5f); var rng = new Rng(8); float lp = 0, ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / sr; float k = t / 0.5f;
                lp += Mathx.Lerp(0.02f, 0.4f, k) * (rng.Range(-1f, 1f) - lp);
                ph += TAU * Mathx.Lerp(200, 700, k) / sr;
                b[i] = (lp * 0.5f + Mathx.Sin(ph) * 0.25f) * Mathx.Sin(k * Mathx.PI);
            }
            return b;
        }

        static float[] Respawn(int sr)
        {
            var b = Buf(sr, 0.35f); float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / sr; float k = t / 0.35f;
                ph += TAU * Mathx.Lerp(300, 900, k * k) / sr;
                b[i] = (Mathx.Sin(ph) * 0.4f + Tri(ph * 0.5f) * 0.2f) * Mathx.Sin(k * Mathx.PI);
            }
            return b;
        }

        static void AddTone(float[] b, int sr, float start, float dur, float f, float vol, float decay, int wave)
        {
            int s = (int)(start * sr), n = (int)(dur * sr); float ph = 0;
            for (int i = 0; i < n && s + i < b.Length; i++)
            {
                float t = (float)i / sr; ph += TAU * f / sr;
                float w = wave == 0 ? Mathx.Sin(ph) : wave == 1 ? SoftSquare(ph) : (Mathx.Sin(ph) * 0.6f + Tri(ph) * 0.4f);
                b[s + i] += w * vol * (float)Math.Exp(-t * decay) * Mathx.Clamp01(t * 300) * Mathx.Clamp01((dur - t) * 40);
            }
        }

        static float Tri(float ph) { float x = Mathx.Repeat(ph / TAU, 1f); return 4 * Math.Abs(x - 0.5f) - 1; }
        static float SoftSquare(float ph) { return (float)Math.Tanh(Mathx.Sin(ph) * 3f) * 0.8f; }

        // ---------------- music ----------------

        sealed class Song
        {
            public float Bpm; public int Root; public int[] Chords; public bool[] Minor; public float Drive; public int Seed;
        }

        static Song SongFor(int id)
        {
            switch (id)
            {
                case Sfx.MusicWorld1: return new Song { Bpm = 112, Root = 45, Chords = new[] { 0, 8, 3, 10 }, Minor = new[] { true, false, false, false }, Drive = 0.8f, Seed = 1 };
                case Sfx.MusicWorld1 + 1: return new Song { Bpm = 118, Root = 50, Chords = new[] { 0, 8, 3, 10 }, Minor = new[] { true, false, false, false }, Drive = 0.9f, Seed = 2 };
                case Sfx.MusicWorld1 + 2: return new Song { Bpm = 124, Root = 52, Chords = new[] { 0, 8, 10, 7 }, Minor = new[] { true, false, false, true }, Drive = 1f, Seed = 3 };
                case Sfx.MusicWorld1 + 3: return new Song { Bpm = 128, Root = 41, Chords = new[] { 0, 8, 3, 10 }, Minor = new[] { true, false, false, false }, Drive = 1f, Seed = 4 };
                case Sfx.MusicWorld1 + 4: return new Song { Bpm = 132, Root = 47, Chords = new[] { 0, 5, 8, 10 }, Minor = new[] { true, true, false, false }, Drive = 1f, Seed = 5 };
                case Sfx.MusicWorld1 + 5: return new Song { Bpm = 136, Root = 43, Chords = new[] { 0, 3, 8, 7 }, Minor = new[] { true, false, false, true }, Drive = 1f, Seed = 6 };
                default: return new Song { Bpm = 96, Root = 48, Chords = new[] { 0, 9, 5, 7 }, Minor = new[] { false, true, false, false }, Drive = 0.35f, Seed = 9 };
            }
        }

        static float Midi(float n) { return 440f * (float)Math.Pow(2, (n - 69) / 12f); }

        /// <summary>Renders a seamless 16-bar synthwave loop (safe to call on a worker thread).</summary>
        public static float[] Music(int id)
        {
            var s = SongFor(id);
            int sr = Sfx.MusicRate;
            float beat = 60f / s.Bpm, bar = beat * 4;
            int bars = 16;
            int len = (int)(bar * bars * sr);
            var mix = new float[len];
            var arpBus = new float[len];
            var rng = new Rng(s.Seed);

            for (int b = 0; b < bars; b++)
            {
                int ci = (b / 2) % 4;
                int root = s.Root + s.Chords[ci];
                int[] tones = s.Minor[ci] ? new[] { 0, 3, 7, 10 } : new[] { 0, 4, 7, 11 };
                float barStart = b * bar;
                bool second = b >= 8;

                // pad (chord sustained over two bars)
                if (b % 2 == 0)
                    foreach (int tn in tones) Pad(mix, sr, barStart, bar * 2, Midi(root + 12 + tn), 0.05f);

                for (int k = 0; k < 4; k++)
                {
                    float t = barStart + k * beat;
                    bool full = s.Drive > 0.5f || k % 2 == 0;
                    if (full) Kick(mix, sr, t, 0.55f * Math.Max(0.6f, s.Drive));
                    if (k % 2 == 1 && s.Drive > 0.5f) Snare(mix, sr, t, 0.22f, rng);
                    for (int h = 0; h < 2; h++)
                        Hat(mix, sr, t + h * beat / 2, (h == 1 ? 0.07f : 0.035f) * (0.4f + s.Drive * 0.6f), rng);
                    if (s.Drive >= 0.9f && second) Hat(mix, sr, t + beat * 0.75f, 0.03f, rng);
                }

                // bass: eighth notes
                for (int e = 0; e < 8; e++)
                {
                    int n = root - 12 + ((e % 4 == 2) ? 12 : 0);
                    Bass(mix, sr, barStart + e * beat / 2, beat / 2 * 0.9f, Midi(n), 0.22f * (0.5f + s.Drive * 0.5f));
                }

                // arpeggio (second half, or always when driving)
                if (second || s.Drive >= 0.8f)
                {
                    int[] pat = { 0, 2, 1, 3, 2, 4, 3, 1, 0, 2, 1, 3, 4, 3, 2, 1 };
                    for (int st = 0; st < 16; st++)
                    {
                        int idx = pat[st];
                        int n = root + 24 + (idx == 4 ? 12 : tones[idx]);
                        Arp(arpBus, sr, barStart + st * beat / 4, beat / 4, Midi(n), second ? 0.07f : 0.045f);
                    }
                }
            }

            // echo on arp bus (wraps around for a seamless loop)
            int delay = (int)(beat * 0.75f * sr);
            var echo = new float[len];
            for (int i = 0; i < len; i++) echo[i] = arpBus[i];
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < len; i++) echo[(i + delay) % len] += echo[i] * 0.35f * (pass == 0 ? 1 : 0.5f);
            for (int i = 0; i < len; i++) mix[i] += echo[i];

            float peak = 0.001f;
            for (int i = 0; i < len; i++) { mix[i] = (float)Math.Tanh(mix[i] * 1.2f); peak = Math.Max(peak, Math.Abs(mix[i])); }
            float g = 0.8f / peak;
            for (int i = 0; i < len; i++) mix[i] *= g;
            return mix;
        }

        static void Write(float[] buf, int i, float v) { int n = buf.Length; i %= n; if (i < 0) i += n; buf[i] += v; }

        static void Kick(float[] b, int sr, float t0, float vol)
        {
            int s = (int)(t0 * sr), n = (int)(0.3f * sr); float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr; float f = 45 + 90 * (float)Math.Exp(-t * 30);
                ph += TAU * f / sr;
                Write(b, s + i, Mathx.Sin(ph) * vol * (float)Math.Exp(-t * 8));
            }
        }

        static void Snare(float[] b, int sr, float t0, float vol, Rng rng)
        {
            int s = (int)(t0 * sr), n = (int)(0.22f * sr); float lp = 0, ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr; float x = rng.Range(-1f, 1f); lp += 0.3f * (x - lp);
                ph += TAU * 190 / sr;
                Write(b, s + i, ((x - lp) * 0.8f * (float)Math.Exp(-t * 16) + Mathx.Sin(ph) * 0.4f * (float)Math.Exp(-t * 30)) * vol);
            }
        }

        static void Hat(float[] b, int sr, float t0, float vol, Rng rng)
        {
            int s = (int)(t0 * sr), n = (int)(0.06f * sr); float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr; float x = rng.Range(-1f, 1f); lp += 0.6f * (x - lp);
                Write(b, s + i, (x - lp) * vol * (float)Math.Exp(-t * 70));
            }
        }

        static void Bass(float[] b, int sr, float t0, float dur, float f, float vol)
        {
            int s = (int)(t0 * sr), n = (int)(dur * sr); float ph = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr; ph += f / sr;
                float saw = (ph % 1f) * 2 - 1;
                float cut = 0.04f + 0.18f * (float)Math.Exp(-t * 18);
                lp += cut * (saw - lp);
                Write(b, s + i, lp * vol * Mathx.Clamp01(t * 400) * Mathx.Clamp01((dur - t) * 60));
            }
        }

        static void Pad(float[] b, int sr, float t0, float dur, float f, float vol)
        {
            int s = (int)(t0 * sr), n = (int)((dur + 0.6f) * sr); float p1 = 0, p2 = 0.37f, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr; p1 += f * 1.004f / sr; p2 += f * 0.996f / sr;
                float x = ((p1 % 1f) + (p2 % 1f) - 1f);
                lp += 0.06f * (x - lp);
                float env = Mathx.Clamp01(t / 0.35f) * (t > dur ? (float)Math.Exp(-(t - dur) * 7) : 1f);
                Write(b, s + i, lp * vol * env);
            }
        }

        static void Arp(float[] b, int sr, float t0, float dur, float f, float vol)
        {
            int s = (int)(t0 * sr), n = (int)(dur * 1.6f * sr); float ph = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr; ph += f / sr;
                float sq = (ph % 1f) < 0.5f ? 1f : -1f;
                lp += 0.25f * (sq - lp);
                Write(b, s + i, lp * vol * (float)Math.Exp(-t * 11) * Mathx.Clamp01(t * 500));
            }
        }
    }
}
