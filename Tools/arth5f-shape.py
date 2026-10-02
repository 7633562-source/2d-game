#!/usr/bin/env python3
"""ART-H5f - the canvas' alpha as an ASCII map plus the ink profile near the ends.

Usage: python Tools/arth5f-shape.py thigh [--map 60]
Prints:
  * an ASCII map of the alpha (>=16) grid, '#' ink, '.' empty, with the pivot row marked;
  * the ink x-extent (metres, pivot-local) at rows sampled from each end of the canvas.
"""
import argparse
import os
import re

import numpy as np
from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
ART = os.path.join(PROJ, "Assets", "Resources", "Art", "Human")

ap = argparse.ArgumentParser()
ap.add_argument("key")
ap.add_argument("--map", type=int, default=56)
args = ap.parse_args()

meta = open(os.path.join(ART, args.key + ".png.meta"), encoding="utf-8").read()
ppu = float(re.search(r"spritePixelsToUnits: ([0-9.]+)", meta).group(1))
piv = re.search(r"spritePivot: \{x: ([0-9.]+), y: ([0-9.]+)\}", meta)
px_, py_ = float(piv.group(1)), float(piv.group(2))
img = np.array(Image.open(os.path.join(ART, args.key + ".png")).convert("RGBA"))
h, w = img.shape[0], img.shape[1]
a = img[:, :, 3] > 16
pivot_row = (1.0 - py_) * h

n = args.map
ys = np.linspace(0, h, n, endpoint=False).astype(int)
xs = np.linspace(0, w, n, endpoint=False).astype(int)
print(f"{args.key}: {w}x{h} px, ppu {ppu:g}, pivot y {py_:.4f} -> row {pivot_row:.1f}")
print(f"scale: 1 col = {w/n/ppu*1000:.2f} mm, 1 row = {h/n/ppu*1000:.2f} mm")
for ry in ys:
    row = a[ry:ry + max(1, h // n)]
    line = "".join("#" if (row[:, rx:rx + max(1, w // n)].any() if row.ndim == 2 else False) else "." for rx in xs)
    mark = "P" if abs((ry + h // (2 * n)) - pivot_row) < h / n else " "
    ym = (pivot_row - (ry + h / (2 * n))) / ppu
    print(f"{ym:+7.4f}{mark} {line}")

print("--- ink x-extent per row (pivot-local m) ---")
rows = sorted(set(list(range(0, min(h, 60))) + list(range(max(0, h - 60), h)) + [int(pivot_row)]))
prev = None
for r in rows:
    ink = np.where(a[r])[0]
    if len(ink) == 0:
        ext = "none"
    else:
        ext = f"x {(ink.min() + 0.5 - px_ * w) / ppu:+.4f} .. {(ink.max() + 0.5 - px_ * w) / ppu:+.4f} m"
    ym = (pivot_row - (r + 0.5)) / ppu
    if ext != prev or r in (0, h - 1, int(pivot_row)):
        print(f"row {r:4d} y {ym:+.4f} m  {ext}")
        prev = ext
