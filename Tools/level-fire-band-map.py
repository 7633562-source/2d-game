"""LEVEL 20.09: WHERE did the two builds' FIRE frames change?

The gallery catalogue prints "logs dressed 0/3" on the freshly built player while
the artist's nineteenth pass wired the three log canvases into the fire row. This
reads the two shipped FIRE stills offline and says where the difference lives:

  * the bounding box of the changed pixels;
  * a 20-px row histogram of them, so the change can be named as the log band
    (low, at the ground) or the flame/core (above it);
  * the fire's own ink box (warm pixels: red clearly above blue), which is what
    the logs and the flame both are - the night ground is not.

No Unity, read-only.

Usage:
    python Tools/level-fire-band-map.py <old.png> <new.png>
"""
import sys

import numpy as np
from PIL import Image


def warm_mask(a):
    """The fire itself: warm ink (r - b), the logs' brown and the flame's orange."""
    return (a[:, :, 0] - a[:, :, 2]) > 25


def main():
    old_path, new_path = sys.argv[1], sys.argv[2]
    a = np.asarray(Image.open(old_path).convert("RGB")).astype(np.int32)
    b = np.asarray(Image.open(new_path).convert("RGB")).astype(np.int32)

    warm = warm_mask(b)
    ys, xs = np.nonzero(warm)
    print("NEW fire ink (warm) box: x %d..%d  y %d..%d  pixels %d of %d"
          % (xs.min(), xs.max(), ys.min(), ys.max(), warm.sum(), warm.size))

    print("--- warm ink per 20-px row band (NEW) ---")
    for row in range(0, b.shape[0], 20):
        n = int(warm[row:row + 20].sum())
        if n:
            print("  y %4d..%4d  warm %6d" % (row, row + 19, n))

    changed = np.abs(a - b).sum(axis=2) > 12
    ys, xs = np.nonzero(changed)
    print("changed pixels: %d  box x %d..%d  y %d..%d"
          % (changed.sum(), xs.min(), xs.max(), ys.min(), ys.max()))
    print("--- changed pixels per 20-px row band ---")
    for row in range(0, b.shape[0], 20):
        n = int(changed[row:row + 20].sum())
        if n:
            print("  y %4d..%4d  changed %6d  (of which warm ink %6d)"
                  % (row, row + 19, n, int((changed & warm)[row:row + 20].sum())))

    print("--- inside the fire ink box only ---")
    x0, x1 = xs.min(), xs.max()
    win = np.abs(a - b).sum(axis=2)
    warm_box = warm
    ys2, xs2 = np.nonzero(warm_box)
    for tag, img in (("OLD", a), ("NEW", b)):
        cut = img[warm_box]
        colours = np.unique(cut, axis=0)
        print("  %s fire-ink pixels %d, distinct colours %d, std %s, mean %s"
              % (tag, cut.shape[0], colours.shape[0],
                 cut.std(axis=0).round(1).tolist(), cut.mean(axis=0).round(1).tolist()))


main()
