"""GRAVEXEL brand builder: final logo (Pixel Hero), lockups, app icons, store art.
Requires: fonttools, cairosvg, pillow.   Run: python make_brand.py"""
import os, io
import cairosvg
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.join(HERE, '..', 'Unity', 'Assets', 'Gravexel', 'Resources', 'Fonts')
TITLE_TTF = os.path.join(FONTS, 'Audiowide-Regular.ttf')
UI_TTF = os.path.join(FONTS, 'Rajdhani-Bold.ttf')
OUT = os.path.join(HERE, 'out')

CYAN, MAGENTA, WHITE, NAVY, INK = '#22E8FF', '#FF3DDB', '#FFFFFF', '#0A1238', '#050816'

# ------------------------------------------------------------------ the mark: Pixel Hero
# The game's hero as 6x5 pixels, eyes cut out (the ground shows through), looking right.
HERO = ['.XXXX.',
        'XXXXXX',
        'XX.X.X',
        'XXXXXX',
        '.XXXX.']

def hero_paths(color=CYAN, unit=40, ox=8, oy=28):
    """Pixel Hero on the 256 grid (6x5 cells of 40 = 240x200, centred)."""
    d = ''
    for j, row in enumerate(HERO):
        for i, ch in enumerate(row):
            if ch == 'X':
                x, y = ox + i * unit, oy + j * unit
                d += 'M%d %dH%dV%dH%dZ' % (x, y, x + unit, y + unit, x)
    return [(d, color)]

# kept for the concept/option boards
def mark_c(top=CYAN, bottom=MAGENTA, pixel=WHITE):
    t = 'M46 24 L88 24 L128 64 L168 24 L210 24 L128 106 Z'
    b = 'M46 232 L88 232 L128 192 L168 232 L210 232 L128 150 Z'
    p = 'M128 113 L143 128 L128 143 L113 128 Z'
    return [(t, top), (b, bottom), (p, pixel)]

def mark_a(c1=CYAN, c2=MAGENTA):
    top = 'M100 40 H156 A18 18 0 0 1 174 58 V102 A18 18 0 0 1 156 120 H100 A18 18 0 0 1 82 102 V58 A18 18 0 0 1 100 40 Z'
    line = 'M32 124 H224 V134 H32 Z'
    bot = ('M100 138 H156 A18 18 0 0 1 174 156 V198 A18 18 0 0 1 156 216 H100 A18 18 0 0 1 82 198 V156 A18 18 0 0 1 100 138 Z '
           'M106 160 V194 H150 V160 Z')
    return [(top, c1), (line, WHITE), (bot, c2)]

def mark_b(c1=CYAN, c2=MAGENTA):
    up = 'M20 128 A96 96 0 0 1 196 80 L164 98 A60 60 0 0 0 56 128 Z'
    lo = 'M236 128 A96 96 0 0 1 44 128 L80 128 A60 60 0 0 0 200 128 Z'
    bar = 'M140 116 H236 V144 H140 Z'
    return [(up, c1), (lo, c2), (bar, c2)]

def svg(paths, w=256, h=256, mono=None, bg=None, extra=''):
    body = ''.join('<path d="%s" fill="%s" fill-rule="evenodd"/>' % (d, mono or c) for d, c in paths)
    bgr = '<rect width="%d" height="%d" fill="%s"/>' % (w, h, bg) if bg else ''
    return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" width="%d" height="%d">%s%s%s</svg>' % (w, h, w, h, bgr, extra, body)

# ------------------------------------------------------------------ the final mark: the X
# Two chevrons pulling toward each other: cyan from the ceiling, magenta from the floor.
X_TOP = 'M46 39 L88 39 L128 79 L168 39 L210 39 L128 121 Z'
X_BOT = 'M46 217 L88 217 L128 175 L168 217 L210 217 L128 135 Z'

def x_paths(top=CYAN, bottom=MAGENTA, mono=None):
    return [(X_TOP, mono or top), (X_BOT, mono or bottom)]

def hero_group(x, y, size, color=None, mono=None):
    """The X mark (art box x 46..210, y 39..217) scaled to 'size' wide, top-left at (x, y)."""
    s = size / 164.0
    return '<g transform="translate(%.2f %.2f) scale(%.4f) translate(-46 -39)">%s</g>' % (
        x, y, s, ''.join('<path d="%s" fill="%s"/>' % (d, c) for d, c in x_paths(mono=mono)))

# ------------------------------------------------------------------ type outlines

def text_paths(ttf, text, size, x, y, tracking=0.0):
    f = TTFont(ttf); gs = f.getGlyphSet(); cmap = f.getBestCmap(); upm = f['head'].unitsPerEm
    k = size / upm; out = []; pen_x = x
    for ch in text:
        g = cmap[ord(ch)]
        if ch != ' ':
            sp = SVGPathPen(gs)
            gs[g].draw(TransformPen(sp, (k, 0, 0, -k, pen_x, y)))
            out.append(sp.getCommands())
        pen_x += gs[g].width * k + tracking * size
    return out, pen_x

