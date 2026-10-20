#!/usr/bin/env python3
"""ART-0 (29th cycle) - what does a SMALL leaf-dressed part actually draw?

Three vision reads of the shipped frame (`Trials/Runs/art0leaf28_tree.png`) named the same
thing on the oak's trunk: "identical small green sprites of buds/moss", "flat green chips,
no volume", "they do not read as oak leaves; they read as a sticker or unfinished
placeholder". `Tools/art0-trunk-band-probe.py` measures them: 13 green clusters in the tree
band, six of them ON the trunk column, 16-32 px across, i.e. 0.04-0.08 m at the tree lens
(414.2 px/m).

`ArtParts.Dress` draws a material part `Tiled` at the material's authored period
(`ArtParts.LeafTileMeters = 0.5 m`), so a part SMALLER than the tile shows a WINDOW of
`Art/leaf.png` at the canvas' own scale - 1024 px/m, i.e. a 0.04-0.08 m part shows a 39-80 px
window of the 512 px canvas, centred on the sprite's pivot.

This tool reads the shipped canvas only (no Unity, no build) and answers:

  * the coverage of leaf ink inside every wrapped window of the sizes the trunk's chips have
    (mean, p05, p95), so "one whole leaf" and "a fragment of a leaf" are told apart;
  * how many ink components touch the window's own border - a component that runs off the
    window edge is a CUT leaf, which is what makes a chip read as a fragment;
  * a contact sheet of 12 windows at 6x, in the shipped tone palette, for the eye.

Usage: python Tools/art0-leaf-window-probe.py [canvas.png]
"""
import os
import sys

import numpy as np
from PIL import Image

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJ, "Assets", "Resources", "Art")
RUNS = os.path.join(PROJ, "Trials", "Runs")

LEAF_SHADOW_LUM = 70       # the canvas' own dark background reads under this; leaf tones over
WINDOWS = (39, 49, 61, 80, 103)


def load(path):
    img = Image.open(path).convert("RGB")
    return np.asarray(img, dtype=np.float64), img


def lum(rgb):
    return 0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2]


def components(mask):
    """4-connected components on a small window; returns (count, components_touching_border)."""
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    touching = 0
    count = 0
    for y0 in range(h):
        for x0 in range(w):
            if not mask[y0, x0] or seen[y0, x0]:
                continue
            count += 1
            stack = [(y0, x0)]
            seen[y0, x0] = True
            border = False
            while stack:
                y, x = stack.pop()
                if y == 0 or x == 0 or y == h - 1 or x == w - 1:
                    border = True
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if border:
                touching += 1
    return count, touching


def window(rgb, size, cy, cx):
    """A wrapped `size` x `size` window centred on (cy, cx)."""
    h, w, _ = rgb.shape
    ys = (np.arange(cy - size // 2, cy - size // 2 + size) % h)
    xs = (np.arange(cx - size // 2, cx - size // 2 + size) % w)
    return rgb[np.ix_(ys, xs)]


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ART, "leaf.png")
    rgb, img = load(path)
    h, w, _ = rgb.shape
    L = lum(rgb)
    print("ART-0-LEAF-WINDOW canvas=%s %dx%d lum mean=%.1f p05=%.1f p95=%.1f"
          % (os.path.basename(path), w, h, L.mean(), np.percentile(L, 5), np.percentile(L, 95)))

    for size in WINDOWS:
        cov, cut, comps = [], [], []
        step = max(1, size // 4)
        for cy in range(0, h, step):
            for cx in range(0, w, step):
                win = window(rgb, size, cy, cx)
                m = lum(win) > LEAF_SHADOW_LUM
                cov.append(m.mean())
                c, t = components(m)
                comps.append(c)
                cut.append(t)
        cov = np.asarray(cov)
        cut = np.asarray(cut)
        print("  window %3d px (%.3f m): coverage mean=%.2f p05=%.2f p95=%.2f ; "
              "leaf components mean=%.2f ; windows with a CUT leaf on the border: %.0f%% ; "
              "windows with NO ink: %.0f%%"
              % (size, size / 1024.0, cov.mean(), np.percentile(cov, 5), np.percentile(cov, 95),
                 float(np.mean(comps)), 100.0 * (cut > 0).mean(), 100.0 * (cov < 0.02).mean()))

    # contact sheet: 12 windows of the chip size (61 px), 6x
    size = 61
    picks = [(64, 64), (64, 192), (64, 320), (192, 64), (192, 192), (192, 320),
             (320, 64), (320, 192), (320, 320), (448, 128), (448, 256), (448, 384)]
    zoom = 6
    sheet = Image.new("RGB", (size * zoom, size * zoom * len(picks)), (255, 0, 255))
    for i, (cy, cx) in enumerate(picks):
        win = window(rgb, size, cy, cx)
        tile = Image.fromarray(win.astype(np.uint8)).resize((size * zoom, size * zoom), Image.NEAREST)
        sheet.paste(tile, (0, i * size * zoom))
    out = os.path.join(RUNS, "art0-leaf-window-sheet.png")
    sheet.save(out)
    print("ART-0-LEAF-WINDOW sheet -> " + out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
