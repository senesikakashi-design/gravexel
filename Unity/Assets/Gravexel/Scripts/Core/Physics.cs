using System;

namespace Gravexel.Core
{
    public struct PlayerState
    {
        public float X, Y, VY, Coyote;
        public sbyte G, Dir;            // G: +1 = gravity down, -1 = gravity up. Dir: +1 right, -1 left
        public bool Grounded, Alive, Won, InPortal, InSwitch;
        public byte Flags;              // stars (bits 0-2), key, toggle
    }

    public static class DeathCause { public const int Spike = 0, Saw = 1, Laser = 2, Fall = 3, Crush = 4; }

    public interface IStepEvents
    {
        void OnFlip();
        void OnLand(float speed);
        void OnTurn();
        void OnOrb(int index);
        void OnStar(int index);
        void OnKey();
        void OnSwitch();
        void OnPortal(byte kind);
        void OnCheckpoint(int index);
        void OnDeath(int cause);
        void OnWin();
    }

    /// <summary>
    /// Deterministic fixed-step platformer physics shared by the game, the Unity build and the level solver.
    /// Units are tiles; y grows downwards.
    /// </summary>
    public static class Physics
    {
        public const float Dt = 1f / 120f;
        public const float RunSpeed = 7.5f, Gravity = 110f, MaxFall = 26f, FlipKick = 4f, CoyoteTime = 0.07f;
        public const float Half = 0.375f, HazardHalf = 0.28f, PickupHalf = 0.45f;
        public const int StarMask = 7, KeyBit = 8, ToggleBit = 16;
        const float Eps = 0.001f;

        public static PlayerState Spawn(LevelData L)
        {
            var p = new PlayerState { X = L.StartX, Y = L.StartY, G = 1, Dir = 1, Alive = true };
            // settle onto the floor
            for (int i = 0; i < 400 && !p.Grounded; i++)
            {
                float ny = p.Y + 0.02f;
                if (BoxSolid(L, p.X, ny, Half, 0)) { p.Grounded = true; p.Y = (float)Math.Floor(ny + Half) - Half - Eps; }
                else p.Y = ny;
            }
            return p;
        }

        public static bool CanFlip(ref PlayerState p) { return p.Alive && !p.Won && (p.Grounded || p.Coyote > 0); }

        public static bool BoxSolid(LevelData L, float x, float y, float h, int flags)
        {
            int x0 = (int)Math.Floor(x - h), x1 = (int)Math.Floor(x + h - 1e-4f);
            int y0 = (int)Math.Floor(y - h), y1 = (int)Math.Floor(y + h - 1e-4f);
            for (int ty = y0; ty <= y1; ty++)
                for (int tx = x0; tx <= x1; tx++)
                    if (L.IsSolid(tx, ty, flags)) return true;
            return false;
        }

        public static void Step(LevelData L, ref PlayerState p, float time, bool flip, IStepEvents ev)
        {
            if (!p.Alive || p.Won) return;

            if (BoxSolid(L, p.X, p.Y, Half - 0.05f, p.Flags)) { Kill(ref p, DeathCause.Crush, ev); return; }

            if (flip && (p.Grounded || p.Coyote > 0))
            {
                p.G = (sbyte)-p.G; p.VY = p.G * FlipKick; p.Grounded = false; p.Coyote = 0;
                if (ev != null) ev.OnFlip();
            }

            p.VY += p.G * Gravity * Dt;
            if (p.VY > MaxFall) p.VY = MaxFall; else if (p.VY < -MaxFall) p.VY = -MaxFall;

            // horizontal: walls turn the runner around
            float nx = p.X + p.Dir * RunSpeed * Dt;
            if (BoxSolid(L, nx, p.Y, Half, p.Flags))
            {
                if (p.Dir > 0) nx = (float)Math.Floor(nx + Half) - Half - Eps;
                else nx = (float)Math.Floor(nx - Half) + 1 + Half + Eps;
                if (BoxSolid(L, nx, p.Y, Half, p.Flags)) nx = p.X;
                p.Dir = (sbyte)-p.Dir;
                if (ev != null) ev.OnTurn();
            }
            p.X = nx;

            // vertical
            bool was = p.Grounded;
            float ny = p.Y + p.VY * Dt;
            if (BoxSolid(L, p.X, ny, Half, p.Flags))
            {
                if (p.VY > 0) ny = (float)Math.Floor(ny + Half) - Half - Eps;
                else ny = (float)Math.Floor(ny - Half) + 1 + Half + Eps;
                if (BoxSolid(L, p.X, ny, Half, p.Flags)) ny = p.Y;
                bool landing = Math.Sign(p.VY) == p.G;
                if (landing && !was && ev != null) ev.OnLand(Math.Abs(p.VY));
                p.Grounded = landing;
                if (landing) p.Coyote = 0;
                p.VY = 0;
            }
            else
            {
                if (was) p.Coyote = CoyoteTime;
                else if (p.Coyote > 0) p.Coyote -= Dt;
                p.Grounded = false;
            }
            p.Y = ny;

            if (p.Y > L.H + 1.5f || p.Y < -1.5f) { Kill(ref p, DeathCause.Fall, ev); return; }

            Triggers(L, ref p, time, ev);
        }

        static void Kill(ref PlayerState p, int cause, IStepEvents ev)
        {
            p.Alive = false;
            if (ev != null) ev.OnDeath(cause);
        }

        static bool Overlap(float ax0, float ay0, float ax1, float ay1, float bx0, float by0, float bx1, float by1)
        {
            return ax0 < bx1 && ax1 > bx0 && ay0 < by1 && ay1 > by0;
        }

