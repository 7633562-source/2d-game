"""ART-0 probe: does the tiled ground_cap read as grass, and does it show a repeating tick?

The cap is drawn by StaticPlatform.SpawnSurfaceCap as SpriteDrawMode.Tiled, size
(slabWidth, 0.14 m), sprite 128x14 @ PPU 100 -> it repeats every 1.28 m along every slab.
A single near-black full-height column therefore paints a vertical tick every 1.28 m.

This probe is the eye-check side of the delivery: it prints the per-column profile of the cap
and writes the pictures to judge (cap tiled 8x, the cap sitting on ground_fill, and a
before/after pair when a previous revision is passed in), all at 2-3x nearest upscale.

  python Tools/art0-cap-probe.py [previous-ground_cap.png]
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LEVEL = os.path.join(ROOT, "Assets", "Resources", "Level")
FRAMES = os.path.join(ROOT, "Trials", "Runs", "ART-0-frames")


def load_rgba(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.int16)


def column_profile(cap):
    """Brightness per column over the opaque pixels only (height must not leak into colour)."""
    lum = cap[:, :, :3].mean(axis=2)
    opaque = (cap[:, :, 3] > 0)
    return (lum * opaque).sum(axis=0) / np.maximum(opaque.sum(axis=0), 1)


def report(tag, cap):
    h, w = cap.shape[:2]
    col = column_profile(cap)
    left = col[(np.arange(w) - 1) % w]
    right = col[(np.arange(w) + 1) % w]
    stand = np.minimum(left, right) - col
    step = np.abs(np.diff(col))
    alpha = cap[:, :, 3] > 0
    heights = alpha.sum(axis=0).astype(float)
    spike = heights - np.maximum(heights[(np.arange(w) - 1) % w], heights[(np.arange(w) + 1) % w])
    rgb = cap[:, :, :3].astype(float)
    hit = alpha[:, 0] & alpha[:, w - 1]
    seam = float(np.abs(rgb[hit, 0] - rgb[hit, w - 1]).mean()) if hit.any() else 0.0
    grain = float(np.abs(np.diff(rgb, axis=1)).mean())
    print("%s: %dx%d  column brightness %.1f..%.1f (mean %.1f)  blade height %d..%d px"
          % (tag, w, h, col.min(), col.max(), col.mean(), heights.min(), heights.max()))
    print("   wrap |col0-col127| %.3f   mean neighbour step %.3f   worst stand-out column %.3f at x=%d"
          % (abs(col[0] - col[-1]), step.mean(), stand.max(), int(stand.argmax())))
    print("   columns >=3 below BOTH neighbours: %s"
          % [int(x) for x in np.where(stand > 3.0)[0]])
    print("   columns >=3 above BOTH neighbours: %s"
          % [int(x) for x in np.where(-stand > 3.0)[0]])
    print("   worst height spike %d px (taller than BOTH neighbours)"
          % int(spike.max()))
    print("   worst BRIGHT stand-out column %.3f at x=%d"
          % ((-stand).max(), int((-stand).argmax())))
    print("   seam across the wrap %.2f vs the tile's own grain %.2f (levels, 0-255)"
          % (seam, grain))
    return col


def band(cap, width, repeats=None):
    reps = repeats or (width // cap.shape[1] + 1)
    return np.tile(cap, (1, reps, 1))[:, :width, :].astype(np.uint8)


def upscale(a, units):
    img = Image.fromarray(a, "RGBA")
    return img.resize((a.shape[1] * units, a.shape[0] * units), Image.NEAREST)


def main():
    before_path = sys.argv[1] if len(sys.argv) > 1 else None
    cap = load_rgba(os.path.join(LEVEL, "ground_cap.png"))
    fill = load_rgba(os.path.join(LEVEL, "ground_fill.png"))
    os.makedirs(FRAMES, exist_ok=True)
    report("cap", cap)
    if before_path:
        before = load_rgba(before_path)
        report("before", before)

    upscale(band(cap, 1024, 8), 3).save(os.path.join(FRAMES, "cap-tiled-8x.png"))
    ground = band(fill[:192, :, :], 1024, 1)
    strip = np.concatenate([band(cap, 1024), ground], axis=0)
    upscale(strip, 2).save(os.path.join(FRAMES, "cap-on-ground.png"))
    print("wrote cap-tiled-8x.png (%dx%d) and cap-on-ground.png" % (1024 * 3, cap.shape[0] * 3))

    if before_path:
        sep = np.tile(np.array([[[30, 30, 30, 255]]], np.uint8), (6, 1024, 1))
        rows = [band(before, 1024), band(fill[:96, :, :], 1024, 1), sep,
                band(cap, 1024), band(fill[:96, :, :], 1024, 1)]
        pair = np.concatenate(rows, axis=0)
        upscale(pair, 2).save(os.path.join(FRAMES, "cap-before-after.png"))
        print("wrote cap-before-after.png (before on top, after below, same 8 x 1.28 m tiling)")


if __name__ == "__main__":
    main()
