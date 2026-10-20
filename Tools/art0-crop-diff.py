#!/usr/bin/env python3
"""How many pixels differ between two stills inside one box, and where.

The vision judge at 8x on the toe band of the oak reported "the two panels are identical" on
two stills whose pixels differ - a read can miss a 1-3 px step. This tool answers the same
question with numbers, so the report carries both the read and the count.

    python Tools/art0-crop-diff.py <before.png> <after.png> <x0,y0,x1,y1> [threshold]
"""
import os
import sys

import numpy as np
from PIL import Image


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        return 2
    before = np.asarray(Image.open(sys.argv[1]).convert("RGB")).astype(np.int16)
    after = np.asarray(Image.open(sys.argv[2]).convert("RGB")).astype(np.int16)
    x0, y0, x1, y1 = (int(v) for v in sys.argv[3].split(","))
    thr = int(sys.argv[4]) if len(sys.argv) > 4 else 2
    if before.shape != after.shape:
        print("SHAPES DIFFER %s vs %s" % (before.shape, after.shape))
        return 1
    diff = np.abs(before - after).max(axis=2) > thr
    box = diff[y0:y1, x0:x1]
    print("CROP-DIFF box x %d..%d y %d..%d (%dx%d px) threshold >%d"
          % (x0, x1 - 1, y0, y1 - 1, x1 - x0, y1 - y0, thr))
    print("CROP-DIFF differing inside the box  = %d of %d px (%.2f%%)"
          % (int(box.sum()), box.size, 100.0 * box.sum() / box.size))
    print("CROP-DIFF differing in the whole frame = %d of %d px (%.3f%%)"
          % (int(diff.sum()), diff.size, 100.0 * diff.sum() / diff.size))
    rows = [(y0 + i, int(box[i].sum())) for i in range(box.shape[0]) if box[i].sum()]
    if rows:
        print("CROP-DIFF by row: %s" % ", ".join("%d:%d" % r for r in rows))
    cols = [(x0 + i, int(box[:, i].sum())) for i in range(box.shape[1]) if box[:, i].sum()]
    if cols:
        print("CROP-DIFF columns touched %d of %d, worst %s"
              % (len(cols), box.shape[1], ", ".join("%d:%d" % c for c in sorted(cols, key=lambda t: -t[1])[:8])))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
