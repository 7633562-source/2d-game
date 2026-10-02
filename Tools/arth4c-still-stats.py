#!/usr/bin/env python3
"""ART-H4c: per-still stats for the overlay proof - size, picture px, near-black px.

Usage: python Tools/arth4c-still-stats.py Trials/Runs/arth4b-still-knee-bent.png [...]
Near-black = sum(RGB) < 24. The rig's debug draw (skeleton bars 0.014 m wide, joint
markers) lands in these windows on a capture without -rigOverlay 0; a clean still
carries only the drawing's own lines.
"""
import sys

import numpy as np
from PIL import Image


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    for p in argv[1:]:
        arr = np.asarray(Image.open(p).convert("RGB"))
        s = arr.sum(axis=2)
        print("%s  %dx%d  picture px=%d  near-black px=%d"
              % (p, arr.shape[1], arr.shape[0], int((s > 24).sum()), int((s < 24).sum())))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
