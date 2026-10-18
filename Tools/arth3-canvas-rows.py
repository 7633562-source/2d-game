#!/usr/bin/env python3
"""ART-H3 probe: one canvas' rows near a box edge, as numbers.

Prints, for a canvas end, each row's offset from the SHIPPING BOX edge (in px and mm), the
ink width (px) and the mean tone of the ink - so the mirror line the generator's fill draws
and the art's own drawn cut rim are told apart, and the seam bridge can be placed from the
real rows.

Usage: python Tools/arth3-canvas-rows.py --part shin --end bottom [--rows 70]
       python Tools/arth3-canvas-rows.py --part foot --end top --rows 110
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")
ALPHA_MIN = 16
BOXES = {  # Human.cs sizes; the foot canvas is dressed on the HEEL box
    "shin": (0.11, 0.43), "foot": (0.26 * 0.70, 0.09), "thigh": (0.16, 0.43),
    "upper": (0.10, 0.33), "lower": (0.08, 0.34), "hand": (0.06, 0.16),
    "pelvis": (0.24, 0.16), "torso": (0.24, 0.34), "neck": (0.12, 0.09),
    "head": (0.20, 0.23), "toe": (0.26 * 0.30, 0.09),
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--part", required=True)
    ap.add_argument("--end", default="bottom", choices=["top", "bottom"])
    ap.add_argument("--rows", type=int, default=70)
    args = ap.parse_args()

    p = os.path.join(ART, args.part + ".png")
    im = Image.open(p).convert("RGBA")
    a = np.array(im)
    meta = open(p + ".meta", encoding="utf-8").read()
    import re
    ppu = float(re.search(r"spritePixelsToUnits: ([0-9.]+)", meta).group(1))
    piv = re.search(r"spritePivot: \{x: ([0-9.]+), y: ([0-9.]+)\}", meta)
    px, py = float(piv.group(1)), float(piv.group(2))
    W, H = im.width, im.height
    bw, bh = BOXES[args.part]
    # the box inside the canvas: its centre is the pivot
    cx = px * W
    cy = (1.0 - py) * H
    box_top = cy - bh * ppu / 2.0
    box_bot = cy + bh * ppu / 2.0
    print("%s %dx%d ppu %.0f pivot(%.4f,%.4f) box rows %.1f..%.1f (top..bottom px)"
          % (args.part, W, H, ppu, px, py, box_top, box_bot))
    m = a[:, :, 3] > ALPHA_MIN
    if args.end == "bottom":
        rng = range(int(box_bot) + args.rows, int(box_bot) - args.rows, -1)
    else:
        rng = range(max(0, int(box_top) - args.rows), int(box_top) + args.rows)
    print("row  off_box_mm  alpha=255 px  n_ink  meanRGB  meanLum")
    for y in rng:
        if y < 0 or y >= H:
            continue
        line = a[y]
        ink = line[line[:, 3] > ALPHA_MIN]
        if len(ink) == 0:
            print("%4d %+8.1f %12d %6d   . . ." % (y, (y - box_bot) / ppu * 1000.0, 0, 0))
            continue
        hard = int((line[:, 3] == 255).sum())
        rgb = ink[:, :3].mean(axis=0)
        lum = 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]
        print("%4d %+8.1f %12d %6d  %3d,%3d,%3d  %6.1f"
              % (y, (y - box_bot) / ppu * 1000.0, hard, len(ink), rgb[0], rgb[1], rgb[2], lum))
    return 0


if __name__ == "__main__":
    sys.exit(main())
