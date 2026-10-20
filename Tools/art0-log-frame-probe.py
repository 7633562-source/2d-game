#!/usr/bin/env python3
"""ART-0 (nineteenth pass): the logs in a FIRE still of two builds, measured where they changed.

Offline (PIL, no Unity). The FIRE row of the gallery draws the assembled stack at the yard's own
boxes, so the three logs are the lowest band of that row - and they sit ON the painted soil, which
is brown too, so a colour search finds the ground as well. This locates them the honest way: the
pixels that DIFFER between the two builds' FIRE stills (the flame, the ember, the label and the
camera are identical), and then measures those pixels in each image.

What it shows:
  * the OLD picture is HumanSegment.CreateBodySprite tinted LogWood = (0.50, 0.25, 0.00) at alpha
    0.5: ONE colour, blended with whatever is behind it, so a handful of RGB values carry almost
    every pixel;
  * the painted canvas is the wood palette plus bark, grain and a sawn end, so the same pixels
    spread over many tones with a higher saturation.

Usage (from the project root):

    python Tools/art0-log-frame-probe.py <old_still.png> <new_still.png> [x0,y0,x1,y1]
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BOX = (880, 700, 1120, 900)


def load(path):
    full = path if os.path.isabs(path) else os.path.join(ROOT, path)
    return os.path.basename(path), np.asarray(Image.open(full).convert("RGB")).astype(np.int32)


def profile(tag, px):
    if px.size == 0:
        print("  %-28s no pixels" % tag)
        return
    cols = np.unique(px, axis=0)
    counts = np.unique(px, axis=0, return_counts=True)[1]
    top = np.sort(counts)[::-1][:3]
    share = 100.0 * top.sum() / px.shape[0]
    hsv = np.asarray(Image.fromarray(px.astype(np.uint8).reshape(-1, 1, 3), "RGB").convert("HSV"))
    sat = hsv[:, 0, 1].astype(np.float64) / 255.0
    print("  %-28s px=%-7d colours=%-6d top3 share=%5.1f%%  mean rgb=(%.0f, %.0f, %.0f)  sat=%.3f"
          % (tag, px.shape[0], len(cols), share,
             px[:, 0].mean(), px[:, 1].mean(), px[:, 2].mean(), sat.mean()))


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1
    box = tuple(int(v) for v in sys.argv[3].split(",")) if len(sys.argv) > 3 else BOX
    na, a = load(sys.argv[1])
    nb, b = load(sys.argv[2])
    if a.shape != b.shape:
        print("FAIL: the two stills are different sizes (%s vs %s)" % (a.shape, b.shape))
        return 1

    win_a = a[box[1]:box[3], box[0]:box[2]]
    win_b = b[box[1]:box[3], box[0]:box[2]]
    diff = np.abs(win_a - win_b).sum(axis=2) > 3
    n = int(diff.sum())
    print("window %s: %d of %d px differ (%.2f%%)" % (box, n, diff.size, 100.0 * n / diff.size))
    if n == 0:
        return 0
    ys, xs = np.nonzero(diff)
    print("changed bbox in the frame: x %d..%d y %d..%d (%dx%d px = %.2f x %.2f m at 340 px/m)"
          % (xs.min() + box[0], xs.max() + box[0], ys.min() + box[1], ys.max() + box[1],
             xs.max() - xs.min() + 1, ys.max() - ys.min() + 1,
             (xs.max() - xs.min() + 1) / 340.0, (ys.max() - ys.min() + 1) / 340.0))
    # the wood underneath the difference: pixels of each build inside the changed bbox that are
    # brown in EITHER image (so soil-only rows do not count)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    ca = win_a[y0:y1, x0:x1].reshape(-1, 3)
    cb = win_b[y0:y1, x0:x1].reshape(-1, 3)
    print("inside the changed bbox:")
    profile("%s (old)" % na, ca)
    profile("%s (new)" % nb, cb)
    # the same two sets restricted to the pixels that actually moved: the sparks and embers move
    # between any two captures (time-driven), the logs are the rest
    da = win_a[y0:y1, x0:x1].reshape(-1, 3)[diff[y0:y1, x0:x1].ravel()]
    db = win_b[y0:y1, x0:x1].reshape(-1, 3)[diff[y0:y1, x0:x1].ravel()]
    print("on the pixels that moved:")
    profile("%s (old)" % na, da)
    profile("%s (new)" % nb, db)
    if da.size:
        counts = np.unique(da, axis=0, return_counts=True)[1]
        print("  the flat box is one colour: the old frame's biggest single value holds %.1f%% "
              "of the moved pixels (%d px), the new frame's holds %.1f%%"
              % (100.0 * counts.max() / da.shape[0], counts.max(),
                 100.0 * np.unique(db, axis=0, return_counts=True)[1].max() / db.shape[0]))

    full = (x0 + box[0], y0 + box[1], x1 + box[0], y1 + box[1])
    print("\nthe blind pair for the eye (%s):\n  python Tools/art0-fire-frame-ab.py %s %s \"%d,%d,%d,%d\" "
          "Trials/Runs/logs-frame-blind.png 8" % (na, sys.argv[1], sys.argv[2], *full))
    return 0


if __name__ == "__main__":
    sys.exit(main())
