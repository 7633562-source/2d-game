#!/usr/bin/env python3
"""ART-H5f - measure the limb canvases: drawn box, pivot, ink reach vs the box ends.

Reads the shipped canvases (Assets/Resources/Art/Human) and the .meta import
settings (pixelsToUnits, pivot) and prints, in metres, where the drawn box sits
and how far the ink (alpha) reaches above/below the collider box ends.

Box sizes come from Assets/Human.cs (thighSize 0.16x0.43, shinSize 0.11x0.43,
upperArmSize 0.10x0.33, lowerArmSize 0.08x0.34); the box centre is the pivot.

The dress puts the sprite's pivot on the Visual origin = the box centre, so the
drawn box is centred on the box centre: box top = +boxY/2, box bottom = -boxY/2
in pivot-local metres.  Ink past the box ends is the FILL/SKIRT reach.
"""
import os
import re
import sys

import numpy as np
from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
ART = os.path.join(PROJ, "Assets", "Resources", "Art", "Human")

BOX = {
    "thigh": (0.16, 0.43),
    "shin": (0.11, 0.43),
    "upper": (0.10, 0.33),
    "lower": (0.08, 0.34),
    "pelvis": (0.24, 0.16),
    "torso": (0.24, 0.34),
}


def meta(path):
    txt = open(path, "r", encoding="utf-8").read()
    ppu = re.search(r"spritePixelsToUnits:\s*([\d.]+)", txt)
    piv = re.search(r"spritePivot:\s*\{\s*x:\s*([-\d.eE]+),\s*y:\s*([-\d.eE]+)\s*\}", txt)
    rect = re.search(r"rect:\s*\n\s*serializedVersion:\s*2\s*\n\s*x:\s*([-\d]+)\s*\n\s*y:\s*([-\d]+)\s*\n\s*width:\s*([\d]+)\s*\n\s*height:\s*([\d]+)", txt)
    return (float(ppu.group(1)) if ppu else None,
            (float(piv.group(1)), float(piv.group(2))) if piv else None)


for key, (bw, bh) in BOX.items():
    png = os.path.join(ART, key + ".png")
    m = os.path.join(ART, key + ".png.meta")
    ppu, piv = meta(m)
    img = np.array(Image.open(png).convert("RGBA"))
    h, w = img.shape[0], img.shape[1]
    a = img[:, :, 3] > 8
    rows = np.where(a.any(axis=1))[0]
    cols = np.where(a.any(axis=0))[0]
    ink_top_px = int(rows.min()); ink_bot_px = int(rows.max())
    ink_l_px = int(cols.min()); ink_r_px = int(cols.max())
    # metres: image y down; sprite y up.  Pivot px from the top = (1 - piv.y) * h
    pivot_px_y = (1.0 - piv[1]) * h
    px2m = 1.0 / ppu
    drawn_w = w / ppu; drawn_h = h / ppu
    # pivot-local metres: y = (pivot_px_y - row) / ppu  (row 0 = top)
    def ym(row_px):
        return (pivot_px_y - row_px) / ppu
    ink_top_m = ym(ink_top_px)     # highest ink (nearest the proximal end)
    ink_bot_m = ym(ink_bot_px)     # lowest ink
    # box ends in pivot-local m
    box_top = bh / 2.0; box_bot = -bh / 2.0
    print(f"{key:6s} png {w}x{h} ppu {ppu:g} drawn {drawn_w:.4f}x{drawn_h:.4f} m  pivot=({piv[0]:.4f},{piv[1]:.4f}) pivot_px_y {pivot_px_y:.1f}")
    print(f"       drawn top {ym(0):+.4f} m drawn bottom {ym(h-1):+.4f} m | box top {box_top:+.4f} box bottom {box_bot:+.4f}")
    print(f"       ink top {ink_top_m:+.4f} m ink bottom {ink_bot_m:+.4f} m (rows {ink_top_px}..{ink_bot_px})")
    print(f"       ink past box top {ink_top_m - box_top:+.4f} m | ink past box bottom {box_bot - ink_bot_m:+.4f} m")
    print(f"       ink cols {ink_l_px}..{ink_r_px} px -> y-half-width at centre: {(ink_r_px-ink_l_px)/2.0*px2m:.4f} m")
