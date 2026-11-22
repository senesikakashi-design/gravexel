"""GRAVEXEL level builder.

Levels are assembled from hand-designed obstacle "chunks" joined by short corridors.
Run:  python make_levels.py ../Unity/Assets/Gravexel/Resources/levels.txt
Then validate (and fill the par times) with the C# solver:  mono LevelTool.exe validate <levels.txt> --write

Map legend (also documented at the top of levels.txt):
  #  solid wall        x  spike (points away from the wall it touches)
  o  orb (points)      *  star (3 per level)
  P  player start      E  exit (finish line)       C  checkpoint
  U  portal: gravity up     D  portal: gravity down     F  portal: flip gravity
  s  static saw        v  saw moving vertically    h  saw moving horizontally
  |  laser (phase A, vertical)   !  laser (phase B, vertical)
  -  laser (phase A, horizontal) ~  laser (phase B, horizontal)
  K  key               G  gate (opens once the key is taken)
  T  switch            a  block solid until switched   b  block solid after switching
  >  <  force running direction
"""
import sys

H = 12


class C:
    def __init__(self, w):
        self.w = w
        self.g = [['.'] * w for _ in range(H)]
        for x in range(w):
            self.g[0][x] = '#'
            self.g[H - 1][x] = '#'

    def put(self, x, y, ch):
        self.g[y][x] = ch
        return self

    def h(self, x0, x1, y, ch):
        for x in range(x0, x1 + 1):
            self.g[y][x] = ch
        return self

    def v(self, x, y0, y1, ch):
        for y in range(y0, y1 + 1):
            self.g[y][x] = ch
        return self


# ---------------------------------------------------------------- chunks

def spk_floor():
    return C(14).h(5, 7, 10, 'x').h(5, 7, 1, 'o').put(11, 10, '*')

def spk_ceil():
    return C(14).h(5, 7, 1, 'x').h(5, 7, 10, 'o').put(11, 1, '*')

def spk_floor_long():
    return C(18).h(4, 11, 10, 'x').h(4, 11, 1, 'o').put(14, 5, '*')

def spk_ceil_long():
    return C(18).h(4, 11, 1, 'x').h(4, 11, 10, 'o').put(14, 6, '*')

def spk_alt():
    return C(22).h(3, 6, 10, 'x').h(3, 6, 1, 'o').h(11, 14, 1, 'x').h(11, 14, 10, 'o').put(9, 5, '*')

def pit():
    return C(14).h(5, 8, 11, '.').h(5, 8, 1, 'o').put(11, 10, '*')

def pit_ceil():
    return C(14).h(5, 8, 0, '.').h(5, 8, 10, 'o').put(11, 1, '*')

def step_up():
    c = C(18)
    for y in range(7, 11):
        c.h(6, 12, y, '#')
    return c.h(7, 11, 6, 'o').put(15, 10, '*')

def step_down():
    c = C(18)
    for y in range(1, 5):
        c.h(6, 12, y, '#')
    return c.h(7, 11, 5, 'o').put(15, 1, '*')

def narrow():
    c = C(24)
    for y in range(1, 5):
        c.h(3, 20, y, '#')
    c.h(7, 9, 10, 'x').h(12, 14, 5, 'x').h(17, 19, 10, 'x')
    c.h(7, 9, 5, 'o').h(12, 14, 10, 'o').h(17, 19, 5, 'o')
    return c.put(22, 5, '*')

def tunnel():
    c = C(22)
    for y in (1, 2, 9, 10):
        c.h(4, 16, y, '#')
    c.h(8, 9, 8, 'x').h(13, 14, 3, 'x').h(8, 9, 3, 'o').h(13, 14, 8, 'o')
    return c.put(11, 6, '*')

def saw_mid():
    return C(22).put(8, 5, 's').put(8, 6, 's').h(15, 17, 10, 'x').h(15, 17, 1, 'o').put(4, 5, '*')

