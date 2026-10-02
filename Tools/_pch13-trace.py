# -*- coding: utf-8 -*-
"""B-PERCH-13 probe 5: the twig's top edge over time in the static wind-0 twin (bend trace).
For each frame, prints the first wood y scanning down from ytop at several columns.
Usage: python Tools/_pch13-trace.py <take> ytop x1,x2,... f1 f2 ...
"""
import sys
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
YTOP = int(sys.argv[2])
COLS = [int(x) for x in sys.argv[3].split(",")]
FRAMES = [int(x) for x in sys.argv[4:]]


def wood(c):
    r, g, b = c
    return 60 < r < 190 and 30 < g < 130 and b < 100 and r > g > b


imgs = {}
for i in FRAMES:
    imgs[i] = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, i)).convert("RGB").load()

print("take=%s ytop=%d cols=%s" % (TAKE, YTOP, COLS))
hdr = "frame  t      " + "  ".join("x%d" % c for c in COLS)
print(hdr)
for i in FRAMES:
    px = imgs[i]
    vals = []
    for c in COLS:
        hit = None
        for y in range(YTOP, YTOP + 260):
            if wood(px[c, y]):
                hit = y
                break
        vals.append(hit)
    print("f%-5d %-6.2f %s" % (i, i / 20.0, "  ".join(str(v) for v in vals)))
