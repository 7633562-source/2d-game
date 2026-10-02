#!/usr/bin/env python3
"""ART-H3 probe: WHERE the hard silhouette crossings are (alpha >= 240 next to <= 16).

The seam table counts them per frame; this prints their positions (as fractions of the
frame) and the local alpha values, so a run can be attributed to the art's own drawn cut,
to a skirt's cap, or to a fade that stops one row short.

Usage: python Tools/arth3-hardpx.py Trials/Runs/arth3-seam-after/shoulder-+000.png
"""
import os
import sys

import numpy as np
from PIL import Image


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    a = np.asarray(Image.open(sys.argv[1]).convert("RGBA"))[:, :, 3]
    h, w = a.shape
    out = []
    for dy, dx, ax in ((0, 1, "H"), (0, -1, "H"), (1, 0, "V"), (-1, 0, "V")):
        s0 = (slice(None), slice(None, -1)) if dx == 1 else \
             (slice(None), slice(1, None)) if dx == -1 else \
             (slice(None, -1), slice(None)) if dy == 1 else (slice(1, None), slice(None))
        s1 = (slice(None), slice(1, None)) if dx == 1 else \
             (slice(None), slice(None, -1)) if dx == -1 else \
             (slice(1, None), slice(None)) if dy == 1 else (slice(None, -1), slice(None))
        m = ((a[s0] >= 240) & (a[s1] <= 16)) | ((a[s0] <= 16) & (a[s1] >= 240))
        ys, xs = np.where(m)
        for y, x in zip(ys, xs):
            out.append((ax, y, x, int(a[y, x])))
    print("%s: %d hard px pairs of %dx%d" % (os.path.basename(sys.argv[1]), len(out), w, h))
    for ax, y, x, v in out[:24]:
        print("  %s  x=%.3f y=%.3f  alpha=%d" % (ax, (x + 0.5) / w, (y + 0.5) / h, v))
    if len(out) > 24:
        print("  ... %d more" % (len(out) - 24))
    return 0


if __name__ == "__main__":
    sys.exit(main())