def saw_vert():
    return C(16).put(8, 2, 'v').h(5, 6, 1, 'o').h(10, 11, 10, 'o').put(13, 5, '*')

def saw_pair():
    return C(24).put(7, 2, 'v').put(16, 9, 'v').h(10, 13, 1, 'o').h(10, 13, 10, 'o').put(20, 6, '*')

def saw_patrol():
    c = C(20).put(3, 5, '#').put(15, 5, '#').put(9, 5, 'h')
    return c.h(7, 11, 10, 'x').h(7, 11, 1, 'o').put(18, 9, '*')

def portal_flip():
    return C(18).v(5, 1, 10, 'F').h(9, 13, 10, 'x').h(9, 13, 1, 'o').put(2, 5, '*')

def portal_up():
    return C(20).v(4, 1, 10, 'U').h(11, 14, 1, 'x').h(11, 14, 10, 'o').put(17, 2, '*')

def portal_down():
    return C(20).v(4, 1, 10, 'D').h(11, 14, 10, 'x').h(11, 14, 1, 'o').put(17, 9, '*')

def portal_combo():
    c = C(28).v(3, 1, 10, 'U').h(10, 13, 1, 'x').h(10, 13, 10, 'o')
    c.v(17, 1, 10, 'D').h(22, 25, 10, 'x').h(22, 25, 1, 'o')
    return c.put(15, 5, '*')

def key_gate():
    c = C(20).h(3, 6, 10, 'x').h(10, 13, 1, 'x').put(9, 5, 'K').v(17, 1, 10, 'G')
    return c.h(3, 6, 1, 'o').h(10, 13, 10, 'o').put(14, 9, '*')

def toggle():
    c = C(22).h(3, 5, 10, 'x').put(4, 1, 'T').v(10, 1, 10, 'a').v(16, 1, 5, 'b')
    return c.h(11, 13, 1, 'o').h(15, 17, 10, 'o').put(19, 9, '*')

def toggle2():
    c = C(26).h(3, 5, 10, 'x').put(4, 1, 'T').v(10, 1, 10, 'a')
    c.h(13, 15, 1, 'x').put(14, 10, 'T').v(19, 1, 10, 'b')
    return c.h(7, 8, 1, 'o').h(21, 23, 10, 'o').put(22, 5, '*')

def laser_halves():
    return C(16).v(8, 1, 5, '|').v(8, 6, 10, '!').h(4, 5, 1, 'o').h(11, 12, 10, 'o').put(13, 5, '*')

def laser_double():
    # pairs 9 tiles apart = half a laser cycle at running speed, so the safe side alternates
    c = C(24).v(6, 1, 5, '|').v(6, 6, 10, '!').v(15, 1, 5, '|').v(15, 6, 10, '!')
    return c.h(9, 12, 1, 'o').h(9, 12, 10, 'o').put(11, 5, '*')

def zigzag():
    c = C(30).h(2, 5, 10, 'x').h(9, 12, 1, 'x').h(16, 19, 10, 'x').h(23, 26, 1, 'x')
    c.h(2, 5, 1, 'o').h(9, 12, 10, 'o').h(16, 19, 1, 'o').h(23, 26, 10, 'o')
    return c.put(14, 5, '*')

def pit_mix():
    c = C(30).h(4, 7, 11, '.').h(11, 14, 1, 'x').h(18, 21, 0, '.').h(25, 27, 10, 'x')
    c.h(4, 7, 1, 'o').h(11, 14, 10, 'o').h(18, 21, 10, 'o').h(25, 27, 1, 'o')
    return c.put(16, 5, '*')


# ---------------------------------------------------------------- hard chunks (worlds 5-6)

def spk_overlap():
    return C(18).h(3, 8, 10, 'x').h(7, 12, 1, 'x').h(3, 6, 1, 'o').h(10, 12, 10, 'o').put(8, 5, '*')

