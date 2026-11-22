using System;
using System.Collections.Generic;

namespace Gravexel.Core
{
    public sealed class SolveResult
    {
        public bool Finishable, AllStars;
        public float FinishTime, AllStarsTime;
        public int PeakStates;
        /// <summary>Frames (physics steps) on which to press flip for the fastest all-stars run.</summary>
        public List<int> FlipFrames = new List<int>();
    }

    /// <summary>
    /// Breadth-first search over the deterministic physics: proves a level can be finished and every
    /// star collected, and measures the fastest time (used for the par time).
    /// </summary>
    public static class Solver
    {
        struct Key : IEquatable<Key>
        {
            public int X, Y, VY, C; public int Misc;
            public bool Equals(Key o) { return X == o.X && Y == o.Y && VY == o.VY && C == o.C && Misc == o.Misc; }
            public override bool Equals(object obj) { return obj is Key && Equals((Key)obj); }
            public override int GetHashCode()
            {
                unchecked { int h = X * 73856093 ^ Y * 19349663 ^ VY * 83492791 ^ C * 50331653 ^ Misc * 16777619; return h; }
            }
        }

        static Key KeyOf(ref PlayerState p)
        {
            return new Key
            {
                X = BitConverter.ToInt32(BitConverter.GetBytes(p.X), 0),
                Y = BitConverter.ToInt32(BitConverter.GetBytes(p.Y), 0),
                VY = BitConverter.ToInt32(BitConverter.GetBytes(p.VY), 0),
                C = BitConverter.ToInt32(BitConverter.GetBytes(p.Coyote), 0),
                Misc = (p.G > 0 ? 1 : 0) | (p.Dir > 0 ? 2 : 0) | (p.Grounded ? 4 : 0) | (p.InPortal ? 8 : 0) | (p.InSwitch ? 16 : 0) | (p.Flags << 5)
            };
        }

        public static SolveResult Solve(LevelData L, float maxSeconds = 160f, int decisionEvery = 3, int maxLayer = 6000)
        {
            var res = new SolveResult();
            int starMask = (1 << Math.Min(3, L.Stars.Count)) - 1;
            int maxFrames = (int)(maxSeconds / Physics.Dt);

            var cur = new List<PlayerState> { Physics.Spawn(L) };
            var parents = new List<int[]>();
            var flips = new List<bool[]>();
            var next = new List<PlayerState>();
            var nextParent = new List<int>();
            var nextFlip = new List<bool>();
            var seen = new Dictionary<Key, int>();

            for (int f = 0; f < maxFrames && cur.Count > 0; f++)
            {
                float time = f * Physics.Dt;
                next.Clear(); nextParent.Clear(); nextFlip.Clear(); seen.Clear();
                bool decide = f % decisionEvery == 0;
                for (int i = 0; i < cur.Count; i++)
                {
                    var s = cur[i];
                    int options = (decide && Physics.CanFlip(ref s)) ? 2 : 1;
                    for (int o = 0; o < options; o++)
                    {
                        var p = s;
                        Physics.Step(L, ref p, time, o == 1, null);
                        if (!p.Alive) continue;
                        if (p.Won)
                        {
                            if (!res.Finishable) { res.Finishable = true; res.FinishTime = (f + 1) * Physics.Dt; }
                            if ((p.Flags & starMask) == starMask)
                            {
                                res.AllStars = true; res.AllStarsTime = (f + 1) * Physics.Dt;
                                // reconstruct inputs
                                if (o == 1) res.FlipFrames.Add(f);
                                int idx = i;
                                for (int back = f - 1; back >= 0; back--)
                                {
                                    if (flips[back][idx]) res.FlipFrames.Add(back);
                                    idx = parents[back][idx];
                                }
                                res.FlipFrames.Reverse();
                                return res;
                            }
                            continue;
                        }
                        var k = KeyOf(ref p);
                        if (seen.ContainsKey(k)) continue;
                        if (next.Count >= maxLayer) continue;
                        seen[k] = next.Count;
                        next.Add(p); nextParent.Add(i); nextFlip.Add(o == 1);
                    }
                }
                res.PeakStates = Math.Max(res.PeakStates, next.Count);
                parents.Add(nextParent.ToArray());
                flips.Add(nextFlip.ToArray());
                var tmp = cur; cur = next; next = tmp;
            }
            return res;
        }
    }
}
