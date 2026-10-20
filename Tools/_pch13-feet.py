# -*- coding: utf-8 -*-
"""B-PERCH-13 probe 8: the seated bird's own pixels in a frame - the leg/foot band
(cream, low saturation, bright) and the body box - so the seat cut can be centred on
the feet the eyes must judge.
Usage: python Tools/_pch13-feet.py <take> <frame> [x0 x1 y0 y1]
"""
import sys
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
FRAME = int(sys.argv[2])
X0 = int(sys.argv[3]) if len(sys.argv) > 3 else 600
X1 = int(sys.argv[4]) if len(sys.argv) > 4 else 1400
Y0 = int(sys.argv[5]) if len(sys.argv) > 5 else 150
Y1 = int(sys.argv[6]) if len(sys.argv) > 6 else 800


def sky(c):
    r, g, b = c
    return abs(r - 140) < 18 and abs(g - 199) < 18 and abs(b - 240) < 18


def leg(c):
    r, g, b = c
    mx, mn = max(c), min(c)
    return mx > 165 and (mx - mn) < 60 and not sky(c)


def body(c):
    r, g, b = c
    return b > r + 12 and b > 90 and r > 50 and not sky(c)


im = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, FRAME)).convert("RGB")
px = im.load()
boxes = {"leg": [X1, Y1, -1, -1, 0], "body": [X1, Y1, -1, -1, 0]}
for y in range(Y0, Y1):
    for x in range(X0, X1):
        c = px[x, y]
        for k, f in (("leg", leg), ("body", body)):
            if f(c):
                b = boxes[k]
                b[0] = min(b[0], x)
                b[1] = min(b[1], y)
                b[2] = max(b[2], x)
                b[3] = max(b[3], y)
                b[4] += 1
for k, b in boxes.items():
    print("%s box=(%d,%d,%d,%d) n=%d" % (k, b[0], b[1], b[2], b[3], b[4]))