def overlap_chain():
    c = C(28).h(3, 8, 10, 'x').h(7, 14, 1, 'x').h(13, 20, 10, 'x').h(19, 24, 1, 'x')
    c.h(3, 6, 1, 'o').h(10, 12, 10, 'o').h(16, 18, 1, 'o').h(22, 24, 10, 'o')
    return c.put(14, 6, '*')

def saw_gauntlet():
    return C(26).put(6, 2, 'v').put(12, 9, 'v').put(18, 5, 'v').h(8, 10, 1, 'o').h(14, 16, 10, 'o').put(22, 5, '*')

def laser_triple():
    c = C(28).v(4, 1, 5, '|').v(4, 6, 10, '!').v(13, 1, 5, '|').v(13, 6, 10, '!').v(22, 1, 5, '|').v(22, 6, 10, '!')
    return c.h(7, 9, 1, 'o').h(16, 18, 10, 'o').put(25, 5, '*')

def portal_maze():
    c = C(32).v(4, 1, 10, 'F').h(9, 12, 1, 'x').v(16, 1, 10, 'F').h(21, 24, 1, 'x').v(28, 1, 10, 'F')
    return c.h(9, 12, 10, 'o').h(21, 24, 10, 'o').put(14, 5, '*')

def toggle_bridge():
    c = C(24).h(2, 4, 10, 'x').put(3, 1, 'T').h(9, 15, 11, 'b').h(9, 15, 1, 'x')
    return c.h(9, 15, 10, 'o').put(20, 10, '*')

def narrow_saw():
    c = C(26)
    for y in range(1, 5):
        c.h(3, 22, y, '#')
    c.put(9, 7, 's').h(12, 14, 10, 'x').put(17, 8, 's').h(19, 21, 5, 'x')
    return c.h(12, 14, 5, 'o').h(19, 21, 10, 'o').put(24, 5, '*')

def tunnel_laser():
    c = C(24)
    for y in (1, 2, 9, 10):
        c.h(4, 19, y, '#')
    c.v(11, 3, 5, '|').v(11, 6, 8, '!').h(15, 16, 8, 'x').h(15, 16, 3, 'o')
    return c.put(8, 6, '*')

def key_saw():
    c = C(24).h(3, 6, 10, 'x').h(11, 14, 1, 'x').put(9, 5, 'K').put(17, 9, 'v').v(21, 1, 10, 'G')
    return c.h(3, 6, 1, 'o').h(11, 14, 10, 'o').put(19, 5, '*')

def zig_tight():
    c = C(30)
    for y in range(1, 5):
        c.h(2, 27, y, '#')
    c.h(5, 7, 10, 'x').h(10, 12, 5, 'x').h(15, 17, 10, 'x').h(20, 22, 5, 'x').h(25, 26, 10, 'x')
    c.h(5, 7, 5, 'o').h(10, 12, 10, 'o').h(15, 17, 5, 'o').h(20, 22, 10, 'o')
    return c.put(28, 6, '*')

def portal_saw():
    c = C(26).v(3, 1, 10, 'U').put(10, 5, 'v').v(16, 1, 10, 'D').h(20, 23, 10, 'x')
    return c.h(6, 8, 1, 'o').h(20, 23, 1, 'o').put(12, 5, '*')

def pit_overlap():
    c = C(24).h(4, 9, 11, '.').h(8, 13, 0, '.')
    return c.h(4, 7, 1, 'o').h(11, 13, 10, 'o').put(9, 5, '*')

HARD = [spk_overlap, overlap_chain, saw_gauntlet, laser_triple, portal_maze, toggle_bridge, narrow_saw,
        tunnel_laser, key_saw, zig_tight, portal_saw, pit_overlap]

