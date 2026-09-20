"""ART-0 31st pass: the foot, before and after, measured on the SHIPPED tree stills.

Before = `Trials/Runs/art0leaf29_tree.png` (the last capture without a foot - the probe of the
30th pass reproduced it pixel for pixel, so it is a valid baseline).
After  = `Trials/Runs/art0bole31_tree.png` (this pass' capture).

Both are the same lens, the same scene, the same build pipeline; the tree row does not sway
(19.09: four stills byte-identical across two captures). The four numbers this pass turns on:

  1. the row of backdrop under the bole - row 1045, the bole's own columns. Before: 29 px of
     EXACT backdrop (76, 82, 92), 100% of the sample - the "gap at the base".
  2. the bole's lowest inked row per column across the trunk: before it is one flat row for
     29 columns (the "flare cut flat at the top edge of the soil").
  3. how far the trunk's ink now reaches below the ground line (row 1045.75 = world y 0).
  4. the pixels that changed outside the foot band - the change must be local.

Usage:  python Tools/art0-bole-after-check.py [before.png] [after.png]
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
BEFORE = sys.argv[1] if len(sys.argv) > 1 else os.path.join(RUNS, "art0leaf29_tree.png")
AFTER = sys.argv[2] if len(sys.argv) > 2 else os.path.join(RUNS, "art0bole31_tree.png")

# The bole's own columns in the shipped lens (Tools/art0-bole-hole-probe.py found them: the
# longest bark run of every row is x 863..907, and the gap row is x 867..895).
BAND_X = (847, 967)
GAP_X = (867, 896)
GAP_ROW = 1045
GROUND_ROW = 1046        # world y = 0 is at screen row 1045.75; the dirt's ink starts here
FOOT_BAND = (847, 967, 1000, 1080)
BACKDROP = np.array([76, 82, 92])


def load(path):
    a = np.asarray(Image.open(path).convert("RGB")).astype(np.int16)
    return a


def backdrop_mask(a):
    return (np.abs(a - BACKDROP[None, None, :]).max(axis=2) <= 1)


def foot_report(a, label):
    bm = backdrop_mask(a)                      # (H, W): the pixel IS the gallery backdrop
    ink = ~bm
    gap = a[GAP_ROW, GAP_X[0]:GAP_X[1], :]
    exact = int(backdrop_mask(gap).all(axis=1).sum())
    low = []
    for x in range(BAND_X[0], BAND_X[1]):
        col = np.nonzero(ink[1000:1080, x])[0]
        low.append(int(col.max()) + 1000 if col.size else -1)
    low = [y for y in low if y >= 0]
    counts = {}
    for y in low:
        counts[y] = counts.get(y, 0) + 1
    longest = max(counts.values())
    # The hole itself: backdrop pixels inside the bole's own column band, between the crown of
    # the frame and the dirt. `ArtBoleProbe` showed the tree's parts and the ground's top edge
    # are both at world y = 0 with the bole's lowest card at +0.00125 m, so these pixels are
    # the rasterized 1.25 mm of nothing between the dirt and the trunk.
    hole = int(bm[1000:1080, BAND_X[0]:BAND_X[1]].sum())
    # and the same count for the trunk's own columns only, one row above the dirt
    strip = int(bm[GAP_ROW - 3:GAP_ROW + 1, BAND_X[0]:BAND_X[1]].sum())
    print("%-8s the gap row %d x %d..%d = %2d of %2d px exact backdrop (%.0f%%);  backdrop px "
          "in the trunk's band y 1000..1079 = %d (of which %d in the four rows above the dirt);  "
          "longest one-row bottom edge = %d columns"
          % (label, GAP_ROW, GAP_X[0], GAP_X[1] - 1, exact, GAP_X[1] - GAP_X[0],
             100.0 * exact / (GAP_X[1] - GAP_X[0]), hole, strip, longest))
    return exact, hole, strip


def main():
    before, after = load(BEFORE), load(AFTER)
    if before.shape != after.shape:
        print("SHAPES DIFFER %s vs %s" % (before.shape, after.shape))
        return 1

    print("ART-0-BOLE-AFTER before=%s after=%s"
          % (os.path.basename(BEFORE), os.path.basename(AFTER)))
    b_gap, b_hole, b_strip = foot_report(before, "before")
    a_gap, a_hole, a_strip = foot_report(after, "after")

    diff = np.abs(before - after).max(axis=2) > 2
    ys, xs = np.nonzero(diff)
    print("ART-0-BOLE-AFTER changed pixels = %d" % int(diff.sum()))
    if diff.sum():
        print("ART-0-BOLE-AFTER changed box x %d..%d y %d..%d"
              % (xs.min(), xs.max(), ys.min(), ys.max()))
        x0, x1, y0, y1 = FOOT_BAND
        inside = int(diff[y0:y1, x0:x1].sum())
        print("ART-0-BOLE-AFTER inside the foot band (x %d..%d, y %d..%d) = %d (%.1f%% of all changes)"
              % (x0, x1 - 1, y0, y1 - 1, inside, 100.0 * inside / max(1, diff.sum())))
        row, other = {}, {}
        for y, x in zip(ys.tolist(), xs.tolist()):
            if x0 <= x < x1 and y0 <= y < y1:
                row[y] = row.get(y, 0) + 1
            else:
                other[y] = other.get(y, 0) + 1
        if other:
            oy = sorted(other)
            print("ART-0-BOLE-AFTER outside the foot band: rows %d..%d, %d px"
                  % (oy[0], oy[-1], sum(other.values())))
        if row:
            print("ART-0-BOLE-AFTER inside the foot band by row: %s"
                  % ", ".join("%d:%d" % (y, n) for y, n in sorted(row.items())[:24]))

    print("ART-0-BOLE-AFTER VERDICT gap row %d -> %d px; backdrop in the bole's band %d -> %d; "
          "the four rows above the dirt %d -> %d"
          % (b_gap, a_gap, b_hole, a_hole, b_strip, a_strip))
    ok = a_gap == 0 and a_strip < b_strip
    print("ART-0-BOLE-AFTER %s" % ("OK" if ok else "FAIL"))
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
