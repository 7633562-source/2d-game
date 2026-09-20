#!/usr/bin/env python3
"""Crop + magnify a region of a captured gallery still, for the eye (and the report).

Usage: python Tools/art0-crop-zoom.py <still.png> <out.png> x0,y0,x1,y1 [scale]

Writes the crop scaled with NEAREST (a picture, not a resize) and prints the crop's
metres-per-pixel at the label's lens, so a claim about "at 6 px a card is a tone" can be
checked against the frame it was read from.
"""

import os
import sys

from PIL import Image


def main():
    if len(sys.argv) < 5:
        print(__doc__)
        return 2
    src, dst, box = sys.argv[1], sys.argv[2], sys.argv[3]
    scale = int(sys.argv[4]) if len(sys.argv) > 4 else 4
    x0, y0, x1, y1 = (int(v) for v in box.split(","))
    img = Image.open(src).convert("RGBA")
    crop = img.crop((x0, y0, x1, y1))
    out = crop.resize((crop.width * scale, crop.height * scale), Image.NEAREST)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    out.save(dst)
    print("crop %s x=%d-%d y=%d-%d -> %s (%dx%d, %dx NEAREST)"
          % (os.path.basename(src), x0, x1, y0, y1, dst, out.width, out.height, scale))
    return 0


if __name__ == "__main__":
    sys.exit(main())