CHUNKS = {f.__name__: f for f in [
    spk_floor, spk_ceil, spk_floor_long, spk_ceil_long, spk_alt, pit, pit_ceil, step_up, step_down,
    narrow, tunnel, saw_mid, saw_vert, saw_pair, saw_patrol, portal_flip, portal_up, portal_down,
    portal_combo, key_gate, toggle, toggle2, laser_halves, laser_double, zigzag, pit_mix] + HARD}


def connector(i, checkpoint, extra=0):
    c = C(7 + extra)
    if i % 2 == 0:
        c.h(2, 4, 10, 'o')
    else:
        c.h(2, 4, 1, 'o')
    if checkpoint:
        c.put(1, 10, 'C')
    return c


def start():
    c = C(8)
    for y in range(H):
        c.put(0, y, '#')
    return c.put(2, 10, 'P')


def finish():
    c = C(10)
    for y in range(H):
        c.put(9, y, '#')
    return c.put(4, 5, 'E')


# ---------------------------------------------------------------- recipes

RECIPES = [
    # World 1 - NEON DOCKS: spikes, pits, walls
    ("First Flip", 1, "Press SPACE or CLICK to flip gravity", ["spk_floor", "spk_floor"]),
    ("Upside Down", 1, "Flip back down before the ceiling spikes", ["spk_ceil", "spk_floor", "spk_ceil"]),
    ("Mind the Gap", 1, "Falling out of the world is fatal - flip over the holes", ["pit", "spk_floor", "pit_ceil"]),
    ("Back and Forth", 1, "", ["spk_alt", "pit", "spk_ceil_long"]),
    ("Stairway", 1, "Walls turn you around - pass them on the other side", ["step_up", "spk_floor", "step_down"]),
    ("Zig Zag", 1, "", ["zigzag", "pit", "spk_alt"]),
    ("Tight Squeeze", 1, "Low ceilings mean faster flips", ["narrow", "spk_floor_long", "pit_mix"]),
    ("Dock Run", 1, "", ["spk_alt", "step_up", "zigzag", "narrow", "pit_mix"]),
    # World 2 - CIRCUIT CITY: portals and saws
    ("Portals", 2, "Glowing gates switch your gravity for you", ["portal_flip", "spk_floor", "portal_up"]),
    ("Spinning Blades", 2, "Saws cut through the air - time your flips", ["saw_mid", "spk_alt", "saw_vert"]),
    ("Down Under", 2, "", ["portal_down", "saw_vert", "pit_mix"]),
    ("Patrol", 2, "", ["saw_patrol", "portal_flip", "zigzag"]),
    ("Crossfire", 2, "", ["saw_pair", "portal_combo", "spk_ceil"]),
    ("Circuit Breaker", 2, "", ["narrow", "saw_vert", "portal_up", "spk_ceil_long"]),
    ("Grinder", 2, "", ["saw_patrol", "saw_pair", "step_up", "portal_down"]),
    ("City Lights", 2, "", ["portal_combo", "saw_mid", "zigzag", "saw_pair", "pit_mix"]),
    # World 3 - LASER CORE: lasers, keys, switches
    ("Laser Grid", 3, "Lasers blink on and off - stay on the dark side", ["laser_halves", "spk_floor", "laser_halves"]),
    ("Keymaster", 3, "Grab the key in mid-air to open the golden gate", ["key_gate", "laser_halves"]),
    ("Switchboard", 3, "Switches swap the coloured blocks", ["toggle", "spk_alt"]),
    ("Double Trouble", 3, "", ["laser_double", "key_gate", "pit"]),
    ("Flip Flop", 3, "", ["toggle2", "laser_halves", "zigzag"]),
    ("Overload", 3, "", ["laser_double", "saw_vert", "toggle"]),
    ("Containment", 3, "", ["key_gate", "narrow", "laser_double", "portal_combo"]),
    ("Meltdown", 3, "", ["toggle2", "saw_pair", "laser_double", "key_gate", "pit_mix"]),
    # World 4 - VOID SPIRE: everything, harder
    ("Into the Void", 4, "Thread the needle - flip at just the right moment", ["tunnel", "spk_alt"]),
    ("Echoes", 4, "", ["portal_combo", "tunnel", "laser_halves"]),
    ("Clockwork", 4, "", ["saw_pair", "toggle2", "saw_patrol"]),
    ("Labyrinth", 4, "", ["key_gate", "tunnel", "toggle", "zigzag"]),
    ("Event Horizon", 4, "", ["laser_double", "portal_combo", "saw_patrol", "narrow"]),
    ("Gravity Well", 4, "", ["tunnel", "pit_mix", "saw_pair", "toggle2"]),
    ("Singularity", 4, "", ["zigzag", "laser_double", "key_gate", "tunnel", "portal_combo"]),
    ("The Spire", 4, "", ["toggle2", "saw_patrol", "tunnel", "laser_double", "key_gate", "pit_mix"]),
    # World 5 - QUANTUM FORGE: overlapping hazards, mid-air timing
    ("Overlap", 5, "Some spikes overlap - cross that gap in mid-air", ["spk_overlap", "spk_alt", "spk_overlap"]),
    ("Phase Shift", 5, "", ["laser_triple", "pit_overlap", "zigzag"]),
    ("Saw Garden", 5, "", ["saw_gauntlet", "spk_overlap", "saw_pair"]),
    ("Triple Beam", 5, "", ["laser_triple", "tunnel_laser", "laser_double"]),
    ("Bridgeworks", 5, "Hit the switch to build a bridge", ["toggle_bridge", "overlap_chain", "pit_mix"]),
    ("Needle Run", 5, "", ["zig_tight", "narrow_saw", "spk_overlap"]),
    ("Half Life", 5, "", ["portal_maze", "laser_triple", "pit_overlap", "saw_vert"]),
    ("Resonance", 5, "", ["overlap_chain", "saw_gauntlet", "toggle_bridge"]),
    ("Fission", 5, "", ["key_saw", "tunnel_laser", "zig_tight", "portal_saw"]),
    ("Feedback Loop", 5, "", ["portal_maze", "narrow_saw", "overlap_chain", "laser_triple"]),
    ("Overclock", 5, "", ["saw_gauntlet", "zig_tight", "key_saw", "pit_overlap", "tunnel"]),
    ("Forge Master", 5, "", ["overlap_chain", "toggle_bridge", "laser_triple", "saw_gauntlet", "portal_maze"]),
    # World 6 - OMEGA RIFT: the hardest combinations, long levels
    ("Rift Walk", 6, "", ["pit_overlap", "portal_saw", "overlap_chain", "tunnel_laser"]),
    ("Zero Point", 6, "", ["zig_tight", "laser_triple", "spk_overlap", "saw_gauntlet"]),
    ("Inversion", 6, "", ["portal_maze", "portal_saw", "portal_combo", "overlap_chain"]),
    ("Dark Matter", 6, "", ["tunnel", "tunnel_laser", "narrow_saw", "key_saw"]),
    ("Collapse", 6, "", ["toggle_bridge", "saw_gauntlet", "pit_overlap", "laser_triple", "zig_tight"]),
    ("Paradox", 6, "", ["overlap_chain", "portal_maze", "key_saw", "spk_overlap", "narrow_saw"]),
    ("Entropy", 6, "", ["laser_triple", "saw_gauntlet", "tunnel_laser", "pit_overlap", "portal_saw"]),
    ("Supernova", 6, "", ["zig_tight", "overlap_chain", "toggle2", "laser_triple", "tunnel"]),
    ("Quasar", 6, "", ["key_saw", "portal_maze", "saw_gauntlet", "narrow_saw", "overlap_chain"]),
    ("Event Zero", 6, "", ["tunnel_laser", "pit_overlap", "portal_saw", "zig_tight", "laser_triple", "spk_overlap"]),
    ("Omega", 6, "", ["overlap_chain", "saw_gauntlet", "toggle_bridge", "tunnel", "portal_maze", "key_saw"]),
    ("GRAVEXEL", 6, "The final test. Defy the gravity.", ["zig_tight", "laser_triple", "overlap_chain", "saw_gauntlet", "tunnel_laser", "portal_maze", "pit_overlap"]),
]


