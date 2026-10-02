#!/usr/bin/env python3
"""ART-H3 probe: the tone of each canvas' own cut ends (the last rows/cols of the ink).

For every segment canvas and every end that the generator grows, print the mean RGB and
alpha coverage of the outermost 10 ink lines - so a drawn dark rim (the "cut face" the art
draws) can be seen as numbers before/after the ART-H3 profile, and the seam-bridge width
can be chosen from the real rim depth instead of a guess.
"""
import os
import sys

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")
ALPHA_MIN = 16

ENDS = {
    "pelvis": [("top", 0), ("bottom", 1)],
    "torso": [("top", 0), ("bottom", 1)],
    "neck": [("top", 0), ("bottom", 1)],
    "head": [("bottom", 1)],
    "upper": [("top", 0), ("bottom", 1)],
    "lower": [("top", 0), ("bottom", 1)],
    "hand": [("top", 0)],
    "thigh": [("top", 0), ("bottom", 1)],
    "shin": [("top", 0), ("bottom", 1)],
    "foot": [("top", 0), ("back", -1)],
    "toe": [("back", -1)],
}


def rows_of(a, axis, side):
    """Line indices of the ink, ordered from the END inwards (10 lines)."""
    m = a[:, :, 3] > ALPHA_MIN
    if axis == 0:
        counts = m.sum(axis=1)
    else:
        counts = m.sum(axis=0)
    idx = np.where(counts >= 3)[0]
    if len(idx) == 0:
        return []
    return list(idx[:10]) if side == 0 else list(idx[-10:])[::-1]


def line_stats(a, axis, i):
    line = a[i, :, :] if axis == 0 else a[:, i, :]
    ink = line[line[:, 3] > ALPHA_MIN]
    if len(ink) == 0:
        return None
    rgb = ink[:, :3].mean(axis=0)
    lum = 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]
    return rgb, lum, len(ink)


def main():
    for part, ends in ENDS.items():
        p = os.path.join(ART, part + ".png")
        a = np.array(Image.open(p).convert("RGBA"))
        for name, side in ends:
            axis = 0 if name in ("top", "bottom") else 1
            idx = rows_of(a, axis, 0 if name in ("top", "back") else 1)
            if not idx:
                print("%-6s %-6s no ink" % (part, name))
                continue
            parts = []
            for k, i in enumerate(idx[:6]):
                st = line_stats(a, axis, i)
                if st is None:
                    continue
                rgb, lum, n = st
                parts.append("k%d lum %5.1f rgb %3d,%3d,%3d n%3d" %
                             (k, lum, rgb[0], rgb[1], rgb[2], n))
            print("%-6s %-6s | %s" % (part, name, " | ".join(parts)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
