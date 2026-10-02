#!/usr/bin/env python3
"""ART-H4 probe: cut and magnify a crop of a still, for the eye.

Usage: python Tools/arth4-zoom.py <src.png> <x0,y0,x1,y1> <factor> <out.png>
""" 
import os
import sys

from PIL import Image


def main():
    if len(sys.argv) != 5:
        print(__doc__)
        return 2
    src, box, factor, out = sys.argv[1], sys.argv[2], float(sys.argv[3]), sys.argv[4]
    x0, y0, x1, y1 = [int(v) for v in box.split(",")]
    im = Image.open(src).convert("RGB")
    crop = im.crop((x0, y0, min(x1, im.width), min(y1, im.height)))
    zoom = crop.resize((int(crop.width * factor), int(crop.height * factor)), Image.NEAREST)
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    zoom.save(out)
    print("%s  crop %dx%d -> %dx%d at %.1fx" % (out, crop.width, crop.height,
                                                zoom.width, zoom.height, factor))
    return 0


if __name__ == "__main__":
    sys.exit(main())
