#!/usr/bin/env python3
"""ART-H4f probe: what changed between two canvas sets (before vs after a paint-rule round).

Usage: python Tools/arth4f-canvas-diff.py <dirA> <dirB>
Prints, per canvas: px changed (alpha step > 8 or any RGB step > 8), the bbox of the change,
and the alpha profile of the changed px (how many of them sit in translucent bands).
"""
import os
import sys

import numpy as np
from PIL import Image


def main(argv):
    da, db = argv[1], argv[2]
    names = sorted(f for f in os.listdir(da) if f.endswith(".png"))
    tot = 0
    for n in names:
        pa, pb = os.path.join(da, n), os.path.join(db, n)
        if not os.path.exists(pb):
            print("%s: MISSING in B" % n)
            continue
        A = np.asarray(Image.open(pa).convert("RGBA")).astype(np.int16)
        B = np.asarray(Image.open(pb).convert("RGBA")).astype(np.int16)
        if A.shape != B.shape:
            print("%s: SHAPE %s vs %s" % (n, A.shape, B.shape))
            continue
        da_ = np.abs(A[:, :, 3] - B[:, :, 3])
        drgb = np.abs(A[:, :, :3] - B[:, :, :3]).max(axis=2)
        ch = (da_ > 8) | (drgb > 8)
        cnt = int(ch.sum())
        tot += cnt
        if cnt == 0:
            print("%-12s identical" % n)
            continue
        ys, xs = np.where(ch)
        trans_before = int(((A[:, :, 3] > 16) & (A[:, :, 3] < 240) & ch).sum())
        trans_after = int(((B[:, :, 3] > 16) & (B[:, :, 3] < 240) & ch).sum())
        print("%-12s %5dx%-5d  changed %6d (%.2f%%)  bbox x %d..%d y %d..%d  "
              "translucent-in-change: %d -> %d"
              % (n, A.shape[1], A.shape[0], cnt, 100.0 * cnt / ch.size,
                 xs.min(), xs.max(), ys.min(), ys.max(), trans_before, trans_after))
    print("TOTAL changed px: %d" % tot)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
