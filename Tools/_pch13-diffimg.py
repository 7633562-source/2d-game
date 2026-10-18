# -*- coding: utf-8 -*-
"""B-PERCH-13 probe 7: make the bend visible as an image - unloaded frame | loaded
frame | the loaded frame with the changed pixels marked red (same crop, 4x).
Usage: python Tools/_pch13-diffimg.py <take> <fUnloaded> <fLoaded> [tag] [box]
"""
import sys
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
FA = int(sys.argv[2])
FB = int(sys.argv[3])
TAG = sys.argv[4] if len(sys.argv) > 4 else "bPCH13_diff"
BOX = tuple(int(x) for x in sys.argv[5].split(",")) if len(sys.argv) > 5 else (860, 540, 1090, 720)
THR = 24
Z = 4

a = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, FA)).convert("RGB")
b = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, FB)).convert("RGB")
pa, pb = a.load(), b.load()
x0, y0, x1, y1 = BOX
mark = b.copy()
pm = mark.load()
n = 0
for y in range(y0, y1):
    for x in range(x0, x1):
        ca, cb = pa[x, y], pb[x, y]
        if abs(ca[0] - cb[0]) + abs(ca[1] - cb[1]) + abs(ca[2] - cb[2]) >= THR:
            pm[x, y] = (255, 0, 0)
            n += 1
print("changed in box =", n)


def panel(img, label):
    crop = img.crop(BOX).resize(((x1 - x0) * Z, (y1 - y0) * Z), Image.NEAREST)
    d = ImageDraw.Draw(crop)
    d.rectangle([0, 0, crop.width - 1, crop.height - 1], outline=(255, 255, 0), width=2)
    d.text((6, 4), label, fill=(255, 255, 0))
    return crop


p1 = panel(a, "f%d (unloaded) t=%.2f" % (FA, FA / 20.0))
p2 = panel(b, "f%d (loaded) t=%.2f" % (FB, FB / 20.0))
p3 = panel(mark, "f%d + changed px red" % FB)
gap = 8
out = Image.new("RGB", (3 * p1.width + 2 * gap, p1.height), (0, 0, 0))
for k, p in enumerate((p1, p2, p3)):
    out.paste(p, (k * (p1.width + gap), 0))
path = RUNS + r"\reviews\%s.png" % TAG
out.save(path)
print("saved", path, out.size)
