"""Pre-renders the game fonts into glyph atlases (fonts.bin) embedded in the Windows build.

Usage: python make_fonts.py <title.ttf> <ui.ttf> <out fonts.bin>
Format (little endian): b'GSF1', int fonts; per font: int sizes; per size: int px, int capHeight, int glyphs;
per glyph: ushort char, float advance, short ox, short oy, ushort w, ushort h, w*h alpha bytes.
ox/oy place the bitmap's top-left relative to the pen position on the baseline.
"""
import struct
import sys
from PIL import Image, ImageDraw, ImageFont

SIZES = [16, 22, 30, 40, 54, 72, 96, 128, 176]
CHARS = [chr(c) for c in range(32, 127)] + ['·']


def font_block(path):
    out = [struct.pack('<i', len(SIZES))]
    for px in SIZES:
        f = ImageFont.truetype(path, px)
        cap = -f.getbbox('H', anchor='ls')[1]
        glyphs = []
        for ch in CHARS:
            adv = f.getlength(ch)
            x0, y0, x1, y1 = f.getbbox(ch, anchor='ls')
            w, h = max(0, x1 - x0), max(0, y1 - y0)
            data = b''
            if w > 0 and h > 0 and ch != ' ':
                im = Image.new('L', (w, h), 0)
                ImageDraw.Draw(im).text((-x0, -y0), ch, font=f, fill=255, anchor='ls')
                data = im.tobytes()
            else:
                w = h = 0
            glyphs.append(struct.pack('<Hfhhhh', ord(ch), adv, x0, y0, w, h) + data)
        out.append(struct.pack('<iii', px, cap, len(glyphs)))
        out.extend(glyphs)
    return b''.join(out)


def main():
    title, ui, dst = sys.argv[1], sys.argv[2], sys.argv[3]
    blob = b'GSF1' + struct.pack('<i', 2) + font_block(title) + font_block(ui)
    with open(dst, 'wb') as fp:
        fp.write(blob)
    print('wrote %s (%d KB)' % (dst, len(blob) // 1024))


if __name__ == '__main__':
    main()
