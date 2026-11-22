"""Five visual-identity options for GRAVEXEL, one PNG board each + an overview."""
import os, io, cairosvg
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import make_brand as mb

HERE = os.path.dirname(os.path.abspath(__file__)); OPT = os.path.join(HERE, 'options')
F = lambda n: os.path.join(OPT, n)

def svgimg(body, size, vb=256):
    s = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" width="%d" height="%d">%s</svg>' % (vb, vb, size, size, body)
    return Image.open(io.BytesIO(cairosvg.svg2png(bytestring=s.encode()))).convert('RGBA')

def p(d, c): return '<path d="%s" fill="%s" fill-rule="evenodd"/>' % (d, c)

# ---------------- marks (256 grid) ----------------
def m_gravity_x(c): return ''.join(p(d, col) for (d, _), col in zip(mb.mark_c(), (c[0], c[1], c[2])))

def m_pixel_mirror(c):
    u = 22; out = ''
    top = ['.XXXX.', 'XXXXXX', 'XX.X.X', 'XXXXXX', '.XXXX.']
    x0, y0 = 128 - 3 * u, 18
    for j, row in enumerate(top):
        for i, ch in enumerate(row):
            if ch == 'X': out += '<rect x="%d" y="%d" width="%d" height="%d" fill="%s"/>' % (x0 + i * u, y0 + j * u, u, u, c[0])
    out += '<rect x="20" y="%d" width="216" height="10" fill="%s"/>' % (y0 + 5 * u + 6, c[2])
    yb = y0 + 5 * u + 22
    for j, row in enumerate(reversed(top)):
        for i, ch in enumerate(row):
            if ch == 'X' and (j in (0, 4) or i in (0, 5) or (j in (1, 3) and i in (1, 4))):
                out += '<rect x="%d" y="%d" width="%d" height="%d" fill="%s"/>' % (x0 + i * u, yb + j * u, u, u, c[1])
    return out

def m_horizon(c):
    # a ring cut on its horizon; the upper half slides left, the lower half right
    top = 'M18 122 A96 96 0 0 1 210 122 L176 122 A62 62 0 0 0 52 122 Z'
    bot = 'M46 134 A96 96 0 0 0 238 134 L204 134 A62 62 0 0 1 80 134 Z'
    line = 'M8 125 H248 V131 H8 Z'
    return p(top, c[0]) + p(bot, c[1]) + p(line, c[2]) + p('M128 108 L148 128 L128 148 L108 128 Z', c[2])

def m_shift_g(c): return ''.join(p(d, col) for (d, _), col in zip(mb.mark_b(), (c[0], c[1], c[1])))

def m_orbit(c):
    ring = 'M64 24 H192 A40 40 0 0 1 232 64 V192 A40 40 0 0 1 192 232 H64 A40 40 0 0 1 24 192 V64 A40 40 0 0 1 64 24 Z M70 58 A12 12 0 0 0 58 70 V186 A12 12 0 0 0 70 198 H186 A12 12 0 0 0 198 186 V70 A12 12 0 0 0 186 58 Z'
    dot = '<circle cx="128" cy="96" r="26" fill="%s"/>' % c[1]
    ghost = '<circle cx="128" cy="166" r="22" fill="none" stroke="%s" stroke-width="10"/>' % c[1]
    return p(ring, c[0]) + dot + ghost + '<rect x="84" y="128" width="88" height="6" rx="3" fill="%s"/>' % c[2]

