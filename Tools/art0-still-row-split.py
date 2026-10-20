"""ART-0 (18th pass): where in a still the difference sits, row band by row band.

`Tools/art0-frame-diff.py` answers "did anything move" with one bounding box per still, and a box
that covers the whole frame hides the question this lane keeps having to answer: is a difference the
change I made, or is it the plant lane's known build-to-build wobble? A ground fill can only move
the pixels the GROUND draws - a band along the floor of the row - so this tool prints, per 40-row
band, how many pixels differ and how wide their x extent is. A band with a handful of scattered
pixels high in the frame is not a texture repeat.

Usage:
  python Tools/art0-still-row-split.py <labelA> <labelB> [still ...]
  python Tools/art0-still-row-split.py art0wrap17 art0fill18
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = r"E:/Games/2D Game/My project"
RUNS = os.path.join(ROOT, "Trials", "Runs")
STILLS = ["gallery", "human", "tree", "apple", "materials", "level", "fire", "placeholder", "photo"]
BAND = 40
TOL = 0            # exact pixel difference


def load(label, name):
    for cand in ("%s_%s.png" % (label, name), "%s_%s.png" % (label, name.replace("photo", "photo"))):
        p = os.path.join(RUNS, cand)
        if os.path.exists(p):
            return np.asarray(Image.open(p).convert("RGB")).astype(np.int16), p
    return None, None


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    a, b = sys.argv[1], sys.argv[2]
    names = sys.argv[3:] or STILLS
    for name in names:
        ia, pa = load(a, name)
        ib, pb = load(b, name)
        if ia is None or ib is None:
            print("== %-12s (missing: %s)" % (name, pa or pb))
            continue
        if ia.shape != ib.shape:
            print("== %-12s size differs %s vs %s" % (name, ia.shape, ib.shape))
            continue
        d = np.abs(ia - ib).max(axis=2) > TOL
        total = int(d.sum())
        print("== %-12s %d differing px (%.4f%% of the frame)"
              % (name, total, 100.0 * total / d.size))
        if total == 0:
            continue
        for y0 in range(0, d.shape[0], BAND):
            sub = d[y0:y0 + BAND]
            n = int(sub.sum())
            if n == 0:
                continue
            xs = np.where(sub.any(axis=0))[0]
            print("   rows %4d-%4d  %6d px  x %4d-%4d" % (y0, min(y0 + BAND, d.shape[0]) - 1,
                                                          n, xs.min(), xs.max()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
