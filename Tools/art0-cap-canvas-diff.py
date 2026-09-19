"""ART-0 cycle: what exactly moved between the old and the new grass-cap canvas.

The old canvas is kept as the probe's candidate `cap_S_3_9_1.png` (same tip band and min_top the
shipped file had), so the two can be compared pixel by pixel: which rows changed, how many pixels,
and whether the root rows (the soil contact band) and the width of the tile are untouched.

Usage:  python Tools/art0-cap-canvas-diff.py <oldCap.png> <newCap.png>
"""
import sys

import numpy as np
from PIL import Image


def main(old, new):
    a = np.asarray(Image.open(old).convert("RGBA")).astype(np.int16)
    b = np.asarray(Image.open(new).convert("RGBA")).astype(np.int16)
    if a.shape != b.shape:
        print("shape differs: %s vs %s" % (a.shape, b.shape))
        return 1
    d = np.abs(a - b).max(axis=2)
    changed = d > 0
    print("%s -> %s   canvas %dx%d" % (old, new, a.shape[1], a.shape[0]))
    print("pixels changed %d of %d (%.2f %%)" % (changed.sum(), changed.size,
                                                 100.0 * changed.sum() / changed.size))
    print("row  changed  old alpha cov  new alpha cov  old mean rgb    new mean rgb")
    for y in range(a.shape[0]):
        ca, cb = a[y], b[y]
        def cov(row):
            return (row[:, 3] > 0).mean()
        def mean(row):
            m = row[:, 3] > 0
            return "n/a" if not m.any() else "%3d,%3d,%3d" % tuple(row[m][:, :3].mean(axis=0).round())
        print(" %2d   %6d   %12.2f  %12.2f  %-14s %-14s" % (
            y, int(changed[y].sum()), cov(ca), cov(cb), mean(ca), mean(cb)))
    print("rows with no change: %s" % [y for y in range(a.shape[0]) if not changed[y].any()])
    print("alpha identical: %s" % bool((a[:, :, 3] == b[:, :, 3]).all()))
    return 0


if __name__ == "__main__":
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    sys.exit(main(sys.argv[1], sys.argv[2]))
