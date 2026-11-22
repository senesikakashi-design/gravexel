using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gravexel.Core
{
    public sealed class Settings
    {
        public float MusicVolume = 0.7f, SfxVolume = 0.85f;
        public bool Fullscreen = true, ScreenShake = true, ShowTimer = true;
        public int Quality = 1;   // 0 performance, 1 balanced, 2 ultra (desktop renderer resolution)
        public int FpsCap = 1;    // 0 = 60, 1 = 120, 2 = unlimited
    }

    public sealed class LevelRecord
    {
        public bool Completed;
        public int StarBits;      // union of stars ever collected
        public int BestScore;
        public float BestTime;    // seconds, 0 = none
        public int StarCount { get { int c = 0; for (int i = 0; i < 3; i++) if ((StarBits & (1 << i)) != 0) c++; return c; } }
    }

    /// <summary>Everything that is saved: progress, unlocks and settings. Stored as simple key=value text.</summary>
    public sealed class SaveData
    {
        public readonly Dictionary<int, LevelRecord> Levels = new Dictionary<int, LevelRecord>();
        public readonly Settings Settings = new Settings();
        public int SelectedSkin;
        public int TotalDeaths, TotalOrbs, TotalFlips;
        public int LastLevel = 1;
        public readonly HashSet<int> SeenSkins = new HashSet<int> { 0 };

        public LevelRecord Get(int level)
        {
            LevelRecord r;
            if (!Levels.TryGetValue(level, out r)) { r = new LevelRecord(); Levels[level] = r; }
            return r;
        }

        public int TotalStars { get { int s = 0; foreach (var r in Levels.Values) s += r.StarCount; return s; } }
        public int TotalScore { get { int s = 0; foreach (var r in Levels.Values) s += r.BestScore; return s; } }

        public bool IsUnlocked(int level)
        {
            if (level <= 1) return true;
            LevelRecord prev;
            return Levels.TryGetValue(level - 1, out prev) && prev.Completed;
        }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("version=1\n");
            sb.Append("music=").Append(Settings.MusicVolume.ToString("0.00", Inv)).Append('\n');
            sb.Append("sfx=").Append(Settings.SfxVolume.ToString("0.00", Inv)).Append('\n');
            sb.Append("fullscreen=").Append(Settings.Fullscreen ? 1 : 0).Append('\n');
            sb.Append("shake=").Append(Settings.ScreenShake ? 1 : 0).Append('\n');
            sb.Append("timer=").Append(Settings.ShowTimer ? 1 : 0).Append('\n');
            sb.Append("quality=").Append(Settings.Quality).Append('\n');
            sb.Append("fps=").Append(Settings.FpsCap).Append('\n');
            sb.Append("skin=").Append(SelectedSkin).Append('\n');
            sb.Append("deaths=").Append(TotalDeaths).Append('\n');
            sb.Append("orbs=").Append(TotalOrbs).Append('\n');
            sb.Append("flips=").Append(TotalFlips).Append('\n');
            sb.Append("last=").Append(LastLevel).Append('\n');
            sb.Append("seen=").Append(string.Join(",", SeenSkins)).Append('\n');
            foreach (var kv in Levels)
            {
                var r = kv.Value;
                sb.Append("L").Append(kv.Key).Append('=')
                  .Append(r.Completed ? 1 : 0).Append(',').Append(r.StarBits).Append(',')
                  .Append(r.BestScore).Append(',').Append(r.BestTime.ToString("0.00", Inv)).Append('\n');
            }
            return sb.ToString();
        }

        public static SaveData Deserialize(string text)
        {
            var d = new SaveData();
            if (string.IsNullOrEmpty(text)) return d;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim(); int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                try
                {
                    switch (k)
                    {
                        case "music": d.Settings.MusicVolume = Mathx.Clamp01(float.Parse(v, Inv)); break;
                        case "sfx": d.Settings.SfxVolume = Mathx.Clamp01(float.Parse(v, Inv)); break;
                        case "fullscreen": d.Settings.Fullscreen = v == "1"; break;
                        case "shake": d.Settings.ScreenShake = v == "1"; break;
                        case "timer": d.Settings.ShowTimer = v == "1"; break;
                        case "quality": d.Settings.Quality = Mathx.Clamp(int.Parse(v, Inv), 0, 2); break;
                        case "fps": d.Settings.FpsCap = Mathx.Clamp(int.Parse(v, Inv), 0, 2); break;
                        case "skin": d.SelectedSkin = int.Parse(v, Inv); break;
                        case "deaths": d.TotalDeaths = int.Parse(v, Inv); break;
                        case "orbs": d.TotalOrbs = int.Parse(v, Inv); break;
                        case "flips": d.TotalFlips = int.Parse(v, Inv); break;
                        case "last": d.LastLevel = int.Parse(v, Inv); break;
                        case "seen":
                            foreach (var s in v.Split(',')) { int n; if (int.TryParse(s, NumberStyles.Integer, Inv, out n)) d.SeenSkins.Add(n); }
                            break;
                        default:
                            if (k.StartsWith("L"))
                            {
                                int lv = int.Parse(k.Substring(1), Inv);
                                var p = v.Split(',');
                                var r = d.Get(lv);
                                r.Completed = p[0] == "1";
                                r.StarBits = int.Parse(p[1], Inv);
                                r.BestScore = int.Parse(p[2], Inv);
                                r.BestTime = float.Parse(p[3], Inv);
                            }
                            break;
                    }
                }
                catch (Exception) { /* ignore a damaged line, keep the rest */ }
            }
            return d;
        }
    }

    public sealed class Skin
    {
        public string Name; public int Shape; public Col Color, Trail; public int StarsNeeded; public bool Rainbow;
        public Skin(string name, int shape, string color, string trail, int stars, bool rainbow = false)
        {
            Name = name; Shape = shape; Color = Col.Hex(color); Trail = Col.Hex(trail); StarsNeeded = stars; Rainbow = rainbow;
        }
        public Col ColorAt(float t) { return Rainbow ? Col.FromHsv(t * 0.25f, 0.7f, 1f) : Color; }
        public Col TrailAt(float t) { return Rainbow ? Col.FromHsv(t * 0.25f + 0.3f, 0.8f, 1f) : Trail; }
    }

    public static class Skins
    {
        public static readonly string[] ShapeNames = { "Cube", "Orb", "Prism", "Hex", "Star" };
        public static readonly Skin[] All =
        {
            new Skin("VOLT",   0, "#22E8FF", "#22E8FF", 0),
            new Skin("BLIP",   1, "#FF3DDB", "#FF7AE8", 3),
            new Skin("SPARK",  0, "#FFE04A", "#FFB020", 6),
            new Skin("PRISM",  2, "#7CFF5B", "#2BFFB0", 10),
            new Skin("HEXO",   3, "#FF8A2B", "#FF4D2B", 15),
            new Skin("NOVA",   4, "#FFF4C9", "#FFD84A", 20),
            new Skin("RUBY",   1, "#FF3355", "#FF7799", 26),
            new Skin("FROST",  2, "#9BE8FF", "#E0FAFF", 33),
            new Skin("VOID",   3, "#A25BFF", "#5B2BFF", 41),
            new Skin("GLITCH", 0, "#FFFFFF", "#FFFFFF", 50, true),
            new Skin("SOLAR",  4, "#FFA62B", "#FF5A1F", 62),
            new Skin("OMEGA",  1, "#FFD34A", "#FFFFFF", 80, true),
            new Skin("QUARK",  3, "#00FFC8", "#00B3FF", 100),
            new Skin("BLAZE",  0, "#FF6A3D", "#FFD34A", 120),
            new Skin("PULSAR", 4, "#EAF2FF", "#22E8FF", 140, true),
            new Skin("GRAVEX", 2, "#FFC83D", "#FF3DDB", 160, true),
        };
    }

    public sealed class Theme
    {
        public string Name; public Col SkyTop, SkyBottom, Block, Edge, Accent, Hazard, Far, Near;
    }

    public static class Themes
    {
        public static readonly Theme[] World =
        {
            new Theme { Name = "NEON DOCKS",   SkyTop = Col.Hex("#050A1C"), SkyBottom = Col.Hex("#1A0B33"), Block = Col.Hex("#132449"), Edge = Col.Hex("#22E8FF"), Accent = Col.Hex("#FF3DDB"), Hazard = Col.Hex("#FF3B6B"), Far = Col.Hex("#3A1F7A"), Near = Col.Hex("#1B4C8C") },
            new Theme { Name = "CIRCUIT CITY", SkyTop = Col.Hex("#03140F"), SkyBottom = Col.Hex("#0A2240"), Block = Col.Hex("#0E3330"), Edge = Col.Hex("#7CFF5B"), Accent = Col.Hex("#2BB8FF"), Hazard = Col.Hex("#FF4D4D"), Far = Col.Hex("#0F4A4A"), Near = Col.Hex("#145A3A") },
            new Theme { Name = "LASER CORE",   SkyTop = Col.Hex("#160406"), SkyBottom = Col.Hex("#2A0A1F"), Block = Col.Hex("#33121C"), Edge = Col.Hex("#FF8A2B"), Accent = Col.Hex("#FFE04A"), Hazard = Col.Hex("#FF2B4A"), Far = Col.Hex("#5A1426"), Near = Col.Hex("#7A2A10") },
            new Theme { Name = "VOID SPIRE",   SkyTop = Col.Hex("#07031A"), SkyBottom = Col.Hex("#1E0838"), Block = Col.Hex("#21143F"), Edge = Col.Hex("#B57BFF"), Accent = Col.Hex("#FFD34A"), Hazard = Col.Hex("#FF3D8A"), Far = Col.Hex("#36186A"), Near = Col.Hex("#4A2490") },
            new Theme { Name = "QUANTUM FORGE", SkyTop = Col.Hex("#021218"), SkyBottom = Col.Hex("#0A2B38"), Block = Col.Hex("#0F3340"), Edge = Col.Hex("#00FFC8"), Accent = Col.Hex("#FF6A3D"), Hazard = Col.Hex("#FF2E63"), Far = Col.Hex("#0F4655"), Near = Col.Hex("#12606A") },
            new Theme { Name = "OMEGA RIFT",    SkyTop = Col.Hex("#08060E"), SkyBottom = Col.Hex("#2A0E2E"), Block = Col.Hex("#231C38"), Edge = Col.Hex("#EAF2FF"), Accent = Col.Hex("#FFC83D"), Hazard = Col.Hex("#FF3355"), Far = Col.Hex("#33285A"), Near = Col.Hex("#46397A") },
        };

        public static Theme For(int world) { return World[Mathx.Clamp(world - 1, 0, World.Length - 1)]; }
    }
}