def word_paths(size, mono=None):
    """The logo: GRAV (cyan) E (white) + the X made of the two chevrons + EL (white).
    Baseline at y=size; returns (svg body, width)."""
    f = TTFont(TITLE_TTF); gs = f.getGlyphSet(); cmap = f.getBestCmap(); k = size / f['head'].unitsPerEm
    body, pen = '', 0.0
    for i, ch in enumerate('GRAVEXEL'):
        g = gs[cmap[ord(ch)]]; adv = g.width * k
        if ch == 'X':
            cap = size * 0.70; w = adv * 1.0
            sx = w / 164.0; sy = cap / 178.0                       # artwork x 46..210, y 39..217
            ox = pen + (adv - w) / 2 - 46 * sx; oy = size - cap - 39 * sy
            for d, col in x_paths(mono=mono):
                body += '<g transform="translate(%.2f %.2f) scale(%.4f %.4f)"><path d="%s" fill="%s"/></g>' % (ox, oy, sx, sy, d, col)
        else:
            sp = SVGPathPen(gs); g.draw(TransformPen(sp, (k, 0, 0, -k, pen, size)))
            body += '<path d="%s" fill="%s"/>' % (sp.getCommands(), mono or (CYAN if i < 4 else WHITE))
        pen += adv + 0.02 * size
    return body, pen

def lockup_svg(kind='full', mono=None, bg=None):
    """full: the logo + tagline + slogan, centred; word: the logo alone."""
    size = 200
    word, ww = word_paths(size, mono)
    if kind == 'word':
        W, H = int(ww + 40), int(size + 30)
        g = '<g transform="translate(20 10)">%s</g>' % word
    else:
        W = int(max(ww, 1100) + 120); H = int(size + 230)
        g = '<g transform="translate(%.1f 20)">%s</g>' % ((W - ww) / 2, word)
        tp, tend = text_paths(TITLE_TTF, 'DEFY THE GRAVITY', 64, 0, 0, 0.12)
        g += '<g transform="translate(%.1f %.1f)">%s</g>' % ((W - tend) / 2, size + 110, ''.join('<path d="%s" fill="%s"/>' % (p, mono or MAGENTA) for p in tp))
        sp, send = text_paths(UI_TTF, 'A NEW DIMENSION OF MOVEMENT', 44, 0, 0, 0.18)
        g += '<g transform="translate(%.1f %.1f)">%s</g>' % ((W - send) / 2, size + 185, ''.join('<path d="%s" fill="%s" fill-opacity="%s"/>' % (p, mono or WHITE, '1' if mono else '0.8') for p in sp))
    bgr = '<rect width="%d" height="%d" fill="%s"/>' % (W, H, bg) if bg else ''
    return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" width="%d" height="%d">%s%s</svg>' % (W, H, W, H, bgr, g)

# ------------------------------------------------------------------ app icon

def icon_svg(size=1024, rounded=False):
    s = size / 1024.0
    defs = ('<defs><linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#0A1238"/><stop offset="1" stop-color="#25093D"/></linearGradient>'
            '<radialGradient id="gc" cx="0.5" cy="0.3" r="0.5"><stop offset="0" stop-color="%s" stop-opacity="0.45"/><stop offset="1" stop-color="%s" stop-opacity="0"/></radialGradient>'
            '<radialGradient id="gm" cx="0.5" cy="0.72" r="0.5"><stop offset="0" stop-color="%s" stop-opacity="0.55"/><stop offset="1" stop-color="%s" stop-opacity="0"/></radialGradient></defs>') % (CYAN, CYAN, MAGENTA, MAGENTA)
    r = 'rx="%d"' % (230 * s) if rounded else ''
    body = '<rect width="%d" height="%d" %s fill="url(#bg)"/>' % (size, size, r)
    body += '<rect width="%d" height="%d" %s fill="url(#gm)"/><rect width="%d" height="%d" %s fill="url(#gc)"/>' % (size, size, r, size, size, r)
    hs = size * 0.46
    body += hero_group((size - hs) / 2, (size - hs * 178 / 164) / 2, hs)
    return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" width="%d" height="%d">%s%s</svg>' % (size, size, size, size, defs, body)

def png(svgtext, path, w=None, h=None):
    cairosvg.svg2png(bytestring=svgtext.encode(), write_to=path, output_width=w, output_height=h)

def to_img(svgtext, w=None, h=None):
    return Image.open(io.BytesIO(cairosvg.svg2png(bytestring=svgtext.encode(), output_width=w, output_height=h))).convert('RGBA')

# ------------------------------------------------------------------ builds

