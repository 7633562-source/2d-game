# -*- coding: utf-8 -*-
"""B-PERCH-13 probe 6: the bend profile - per column, the twig's top edge in an
UNLOADED frame vs a LOADED one (static wind-0 twin), so the cut can be centred on
the columns that actually move.
Usage: python Tools/_pch13-profile.py <take> <fUnloaded> <fLoaded> [x0 x1 step ytop]
"""
import sys
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
FA = int(sys.argv[2])
FB = int(sys.argv[3])
X0 = int(sys.argv[4]) if len(sys.argv) > 4 else 860
X1 = int(sys.argv[5]) if len(sys.argv) > 5 else 1100
STEP = int(sys.argv[6]) if len(sys.argv) > 6 else 10
YTOP = int(sys.argv[7]) if len(sys.argv) > 7 else 540


def wood(c):
    r, g, b = c
    return 60 < r < 190 and 30 < g < 130 and b < 100 and r > g > b


a = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, FA)).convert("RGB").load()
b = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, FB)).convert("RGB").load()
rows = []
best = (0, None)
for x in range(X0, X1, STEP):
    ta = tb = None
    for y in range(YTOP, YTOP + 300):
        if ta is None and wood(a[x, y]):
            ta = y
        if tb is None and wood(b[x, y]):
            tb = y
        if ta is not None and tb is not None:
            break
    if ta is None or tb is None:
        rows.append((x, ta, tb, None))
        continue
    d = tb - ta
    rows.append((x, ta, tb, d))
    if abs(d) > abs(best[0]):
        best = (d, x)
for r in rows:
    print("x%-5d un=%-5s ld=%-5s d=%s" % r)
print("MAX |d| = %s at x=%s" % (best[0], best[1]))
