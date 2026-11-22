using System;
using System.Collections.Generic;

namespace Gravexel.Core
{
    /// <summary>Draws a level: static geometry is baked once into a static layer, moving things every frame.</summary>
    public sealed class WorldView
    {
        public const float TilePx = 64f;
        public const int LayerLevel = 1;
        /// <summary>Extra space above/below the level inside the static layer (for the extended floor/ceiling).</summary>
        public const float LayerPad = 300f;
        public static readonly Col PortalUpCol = Col.Hex("#FFB020"), PortalDownCol = Col.Hex("#2BFFB0"), PortalFlipCol = Col.Hex("#C77BFF");
        public static readonly Col StarCol = Col.Hex("#FFE04A"), KeyCol = Col.Hex("#FFD34A");

        LevelData L; Theme T;

        public void Build(IRenderer r, LevelData level, Theme theme)
        {
            L = level; T = theme;
            var c = new List<DrawCmd>();
            float S = TilePx;

            // block fills (row runs) and inner panels
            for (int y = 0; y < L.H; y++)
            {
                int x = 0;
                while (x < L.W)
                {
                    if (L.Tile(x, y) != TileKind.Solid) { x++; continue; }
                    int s = x; while (x < L.W && L.Tile(x, y) == TileKind.Solid) x++;
                    Add(c, Tx.Pixel, (s + x) * 0.5f * S, (y + 0.5f) * S, (x - s) * S + 0.5f, S + 0.5f, 0, T.Block, Blend.Alpha);
                }
            }
            // extend solid top/bottom rows past the level edge so no sky shows above the ceiling or below the floor
            const float Ext = LayerPad;
            foreach (int row in new[] { 0, L.H - 1 })
            {
                int x = 0;
                while (x < L.W)
                {
                    if (L.Tile(x, row) != TileKind.Solid) { x++; continue; }
                    int s = x; while (x < L.W && L.Tile(x, row) == TileKind.Solid) x++;
                    float yc = row == 0 ? -Ext / 2 : L.H * S + Ext / 2;
                    Add(c, Tx.Pixel, (s + x) * 0.5f * S, yc, (x - s) * S + 0.5f, Ext, 0, T.Block, Blend.Alpha);
                    // soft dark falloff towards the screen edge
                    Add(c, Tx.GlowLine, (s + x) * 0.5f * S, row == 0 ? -Ext : L.H * S + Ext, (x - s) * S + 0.5f, Ext * 1.4f, 0, new Col(0, 0, 0, 0.6f), Blend.Alpha);
                }
            }
            for (int y = 0; y < L.H; y++)
                for (int x = 0; x < L.W; x++)
                    if (L.Tile(x, y) == TileKind.Solid)
                    {
                        Add(c, Tx.Pixel, (x + 0.5f) * S, (y + 0.5f) * S, S - 8, S - 8, 0, Col.White.WithA(((x + y) & 1) == 0 ? 0.045f : 0.02f), Blend.Alpha);
                        Add(c, Tx.Pixel, (x + 0.5f) * S, y * S + 6, S - 16, 2, 0, Col.White.WithA(0.05f), Blend.Alpha);
                    }

            // exposed edges
            Col edge = T.Edge, core = T.Edge.Brighten(0.45f);
            for (int y = 0; y < L.H; y++)
                for (int side = 0; side < 2; side++)
                {
                    int x = 0;
                    while (x < L.W)
                    {
                        if (!Exposed(x, y, 0, side == 0 ? -1 : 1)) { x++; continue; }
                        int s = x; while (x < L.W && Exposed(x, y, 0, side == 0 ? -1 : 1)) x++;
                        float ey = (side == 0 ? y : y + 1) * S, cx = (s + x) * 0.5f * S, len = (x - s) * S;
                        Add(c, Tx.GlowLine, cx, ey, len + 20, 34, 0, edge.WithA(0.5f), Blend.Add);
                        Add(c, Tx.Pixel, cx, ey, len, 4, 0, core, Blend.Alpha);
                    }
                }
            for (int x = 0; x < L.W; x++)
                for (int side = 0; side < 2; side++)
                {
                    int y = 0;
                    while (y < L.H)
                    {
                        if (!Exposed(x, y, side == 0 ? -1 : 1, 0)) { y++; continue; }
                        int s = y; while (y < L.H && Exposed(x, y, side == 0 ? -1 : 1, 0)) y++;
                        float ex = (side == 0 ? x : x + 1) * S, cy = (s + y) * 0.5f * S, len = (y - s) * S;
                        Add(c, Tx.GlowLine, ex, cy, len + 20, 34, Mathx.PI / 2, edge.WithA(0.5f), Blend.Add);
                        Add(c, Tx.Pixel, ex, cy, 4, len, 0, core, Blend.Alpha);
                    }
                }
            // convex corner sparks
            for (int y = 0; y < L.H; y++)
                for (int x = 0; x < L.W; x++)
                {
                    if (L.Tile(x, y) != TileKind.Solid) continue;
                    for (int k = 0; k < 4; k++)
                    {
                        int dx = (k & 1) == 0 ? -1 : 1, dy = (k & 2) == 0 ? -1 : 1;
                        if (Exposed(x, y, dx, 0) && Exposed(x, y, 0, dy))
                        {
                            float px = (dx < 0 ? x : x + 1) * S, py = (dy < 0 ? y : y + 1) * S;
                            Add(c, Tx.Glow, px, py, 40, 40, 0, edge.WithA(0.7f), Blend.Add);
                            Add(c, Tx.Circle, px, py, 9, 9, 0, Col.White, Blend.Alpha);
                        }
                    }
                }

            // spikes, arrows, laser emitters
            for (int y = 0; y < L.H; y++)
                for (int x = 0; x < L.W; x++)
                {
                    byte t = L.Tile(x, y);
                    float cx = (x + 0.5f) * S, cy = (y + 0.5f) * S;
                    if (t == TileKind.Spike)
                    {
                        int d = L.SpikeDir[y * L.W + x];
                        float rot = d == 0 ? 0 : d == 1 ? Mathx.PI : d == 2 ? Mathx.PI / 2 : -Mathx.PI / 2;
                        float ox = d == 2 ? -6 : d == 3 ? 6 : 0, oy = d == 0 ? 6 : d == 1 ? -6 : 0;
                        Add(c, Tx.Glow, cx + ox, cy + oy, 90, 90, 0, T.Hazard.WithA(0.35f), Blend.Add);
                        Add(c, Tx.Spike, cx + ox * 0.3f, cy + oy * 0.3f, 56, 56, rot, T.Hazard.Brighten(0.25f), Blend.Alpha);
                    }
                    else if (t == TileKind.ArrowR || t == TileKind.ArrowL)
                    {
                        float rot = t == TileKind.ArrowR ? 0 : Mathx.PI;
                        Add(c, Tx.Glow, cx, cy, 80, 80, 0, T.Accent.WithA(0.3f), Blend.Add);
                        Add(c, Tx.Chevron, cx - 8 * (t == TileKind.ArrowR ? 1 : -1), cy, 36, 36, rot, T.Accent.WithA(0.5f), Blend.Alpha);
                        Add(c, Tx.Chevron, cx + 8 * (t == TileKind.ArrowR ? 1 : -1), cy, 36, 36, rot, T.Accent, Blend.Alpha);
                    }
                }
            foreach (var lz in L.Lasers)
            {
                if (lz.Vertical)
                {
                    if (L.Tile((int)lz.X0, (int)lz.Y0 - 1) == TileKind.Solid) Add(c, Tx.Emitter, lz.X0 * S, lz.Y0 * S + 6, 40, 22, 0, T.Hazard.Darken(0.2f), Blend.Alpha);
                    if (L.Tile((int)lz.X0, (int)lz.Y1) == TileKind.Solid) Add(c, Tx.Emitter, lz.X0 * S, lz.Y1 * S - 6, 40, 22, 0, T.Hazard.Darken(0.2f), Blend.Alpha);
                }
                else
                {
                    if (L.Tile((int)lz.X0 - 1, (int)lz.Y0) == TileKind.Solid) Add(c, Tx.Emitter, lz.X0 * S + 6, lz.Y0 * S, 40, 22, Mathx.PI / 2, T.Hazard.Darken(0.2f), Blend.Alpha);
                    if (L.Tile((int)lz.X1, (int)lz.Y0) == TileKind.Solid) Add(c, Tx.Emitter, lz.X1 * S - 6, lz.Y0 * S, 40, 22, Mathx.PI / 2, T.Hazard.Darken(0.2f), Blend.Alpha);
                }
            }

            var arr = c.ToArray();
            for (int i = 0; i < arr.Length; i++) arr[i].Y += LayerPad;
            r.BuildStaticLayer(LayerLevel, arr, arr.Length, L.W * S, L.H * S + LayerPad * 2);
        }

