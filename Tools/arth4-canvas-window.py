#!/usr/bin/env python3
"""ART-H4 probe: a window of one shipped canvas as ASCII (luminance + alpha + RGB strip).

Usage: python Tools/arth4-canvas-window.py --part thigh --x 60 130 --y 600 660 [--rows 8]
Prints the luminance map (same ramp as the other probes), the alpha map (0-9), and the actual
RGB of every Nth row's centre columns - so the art's own drawn bands can be read as pixels.
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")
RAMP = "@%#*+=-:."


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--part", required=True)
    ap.add_argument("--dir", default=ART, help="canvas folder (default the shipped Resources/Art/Human)")
    ap.add_argument("--x", nargs=2, type=int, required=True, help="x0 x1")
    ap.add_argument("--y", nargs=2, type=int, required=True, help="y0 y1")
    ap.add_argument("--rows", type=int, default=8, help="print raw RGB every Nth row")
    args = ap.parse_args()

    a = np.asarray(Image.open(os.path.join(args.dir, args.part + ".png")).convert("RGBA")).astype(np.float64)
    x0, x1 = args.x
    y0, y1 = args.y
    lum = 0.2126 * a[:, :, 0] + 0.7152 * a[:, :, 1] + 0.0722 * a[:, :, 2]
    print("%s x %d..%d y %d..%d  canvas %dx%d" % (args.part, x0, x1, y0, y1, a.shape[1], a.shape[0]))
    print("luminance ('@' 0..31, '.' 224..255, space = transparent):")
    for y in range(y0, y1):
        line = []
        for x in range(x0, x1):
            if a[y, x, 3] < 16:
                line.append(" ")
                continue
            k = int(np.clip(lum[y, x] / 255.0 * (len(RAMP) - 1), 0, len(RAMP) - 1))
            line.append(RAMP[k])
        print("   %4d %s" % (y, "".join(line)))
    print("alpha (0 none .. 9 opaque):")
    for y in range(y0, y1):
        line = []
        for x in range(x0, x1):
            line.append(str(int(np.clip(a[y, x, 3] / 255.0 * 9.0, 0, 9))))
        print("   %4d %s" % (y, "".join(line)))
    print("raw RGB of every %dth row, middle columns:" % args.rows)
    for y in range(y0, y1, args.rows):
        cells = []
        for x in range(x0 + (x1 - x0) // 4, x1, max(1, (x1 - x0) // 8)):
            cells.append("x%d a%3d rgb%3d,%3d,%3d" % (x, a[y, x, 3],
                                                      a[y, x, 0], a[y, x, 1], a[y, x, 2]))
        print("   %4d %s" % (y, " | ".join(cells)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
