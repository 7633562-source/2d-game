#!/usr/bin/env python3
"""ART-H4d probe: zero the alpha of a canvas' row band (a boundary experiment, offline only).

Usage: python Tools/arth4d-dome-off.py <canvas_dir> <part> <row_from> [<row_to>]
Edits <canvas_dir>/<part>.png in place (the .meta is untouched - the canvas size does not
change).  Prints the row band and how many px it cleared.
"""
import sys

import numpy as np
from PIL import Image


def main(argv):
    if len(argv) < 4:
        print(__doc__)
        return 2
    d, part, r0 = argv[1], argv[2], int(argv[3])
    r1 = int(argv[4]) if len(argv) > 4 else None
    path = d + "/" + part + ".png"
    a = np.array(Image.open(path).convert("RGBA"))
    h = a.shape[0]
    r1 = h if r1 is None else min(r1, h)
    band = a[r0:r1, :, 3]
    cleared = int((band > 16).sum())
    band[:] = 0
    Image.fromarray(a).save(path)
    print("%s rows %d..%d cleared %d ink px (canvas %dx%d)" % (path, r0, r1 - 1, cleared, a.shape[1], h))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
