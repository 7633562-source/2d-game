#!/usr/bin/env python3
"""ART-H4e probe: the before/after pair of ONE pick - what the far-side change moved.

The take is byte-identical before and after the change and the picks are read from the take's
own CSV, so the two crops are the SAME frame: every differing pixel IS a pixel of the far side
(the only thing the dress tint touches).  Over that mask the tool prints, for A (before) and B
(after): the mean RGB, the mean red-minus-blue (the warm/cool axis: the ghost wash is cool,
the drawing is warm) and the mean chroma (max-min; the wash is low-chroma).

Usage: python Tools/arth4e-pair.py <before.png> <after.png>
"""
import sys

import numpy as np
from PIL import Image


def main(argv):
    a = np.asarray(Image.open(argv[1]).convert("RGB")).astype(np.int16)
    b = np.asarray(Image.open(argv[2]).convert("RGB")).astype(np.int16)
    if a.shape != b.shape:
        print("shape differ %s vs %s" % (a.shape, b.shape))
        return 2
    win = None
    if len(argv) > 3 and argv[3] not in ("-", ""):
        win = [int(v) for v in argv[3].split(",")]
    if len(argv) > 4:
        dx, dy = [int(v) for v in argv[4].split(",")]
        b = np.roll(np.roll(b, -dy, axis=0), -dx, axis=1)   # b_shifted[x] = b[x+dx]
        print("aligned: b shifted by dx=%d dy=%d (b[x+dx] matches a)" % (dx, dy))
    d = np.abs(a - b).max(axis=2)
    m = d > 16
    if win:
        x0, x1, y0, y1 = win
        sub = m[y0:y1, x0:x1]
        print("%s  vs  %s  WINDOW x %d..%d y %d..%d" % (argv[1], argv[2], x0, x1, y0, y1))
        print("window moved px>16: %d of %d (%.2f%%)" % (int(sub.sum()), sub.size,
                                                         100.0 * sub.sum() / sub.size))
        # best (dx, dy) shift of B that matches A over the window (coarse, +-6)
        best = None
        for dy in range(-6, 7):
            for dx in range(-6, 7):
                y0b, y1b = max(0, y0 + dy), min(a.shape[0], y1 + dy)
                x0b, x1b = max(0, x0 + dx), min(a.shape[1], x1 + dx)
                ya0, xa0 = y0b - dy, x0b - dx
                dd = np.abs(a[y0b:y1b, x0b:x1b] - b[ya0:ya0 + (y1b - y0b), xa0:xa0 + (x1b - x0b)])
                mm = dd.max(axis=2) > 16
                frac = mm.mean()
                if best is None or frac < best[0]:
                    best = (frac, dx, dy)
        print("best shift of B: dx=%d dy=%d -> moved %.2f%% (0.00%% = the window matches shifted)"
              % (best[1], best[2], 100.0 * best[0]))
    n = int(m.sum())
    print("moved px>16: %d of %d (%.2f%%)" % (n, d.size, 100.0 * n / d.size))
    # a coarse moved-density map: 32 cols x 18 rows, one digit = moved %% of the cell (0-9)
    h, w = a.shape[0], a.shape[1]
    print("moved density map (32x18, digit = %% of cell >16):")
    for i in range(18):
        line = []
        for j in range(32):
            y0, y1 = i * h // 18, (i + 1) * h // 18
            x0, x1 = j * w // 32, (j + 1) * w // 32
            f = m[y0:y1, x0:x1].mean()
            line.append(str(min(9, int(f * 10))))
        print("   %s" % "".join(line))
    if not n:
        return 0
    ys, xs = np.where(m)
    print("bbox x %d..%d y %d..%d" % (xs.min(), xs.max(), ys.min(), ys.max()))

    def sig(arr, tag):
        px = arr[m]
        r, g, bl = px[:, 0].astype(float), px[:, 1].astype(float), px[:, 2].astype(float)
        chroma = px.max(axis=1) - px.min(axis=1)
        print("  %s meanRGB=(%.0f, %.0f, %.0f)  r-b=%+.1f  chroma=%.1f  blue-dominated px=%.1f%%"
              % (tag, r.mean(), g.mean(), bl.mean(), (r - bl).mean(), chroma.mean(),
                 100.0 * ((bl - r) > 10).mean()))
    sig(a, "BEFORE")
    sig(b, "AFTER ")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
