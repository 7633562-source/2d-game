#!/usr/bin/env python3
"""ART-H4f probe: WHERE the two takes differ (and what the pixels look like in each).

The camera phase between takes can shift a few px, so this tool looks at STRONG changes only
(any channel step > 48) and buckets them into 16 px cells, printing the busiest cells with the
mean RGB of those px in A and in B - enough to tell a joint-arc change from a shifted edge.

Usage: python Tools/arth4f-joint-diff.py <a.png> <b.png> [threshold]
"""
import sys

import numpy as np
from PIL import Image


def main(argv):
    a_path, b_path = argv[1], argv[2]
    thr = int(argv[3]) if len(argv) > 3 else 48
    A = np.asarray(Image.open(a_path).convert("RGB")).astype(np.int16)
    B = np.asarray(Image.open(b_path).convert("RGB")).astype(np.int16)
    if A.shape != B.shape:
        print("shape differ:", A.shape, B.shape)
        return 2
    d = np.abs(A - B).max(axis=2)
    m = d > thr
    h, w = m.shape
    bw = 16
    cells = {}
    ys, xs = np.where(m)
    for y, x in zip(ys, xs):
        cells.setdefault((y // bw, x // bw), []).append((y, x))
    top = sorted(cells.items(), key=lambda kv: -len(kv[1]))[:10]
    print("%s vs %s: strong px %d (thr %d)" % (a_path.split("/")[-1], b_path.split("/")[-1],
                                               int(m.sum()), thr))
    for (cy, cx), pts in top:
        pts = np.array(pts)
        pa = A[pts[:, 0], pts[:, 1]]
        pb = B[pts[:, 0], pts[:, 1]]
        print("  cell y %4d x %4d  n=%5d  A mean %s  B mean %s"
              % (cy * bw, cx * bw, len(pts),
                 tuple(int(v) for v in pa.mean(axis=0)),
                 tuple(int(v) for v in pb.mean(axis=0))))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
