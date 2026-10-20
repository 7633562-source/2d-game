#!/usr/bin/env python3
"""ART-H3 probe: where a shot's character stands (text only).

Prints the frame size, the background colour, the character's bbox, and a coarse ASCII
occupancy map (the fraction of ink per cell) so a crop window can be picked without
guessing from the pixels.

Usage: python Tools/arth3-frame-bbox.py Trials/Runs/arth2-still-fullbody.png [--cols 72]
"""
import argparse
import os

import numpy as np
from PIL import Image


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("png")
    ap.add_argument("--cols", type=int, default=72)
    ap.add_argument("--rows", type=int, default=30)
    a = ap.parse_args()
    im = Image.open(a.png).convert("RGB")
    arr = np.asarray(im).astype(np.int16)
    h, w, _ = arr.shape
    bg = arr[2, 2]
    # ink = px far from the background in any channel (the character + any stage marks)
    d = np.abs(arr - bg[None, None, :]).sum(axis=2)
    ink = d > 40
    ys, xs = np.where(ink)
    print("%s  %dx%d  bg=%s  ink px=%d" % (os.path.basename(a.png), w, h,
                                           tuple(bg.tolist()), int(ink.sum())))
    if ink.any():
        print("bbox x %d..%d  y %d..%d" % (xs.min(), xs.max(), ys.min(), ys.max()))
    cw, ch = w / float(a.cols), h / float(a.rows)
    print("map: each cell %.1fx%.1f px, '#'=dense, '+'=half, '.'=few, ' '=empty"
          % (cw, ch))
    for r in range(a.rows):
        y0, y1 = int(r * ch), max(int(r * ch) + 1, int((r + 1) * ch))
        line = []
        for c in range(a.cols):
            x0, x1 = int(c * cw), max(int(c * cw) + 1, int((c + 1) * cw))
            f = ink[y0:y1, x0:x1].mean() if ink[y0:y1, x0:x1].size else 0.0
            line.append("#" if f > 0.6 else ("+" if f > 0.25 else ("." if f > 0.04 else " ")))
        print("%4d|%s|" % (int((y0 + y1) / 2), "".join(line)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
