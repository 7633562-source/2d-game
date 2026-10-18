"""ART-0 (17th pass) step 3: the material set's wrap state on today's shipped bytes.

Two questions, both answered from files, no Unity:

1. What does each material's tile do at its own wrap? `cut` = |first edge pixel - last edge pixel|,
   `inner` = |neighbour pixel| - the card's own metric. A cut no worse than the grain cannot be seen
   to repeat; the ratio is what made `skin` (5.4x) and `fabric` (2.4x) shipping defects and what left
   `bark` alone.

2. Does the delivered frame show the tile repeating? In `art0leaffield_materials.png` the swatch row
   draws each material Tiled, so a 0.5 m tile is exactly `0.5 * px_per_m` apart and the same patch
   comes back - a local 5x5 mean-difference at that shift should collapse if the swatch is a flat
   surface of that material, and stay at its grain if the swatch is not flat.

Also prints the capture's row boxes so the swatch rect can be read instead of guessed.
"""
import json
import os
import re
import sys

import numpy as np
from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials", "Runs")
ART = os.path.join(PROJ, "Assets", "Resources", "Art")
MATERIALS = ["skin", "fabric", "denim", "dirt", "grass", "bark", "leaf"]
LABEL = "art0leaffield"
MATERIALS_ORTHO = 3.5774998664855957
TILE_M = 0.5
HALF = 2


def wrap_stats(a, axis):
    cut = np.abs(a.take(0, axis=axis).astype(np.int16) - a.take(-1, axis=axis).astype(np.int16))
    inner = np.abs(np.diff(a.astype(np.int16), axis=axis))
    return float(cut.mean()), float(inner.mean())


def box_mean(img, half):
    k = 2 * half + 1
    pad = np.pad(img, ((half, half), (half, half), (0, 0)), mode="edge")
    out = np.zeros_like(img, dtype=np.float64)
    for dy in range(k):
        for dx in range(k):
            out += pad[dy:dy + img.shape[0], dx:dx + img.shape[1]]
    return out / (k * k)


def main():
    print("=== 1. shipped tiles: wrap cut vs own grain (0-255 levels, RGB mean) ===")
    print("%-8s %10s %10s %8s   %s" % ("key", "cut X", "cut Y", "ratio", "inner X / Y"))
    for key in MATERIALS:
        a = np.asarray(Image.open(os.path.join(ART, key + ".png")).convert("RGB"))
        cx, ix = wrap_stats(a, 1)
        cy, iy = wrap_stats(a, 0)
        print("%-8s %10.2f %10.2f %8s   %.2f / %.2f"
              % (key, cx, cy, "%.2fx/%.2fx" % (cx / ix, cy / iy), ix, iy))

    with open(os.path.join(RUNS, LABEL + "-gallery.json"), encoding="utf-8") as fh:
        data = json.load(fh)
    print()
    print("=== capture row boxes ===")
    for b in data.get("rowBoxes", []):
        print("  %s" % b)

    img = np.asarray(Image.open(os.path.join(RUNS, LABEL + "_materials.png")).convert("RGB"), dtype=np.float64)
    px_per_m = (img.shape[0] / 2.0) / MATERIALS_ORTHO
    period = TILE_M * px_per_m
    print()
    print("materials frame %s  px/m %.2f  tile %.2f px" % (img.shape[:2], px_per_m, period))
    g = box_mean(img, HALF)
    h, w = g.shape[:2]
    best = []
    for d in range(40, 400):
        if w <= d:
            break
        best.append((float(np.abs(g[:, :w - d] - g[:, d:]).mean()), d))
    best.sort()
    print("the 8 shifts with the lowest horizontal MAD (a real repeat shows as a sharp minimum):")
    for mad, d in best[:8]:
        print("  d=%-4d MAD=%8.3f %s" % (d, mad, "  <- 0.5 m tile" if abs(d - period) < 2 else ""))
    near = [m for m, d in best if abs(d - period) < 2]
    print("MAD at the 0.5 m tile (%.1f px): %s" % (period, ("%.3f" % near[0]) if near else "not in range"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
