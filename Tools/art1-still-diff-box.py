#!/usr/bin/env python3
"""Where did two 1920x1080 stills of the same build change? A coarse locator.

The PSNR pair (`Tools/capture-still-psnr.py`) says a still moved; it does not say
where. A row-by-row dump of a full frame is 1080 lines. This tool reports the
bounding box of everything that differs and the 64 px cells that hold the change,
so a "the trees moved" surprise is a named region, not a guess.

    python Tools/art1-still-diff-box.py <before.png> <after.png> [threshold]
"""
import sys

import numpy as np
from PIL import Image


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    before = np.asarray(Image.open(sys.argv[1]).convert("RGB")).astype(np.int16)
    after = np.asarray(Image.open(sys.argv[2]).convert("RGB")).astype(np.int16)
    thr = int(sys.argv[3]) if len(sys.argv) > 3 else 2
    if before.shape != after.shape:
        print("SHAPES DIFFER %s vs %s" % (before.shape, after.shape))
        return 1
    diff = np.abs(before - after).max(axis=2) > thr
    total = int(diff.sum())
    height, width = diff.shape
    print("DIFF-BOX frame %dx%d threshold >%d differing %d px (%.3f%%)"
          % (width, height, thr, total, 100.0 * total / diff.size))
    if total == 0:
        return 0
    ys, xs = np.nonzero(diff)
    print("DIFF-BOX bbox x %d..%d y %d..%d" % (xs.min(), xs.max(), ys.min(), ys.max()))
    cell = 64
    rows = []
    for cy in range(0, height, cell):
        for cx in range(0, width, cell):
            count = int(diff[cy:cy + cell, cx:cx + cell].sum())
            if count:
                rows.append((count, cx, cy))
    rows.sort(reverse=True)
    print("DIFF-BOX %d of %d cells (%d px each) hold ink" % (len(rows), (width // cell) * (height // cell), cell * cell))
    for count, cx, cy in rows[:24]:
        print("DIFF-BOX cell x %4d..%4d y %4d..%4d  %5d px" % (cx, cx + cell - 1, cy, cy + cell - 1, count))
    return 0


if __name__ == "__main__":
    sys.exit(main())
