# -*- coding: utf-8 -*-
"""B-PERCH-13 probe 4: locate the bend by frame differencing in the static wind-0
twin (one camera, no leaf sway): diff an UNLOADED frame against a LOADED one and
report the bounding boxes of the changed pixels + a coarse row/col histogram.
Usage: python Tools/_pch13-diff.py <take> <fA unloaded> <fB loaded> [thr]
"""
import sys
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
FA = int(sys.argv[2])
FB = int(sys.argv[3])
THR = int(sys.argv[4]) if len(sys.argv) > 4 else 24

a = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, FA)).convert("RGB")
b = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, FB)).convert("RGB")
pa, pb = a.load(), b.load()
W, H = a.size
cols = {}
rows = {}
x0, y0, x1, y1 = W, H, -1, -1
n = 0
for y in range(0, H):
    for x in range(0, W):
        ca, cb = pa[x, y], pb[x, y]
        d = abs(ca[0] - cb[0]) + abs(ca[1] - cb[1]) + abs(ca[2] - cb[2])
        if d >= THR:
            n += 1
            if x < x0:
                x0 = x
            if x > x1:
                x1 = x
            if y < y0:
                y0 = y
            if y > y1:
                y1 = y
            cols[x // 40 * 40] = cols.get(x // 40 * 40, 0) + 1
            rows[y // 40 * 40] = rows.get(y // 40 * 40, 0) + 1
print("take=%s fA=%d fB=%d thr=%d changed=%d box=(%d,%d,%d,%d)" % (TAKE, FA, FB, THR, n, x0, y0, x1, y1))
top_c = sorted(cols.items(), key=lambda kv: -kv[1])[:8]
top_r = sorted(rows.items(), key=lambda kv: -kv[1])[:8]
print("cols:", top_c)
print("rows:", top_r)