        static void Triggers(LevelData L, ref PlayerState p, float time, IStepEvents ev)
        {
            float hx0 = p.X - HazardHalf, hx1 = p.X + HazardHalf, hy0 = p.Y - HazardHalf, hy1 = p.Y + HazardHalf;

            // spikes
            for (int ty = (int)Math.Floor(hy0); ty <= (int)Math.Floor(hy1); ty++)
                for (int tx = (int)Math.Floor(hx0); tx <= (int)Math.Floor(hx1); tx++)
                {
                    if (L.Tile(tx, ty) != TileKind.Spike) continue;
                    float x0 = tx, y0 = ty, x1 = tx + 1, y1 = ty + 1;
                    switch (L.SpikeDir[ty * L.W + tx])
                    {
                        case 0: x0 += 0.18f; x1 -= 0.18f; y0 += 0.45f; break;
                        case 1: x0 += 0.18f; x1 -= 0.18f; y1 -= 0.45f; break;
                        case 2: y0 += 0.18f; y1 -= 0.18f; x1 -= 0.45f; break;
                        default: y0 += 0.18f; y1 -= 0.18f; x0 += 0.45f; break;
                    }
                    if (Overlap(hx0, hy0, hx1, hy1, x0, y0, x1, y1)) { Kill(ref p, DeathCause.Spike, ev); return; }
                }

            // saws
            for (int i = 0; i < L.Saws.Count; i++)
            {
                float sx, sy; L.Saws[i].Pos(time, out sx, out sy);
                float cx = Mathx.Clamp(sx, hx0, hx1), cy = Mathx.Clamp(sy, hy0, hy1);
                float dx = sx - cx, dy = sy - cy;
                if (dx * dx + dy * dy < 0.38f * 0.38f) { Kill(ref p, DeathCause.Saw, ev); return; }
            }

            // lasers
            for (int i = 0; i < L.Lasers.Count; i++)
            {
                var lz = L.Lasers[i];
                if (!lz.Active(time)) continue;
                bool hit = lz.Vertical
                    ? Overlap(hx0, hy0, hx1, hy1, lz.X0 - 0.12f, lz.Y0, lz.X0 + 0.12f, lz.Y1)
                    : Overlap(hx0, hy0, hx1, hy1, lz.X0, lz.Y0 - 0.12f, lz.X1, lz.Y0 + 0.12f);
                if (hit) { Kill(ref p, DeathCause.Laser, ev); return; }
            }

            // pickups, portals, switches, arrows
            bool inPortal = false, inSwitch = false;
            float px0 = p.X - PickupHalf, px1 = p.X + PickupHalf, py0 = p.Y - PickupHalf, py1 = p.Y + PickupHalf;
            for (int ty = (int)Math.Floor(py0); ty <= (int)Math.Floor(py1); ty++)
                for (int tx = (int)Math.Floor(px0); tx <= (int)Math.Floor(px1); tx++)
                {
                    byte t = L.Tile(tx, ty);
                    if (t < TileKind.PortalUp || ty < 0 || ty >= L.H || tx < 0 || tx >= L.W) continue;
                    int idx = L.Idx[ty * L.W + tx];
                    switch (t)
                    {
                        case TileKind.Orb: if (ev != null) ev.OnOrb(idx); break;
                        case TileKind.Star:
                            {
                                int bit = 1 << Math.Min(idx, 2);
                                if ((p.Flags & bit) == 0) { p.Flags |= (byte)bit; if (ev != null) ev.OnStar(idx); }
                                break;
                            }
                        case TileKind.Key:
                            if ((p.Flags & KeyBit) == 0) { p.Flags |= KeyBit; if (ev != null) ev.OnKey(); }
                            break;
                        case TileKind.Switch: inSwitch = true; break;
                        case TileKind.PortalUp:
                        case TileKind.PortalDown:
                        case TileKind.PortalFlip:
                            if (Overlap(p.X - Half, p.Y - Half, p.X + Half, p.Y + Half, tx + 0.2f, ty, tx + 0.8f, ty + 1))
                            {
                                if (!p.InPortal && !inPortal)
                                {
                                    sbyte ng = t == TileKind.PortalUp ? (sbyte)-1 : t == TileKind.PortalDown ? (sbyte)1 : (sbyte)-p.G;
                                    if (ng != p.G) { p.G = ng; p.Grounded = false; p.Coyote = 0; p.VY = p.G * FlipKick * 0.5f; }
                                    if (ev != null) ev.OnPortal(t);
                                }
                                inPortal = true;
                            }
                            break;
                    }
                }
            if (inSwitch && !p.InSwitch)
            {
                p.Flags ^= ToggleBit;
                if (ev != null) ev.OnSwitch();
            }
            p.InSwitch = inSwitch;
            p.InPortal = inPortal;

            // arrows and checkpoints use the tile under the centre
            int cxT = (int)Math.Floor(p.X), cyT = (int)Math.Floor(p.Y);
            byte ct = L.Tile(cxT, cyT);
            if (ct == TileKind.ArrowR) p.Dir = 1;
            else if (ct == TileKind.ArrowL) p.Dir = -1;
            if (ev != null)
                for (int i = 0; i < L.Checkpoints.Count; i++)
                    if (L.Checkpoints[i].TX == cxT) ev.OnCheckpoint(i);

            if (L.ExitX >= 0)
            {
                // the exit is a finish line: crossing its column at any height wins
                if (Math.Abs(p.X - (L.ExitX + 0.5f)) < 0.5f) { p.Won = true; if (ev != null) ev.OnWin(); }
            }
        }
    }
}
