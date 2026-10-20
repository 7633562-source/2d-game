#!/usr/bin/env python3
"""ART-H4e probe: print one row of a still as x=r,g,b samples (locate a limb's pixels).

Usage: python Tools/arth4e-rowscan.py <png> <y> <x0> <x1> [<step>]
"""
import sys

import numpy as np
from PIL import Image


def main(argv):
    p, y, x0, x1 = argv[1], int(argv[2]), int(argv[3]), int(argv[4])
    step = int(argv[5]) if len(argv) > 5 else 16
    a = np.asarray(Image.open(p).convert("RGB"))
    parts = []
    for x in range(x0, x1, step):
        r, g, b = a[y, x]
        parts.append("%d=%d,%d,%d" % (x, r, g, b))
    print("y=%d %s" % (y, "  ".join(parts)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
