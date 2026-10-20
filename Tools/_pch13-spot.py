# -*- coding: utf-8 -*-
"""B-PERCH-13 probe: where is the bird on screen at the given frames of a take
(the crow's near-black body bounding box), so a fixed crop can be checked for
cut defects before the eyes see it.
Usage: python Tools/_pch13-spot.py <take_label> f1 f2 ...
"""
import sys
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
FRAMES = [int(x) for x in sys.argv[2:]]

for i in FRAMES:
    im = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, i)).convert("RGB")
    px = im.load()
    W, H = im.size
    x0, y0, x1, y1 = W, H, -1, -1
    n = 0
    for y in range(0, H):
        for x in range(0, W):
            r, g, b = px[x, y]
            if r < 60 and g < 60 and b < 70:
                n += 1
                if x < x0:
                    x0 = x
                if x > x1:
                    x1 = x
                if y < y0:
                    y0 = y
                if y > y1:
                    y1 = y
    print("f%-5d box=(%d,%d,%d,%d) n=%d frame=%dx%d" % (i, x0, y0, x1, y1, n, W, H))
