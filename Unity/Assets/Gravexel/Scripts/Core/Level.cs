using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gravexel.Core
{
    public static class TileKind
    {
        public const byte Empty = 0, Solid = 1, Spike = 2, Gate = 3, BlockA = 4, BlockB = 5,
            PortalUp = 6, PortalDown = 7, PortalFlip = 8, ArrowR = 9, ArrowL = 10,
            Orb = 11, Star = 12, Key = 13, Switch = 14, Checkpoint = 15, Exit = 16, Laser = 17;
    }

    public struct Item { public float X, Y; public int TX, TY; }

    public sealed class Saw
    {
        public bool Horizontal; public float Min, Max, Fixed, Start, Speed = 3.2f;
        public void Pos(float t, out float x, out float y)
        {
            float len = Max - Min, v;
            if (len < 0.01f) v = Min;
            else v = Min + Mathx.PingPong(Start - Min + Speed * t, len);
            if (Horizontal) { x = v; y = Fixed; } else { x = Fixed; y = v; }
        }
    }

    public sealed class Laser
    {
        public const float Period = 2.4f, OnTime = 1.2f, Warn = 0.35f;
        public bool Vertical; public float X0, Y0, X1, Y1; public int Phase;
        float Local(float t) { return Mathx.Repeat(t + (Phase == 1 ? OnTime : 0f), Period); }
        public bool Active(float t) { return Local(t) < OnTime; }
        /// <summary>0..1 warning intensity shortly before the beam turns on.</summary>
        public float Warning(float t) { float l = Local(t); return l > Period - Warn ? (l - (Period - Warn)) / Warn : 0f; }
    }

    /// <summary>Text description of a level, as stored in levels.txt.</summary>
    public sealed class LevelInfo
    {
        public int Number, World;
        public string Name = "", Hint = "";
        public float Par;
        public string[] Rows = new string[0];
        public int Width { get { int w = 0; foreach (var r in Rows) w = Math.Max(w, r.Length); return w; } }
    }

    public static class LevelFile
    {
        public static List<LevelInfo> Parse(string text)
        {
            var list = new List<LevelInfo>();
            LevelInfo cur = null; List<string> rows = null;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (rows != null)
                {
                    if (line.Trim() == "end") { cur.Rows = rows.ToArray(); list.Add(cur); cur = null; rows = null; }
                    else rows.Add(line);
                    continue;
                }
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int c = line.IndexOf(':'); if (c < 0) continue;
                string key = line.Substring(0, c).Trim().ToLowerInvariant(), val = line.Substring(c + 1).Trim();
                switch (key)
                {
                    case "level": cur = new LevelInfo { Number = int.Parse(val, CultureInfo.InvariantCulture) }; break;
                    case "name": if (cur != null) cur.Name = val; break;
                    case "world": if (cur != null) cur.World = int.Parse(val, CultureInfo.InvariantCulture); break;
                    case "par": if (cur != null) cur.Par = float.Parse(val, CultureInfo.InvariantCulture); break;
                    case "hint": if (cur != null) cur.Hint = val; break;
                    case "map": if (cur != null) rows = new List<string>(); break;
                }
            }
            return list;
        }

        public static string Write(List<LevelInfo> levels, string header)
        {
            var sb = new StringBuilder();
            sb.Append(header);
            foreach (var l in levels)
            {
                sb.Append("\nlevel: ").Append(l.Number).Append('\n');
                sb.Append("name: ").Append(l.Name).Append('\n');
                sb.Append("world: ").Append(l.World).Append('\n');
                sb.Append("par: ").Append(l.Par.ToString("0.0", CultureInfo.InvariantCulture)).Append('\n');
                if (!string.IsNullOrEmpty(l.Hint)) sb.Append("hint: ").Append(l.Hint).Append('\n');
                sb.Append("map:\n");
                foreach (var r in l.Rows) sb.Append(r).Append('\n');
                sb.Append("end\n");
            }
            return sb.ToString();
        }
    }

    /// <summary>Parsed, query-ready level used by the physics, the renderer and the solver.</summary>
    public sealed class LevelData
    {
        public readonly int W, H;
        public readonly byte[] Tiles;
        public readonly short[] Idx;
        public readonly byte[] SpikeDir; // 0 up, 1 down, 2 right, 3 left
        public float StartX, StartY;
        public int ExitX = -1, ExitY = -1;
        public readonly List<Item> Orbs = new List<Item>(), Stars = new List<Item>(), Keys = new List<Item>(), Checkpoints = new List<Item>(), Switches = new List<Item>();
        public readonly List<Saw> Saws = new List<Saw>();
        public readonly List<Laser> Lasers = new List<Laser>();
        public bool HasGate, HasToggle;
        public readonly LevelInfo Info;

        public LevelData(LevelInfo info)
        {
            Info = info;
            H = info.Rows.Length; W = info.Width;
            Tiles = new byte[W * H]; Idx = new short[W * H]; SpikeDir = new byte[W * H];
            var chars = new char[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    chars[y * W + x] = x < info.Rows[y].Length ? info.Rows[y][x] : '.';

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char ch = chars[y * W + x]; int i = y * W + x; byte t = TileKind.Empty;
                    var item = new Item { X = x + 0.5f, Y = y + 0.5f, TX = x, TY = y };
                    switch (ch)
                    {
                        case '#': t = TileKind.Solid; break;
                        case 'x': t = TileKind.Spike; break;
                        case 'G': t = TileKind.Gate; HasGate = true; break;
                        case 'a': t = TileKind.BlockA; HasToggle = true; break;
                        case 'b': t = TileKind.BlockB; HasToggle = true; break;
                        case 'U': t = TileKind.PortalUp; break;
                        case 'D': t = TileKind.PortalDown; break;
                        case 'F': t = TileKind.PortalFlip; break;
                        case '>': t = TileKind.ArrowR; break;
                        case '<': t = TileKind.ArrowL; break;
                        case 'o': t = TileKind.Orb; Idx[i] = (short)Orbs.Count; Orbs.Add(item); break;
                        case '*': t = TileKind.Star; Idx[i] = (short)Stars.Count; Stars.Add(item); break;
                        case 'K': t = TileKind.Key; Idx[i] = (short)Keys.Count; Keys.Add(item); break;
                        case 'T': t = TileKind.Switch; Idx[i] = (short)Switches.Count; Switches.Add(item); break;
                        case 'C': t = TileKind.Checkpoint; Idx[i] = (short)Checkpoints.Count; Checkpoints.Add(item); break;
                        case 'E': t = TileKind.Exit; ExitX = x; ExitY = y; break;
                        case 'P': StartX = x + 0.5f; StartY = y + 0.5f; break;
                        case '|': case '!': case '-': case '~': t = TileKind.Laser; break;
                        case 'h': case 'v': case 's': break;
                    }
                    Tiles[i] = t;
                }

            // number stars left to right so the HUD fills in order
            Stars.Sort((a, b) => a.TX != b.TX ? a.TX.CompareTo(b.TX) : a.TY.CompareTo(b.TY));
            for (int i = 0; i < Stars.Count; i++) Idx[Stars[i].TY * W + Stars[i].TX] = (short)i;

            // spike orientation from neighbouring solids
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (Tiles[y * W + x] == TileKind.Spike)
                    {
                        byte d = 0;
                        if (IsWall(x, y + 1)) d = 0; else if (IsWall(x, y - 1)) d = 1; else if (IsWall(x - 1, y)) d = 2; else if (IsWall(x + 1, y)) d = 3;
                        SpikeDir[y * W + x] = d;
                    }

            // saws
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char ch = chars[y * W + x];
                    if (ch != 'h' && ch != 'v' && ch != 's') continue;
                    var saw = new Saw();
                    if (ch == 'h')
                    {
                        int l = x, r = x;
                        while (l - 1 >= 0 && !IsWall(l - 1, y)) l--;
                        while (r + 1 < W && !IsWall(r + 1, y)) r++;
                        saw.Horizontal = true; saw.Min = l + 0.5f; saw.Max = r + 0.5f; saw.Fixed = y + 0.5f; saw.Start = x + 0.5f;
                    }
                    else if (ch == 'v')
                    {
                        int u = y, d = y;
                        while (u - 1 >= 0 && !IsWall(x, u - 1)) u--;
                        while (d + 1 < H && !IsWall(x, d + 1)) d++;
                        saw.Horizontal = false; saw.Min = u + 0.5f; saw.Max = d + 0.5f; saw.Fixed = x + 0.5f; saw.Start = y + 0.5f;
                    }
                    else { saw.Horizontal = true; saw.Min = saw.Max = saw.Start = x + 0.5f; saw.Fixed = y + 0.5f; }
                    Saws.Add(saw);
                }

            // lasers: merge runs of beam characters
            var used = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char ch = chars[y * W + x];
                    if (used[y * W + x]) continue;
                    if (ch == '|' || ch == '!')
                    {
                        int e = y; while (e + 1 < H && chars[(e + 1) * W + x] == ch) e++;
                        for (int k = y; k <= e; k++) used[k * W + x] = true;
                        Lasers.Add(new Laser { Vertical = true, X0 = x + 0.5f, X1 = x + 0.5f, Y0 = y, Y1 = e + 1, Phase = ch == '!' ? 1 : 0 });
                    }
                    else if (ch == '-' || ch == '~')
                    {
                        int e = x; while (e + 1 < W && chars[y * W + e + 1] == ch) e++;
                        for (int k = x; k <= e; k++) used[y * W + k] = true;
                        Lasers.Add(new Laser { Vertical = false, X0 = x, X1 = e + 1, Y0 = y + 0.5f, Y1 = y + 0.5f, Phase = ch == '~' ? 1 : 0 });
                    }
                }
        }

        bool IsWall(int x, int y)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return false;
            byte t = Tiles[y * W + x];
            return t == TileKind.Solid;
        }

        public byte Tile(int x, int y)
        {
            if (y < 0 || y >= H) return TileKind.Empty;
            if (x < 0 || x >= W) return TileKind.Solid;
            return Tiles[y * W + x];
        }

        public bool IsSolid(int x, int y, int flags)
        {
            byte t = Tile(x, y);
            switch (t)
            {
                case TileKind.Solid: return true;
                case TileKind.Gate: return (flags & Physics.KeyBit) == 0;
                case TileKind.BlockA: return (flags & Physics.ToggleBit) == 0;
                case TileKind.BlockB: return (flags & Physics.ToggleBit) != 0;
                default: return false;
            }
        }
    }
}