OPTIONS = [
    dict(key='A', name='NEON GRAVITY', idea='Two chevrons pull toward one pixel - the X of GRAVEXEL. Neon synthwave, premium night look.',
         mark=m_gravity_x, mc=('#22E8FF', '#FF3DDB', '#FFFFFF'), bg='#050816', bg2='#25093D', fg='#FFFFFF', accent='#FF3DDB',
         palette=[('#22E8FF', 'NEON CYAN'), ('#FF3DDB', 'NEON MAGENTA'), ('#FFFFFF', 'PIXEL WHITE'), ('#0A1238', 'DEEP NAVY'), ('#050816', 'VOID INK')],
         title_font=mb.TITLE_TTF, ui_font=mb.UI_TTF, fonts='Audiowide + Rajdhani', split=4, c1='#22E8FF', c2='#FFFFFF'),
    dict(key='B', name='PIXEL FLIP', idea='A pixel hero and its flipped ghost under the horizon. Retro arcade, pixel-art charm.',
         mark=m_pixel_mirror, mc=('#B6FF3B', '#7A3CFF', '#FFFFFF'), bg='#120A24', bg2='#2A1452', fg='#FFFFFF', accent='#B6FF3B',
         palette=[('#B6FF3B', 'ACID LIME'), ('#7A3CFF', 'ELECTRIC VIOLET'), ('#FFFFFF', 'WHITE'), ('#2A1452', 'GRAPE'), ('#120A24', 'NIGHT')],
         title_font=F('Silkscreen-Bold.ttf'), ui_font=mb.UI_TTF, fonts='Silkscreen + Rajdhani', split=4, c1='#B6FF3B', c2='#FFFFFF'),
    dict(key='C', name='HORIZON SHIFT', idea='A world split on its horizon, the halves sliding apart. Minimal, premium, gold on black.',
         mark=m_horizon, mc=('#FFC83D', '#F2F2F2', '#FFC83D'), bg='#0B0B0F', bg2='#1C1A24', fg='#F2F2F2', accent='#FFC83D',
         palette=[('#FFC83D', 'SOLAR GOLD'), ('#F2F2F2', 'IVORY'), ('#8A8A99', 'STEEL'), ('#1C1A24', 'GRAPHITE'), ('#0B0B0F', 'BLACK')],
         title_font=F('Michroma-Regular.ttf'), ui_font=mb.UI_TTF, fonts='Michroma + Rajdhani', split=4, c1='#FFC83D', c2='#F2F2F2'),
    dict(key='D', name='SHIFT G', idea='A G cut on its horizon, displaced by a gravity glitch. Bold, sporty, energetic.',
         mark=m_shift_g, mc=('#00E1C8', '#FF6A3D', '#FF6A3D'), bg='#0D1B2A', bg2='#132F45', fg='#FFFFFF', accent='#FF6A3D',
         palette=[('#FF6A3D', 'BLAZE ORANGE'), ('#00E1C8', 'TURBO TEAL'), ('#FFFFFF', 'WHITE'), ('#132F45', 'DEEP SEA'), ('#0D1B2A', 'MIDNIGHT')],
         title_font=F('RussoOne-Regular.ttf'), ui_font=mb.UI_TTF, fonts='Russo One + Rajdhani', split=4, c1='#00E1C8', c2='#FF6A3D'),
    dict(key='E', name='ORBIT POP', idea='A friendly rounded frame where the dot flips to its ghost. Playful, bright, casual-mobile.',
         mark=m_orbit, mc=('#2B2D42', '#FF4F7B', '#4FC3FF'), bg='#FFF4E8', bg2='#FFE3CC', fg='#2B2D42', accent='#FF4F7B',
         palette=[('#FF4F7B', 'CORAL POP'), ('#4FC3FF', 'SKY'), ('#FFC93C', 'SUNNY'), ('#2B2D42', 'INK'), ('#FFF4E8', 'CREAM')],
         title_font=F('ZenDots-Regular.ttf'), ui_font=mb.UI_TTF, fonts='Zen Dots + Rajdhani', split=4, c1='#2B2D42', c2='#FF4F7B'),
]

def grad(w, h, a, b):
    im = Image.new('RGB', (w, h), a); d = ImageDraw.Draw(im)
    ca = tuple(int(a[i:i + 2], 16) for i in (1, 3, 5)); cb = tuple(int(b[i:i + 2], 16) for i in (1, 3, 5))
    for y in range(h):
        t = y / max(1, h - 1); d.line((0, y, w, y), fill=tuple(int(ca[k] + (cb[k] - ca[k]) * t) for k in range(3)))
    return im

def glow(im, color, box, radius=60, alpha=110):
    layer = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(layer)
    d.ellipse(box, fill=color + (alpha,)) if isinstance(color, tuple) else d.ellipse(box, fill=color)
    layer = layer.filter(ImageFilter.GaussianBlur(radius)); im.alpha_composite(layer)

def hexrgb(h): return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))

def fit_font(path, text, maxw, start):
    s = start
    while s > 20:
        f = ImageFont.truetype(path, s)
        if f.getlength(text) <= maxw: return f
        s -= 4
    return ImageFont.truetype(path, s)

