# -*- coding: utf-8 -*-
"""B-PERCH-13 probe 2: learn the bird's on-screen palette and locate it per frame.
Prints the top non-sky/non-wood colors in a region, then the bounding box of pixels
matching the learned "ghost" band for a list of frames.
Usage: python Tools/_pch13-spot2.py <take> f1 f2 ... [--learn f_learn x0 y0 x1 y1]
"""
import sys
from collections import Counter
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
args = sys.argv[2:]
learn = None
if "--learn" in args:
    k = args.index("--learn")
    learn = args[k + 1:]
    args = args[:k]
FRAMES = [int(x) for x in args]


def sky(c):
    r, g, b = c
    return abs(r - 140) < 16 and abs(g - 199) < 16 and abs(b - 240) < 16


def wood(c):
    r, g, b = c
    return 60 < r < 190 and 30 < g < 130 and b < 100 and r > g > b


def leaf(c):
    r, g, b = c
    return g > r + 8 and g > b + 20


def ground(c):
    r, g, b = c
    return not sky(c) and not wood(c) and not leaf(c)


if learn:
    lf, x0, y0, x1, y1 = (int(learn[0]), int(learn[1]), int(learn[2]), int(learn[3]), int(learn[4]))
    im = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, lf)).convert("RGB")
    cnt = Counter()
    px = im.load()
    for y in range(y0, y1, 2):
        for x in range(x0, x1, 2):
            c = px[x, y]
            if not sky(c) and not leaf(c) and not wood(c) and not ground(c):
                cnt[c] += 1
    for c, n in cnt.most_common(12):
        print("learn", c, n)

# The ghost band: bluish-grey, brighter than wood, darker than sky, low saturation.
def ghost(c):
    r, g, b = c
    mx, mn = max(c), min(c)
    return (mx - mn) < 45 and 120 < mx < 225 and b >= r - 6 and not sky(c) and not leaf(c) and not wood(c)


for i in FRAMES:
    im = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, i)).convert("RGB")
    px = im.load()
    W, H = im.size
    x0, y0, x1, y1 = W, H, -1, -1
    n = 0
    for y in range(150, 700):
        for x in range(300, 1500):
            if ghost(px[x, y]):
                n += 1
                if x < x0:
                    x0 = x
                if x > x1:
                    x1 = x
                if y < y0:
                    y0 = y
                if y > y1:
                    y1 = y
    print("f%-5d ghost box=(%d,%d,%d,%d) n=%d" % (i, x0, y0, x1, y1, n))
