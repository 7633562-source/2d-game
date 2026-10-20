#!/usr/bin/env python3
"""Per-REGION frame diff: is the change inside the rectangle it claims to be in?

    python Tools/art0-region-diff.py <labelA> <labelB> <still> name=x0,y0,x1,y1 [name=box ...]

`art0-frame-diff.py` answers "how many pixels differ" for a whole still and
`art0-delta-hist.py` merges them into clusters, printing only the six largest. Neither can say
"the change is inside the part whose art moved": a whole-still count cannot separate the part
from a label that re-flowed next to it, and the cluster list hides everything past the sixth
box. This tool takes the rectangles BY NAME, so the claim and the measurement are the same
thing:

    hip=1455,565,1520,635 crocus=915,600,1005,865 labels=0,860,1920,980 rest=...

What it prints per region: px>3 (> the rounding bar), px>10, px>30, the max delta, the share of
the region, and the mean |delta| over the region. A region with px>30 == 0 is rounding residue;
a region with a dense >30 cluster is a redraw. Outside every named region it prints `OUTSIDE`
(the whole still minus those rectangles), which is the number that makes "confined to" a claim
instead of an impression.
"""
import os
import sys

import numpy as np
from PIL import Image

RUNS = os.path.join("Trials", "Runs")


def load(label, still):
    return Image.open(os.path.join(RUNS, "%s_%s.png" % (label, still))).convert("RGB")


def main():
    if len(sys.argv) < 5:
        print(__doc__)
        return 2
    a_label, b_label, still = sys.argv[1], sys.argv[2], sys.argv[3]
    regions = []
    for spec in sys.argv[4:]:
        name, _, box = spec.partition("=")
        x0, y0, x1, y1 = (int(v) for v in box.split(","))
        regions.append((name, x0, y0, x1, y1))

    a = np.asarray(load(a_label, still)).astype(np.int16)
    b = np.asarray(load(b_label, still)).astype(np.int16)
    if a.shape != b.shape:
        print("SHAPE %s vs %s" % (a.shape, b.shape))
        return 1
    d = np.abs(a - b).max(axis=2)
    h, w = d.shape
    print("still %s: %s -> %s   %dx%d" % (still, a_label, b_label, w, h))
    print("  %-12s %-22s %-8s %-8s %-8s %-6s %-8s %s"
          % ("region", "box", "px>3", "px>10", "px>30", "max", "mean", "share>3"))
    covered = np.zeros_like(d, dtype=bool)
    for name, x0, y0, x1, y1 in regions:
        x0c, y0c = max(0, x0), max(0, y0)
        x1c, y1c = min(w, x1), min(h, y1)
        sub = d[y0c:y1c, x0c:x1c]
        covered[y0c:y1c, x0c:x1c] = True
        n = sub.size
        print("  %-12s %-22s %-8d %-8d %-8d %-6d %-8.3f %.3f%%"
              % (name, "%d,%d,%d,%d" % (x0, y0, x1, y1),
                 int((sub > 3).sum()), int((sub > 10).sum()), int((sub > 30).sum()),
                 int(sub.max()) if n else 0, float(sub.mean()) if n else 0.0,
                 100.0 * (sub > 3).sum() / n if n else 0.0))
    out = d[~covered]
    print("  %-12s %-22s %-8d %-8d %-8d %-6d %-8.3f %.3f%%"
          % ("OUTSIDE", "-", int((out > 3).sum()), int((out > 10).sum()),
             int((out > 30).sum()), int(out.max()) if out.size else 0,
             float(out.mean()) if out.size else 0.0,
             100.0 * (out > 3).sum() / out.size if out.size else 0.0))
    return 0


if __name__ == "__main__":
    sys.exit(main())
