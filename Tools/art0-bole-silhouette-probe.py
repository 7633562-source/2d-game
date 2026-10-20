"""ART-0 31st pass: the bole canvas' silhouette as pixels, not as a read.

Two vision reads of the canvas named "flat horizontal shelves at both sides, parallel to the
ground" and the eye cannot be argued with by adjectives. This probe prints the silhouette:

  * an ASCII map of the alpha channel (96 x 40 characters, `#` ink, `.` clear), and
  * the contour itself - for every 4th column, the lowest inked row - so a straight run, a
    shelf, a hanging fragment or a one-pixel spur is a number in a list.

Usage:  python Tools/art0-bole-silhouette-probe.py [png]
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    ROOT, "Assets", "Resources", "Art", "Tree", "bole_foot.png")
COLS = 96


def main():
    a = np.asarray(Image.open(PATH).convert("RGBA"))
    h, w = a.shape[0], a.shape[1]
    ink = a[:, :, 3] > 110
    print("SILHOUETTE %s  %dx%d px  ink %.3f  clear px inside the canvas %d"
          % (os.path.basename(PATH), w, h, ink.mean(), int((~ink).sum())))

    step_x = max(1, w // COLS)
    print("MAP (each row is one canvas row, # = ink):")
    for y in range(0, h, 2):
        line = "".join("#" if ink[y, x:x + step_x].any() else "." for x in range(0, w, step_x))
        print("  %2d %s" % (y, line))

    print("CONTOUR lowest inked row per column (every 4 px):")
    lows = []
    for x in range(0, w, 4):
        col = np.nonzero(ink[:, x])[0]
        lows.append((x, int(col.max()) if col.size else -1))
    line = " ".join("%d:%d" % (x, y) for x, y in lows)
    print("  " + line)

    ys = [y for _, y in lows if y >= 0]
    print("CONTOUR summary: lowest %d, highest %d, spread %d px"
          % (max(ys), min(ys), max(ys) - min(ys)))

    # straight runs: consecutive columns whose lowest row stays within a 2 px band IN TOTAL.
    # A run of 4 columns within 1 px is just a slope (any smooth curve has them) - what the
    # eye reads as a cut is a LONG stretch that does not descend at all.
    runs = []
    cur = [lows[0]]
    for (x1, y1), (x2, y2) in zip(lows, lows[1:]):
        if y1 >= 0 and y2 >= 0 and abs(y2 - cur[0][1]) <= 2:
            cur.append((x2, y2))
        else:
            if len(cur) >= 8:
                runs.append((cur[0][0], cur[-1][0], min(y for _, y in cur), len(cur)))
            cur = [(x2, y2)]
    if len(cur) >= 8:
        runs.append((cur[0][0], cur[-1][0], min(y for _, y in cur), len(cur)))
    print("STRAIGHT bottom runs: 8+ columns whose lowest row stays in a 2 px band (a flat edge):")
    for (xa, xb, y, n) in runs:
        print("  x %3d..%3d at row %d (%d columns, %.3f of the width)"
              % (xa, xb, y, n, (xb - xa) / float(w)))
    if not runs:
        print("  none")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
