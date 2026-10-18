#!/usr/bin/env python3
"""Delta histogram + cluster pass on a frame diff: is the change a REDRAW or rounding residue?

    python Tools/art0-delta-hist.py <labelA> <labelB> [still ...]

`art0-frame-diff.py` answers "how many pixels differ". That is not enough to call a change:
a part that was redrawn shows a dense cluster of large deltas; a live scene's rounding
residue shows thousands of pixels at <= 3 levels scattered thin (the rule from the
art-frame-verification skill). This prints, per still:

    px > 3 / > 10 / > 30 levels, the max delta, and the boxes of the > 30 clusters
    (an O(n^2) merge of the marked pixels, so no dtype games).

A still with px>30 == 0 is rounding; a still with one or a few tight clusters is a redraw.
"""
import os
import sys

import numpy as np
from PIL import Image

RUNS = os.path.join("Trials", "Runs")


def load(label, still):
    return Image.open(os.path.join(RUNS, "%s_%s.png" % (label, still))).convert("RGB")


def clusters(mask, min_px=12, gap=6):
    """Merge marked pixels into boxes: sort by x, walk rows of marked pixels, union overlaps."""
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return []
    order = np.argsort(xs)
    xs, ys = xs[order], ys[order]
    boxes = []
    for x, y in zip(xs, ys):
        for b in boxes:
            if x - b[2] <= gap and b[1] - gap <= y <= b[3] + gap:
                b[0] = min(b[0], x)
                b[2] = max(b[2], x)
                b[1] = min(b[1], y)
                b[3] = max(b[3], y)
                b[4] += 1
                break
        else:
            boxes.append([x, y, x, y, 1])
    merged = []
    for b in sorted(boxes, key=lambda b: -b[4]):
        for m in merged:
            if not (b[2] + gap < m[0] or b[0] - gap > m[2] or b[3] + gap < m[1] or b[1] - gap > m[3]):
                m[0] = min(m[0], b[0]); m[1] = min(m[1], b[1])
                m[2] = max(m[2], b[2]); m[3] = max(m[3], b[3]); m[4] += b[4]
                break
        else:
            merged.append(list(b))
    return [b for b in merged if b[4] >= min_px]


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    a_label, b_label = sys.argv[1], sys.argv[2]
    stills = sys.argv[3:] or ["photo", "human", "tree", "apple", "materials", "level",
                              "fire", "flower", "placeholder", "flowerbox"]
    for still in stills:
        a = np.asarray(load(a_label, still)).astype(np.int16)
        b = np.asarray(load(b_label, still)).astype(np.int16)
        if a.shape != b.shape:
            print("%-12s SHAPE %s vs %s" % (still, a.shape, b.shape))
            continue
        d = np.abs(a - b).max(axis=2)
        n3, n10, n30 = int((d > 3).sum()), int((d > 10).sum()), int((d > 30).sum())
        line = ("%-12s px>3=%-8d >10=%-7d >30=%-7d maxdelta=%-4d" % (still, n3, n10, n30, int(d.max())))
        cl = clusters(d > 30, min_px=12, gap=6)
        if not cl:
            line += "  -> rounding residue (no cluster above 30 levels)"
        else:
            line += "  -> %d cluster(s) above 30 levels:" % len(cl)
            for b_ in cl[:6]:
                line += "\n%-12s    box x %d-%d y %d-%d  %d px" % ("", b_[0], b_[2], b_[1], b_[3], b_[4])
        print(line)
    return 0


if __name__ == "__main__":
    sys.exit(main())