def main():
    os.makedirs(OUT, exist_ok=True)
    for sub in ('logo', 'icons/android', 'icons/ios', 'store', 'game'):
        os.makedirs(os.path.join(OUT, sub), exist_ok=True)
    L = os.path.join(OUT, 'logo')
    for f in os.listdir(L):
        if f.startswith('gravexel-'): os.remove(os.path.join(L, f))
    hero = x_paths()
    files = {
        'gravexel-icon-mark.svg': svg(hero),
        'gravexel-icon-mark-black.svg': svg(hero, mono='#000000'),
        'gravexel-icon-mark-white.svg': svg(hero, mono='#FFFFFF'),
        'gravexel-logo-full-dark.svg': lockup_svg('full', bg=INK),
        'gravexel-logo-full.svg': lockup_svg('full'),
        'gravexel-logo.svg': lockup_svg('word'),
        'gravexel-logo-white.svg': lockup_svg('word', mono='#FFFFFF'),
        'gravexel-logo-black.svg': lockup_svg('word', mono='#000000'),
    }
    for n, s in files.items():
        open(os.path.join(L, n), 'w').write(s)
        png(s, os.path.join(L, n.replace('.svg', '.png')), *((1024, 1024) if n.startswith('gravexel-icon-mark') else (None, None)))
    icon = icon_svg(1024)
    open(os.path.join(OUT, 'icons', 'gravexel-icon-1024.svg'), 'w').write(icon)
    for n, sz in [('ic_launcher-mdpi', 48), ('ic_launcher-hdpi', 72), ('ic_launcher-xhdpi', 96), ('ic_launcher-xxhdpi', 144), ('ic_launcher-xxxhdpi', 192), ('play-store-512', 512)]:
        png(icon, os.path.join(OUT, 'icons/android', n + '.png'), sz, sz)
    for sz in (1024, 180, 167, 152, 120, 87, 80, 76, 60, 58, 40, 29, 20):
        png(icon, os.path.join(OUT, 'icons/ios', 'AppIcon-%d.png' % sz), sz, sz)
    png(icon_svg(1024, rounded=True), os.path.join(OUT, 'icons', 'gravexel-icon-rounded-1024.png'), 1024, 1024)
    png(icon_svg(1024, rounded=True), os.path.join(OUT, 'game', 'icon256.png'), 256, 256)
    store(os.path.join(OUT, 'store'))

def scene(w, h):
    bg = ('<defs><linearGradient id="s" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#050A1C"/><stop offset="1" stop-color="#2A0B45"/></linearGradient>'
          '<radialGradient id="g" cx="0.5" cy="0.45" r="0.55"><stop offset="0" stop-color="%s" stop-opacity="0.3"/><stop offset="1" stop-color="%s" stop-opacity="0"/></radialGradient></defs>'
          '<rect width="%d" height="%d" fill="url(#s)"/><rect width="%d" height="%d" fill="url(#g)"/>') % (MAGENTA, MAGENTA, w, h, w, h)
    for i in range(0, w, 60):
        bg += '<rect x="%d" y="0" width="1" height="%d" fill="#FFFFFF" fill-opacity="0.035"/>' % (i, h)
    for yy, col in ((h * 0.1, CYAN), (h * 0.9, MAGENTA)):
        bg += '<rect x="0" y="%.0f" width="%d" height="5" fill="%s"/><rect x="0" y="%.0f" width="%d" height="40" fill="%s" fill-opacity="0.12"/>' % (yy, w, col, yy - 18, w, col)
    return bg

def embed(svgtext, x, y, scale):
    inner = svgtext[svgtext.index('>') + 1:svgtext.rindex('</svg>')]
    return '<g transform="translate(%.1f %.1f) scale(%.3f)">%s</g>' % (x, y, scale, inner)

def store(d):
    lg = lockup_svg('full'); lw = float(lg.split('width="')[1].split('"')[0]); lh = float(lg.split('height="')[1].split('"')[0])
    def page(w, h, art):
        return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" width="%d" height="%d">%s%s</svg>' % (w, h, w, h, scene(w, h), art)
    for name, w, h, frac in [('play-feature-graphic-1024x500', 1024, 500, 0.8), ('splash-portrait-1242x2688', 1242, 2688, 0.85),
                             ('splash-landscape-2688x1242', 2688, 1242, 0.6), ('cover-1920x1080', 1920, 1080, 0.7)]:
        sc = min(w * frac / lw, h * 0.6 / lh)
        art = embed(lg, (w - lw * sc) / 2, (h - lh * sc) / 2, sc)
        if h > w:   # portrait splash: the app icon mark above the logo
            ms = w * 0.42
            art = hero_group((w - ms) / 2, (h - lh * sc) / 2 - ms * 178 / 164 - 140, ms) + art
        png(page(w, h, art), os.path.join(d, name + '.png'))

if __name__ == '__main__':
    main()
    print('brand assets written to', OUT)