def assemble(chunks):
    parts = [start()]
    # Lasers blink every 1.2 s and the runner covers 9 tiles in that time. On the first run the
    # crossing time of a column is fixed by its x position, so laser chunks are shifted (by widening
    # the corridor before them) until their first beam is met in the middle of a phase.
    pending = None
    for i, name in enumerate(chunks):
        chunk = CHUNKS[name]()
        laser_cols = [x for x in range(chunk.w) for y in range(H) if chunk.g[y][x] in '|!']
        if laser_cols and pending is not None:
            x0 = sum(p.w for p in parts) + pending[2]
            pad = (4 - (x0 + 7 + min(laser_cols) - 2)) % 9
            pending = (pending[0], pending[1], pad)
        if pending is not None:
            parts.append(connector(pending[0], pending[1], pending[2]))
        elif laser_cols:
            x0 = sum(p.w for p in parts)
            pad = (4 - (x0 + min(laser_cols) - 2)) % 9
            if pad:
                parts.append(connector(99, False, pad - 7) if pad >= 7 else C(pad))
        parts.append(chunk)
        pending = (i, len(chunks) >= 3 and i % 2 == 1 and i < len(chunks) - 1, 0)
    parts.append(connector(pending[0], False, pending[2]))
    parts.append(finish())
    rows = [''.join(''.join(p.g[y]) for p in parts) for y in range(H)]
    # keep exactly three stars, spread along the level
    stars = [(x, y) for y in range(H) for x in range(len(rows[0])) if rows[y][x] == '*']
    stars.sort()
    if len(stars) < 3:
        # top up with mid-air stars in the joining corridors
        grid = [list(r) for r in rows]
        x = 0
        for p in parts:
            if len(stars) >= 3:
                break
            if p.w >= 7 and grid[5][x + 3] == '.' and grid[0][x] == '#' and grid[1][x] == '.':
                grid[5][x + 3] = '*'
                stars.append((x + 3, 5))
            x += p.w
        rows = [''.join(r) for r in grid]
        stars.sort()
    if len(stars) > 3:
        keep = {stars[0], stars[len(stars) // 2], stars[-1]}
        grid = [list(r) for r in rows]
        for (x, y) in stars:
            if (x, y) not in keep:
                grid[y][x] = 'o'
        rows = [''.join(r) for r in grid]
    return rows


HEADER = """# GRAVEXEL levels  (generated by Tools/make_levels.py, validated by Tools/LevelTool)
#
# Each level: 'level', 'name', 'world', 'par' (seconds for the time bonus), optional 'hint', then 'map:' rows and 'end'.
# Legend:
#   #  wall          x  spike        o  orb          *  star (3 per level)
#   P  start         E  exit line    C  checkpoint
#   U/D/F  gravity portal: up / down / flip
#   s/v/h  saw: static / vertical / horizontal
#   | !  vertical laser (phase A / B)     - ~  horizontal laser (phase A / B)
#   K  key   G  gate   T  switch   a/b  switch blocks   > <  force direction
"""


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "levels.txt"
    blocks = [HEADER]
    for n, (name, world, hint, chunks) in enumerate(RECIPES, start=1):
        rows = assemble(chunks)
        blocks.append("\nlevel: %d\nname: %s\nworld: %d\npar: 0.0\n" % (n, name, world))
        if hint:
            blocks.append("hint: %s\n" % hint)
        blocks.append("map:\n" + "\n".join(rows) + "\nend\n")
    with open(out, "w") as f:
        f.write("".join(blocks))
    print("wrote %d levels to %s" % (len(RECIPES), out))


if __name__ == "__main__":
    main()