def wordmark(d, o, x, y, maxw, start=150, center=True):
    f = fit_font(o['title_font'], 'GRAVEXEL', maxw, start)
    a, b = 'GRAV', 'EXEL'
    wa, wb = f.getlength(a), f.getlength(b)
    sx = x - (wa + wb) / 2 if center else x
    d.text((sx, y), a, font=f, fill=o['c1']); d.text((sx + wa, y), b, font=f, fill=o['c2'])
    return f.size

def app_icon(o, size):
    bg = grad(size, size, o['bg2'], o['bg']).convert('RGBA')
    glow(bg, hexrgb(o['accent']), (size * 0.2, size * 0.2, size * 0.8, size * 0.8), size // 8, 90)
    m = svgimg(o['mark'](o['mc']), int(size * 0.62)); bg.alpha_composite(m, (int(size * 0.19), int(size * 0.19)))
    mask = Image.new('L', (size, size), 0); ImageDraw.Draw(mask).rounded_rectangle((0, 0, size - 1, size - 1), radius=int(size * 0.225), fill=255)
    out = Image.new('RGBA', (size, size), (0, 0, 0, 0)); out.paste(bg, (0, 0), mask); return out

def board(o):
    W, H = 1920, 1080
    im = Image.new('RGBA', (W, H), '#111318'); d = ImageDraw.Draw(im)
    left = grad(1080, H, o['bg'], o['bg2']).convert('RGBA'); im.alpha_composite(left)
    sf = ImageFont.truetype(o['ui_font'], 30); st = 'A  N E W   D I M E N S I O N   O F   M O V E M E N T'
    if o.get('layout') == 'horizontal':
        glow(im, hexrgb(o['accent']), (40, 350, 420, 730), 100, 70)
        mk = svgimg(o['mark'](o['mc']), 300); im.alpha_composite(mk, (70, 390))
        d = ImageDraw.Draw(im)
        d.rectangle((392, 410, 396, 670), fill=o['c1'])
        wordmark(d, o, 430, 420, 600, 130, center=False)
        tf = fit_font(o['title_font'], 'DEFY THE GRAVITY', 600, 50)
        d.text((432, 580), 'DEFY THE GRAVITY', font=tf, fill=o['accent'])
        sf2 = ImageFont.truetype(o['ui_font'], 24); st2 = 'A NEW DIMENSION OF MOVEMENT'
        d.text((434, 648), ' '.join(st2), font=sf2, fill=o['fg'])
    elif o.get('layout') == 'badge':
        glow(im, hexrgb(o['accent']), (290, 120, 790, 620), 120, 80)
        mk = svgimg(o['mark'](o['mc']), 420); im.alpha_composite(mk, (330, 110))
        d = ImageDraw.Draw(im)
        wordmark(d, o, 540, 600, 760, 120)
        tf = fit_font(o['title_font'], 'DEFY THE GRAVITY', 560, 44); tw = tf.getlength('DEFY THE GRAVITY')
        d.rectangle((540 - tw / 2 - 30, 778, 540 + tw / 2 + 30, 842), outline=o['accent'], width=3)
        d.text((540 - tw / 2, 786), 'DEFY THE GRAVITY', font=tf, fill=o['accent'])
        d.text((540 - sf.getlength(st) / 2, 880), st, font=sf, fill=o['fg'])
    else:
        glow(im, hexrgb(o['accent']), (290, 140, 790, 640), 120, 70)
        mk = svgimg(o['mark'](o['mc']), 380); im.alpha_composite(mk, (350, 150))
        d = ImageDraw.Draw(im)
        wordmark(d, o, 540, 590, 900)
        tf = fit_font(o['title_font'], 'DEFY THE GRAVITY', 700, 52); tw = tf.getlength('DEFY THE GRAVITY')
        d.text((540 - tw / 2, 790), 'DEFY THE GRAVITY', font=tf, fill=o['accent'])
        d.text((540 - sf.getlength(st) / 2, 870), st, font=sf, fill=o['fg'])
    # right panel
    R = 1080; d.rectangle((R, 0, W, H), fill='#16181F')
    hf = ImageFont.truetype(mb.TITLE_TTF, 30); nf = ImageFont.truetype(mb.TITLE_TTF, 54); bf = ImageFont.truetype(mb.UI_TTF, 30)
    d.text((R + 60, 50), o.get('label', 'OPTION ' + o['key']), font=hf, fill='#8A90A8')
    d.text((R + 60, 92), o['name'], font=nf, fill='#FFFFFF')
    words, line, y = o['idea'].split(), '', 170
    for wd in words:
        t = (line + ' ' + wd).strip()
        if bf.getlength(t) > 720: d.text((R + 60, y), line, font=bf, fill='#C9CCD8'); y += 38; line = wd
        else: line = t
    d.text((R + 60, y), line, font=bf, fill='#C9CCD8')
    # app icon + sizes
    lf = ImageFont.truetype(mb.UI_TTF, 24)
    d.text((R + 60, 290), 'APP ICON', font=lf, fill='#8A90A8')
    ic = app_icon(o, 240); im.alpha_composite(ic, (R + 60, 330))
    x = R + 340
    for s in (96, 56, 32):
        im.alpha_composite(app_icon(o, s), (x, 330 + (240 - s) // 2)); x += s + 30
    # phone splash mockup
    ph = Image.new('RGBA', (170, 340), (0, 0, 0, 0))
    scr = grad(150, 320, o['bg'], o['bg2']).convert('RGBA')
    scr.alpha_composite(svgimg(o['mark'](o['mc']), 90), (30, 90))
    sd = ImageDraw.Draw(scr); wf = fit_font(o['title_font'], 'GRAVEXEL', 120, 30)
    sd.text((75 - wf.getlength('GRAVEXEL') / 2, 205), 'GRAVEXEL', font=wf, fill=o['c1'])
    pd = ImageDraw.Draw(ph); pd.rounded_rectangle((0, 0, 169, 339), 26, fill='#2A2D38')
    m = Image.new('L', (150, 320), 0); ImageDraw.Draw(m).rounded_rectangle((0, 0, 149, 319), 20, fill=255)
    ph.paste(scr, (10, 10), m); im.alpha_composite(ph, (W - 230, 290))
    # palette
    d = ImageDraw.Draw(im)
    d.text((R + 60, 610), 'COLOURS', font=lf, fill='#8A90A8')
    sw = 136
    for i, (hx, nm) in enumerate(o['palette']):
        x0 = R + 60 + i * (sw + 12)
        d.rectangle((x0, 650, x0 + sw, 780), fill=hx, outline='#2E3240')
        sf2 = ImageFont.truetype(mb.UI_TTF, 20)
        d.text((x0, 790), nm, font=sf2, fill='#FFFFFF'); d.text((x0, 814), hx, font=sf2, fill='#8A90A8')
    # type
    d.text((R + 60, 870), 'TYPOGRAPHY  -  ' + o['fonts'], font=lf, fill='#8A90A8')
    tfont = ImageFont.truetype(o['title_font'], 64); d.text((R + 60, 905), 'Aa 123', font=tfont, fill='#FFFFFF')
    uf = ImageFont.truetype(o['ui_font'], 34); d.text((R + 420, 930), 'PLAY   LEVELS   SETTINGS', font=uf, fill=o['accent'] if o['key'] != 'E' else '#FF4F7B')
    return im.convert('RGB')

def main():
    out = os.path.join(HERE, 'out', 'options'); os.makedirs(out, exist_ok=True)
    thumbs = []
    for o in OPTIONS:
        b = board(o); path = os.path.join(out, 'GRAVEXEL-identity-option-%s-%s.png' % (o['key'], o['name'].replace(' ', '-').lower()))
        b.save(path); thumbs.append(b.resize((960, 540)))
    ov = Image.new('RGB', (1920, 1620), '#0E1014')
    for i, t in enumerate(thumbs): ov.paste(t, ((i % 2) * 960, (i // 2) * 540))
    d = ImageDraw.Draw(ov); f = ImageFont.truetype(mb.TITLE_TTF, 46)
    d.text((1020, 1280), 'CHOOSE: A  B  C  D  E', font=f, fill='#FFFFFF')
    ov.save(os.path.join(out, 'GRAVEXEL-identity-options-overview.png'))
    print('ok', out)

if __name__ == '__main__':
    main()
