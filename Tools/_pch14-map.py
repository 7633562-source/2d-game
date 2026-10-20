# -*- coding: utf-8 -*-
"""B-PERCH-14 probe: an ASCII class map of a frame region - the seated bird's
claws vs the branch's top silhouette. Classes: S sky, W wood/brown, G green leaf,
B bird body (blue-gray), P pale (leg/belly/cream), C dark/claw, ? other.
Usage: python Tools/_pch14-map.py <take> <frame> [x0 x1 y0 y1 step]
"""
import sys
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
FRAME = int(sys.argv[2])
X0 = int(sys.argv[3]) if len(sys.argv) > 3 else 820
X1 = int(sys.argv[4]) if len(sys.argv) > 4 else 880
Y0 = int(sys.argv[5]) if len(sys.argv) > 5 else 240
Y1 = int(sys.argv[6]) if len(sys.argv) > 6 else 340
STEP = int(sys.argv[7]) if len(sys.argv) > 7 else 2


def cls(c):
    r, g, b = c
    if abs(r - 140) < 22 and abs(g - 199) < 22 and abs(b - 240) < 24:
        return "S"
    if g > r + 8 and g > b + 8:
        return "G"
    if r > g > b and 40 < r < 200 and b < 110:
        return "W"
    if max(c) < 90:
        return "C"
    if b > r + 12:
        return "B"
    if min(c) > 150:
        return "P"
    return "?"


im = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, FRAME)).convert("RGB")
px = im.load()
print("take=%s f=%d box x%d-%d y%d-%d step=%d" % (TAKE, FRAME, X0, X1, Y0, Y1, STEP))
hdr = "     " + "".join(str((x // 10) % 10) for x in range(X0, X1, STEP))
print(hdr)
for y in range(Y0, Y1, STEP):
    row = "".join(cls(px[x, y]) for x in range(X0, X1, STEP))
    print("%4d %s" % (y, row))
