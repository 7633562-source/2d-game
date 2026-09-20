"""ART-0 (fresh eye read follow-up): is the shipped `skin` tile's wrap visible in the drawn swatch?

The gallery's material row draws each 1 x 1 m swatch as `Tiled(key, 0.5 m)`, i.e. a 2 x 2 repeat of the
512 px tile. So a joint in the middle of the swatch IS the tile wrap. A read of the current build's
frame (`Trials/Runs/gallery7_materials.png`) named exactly that on `skin`.

This prints, for every shipped material tile:
  cut   = mean |first edge pixel - last edge pixel| (X and Y), the wrap step
  grain = mean |neighbour| inside the tile, its own noise
  rank  = where the wrap step sits among every single-pixel step a 3 x 3 repeat shows (percentile)
and writes a 2 x 2 native repeat of each tile to Trials/Runs/art-0-wrap-fresh/ for the eye.

Usage: python Tools/art0-wrap-fresh.py
"""
import os

import numpy as np
from PIL import Image

ROOT = r"E:/Games/2D Game/My project"
ART = os.path.join(ROOT, "Assets", "Resources", "Art")
OUT = os.path.join(ROOT, "Trials", "Runs", "art-0-wrap-fresh")
KEYS = ("skin", "fabric", "denim", "dirt", "grass", "bark", "leaf")


def probe(arr):
    cut_x = float(np.abs(arr[:, 0] - arr[:, -1]).mean())
    cut_y = float(np.abs(arr[0, :] - arr[-1, :]).mean())
    grain_x = float(np.abs(np.diff(arr, axis=1)).mean())
    grain_y = float(np.abs(np.diff(arr, axis=0)).mean())
    rep = np.tile(arr, (3, 3, 1))
    steps_x = np.abs(np.diff(rep, axis=1)).ravel()
    steps_y = np.abs(np.diff(rep, axis=0)).ravel()
    rank_x = float((steps_x < cut_x).mean() * 100.0)
    rank_y = float((steps_y < cut_y).mean() * 100.0)
    return cut_x, cut_y, grain_x, grain_y, rank_x, rank_y


def main():
    os.makedirs(OUT, exist_ok=True)
    print("%-7s %-9s %7s %7s %7s %7s %6s %6s" % ("key", "size", "cutX", "grainX", "cutY", "grainY", "rkX%", "rkY%"))
    for key in KEYS:
        path = os.path.join(ART, key + ".png")
        img = Image.open(path).convert("RGB")
        arr = np.asarray(img).astype(np.float64)
        cx, cy, gx, gy, rx, ry = probe(arr)
        w, h = img.size
        print("%-7s %-9s %7.2f %7.2f %7.2f %7.2f %6.1f %6.1f"
              % (key, "%dx%d" % (w, h), cx, gx, cy, gy, rx, ry))
        rep = Image.new("RGB", (w * 2, h * 2))
        for i in range(2):
            for j in range(2):
                rep.paste(img, (i * w, j * h))
        rep.save(os.path.join(OUT, "%s_2x2.png" % key))
    print("panels ->", OUT)


if __name__ == "__main__":
    main()
