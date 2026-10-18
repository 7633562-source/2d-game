#!/usr/bin/env python3
"""ART-0 two-label frame A/B: the same crop out of two capture labels, side by side.

A picture change is accepted by the eye on the frame the game draws, not on the tile.
This puts the OLD build's crop on the LEFT and the NEW build's on the RIGHT, with a
separator and no label, so the read is blind: the numbers (frame diff) say where the
difference is, the eye says which side is the surface.

Usage: python Tools/art0-leafwrap-ab.py <oldLabel> <newLabel> <still> x0 y0 x1 y1 <zoom> <out>
"""

import os
import sys

from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials/Runs")


def main():
    old, new, still, x0, y0, x1, y1, zoom, out = sys.argv[1:10]
    x0, y0, x1, y1, zoom = int(x0), int(y0), int(x1), int(y1), int(zoom)
    sheet = None
    for i, label in enumerate((old, new)):
        p = os.path.join(RUNS, "%s_%s.png" % (label, still))
        img = Image.open(p).convert("RGB")
        c = img.crop((x0, y0, x1, y1))
        c = c.resize((c.width * zoom, c.height * zoom), Image.NEAREST)
        if sheet is None:
            sheet = Image.new("RGB", (c.width * 2 + 12, c.height), (200, 0, 200))
        sheet.paste(c, (i * (c.width + 12), 0))
    sheet.save(out)
    print("wrote %s  (%dx%d, LEFT=%s RIGHT=%s, crop %d,%d..%d,%d x%d)"
          % (out, sheet.width, sheet.height, old, new, x0, y0, x1, y1, zoom))


if __name__ == "__main__":
    main()
