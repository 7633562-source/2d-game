#!/usr/bin/env python3
"""What the dress order did to the drawn box, measured on the two oak stills.

Usage:  python Tools/art0-tree-draw-probe.py <labelOld> <labelNew> [still]

Both labels are the same scene, the same lens and (for the A/B this tool was written
for) the same tree - only `ArtParts.Dress`' order differs. The question is not "did
pixels move" (they did) but WHAT moved: the drawn box of the bark/leaf parts. So the
tool masks the ink above the ground band and reports

  * the ink bbox and area of the whole tree,
  * the horizontal extent (left .. right, width) at a ladder of rows down the trunk,
  * the same ladder for the label printed second, and the width ratio per row.

A stretch writes a wider row on one side of the A/B without moving the tree's own
outline; a scale artefact writes nothing but the outline.
"""
import os
import sys

import numpy as np
from PIL import Image

PROJ = "E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials", "Runs")


def load(label, still):
    path = os.path.join(RUNS, "%s_%s.png" % (label, still))
    if not os.path.exists(path):
        raise SystemExit("no such still: " + path)
    return np.asarray(Image.open(path).convert("RGB")).astype(np.int32), path


def bg_color(a):
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    return np.median(border, axis=0)


def mask_of(a, tol=26):
    m = np.abs(a - bg_color(a)).sum(axis=2) > tol
    # The ground band spans the frame; everything below its top row is not the tree.
    spans = m.sum(axis=1)
    wide = np.flatnonzero(spans > 0.9 * m.shape[1])
    if wide.size:
        m[wide[0]:, :] = False
    return m


def report(label, still, rows):
    a, path = load(label, still)
    m = mask_of(a)
    ys, xs = np.nonzero(m)
    print("%-14s %s" % (label, os.path.basename(path)))
    print("  build guid   %s" % guid(label))
    print("  ink bbox     x %d-%d y %d-%d   area=%d px" % (xs.min(), xs.max(), ys.min(), ys.max(),
                                                           int(m.sum())))
    ladder = []
    for r in rows:
        idx = np.flatnonzero(m[r])
        if idx.size == 0:
            ladder.append((r, None, None, 0))
            print("  row %4d     (no ink)" % r)
            continue
        # Keep the trunk band only: the leftmost run at that row.
        split = np.flatnonzero(np.diff(idx) > 1)
        first = np.split(idx, split + 1)[0]
        ladder.append((r, int(first[0]), int(first[-1]), int(idx.size)))
        print("  row %4d     trunk run x %4d-%4d width=%3d px   ink on row=%4d px"
              % (r, first[0], first[-1], first[-1] - first[0] + 1, int(idx.size)))
    return ladder


def guid(label):
    p = os.path.join(RUNS, "%s-gallery.json" % label)
    if not os.path.exists(p):
        return "?"
    txt = open(p, encoding="utf-8").read()
    i = txt.find('"buildGuid"')
    return txt[i + 13:].split('"')[1] if i >= 0 else "?"


def main():
    old, new = sys.argv[1], sys.argv[2]
    still = sys.argv[3] if len(sys.argv) > 3 else "tree"
    rows = [300, 400, 500, 600, 700, 800, 900, 1000, 1050]
    lo = report(old, still, rows)
    ln = report(new, still, rows)
    print("=== trunk width, %s -> %s ===" % (old, new))
    for (r, a0, a1, ac), (_, b0, b1, bc) in zip(lo, ln):
        if a0 is None or b0 is None:
            print("  row %4d  n/a" % r)
            continue
        wa, wb = a1 - a0 + 1, b1 - b0 + 1
        print("  row %4d  width %3d -> %3d px  (%.1f%%)   ink %4d -> %4d px  (%.1f%%)"
              % (r, wa, wb, 100.0 * wb / wa - 100.0, ac, bc, 100.0 * bc / max(1, ac) - 100.0))


if __name__ == "__main__":
    main()
