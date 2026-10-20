#!/usr/bin/env python
"""B-WING-4 close: locate the bird in a shot (ASCII map + bbox). Temp helper.

Usage: python Tools/_bw4-locate.py <label> <n>
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")


def main():
    label, n = sys.argv[1], int(sys.argv[2])
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
    W, H = im.size
    small = im.resize((96, 54))
    px = small.load()
    print("frame %d  size %dx%d" % (n, W, H))
    for y in range(54):
        row = ""
        for x in range(96):
            r, g, b = px[x, y]
            lum = (r + g + b) // 3
            row += "#" if lum < 70 else ("+" if lum < 130 else ("." if lum < 200 else " "))
        print("%2d %s" % (y, row))
    # dark blob above the ground line
    full = im.resize((480, 270))
    pf = full.load()
    dark = [(x, y) for y in range(270) for x in range(480)
            if sum(pf[x, y]) < 240 and y < 160]
    if dark:
        xs = [d[0] for d in dark]
        ys = [d[1] for d in dark]
        print("dark-above-ground: n=%d bbox=(%d,%d)-(%d,%d)" % (
            len(dark), min(xs), min(ys), max(xs), max(ys)))
        cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        print("center 480-scale=(%.0f,%.0f) -> full-scale=(%.0f,%.0f)" % (
            cx, cy, cx * W / 480.0, cy * H / 270.0))


if __name__ == "__main__":
    main()
