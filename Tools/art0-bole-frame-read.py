"""ART-0 31st pass: read the four probe frames - which layer owns the foot's backdrop row?

`Assets/Editor/ArtBoleProbe.cs` writes, from the SHIPPED tree lens (validated against
`art0leaf29_tree.png` by this file's first check):

    art0bole31-probe.png      every renderer as the dress left it
    art0bole31-groundoff.png  every GroundStrip renderer off
    art0bole31-stumpoff.png   the oak's Stump part off
    art0bole31-bothoff.png    both off

The measured defect on the shipped still is one row of backdrop at y=1045, x 867..895
(29 px, 100 % exactly the gallery's (76,82,92)) - the bole's central 29 px of foot. This
reader prints, for the same 29 px, what each frame shows there, so the owner is the frame
whose absence removes the row:

  * probe == art0leaf29_tree.png (0 px) is this pass' lens validation - without it the
    "which layer" answer would be about a different picture;
  * the row 1045 sample per frame (backdrop count of 29);
  * the per-frame count of backdrop pixels inside the bole band (the hole measure).

Usage:  python Tools/art0-bole-frame-read.py
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
SHIPPED = os.path.join(RUNS, "art0leaf29_tree.png")
LABEL = "art0bole31"
ROW = 1045
X0, X1 = 867, 895
BAND_X = (847, 967)
BAND_Y = (1000, 1080)          # the bole's own foot band, below the last spur tuft


def backdrop_mask(px):
    r = px[:, :, 0].astype(np.int16)
    g = px[:, :, 1].astype(np.int16)
    b = px[:, :, 2].astype(np.int16)
    return (np.abs(r - g) < 14) & (np.abs(g - b) < 20) & (np.abs(r - b) < 22)


def main():
    frames = {}
    for name in ("probe", "groundoff", "stumpoff", "bothoff"):
        path = os.path.join(RUNS, "%s-%s.png" % (LABEL, name))
        if not os.path.exists(path):
            print("MISSING %s" % path)
            return 1
        frames[name] = np.asarray(Image.open(path).convert("RGB"))

    shipped = np.asarray(Image.open(SHIPPED).convert("RGB"))
    probe = frames["probe"]
    diff = int((np.abs(shipped.astype(np.int16) - probe.astype(np.int16)).sum(axis=2) > 0).sum())
    print("LENS   art0bole31-probe vs art0leaf29_tree: %d px differ of %d"
          % (diff, shipped.shape[0] * shipped.shape[1]))

    for name, px in frames.items():
        m = backdrop_mask(px)
        row = m[ROW, X0:X1 + 1]
        band = m[BAND_Y[0]:BAND_Y[1], BAND_X[0]:BAND_X[1]]
        sample = px[ROW, X0]
        print("FRAME  %-9s row %d x %d..%d : %2d/29 backdrop (sample %s) | band backdrop %d px of %d"
              % (name, ROW, X0, X1, int(row.sum()), tuple(int(v) for v in sample),
                 int(band.sum()), band.size))

    # the row's own colour histogram in the shipped frame: is the row ONE colour?
    row_px = probe[ROW, X0:X1 + 1].reshape(-1, 3)
    uniq = sorted({tuple(int(v) for v in c) for c in row_px})
    print("SHIPPED row %d x %d..%d colours: %s" % (ROW, X0, X1, uniq))

    # where the bole's own ink ends per column, in the probe frame and with the ground off
    for name in ("probe", "groundoff", "stumpoff"):
        px = frames[name]
        m = ~backdrop_mask(px)
        last = []
        for x in range(X0, X1 + 1):
            col = np.nonzero(m[1000:ROW + 1, x])[0]
            last.append(int(col.max()) + 1000 if col.size else -1)
        print("EDGE   %-9s last ink row per column x %d..%d: min %d max %d -> %s"
              % (name, X0, X1, min(last), max(last),
                 "straight" if max(last) - min(last) <= 1 else "ragged"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
