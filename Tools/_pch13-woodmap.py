# -*- coding: utf-8 -*-
"""B-PERCH-13 probe 3: the wood's first-hit y per x column in a frame - to choose a
crop that keeps the seated bird AND the wood under it inside the frame.
Usage: python Tools/_pch13-woodmap.py <take> <frame> [x0 x1 step]
"""
import sys
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
FRAME = int(sys.argv[2])
X0 = int(sys.argv[3]) if len(sys.argv) > 3 else 640
X1 = int(sys.argv[4]) if len(sys.argv) > 4 else 1160
STEP = int(sys.argv[5]) if len(sys.argv) > 5 else 20


def wood(c):
    r, g, b = c
    return 60 < r < 190 and 30 < g < 130 and b < 100 and r > g > b


im = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, FRAME)).convert("RGB")
px = im.load()
out = []
for x in range(X0, X1, STEP):
    hit = None
    for y in range(120, 900):
        if wood(px[x, y]):
            hit = y
            break
    out.append("%d:%s" % (x, hit))
print("take=%s f=%d wood-first-y per x:" % (TAKE, FRAME))
print(" ".join(out))
