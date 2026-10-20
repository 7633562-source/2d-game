#!/usr/bin/env python3
"""ART-H4d probe 3: any PNG as a luminance ASCII map (for reading a still's pixels).

Usage: python Tools/arth4d-still-map.py <png> x0 x1 y0 y1 [--step 1] [--sat]
Prints the luminance ramp ('@' darkest .. '.' lightest) and, with --sat, an RGB description
of every Nth row so a coloured halo (whitish/blue/green) can be told apart from the sky.
"""
import argparse

import numpy as np
from PIL import Image

RAMP = "@%#*+=-:."


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("png")
    ap.add_argument("x0", type=int)
    ap.add_argument("x1", type=int)
    ap.add_argument("y0", type=int)
    ap.add_argument("y1", type=int)
    ap.add_argument("--step", type=int, default=1)
    ap.add_argument("--at", nargs=2, type=int, default=None, help="print the RGB of Y X")
    args = ap.parse_args()

    a = np.asarray(Image.open(args.png).convert("RGB")).astype(np.float64)
    if args.at:
        y, x = args.at
        print("rgb at y=%d x=%d: (%d, %d, %d)" % (y, x, a[y, x, 0], a[y, x, 1], a[y, x, 2]))
    lum = 0.2126 * a[:, :, 0] + 0.7152 * a[:, :, 1] + 0.0722 * a[:, :, 2]
    print("%s  %dx%d  x %d..%d y %d..%d  step %d" %
          (args.png, a.shape[1], a.shape[0], args.x0, args.x1, args.y0, args.y1, args.step))
    print("     " + "".join(str(x % 10) if x % 10 == 0 else " " for x in range(args.x0, args.x1)))
    for y in range(args.y0, args.y1, args.step):
        line = []
        for x in range(args.x0, args.x1):
            k = int(np.clip(lum[y, x] / 256.0 * len(RAMP), 0, len(RAMP) - 1))
            line.append(RAMP[k])
        print("%4d %s" % (y, "".join(line)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
