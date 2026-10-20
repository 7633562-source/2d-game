#!/usr/bin/env python3
"""ART-H4d probe: two stills of the same pick - px diff, bbox, and a side-by-side strip.

Usage: python Tools/arth4d-diff.py <a.png> <b.png> <out-ab.png>
Prints how many px differ by >16 in any channel, the diff's bbox, and writes A|B.
"""
import sys

import numpy as np
from PIL import Image


def main(argv):
    a_path, b_path, out = argv[1], argv[2], argv[3]
    A = np.asarray(Image.open(a_path).convert("RGB")).astype(np.int16)
    B = np.asarray(Image.open(b_path).convert("RGB")).astype(np.int16)
    if A.shape != B.shape:
        print("shape differ: %s vs %s" % (A.shape, B.shape))
        return 2
    d = np.abs(A - B).max(axis=2)
    m = d > 16
    n = int(m.sum())
    print("diff>16 px: %d of %d (%.3f%%)" % (n, d.size, 100.0 * n / d.size))
    if n:
        ys, xs = np.where(m)
        print("bbox x %d..%d y %d..%d" % (xs.min(), xs.max(), ys.min(), ys.max()))
        print("max channel diff %d, mean over changed %.1f" % (int(d.max()), float(d[m].mean())))
    w, h = Image.open(a_path).size
    strip = Image.new("RGB", (w * 2 + 8, h), (30, 30, 30))
    strip.paste(Image.open(a_path).convert("RGB"), (0, 0))
    strip.paste(Image.open(b_path).convert("RGB"), (w + 8, 0))
    strip.save(out)
    print("strip %s" % out)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
