# ART-BW3R legs strip: leg-tight cells (the lower part of the bird's own mask box), one row,
# captioned per frame. The box follows the bird, so the same tool crops both takes identically.
# usage: _art-bw3r-legs.py <label> <rate> <scale> <out.png> <f1> <f2> ...
import os
import sys

from PIL import Image, ImageDraw, ImageFont

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
FONT = "C:/Windows/Fonts/arial.ttf"


def dense_box(im):
    px = im.load()
    w, h = im.size
    m = []
    for y in range(h):
        row = []
        for x in range(w):
            r, g, b = px[x, y]
            row.append(1 if (r < 135 and g < 192 and b < 232 and b > 90) else 0)
        m.append(row)
    cols = [sum(m[y][x] for y in range(h)) for x in range(w)]
    runs = []
    start = None
    for x in range(w):
        if cols[x] >= 3:
            if start is None:
                start = x
        else:
            if start is not None:
                runs.append((start, x - 1))
                start = None
    if start is not None:
        runs.append((start, w - 1))
    if not runs:
        return None
    bx0, bx1 = max(runs, key=lambda r: r[1] - r[0])
    ys = [y for y in range(h) if sum(m[y][bx0:bx1 + 1]) >= 3]
    return bx0, min(ys), bx1, max(ys)


label, rate, scale, out = sys.argv[1], int(sys.argv[2]), float(sys.argv[3]), sys.argv[4]
frames = [int(v) for v in sys.argv[5:]]
tiles = []
for n in frames:
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
    bb = dense_box(im)
    if bb is None:
        print("f%d: no mask" % n)
        continue
    bx0, by0, bx1, by1 = bb
    W, H = im.size
    box = (max(0, bx0 - 12), max(0, int(by0 + (by1 - by0) * 0.38)), min(W, bx1 + 12), min(H, by1 + 34))
    c = im.crop(box)
    c = c.resize((int(c.size[0] * scale), int(c.size[1] * scale)), Image.LANCZOS)
    t = ImageDraw.Draw(c)
    f = ImageFont.truetype(FONT, int(6 * scale))
    t.rectangle([0, 0, c.size[0], int(13 * scale)], fill=(0, 0, 0))
    t.text((int(1.5 * scale), int(0.8 * scale)), "%s f%d t=%.2fs" % (label, n, n / float(rate)),
           font=f, fill=(255, 255, 0))
    tiles.append(c)
    print("f%-4d box %s crop %s" % (n, box, c.size))
if tiles:
    h = max(t.size[1] for t in tiles)
    w = sum(t.size[0] for t in tiles) + 8 * (len(tiles) - 1)
    sheet = Image.new("RGB", (w, h), (20, 20, 20))
    x = 0
    for t in tiles:
        sheet.paste(t, (x, 0))
        x += t.size[0] + 8
    sheet.save(out)
    print("strip -> %s %s" % (out, sheet.size))
