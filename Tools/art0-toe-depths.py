#!/usr/bin/env python3
"""The toes of a bole-foot canvas as NUMBERS: how deep each one reaches below the soil line.

The generator prints the depth at each `LOBE_TABLE` position, which is the DESIGN, and the
silhouette probe prints the contour, which is the PIXELS - but a read of either has to count in
its head. This tool measures the shipped canvas' own depth profile and reports every toe it
finds (a local maximum of the depth profile whose prominence is at least `--prom 2` px), so a
before/after claim about "the toe rhythm" is a list of numbers for both canvases.

    python Tools/art0-toe-depths.py <canvas.png> [soil] [prominence]

`soil` is the canvas' buried share of its height (0.30 for this canvas: the soil line is at
row height * (1 - soil) = 56 of 80).
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def profile(path, soil):
    a = np.asarray(Image.open(path).convert("RGBA"))
    ink = a[:, :, 3] > 110
    h = a.shape[0]
    ground = int(round(h * (1.0 - soil)))
    depth = np.full(a.shape[1], -1, dtype=np.int64)
    for x in range(a.shape[1]):
        col = np.nonzero(ink[:, x])[0]
        if col.size:
            depth[x] = int(col.max()) - ground
    return depth, ground, h


def toes(depth, prom):
    """Local maxima of the depth profile, with their TRUE prominence (the classic definition:
    the highest of the two saddles that must be crossed to reach a deeper maximum). A plateau's
    steps are a staircase, not toes - the staircase of the first cut of this canvas reported 33
    'toes' with the saddle taken globally, which is a measurement of the quantisation."""
    out = []
    n = len(depth)
    peaks = [x for x in range(1, n - 1)
             if depth[x] >= 0 and depth[x] >= depth[x - 1] and depth[x] > depth[x + 1]]
    for x in peaks:
        # left saddle: the lowest column between this peak and the first higher column
        ls = depth[x]
        i = x - 1
        while i >= 0 and depth[i] <= depth[x]:
            ls = min(ls, depth[i])
            i -= 1
        rs = depth[x]
        i = x + 1
        while i < n and depth[i] <= depth[x]:
            rs = min(rs, depth[i])
            i += 1
        if depth[x] - max(ls, rs) >= prom and depth[x] >= 3:
            out.append((x, int(depth[x])))
    return out


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        ROOT, "Assets", "Resources", "Art", "Tree", "bole_foot.png")
    soil = float(sys.argv[2]) if len(sys.argv) > 2 else 0.30
    prom = int(sys.argv[3]) if len(sys.argv) > 3 else 2
    depth, ground, h = profile(path, soil)
    t = toes(depth, prom)
    print("TOES %s  %dx%d px  soil line at row %d (%.2f of the height buried), prominence >= %d px"
          % (os.path.basename(path), depth.size, h, ground, soil, prom))
    if not t:
        print("  no toe found")
        return 0
    ds = [d for _, d in t]
    for i, (x, d) in enumerate(t):
        print("  toe %d at x %3d (%.3f of the width): %2d px below the soil line%s"
              % (i + 1, x, x / float(depth.size), d,
                 "  <- deepest" if d == max(ds) else ""))
    print("  depths %d..%d px, spread %d, deepest / shallowest = %.1fx, %d toes"
          % (min(ds), max(ds), max(ds) - min(ds), max(ds) / float(max(1, min(ds))), len(ds)))
    # the flatness of the underside: how many columns sit inside each 2 px band, worst first
    bands = {}
    for x in range(depth.size):
        if depth[x] >= 0:
            bands.setdefault(int(depth[x]) // 2, []).append(x)
    worst = sorted(bands.items(), key=lambda kv: -len(kv[1]))[:3]
    print("  the busiest 2 px bands of the underside: %s"
          % ", ".join("rows %d-%d: %d columns" % (k * 2, k * 2 + 1, len(v)) for k, v in worst))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