        bool Exposed(int x, int y, int dx, int dy)
        {
            if (L.Tile(x, y) != TileKind.Solid) return false;
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || nx >= L.W || ny < 0 || ny >= L.H) return false;
            return L.Tile(nx, ny) != TileKind.Solid;
        }

        static void Add(List<DrawCmd> c, int tex, float x, float y, float w, float h, float rot, Col col, Blend b)
        {
            c.Add(new DrawCmd { Tex = tex, X = x, Y = y, W = w, H = h, Rot = rot, Color = col, Blend = b });
        }

        /// <summary>Draws everything that moves or changes state. ox/oy convert tiles to screen: sx = x*64 + ox.</summary>
        public void DrawDynamic(IRenderer r, float ox, float oy, float time, int flags, bool[] orbTaken, int runStars, int savedStars, int checkpoint)
        {
            float S = TilePx;
            int x0 = Math.Max(0, (int)Math.Floor(-ox / S) - 2), x1 = Math.Min(L.W - 1, (int)Math.Ceiling((r.ViewW - ox) / S) + 2);
            bool toggled = (flags & Physics.ToggleBit) != 0, hasKey = (flags & Physics.KeyBit) != 0;

            for (int y = 0; y < L.H; y++)
                for (int x = x0; x <= x1; x++)
                {
                    byte t = L.Tiles[y * L.W + x];
                    if (t == TileKind.Empty || t == TileKind.Solid || t == TileKind.Spike) continue;
                    float cx = (x + 0.5f) * S + ox, cy = (y + 0.5f) * S + oy;
                    switch (t)
                    {
                        case TileKind.BlockA:
                        case TileKind.BlockB:
                            {
                                bool solid = (t == TileKind.BlockA) != toggled;
                                Col col = t == TileKind.BlockA ? T.Accent : T.Edge;
                                if (solid)
                                {
                                    r.Sprite(Tx.Glow, cx, cy, 110, 110, 0, col.WithA(0.22f), Blend.Add);
                                    r.Sprite(Tx.ToggleBlock, cx, cy, S, S, 0, col, Blend.Alpha);
                                }
                                else r.Sprite(Tx.ToggleBlock, cx, cy, S * 0.8f, S * 0.8f, 0, col.WithA(0.16f), Blend.Alpha);
                                break;
                            }
                        case TileKind.Gate:
                            if (!hasKey)
                            {
                                r.Sprite(Tx.Glow, cx, cy, 110, 110, 0, KeyCol.WithA(0.2f), Blend.Add);
                                r.Sprite(Tx.Gate, cx, cy, S, S, 0, KeyCol, Blend.Alpha);
                                r.Sprite(Tx.Lock, cx, cy, 34, 34, 0, Col.Hex("#1A1206"), Blend.Alpha);
                            }
                            else r.Sprite(Tx.Gate, cx, cy, S * 0.7f, S * 0.7f, 0, KeyCol.WithA(0.08f), Blend.Alpha);
                            break;
                        case TileKind.PortalUp:
                        case TileKind.PortalDown:
                        case TileKind.PortalFlip:
                            {
                                Col col = t == TileKind.PortalUp ? PortalUpCol : t == TileKind.PortalDown ? PortalDownCol : PortalFlipCol;
                                float pulse = 0.75f + 0.25f * Mathx.Sin(time * 6 + y * 0.7f);
                                r.Sprite(Tx.GlowLine, cx, cy, S + 2, 70, Mathx.PI / 2, col.WithA(0.55f * pulse), Blend.Add);
                                r.Sprite(Tx.Pixel, cx, cy, 5, S + 1, 0, col.Brighten(0.5f).WithA(0.9f), Blend.Alpha);
                                float m = Mathx.Repeat(time * 1.6f + y * 0.25f, 1f);
                                if (t != TileKind.PortalDown)
                                    r.Sprite(Tx.Chevron, cx, cy + S * 0.5f - m * S, 30, 30, -Mathx.PI / 2, col.WithA(Mathx.Sin(m * Mathx.PI)), Blend.Alpha);
                                if (t != TileKind.PortalUp)
                                    r.Sprite(Tx.Chevron, cx, cy - S * 0.5f + m * S, 30, 30, Mathx.PI / 2, col.WithA(Mathx.Sin(m * Mathx.PI)), Blend.Alpha);
                                break;
                            }
                        case TileKind.Switch:
                            {
                                Col col = toggled ? T.Edge : T.Accent;
                                r.Sprite(Tx.Glow, cx, cy, 120, 120, 0, col.WithA(0.35f), Blend.Add);
                                r.Sprite(Tx.Switch, cx, cy, 52, 52, toggled ? Mathx.PI / 4 : 0, col, Blend.Alpha);
                                break;
                            }
                        case TileKind.Checkpoint:
                            {
                                int idx = L.Idx[y * L.W + x];
                                bool active = idx == checkpoint;
                                bool ceiling = !L.IsSolid(x, y + 1, 0) && L.IsSolid(x, y - 1, 0);
                                float by = ceiling ? cy - S * 0.5f + 40 : cy + S * 0.5f - 40;
                                Col col = active ? T.Accent : Col.White.WithA(0.45f);
                                if (active) r.Sprite(Tx.Glow, cx, by - (ceiling ? -24 : 24), 140, 140, 0, T.Accent.WithA(0.35f + 0.15f * Mathx.Sin(time * 5)), Blend.Add);
                                r.Sprite(Tx.Beacon, cx, by, 40, 80, ceiling ? Mathx.PI : 0, col, Blend.Alpha);
                                break;
                            }
                        case TileKind.Laser:
                            break;
                    }
                }

            // orbs
            for (int i = 0; i < L.Orbs.Count; i++)
            {
                var o = L.Orbs[i]; if (orbTaken[i] || o.TX < x0 || o.TX > x1) continue;
                float cx = o.X * S + ox, cy = o.Y * S + oy + Mathx.Sin(time * 3 + i) * 5;
                r.Sprite(Tx.Glow, cx, cy, 70, 70, 0, T.Edge.WithA(0.55f), Blend.Add);
                r.Sprite(Tx.Ring, cx, cy, 26, 26, 0, T.Edge.Brighten(0.3f), Blend.Alpha);
                r.Sprite(Tx.Circle, cx, cy, 13, 13, 0, Col.White, Blend.Alpha);
            }
            // stars
            for (int i = 0; i < L.Stars.Count; i++)
            {
                var s = L.Stars[i]; int bit = 1 << Math.Min(i, 2);
                if ((runStars & bit) != 0 || s.TX < x0 || s.TX > x1) continue;
                bool ghost = (savedStars & bit) != 0;
                float cx = s.X * S + ox, cy = s.Y * S + oy + Mathx.Sin(time * 2.4f + i) * 6;
                float rot = Mathx.Sin(time * 1.7f + i) * 0.25f;
                float a = ghost ? 0.45f : 1f;
                r.Sprite(Tx.Glow, cx, cy, 150, 150, 0, StarCol.WithA(0.45f * a), Blend.Add);
                r.Sprite(Tx.Star, cx, cy, 54, 54, rot, StarCol.WithA(a), Blend.Alpha);
                r.Sprite(Tx.StarOutline, cx, cy, 54, 54, rot, Col.White.WithA(0.7f * a), Blend.Alpha);
            }
            // keys
            if (!hasKey)
                foreach (var k in L.Keys)
                {
                    if (k.TX < x0 || k.TX > x1) continue;
                    float cx = k.X * S + ox, cy = k.Y * S + oy + Mathx.Sin(time * 3) * 6;
                    r.Sprite(Tx.Glow, cx, cy, 130, 130, 0, KeyCol.WithA(0.45f), Blend.Add);
                    r.Sprite(Tx.Key, cx, cy, 50, 50, Mathx.Sin(time * 2) * 0.3f, KeyCol, Blend.Alpha);
                }
            // exit portal
            if (L.ExitX >= 0)
            {
                float cx = (L.ExitX + 0.5f) * S + ox, cy = (L.ExitY + 0.5f) * S + oy;
                if (cx > -200 && cx < r.ViewW + 200)
                {
                    float p = 0.85f + 0.15f * Mathx.Sin(time * 4);
                    float midY = L.H * 0.5f * S + oy, fullH = L.H * S;
                    r.Sprite(Tx.GlowLine, cx, midY, fullH, 110, Mathx.PI / 2, T.Accent.WithA(0.35f * p), Blend.Add);
                    r.Sprite(Tx.Pixel, cx, midY, 4, fullH, 0, Col.White.WithA(0.55f), Blend.Add);
                    for (int k = 0; k < L.H * 2; k++)
                    {
                        float yy = Mathx.Repeat(k * S * 0.5f - time * 90f, fullH) + oy;
                        r.Sprite(Tx.Chevron, cx, yy, 20, 20, 0, Col.White.WithA(0.35f), Blend.Add);
                    }
                    r.Sprite(Tx.Glow, cx, cy, 300 * p, 300 * p, 0, T.Accent.WithA(0.5f), Blend.Add);
                    r.Sprite(Tx.Swirl, cx, cy, 180, 180, time * 2.5f, Col.White.WithA(0.75f), Blend.Add);
                    r.Sprite(Tx.Swirl, cx, cy, 130, 130, -time * 3.5f, T.Edge.WithA(0.8f), Blend.Add);
                    r.Sprite(Tx.RingSoft, cx, cy, 190 * p, 190 * p, 0, T.Accent.WithA(0.9f), Blend.Add);
                    r.Sprite(Tx.Ring, cx, cy, 120, 120, 0, Col.White.WithA(0.8f), Blend.Alpha);
                    r.Sprite(Tx.Circle, cx, cy, 34, 34, 0, Col.White, Blend.Alpha);
                }
            }
            // lasers
            foreach (var lz in L.Lasers)
            {
                float ax = lz.X0 * S + ox, ay = lz.Y0 * S + oy, bx = lz.X1 * S + ox, by = lz.Y1 * S + oy;
                if (Math.Max(ax, bx) < -50 || Math.Min(ax, bx) > r.ViewW + 50) continue;
                float cx = (ax + bx) / 2, cy = (ay + by) / 2, len = lz.Vertical ? by - ay : bx - ax;
                float rot = lz.Vertical ? Mathx.PI / 2 : 0;
                if (lz.Active(time))
                {
                    float flick = 0.85f + 0.15f * Mathx.Sin(time * 60);
                    r.Sprite(Tx.GlowLine, cx, cy, len, 60, rot, T.Hazard.WithA(0.8f * flick), Blend.Add);
                    r.Sprite(Tx.GlowLine, cx, cy, len, 20, rot, Col.White.WithA(0.8f), Blend.Add);
                }
                else
                {
                    float w = lz.Warning(time);
                    float a = 0.12f + w * 0.5f * (0.5f + 0.5f * Mathx.Sin(time * 40));
                    if (lz.Vertical) r.Sprite(Tx.Pixel, cx, cy, 2 + w * 3, len, 0, T.Hazard.WithA(a), Blend.Add);
                    else r.Sprite(Tx.Pixel, cx, cy, len, 2 + w * 3, 0, T.Hazard.WithA(a), Blend.Add);
                }
            }
            // saws
            foreach (var saw in L.Saws)
            {
                float sx, sy; saw.Pos(time, out sx, out sy);
                float cx = sx * S + ox, cy = sy * S + oy;
                if (cx < -100 || cx > r.ViewW + 100) continue;
                r.Sprite(Tx.Glow, cx, cy, 150, 150, 0, T.Hazard.WithA(0.45f), Blend.Add);
                r.Sprite(Tx.Saw, cx, cy, 62, 62, time * 9f, T.Hazard.Brighten(0.35f), Blend.Alpha);
            }
        }

