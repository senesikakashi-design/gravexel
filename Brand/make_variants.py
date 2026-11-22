"""Six shape/arrangement variations of identity A (NEON GRAVITY)."""
import os
from PIL import Image, ImageDraw, ImageFont
import make_options as mo, make_brand as mb

CY, MG, WH = '#22E8FF', '#FF3DDB', '#FFFFFF'
P = mo.p

def v1(c):  # classic
    return mo.m_gravity_x(c)

def v2(c):  # heavy chevrons, the pixel locked between the tips
    t = 'M20 36 L92 36 L128 72 L164 36 L236 36 L128 120 Z'
    b = 'M20 220 L92 220 L128 184 L164 220 L236 220 L128 136 Z'
    return P(t, c[0]) + P(b, c[1]) + P('M128 114 L142 128 L128 142 L114 128 Z', c[2])

def v3(c):  # rounded neon tubes
    s = '<polyline points="%s" fill="none" stroke="%s" stroke-width="30" stroke-linecap="round" stroke-linejoin="round"/>'
    return (s % ('48,44 128,104 208,44', c[0]) + s % ('48,212 128,152 208,212', c[1])
            + '<circle cx="128" cy="128" r="15" fill="%s"/>' % c[2])

def v4(c):  # floor & ceiling rails with the X between them
    t = 'M60 44 L96 44 L128 76 L160 44 L196 44 L128 112 Z'
    b = 'M60 212 L96 212 L128 180 L160 212 L196 212 L128 144 Z'
    rails = '<rect x="16" y="20" width="224" height="14" rx="7" fill="%s"/><rect x="16" y="222" width="224" height="14" rx="7" fill="%s"/>' % (c[0], c[1])
    return rails + P(t, c[0]) + P(b, c[1]) + '<rect x="117" y="117" width="22" height="22" fill="%s"/>' % c[2]

def v5(c):  # badge: rounded frame split on the horizon
    frame_t = 'M60 16 H196 A44 44 0 0 1 240 60 V116 H222 V60 A26 26 0 0 0 196 34 H60 A26 26 0 0 0 34 60 V116 H16 V60 A44 44 0 0 1 60 16 Z'
    frame_b = 'M16 140 H34 V196 A26 26 0 0 0 60 222 H196 A26 26 0 0 0 222 196 V140 H240 V196 A44 44 0 0 1 196 240 H60 A44 44 0 0 1 16 196 Z'
    g = '<g transform="translate(51.2 51.2) scale(0.6)">%s</g>' % mo.m_gravity_x(c)
    return P(frame_t, c[0]) + P(frame_b, c[1]) + g

def v6(c):  # double chevrons = motion
    t1 = 'M44 20 L74 20 L128 74 L182 20 L212 20 L128 104 Z'
    t2 = 'M80 4 L100 4 L128 32 L156 4 L176 4 L128 52 Z'
    b1 = 'M44 236 L74 236 L128 182 L182 236 L212 236 L128 152 Z'
    b2 = 'M80 252 L100 252 L128 224 L156 252 L176 252 L128 204 Z'
    return (P(t2, c[0]) + P(t1, c[0]) + P(b2, c[1]) + P(b1, c[1])
            + P('M128 112 L144 128 L128 144 L112 128 Z', c[2]))

base = dict(mc=(CY, MG, WH), bg='#050816', bg2='#25093D', fg='#FFFFFF', accent=MG,
            palette=[(CY, 'NEON CYAN'), (MG, 'NEON MAGENTA'), (WH, 'PIXEL WHITE'), ('#0A1238', 'DEEP NAVY'), ('#050816', 'VOID INK')],
            title_font=mb.TITLE_TTF, ui_font=mb.UI_TTF, fonts='Audiowide + Rajdhani', c1=CY, c2=WH)

VARIANTS = [
    ('A1', 'CLASSIC X', 'The original: sharp chevrons and a floating pixel. Stacked lockup.', v1, 'stacked', {}),
    ('A2', 'HEAVY X', 'Wider, heavier chevrons that grip the pixel. Horizontal lockup for headers and store banners.', v2, 'horizontal', {}),
    ('A3', 'NEON TUBE', 'Rounded strokes like a real neon sign, softer and friendlier. Stacked lockup.', v3, 'stacked', dict(bg='#07041A', bg2='#1B0630')),
    ('A4', 'RAILS', 'The floor and ceiling rails of the game frame the X: the whole game in one icon. Horizontal lockup.', v4, 'horizontal', dict(bg='#040B1E', bg2='#0F1B45')),
    ('A5', 'BADGE', 'A rounded badge split on the horizon around the X, built to be an app icon. Badge lockup.', v5, 'badge', {}),
    ('A6', 'MOTION X', 'Double chevrons that add speed and motion. Badge lockup with a framed tagline.', v6, 'badge', dict(bg='#0A0420', bg2='#2D0A3E')),
]

def main():
    out = os.path.join(mo.HERE, 'out', 'variants-A'); os.makedirs(out, exist_ok=True)
    thumbs = []
    for key, name, idea, fn, layout, extra in VARIANTS:
        o = dict(base); o.update(extra)
        o.update(key=key, name=name, idea=idea, mark=fn, layout=layout, label='VARIATION ' + key)
        b = mo.board(o)
        b.save(os.path.join(out, 'GRAVEXEL-A-variation-%s-%s.png' % (key, name.replace(' ', '-').lower())))
        thumbs.append((key, name, b.resize((960, 540))))
    ov = Image.new('RGB', (1920, 1620), '#0E1014')
    for i, (_, _, t) in enumerate(thumbs): ov.paste(t, ((i % 2) * 960, (i // 2) * 540))
    ov.save(os.path.join(out, 'GRAVEXEL-A-variations-overview.png'))
    # icon comparison strip
    strip = Image.new('RGB', (1920, 560), '#12141A'); d = ImageDraw.Draw(strip)
    f = ImageFont.truetype(mb.TITLE_TTF, 34); f2 = ImageFont.truetype(mb.UI_TTF, 24)
    for i, (key, name, fn, *_r) in enumerate([(v[0], v[1], v[3]) for v in VARIANTS]):
        o = dict(base); o.update(mark=fn)
        x = 40 + i * 315
        strip.paste(mo.app_icon(o, 220), (x, 60), mo.app_icon(o, 220))
        sx = x
        for s in (64, 40, 24):
            ic = mo.app_icon(o, s); strip.paste(ic, (sx, 320), ic); sx += s + 16
        d.text((x, 420), key, font=f, fill='#FFFFFF'); d.text((x, 470), name, font=f2, fill='#9AA0B8')
    strip.save(os.path.join(out, 'GRAVEXEL-A-app-icons-compared.png'))
    print('ok', out)

if __name__ == '__main__':
    main()
