#!/usr/bin/env python3
"""ART-H4f probe: how SOFT is a canvas' own silhouette edge vs the added paint's rim.

The judge names "полупрозрачные ореолы" at the joints.  Two candidates produce translucency:
the designer's DRAWING (its own anti-aliased / soft edge rows) and the ADDED paint (the disc
rim fade, the dome tip feather, the extension's side feather).  This tool separates them with
pixels only: for every ink row of a canvas it measures how many px the silhouette edge takes
to go from opaque (>240) to transparent (<16), and counts translucent px that sit further
than 6 px from any transparent px - the drawing's own see-through bits.

Usage: python Tools/arth4f-alpha-profile.py thigh.png shin.png foot.png [...]
       (bare names resolve under Assets/Resources/Art/Human)
"""
import os
import sys

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")


def edge_ramp(alpha_line):
    """How many px between the outer alpha>240 px and alpha<16, walking inward."""
    idx = np.where(alpha_line > 240)[0]
    if idx.size < 3:
        return None
    lo, hi = idx[0], idx[-1]
    left = 0
    x = lo - 1
    while x >= 0 and alpha_line[x] >= 16:
        left += 1
        x -= 1
    right = 0
    x = hi + 1
    while x < alpha_line.size and alpha_line[x] >= 16:
        right += 1
        x += 1
    return left, right


def main(argv):
    names = argv[1:] or ["thigh.png", "shin.png", "foot.png", "torso.png"]
    for n in names:
        p = n if os.path.sep in n or "/" in n else os.path.join(ART, n)
        a = np.asarray(Image.open(p).convert("RGBA"))[:, :, 3].astype(np.int32)
        ink = a > 16
        trans = ~ink
        h, w = a.shape
        if not ink.any():
            print("%s: no ink" % p)
            continue
        # windowed transparency count (cumsum), pad 6 -> "no transparent px within 6"
        pad = 6
        c = np.cumsum(np.cumsum(trans.astype(np.int64), axis=0), axis=1)
        c = np.pad(c, ((1, 0), (1, 0)))
        y0 = np.clip(np.arange(h) - pad, 0, h)
        y1 = np.clip(np.arange(h) + pad + 1, 0, h)
        x0 = np.clip(np.arange(w) - pad, 0, w)
        x1 = np.clip(np.arange(w) + pad + 1, 0, w)
        Y0, X0 = np.meshgrid(y0, x0, indexing="ij")
        Y1, X1 = np.meshgrid(y1, x1, indexing="ij")
        win = c[Y1, X1] - c[Y0, X1] - c[Y1, X0] + c[Y0, X0]
        interior = ink & (win == 0)
        soft = ink & (a < 240)
        rows = np.where(ink.any(axis=1))[0]
        step = max(1, len(rows) // 200)
        ramps = [edge_ramp(a[y]) for y in rows[::step]]
        ramps = [r for r in ramps if r]
        arr = np.array(ramps) if ramps else np.zeros((1, 2))
        print("%s  %dx%d  ink=%d  soft(16<a<240)=%d  interior-soft=%d  "
              "edge ramp px: mean %.2f max %d"
              % (os.path.basename(p), w, h, int(ink.sum()), int(soft.sum()),
                 int((interior & soft).sum()), float(arr.mean()), int(arr.max())))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
