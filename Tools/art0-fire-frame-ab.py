#!/usr/bin/env python3
"""ART-0 thirteenth pass: the BLIND window A/B of the campfire row, two builds.

Offline (PIL, no Unity). The acceptance for a picture here is a frame, and the
frame the campfire is judged in is the gallery's FIRE row: it draws the core
alone, at its own 0.16 x 0.38 m box, so the window around it is the only place
where a change to the core canvas can be seen at all.

Two frames of the same window from two player builds, magnified NEAREST, side by
side, with the panels in a SHUFFLED order and the mapping written to a file: a
named sheet reads the reader's expectation back, and this pass has already been
misled by one (the two named reads that rejected the heat ramp both knew which
panel was the candidate). Read the picture, then the map.

Usage (from the project root):

    python Tools/art0-fire-frame-ab.py <a.png> <b.png> <x0,y0,x1,y1> <out.png> [scale]
"""

import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NIGHT_BG = (26, 30, 44)


def warm_box(img, win):
    """Where the drawn core actually is inside the window: warmth, not whiteness."""
    a = np.asarray(img.crop(win).convert("RGB")).astype(np.int32)
    ink = (a[:, :, 0] - a[:, :, 2]) > 25
    ys, xs = np.nonzero(ink)
    if xs.size == 0:
        return None
    return (xs.min(), ys.min(), xs.max(), ys.max(), int(ink.sum()))


def main():
    if len(sys.argv) < 5:
        print(__doc__)
        return 1
    pa, pb, region, out = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
    scale = int(sys.argv[5]) if len(sys.argv) > 5 else 5
    win = tuple(int(v) for v in region.split(","))

    ia = Image.open(pa).convert("RGB")
    ib = Image.open(pb).convert("RGB")
    for name, path, img in (("A", pa, ia), ("B", pb, ib)):
        box = warm_box(img, win)
        print("  %s %s: warm ink in the window: %s"
              % (name, os.path.basename(path),
                 "none" if box is None else "x %d..%d y %d..%d (%d px)"
                 % (box[0], box[2], box[1], box[3], box[4])))

    ca = ia.crop(win).resize(((win[2] - win[0]) * scale, (win[3] - win[1]) * scale),
                             Image.NEAREST)
    cb = ib.crop(win).resize(((win[2] - win[0]) * scale, (win[3] - win[1]) * scale),
                             Image.NEAREST)

    # fixed shuffle: slot 0 = the SECOND file, slot 1 = the first
    sheet = Image.new("RGB", (ca.width * 2 + 16, ca.height), NIGHT_BG)
    sheet.paste(cb, (0, 0))
    sheet.paste(ca, (ca.width + 16, 0))
    sheet.save(out)
    mp = os.path.splitext(out)[0] + "_map.txt"
    with open(mp, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("LEFT  = %s\n" % os.path.basename(pb))
        fh.write("RIGHT = %s\n" % os.path.basename(pa))
        fh.write("window = %s  scale = %dx\n" % (region, scale))
    print("  wrote %s (mapping in %s)" % (os.path.relpath(out, ROOT), os.path.basename(mp)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
