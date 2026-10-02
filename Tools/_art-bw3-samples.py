# ART-BW3 crop colour sampler: the most common colours in the mic crop, so the fan mask's
# thresholds come from the pixels, not from guesses.
# usage: python _art-bw3-samples.py <label> <frame>
import sys
from collections import Counter

from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)

label, frame = sys.argv[1], int(sys.argv[2])
im = Image.open(RUNS + "\\shot_%s_%d.png" % (label, frame)).convert("RGB").crop(BOX)
c = Counter(im.getdata())
print("crop", im.size, "distinct", len(c))
for col, n in c.most_common(24):
    print("  %-16s %6d" % (str(col), n))
# a coarse map: 10x9 cells, each an average
w, h = im.size
px = im.load()
for gy in range(9):
    row = []
    for gx in range(10):
        xs = range(gx * w // 10, (gx + 1) * w // 10)
        ys = range(gy * h // 9, (gy + 1) * h // 9)
        r = sum(px[x, y][0] for y in ys for x in xs) // (len(xs) * len(ys))
        g = sum(px[x, y][1] for y in ys for x in xs) // (len(xs) * len(ys))
        b = sum(px[x, y][2] for y in ys for x in xs) // (len(xs) * len(ys))
        row.append("%3d,%3d,%3d" % (r, g, b))
    print(" ".join(row))