        /// <summary>Draws a character (also used in menus). rot is the body rotation; upright eyes follow 'eyeUp'.</summary>
        public static void DrawCharacter(IRenderer r, Skin skin, float cx, float cy, float size, float rot, int dir, float time,
            float sqX = 1f, float sqY = 1f, float alpha = 1f, float eyeUp = 1f)
        {
            Col c = skin.ColorAt(time);
            r.Sprite(Tx.Glow, cx, cy, size * 3f, size * 3f, 0, c.WithA(0.42f * alpha), Blend.Add);
            r.Sprite(Tx.BodyFor(skin.Shape), cx, cy, size * sqX, size * sqY, rot, c.WithA(alpha), Blend.Alpha);
            float blink = Mathx.Repeat(time, 3.3f) > 3.18f ? 0.15f : 1f;
            float ex = dir * size * 0.13f, ey = -size * 0.08f * eyeUp;
            float ew = size * 0.17f, eh = size * 0.24f * blink;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = cx + ex + s * size * 0.15f, y = cy + ey;
                r.Sprite(Tx.Eye, x, y, ew * sqX, eh * sqY, 0, Col.White.WithA(alpha), Blend.Alpha);
                if (blink > 0.5f) r.Sprite(Tx.Circle, x + dir * size * 0.03f, y + size * 0.02f * eyeUp, ew * 0.55f, ew * 0.55f, 0, new Col(0.04f, 0.04f, 0.1f, alpha), Blend.Alpha);
            }
        }
    }
}
