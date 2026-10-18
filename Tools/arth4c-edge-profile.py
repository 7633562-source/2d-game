#!/usr/bin/env python3
"""ART-H4c: the canvas END profiles - what tone the drawing carries at its cut edges.

For each named end (top/bottom/left/right) print, row by row walking 24 px INTO the
drawing from that edge: how many px are ink (alpha>16), and the mean RGB + luma of the
ink. A dark row just inside an edge is the drawing's own cap/outline stroke - the line
the eye reads at a joint when the two canvases come together.

Usage: python Tools/arth4c-edge-profile.py <canvas.png> [-n 24]
"""
import argparse
import sys

import numpy as np
from PIL import Image


def luma(rgb):
    return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]


def profile(a, name, upto):
    h, w = a.shape[:2]
    lines = []
    for k in range(upto):
        if name == "top":
            row = a[k, :, :]
        elif name == "bottom":
            row = a[h - 1 - k, :, :]
        elif name == "left":
            row = a[:, k, :]
        else:
            row = a[:, w - 1 - k, :]
        ink = row[:, 3] > 16
        n = int(ink.sum())
        if n == 0:
            lines.append((k, 0, None))
            continue
        m = row[ink, :3].mean(axis=0)
        lines.append((k, n, (m[0], m[1], m[2], luma(m))))
    return lines


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("canvas")
    ap.add_argument("-n", type=int, default=24)
    args = ap.parse_args(argv[1:])
    a = np.array(Image.open(args.canvas).convert("RGBA"))
    print("%s  %dx%d" % (args.canvas, a.shape[1], a.shape[0]))
    for name in ("top", "bottom"):
        print("--- %s, %d px inward:" % (name, args.n))
        for k, n, rgb in profile(a, name, args.n):
            if n == 0:
                print("  k=%2d  ink 0" % k)
            else:
                print("  k=%2d  ink %4d  rgb(%3.0f,%3.0f,%3.0f)  luma %5.1f"
                      % (k, n, rgb[0], rgb[1], rgb[2], rgb[3]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
