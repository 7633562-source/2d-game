#!/usr/bin/env python3
"""ART-0 sign cells, pass 4: verify the repaint on the shipped bytes.

Two questions, both answered from pixels rather than from the generator's own report:

1. Is the change RGB-only? The chequer is clipped by the banner silhouette, so the alpha channel
   must be byte-identical to the previous revision and no opaque pixel may appear or disappear.
2. Is the grid square on the canvas - and what does one cell measure at the size the level draws
   the piece (0.18 x 0.90 m at ortho 4.6 = 117.39 px/m, `Assets/GameProcess.cs:939`)?

Run from the project root (needs `git show HEAD:...` for the previous revision):
    git show HEAD:Assets/Resources/Level/sign_finish.png > "$LOCALAPPDATA/Temp/grok-burn/sign-before.png"
    python Tools/art0-sign-cells-verify.py "$LOCALAPPDATA/Temp/grok-burn/sign-before.png"
"""

import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHIPPED = os.path.join(ROOT, "Assets", "Resources", "Level", "sign_finish.png")
PX_PER_M = 1080.0 / (2.0 * 4.6)          # the level's own lens
BOX = (0.18, 0.90)                       # LevelTest1.cs:3059
BANNER = (18, 10, 61, 196)               # left, top, right, bottom of the cloth (post_r + 1 .. fly)


def runs(values):
    """Lengths of consecutive equal values -> the cell size along one line."""
    out, n = [], 1
    for a, b in zip(values, values[1:]):
        if a == b:
            n += 1
        else:
            out.append(n)
            n = 1
    out.append(n)
    return out


def main():
    before_path = sys.argv[1]
    before = np.asarray(Image.open(before_path).convert("RGBA")).astype(int)
    after = np.asarray(Image.open(SHIPPED).convert("RGBA")).astype(int)
    assert before.shape == after.shape, "canvas changed: %s -> %s" % (before.shape, after.shape)

    alpha_same = bool((before[:, :, 3] == after[:, :, 3]).all())
    opaque_before = int((before[:, :, 3] > 0).sum())
    opaque_after = int((after[:, :, 3] > 0).sum())
    rgb_changed = int(((before[:, :, :3] != after[:, :, :3]).any(axis=2)).sum())
    print("canvas                %dx%d" % after.shape[1::-1])
    print("alpha byte-identical  %s" % alpha_same)
    print("opaque pixels         %d -> %d" % (opaque_before, opaque_after))
    print("RGB pixels changed    %d of %d (%.2f %%)"
          % (rgb_changed, after.shape[0] * after.shape[1], 100.0 * rgb_changed / after.shape[0] / after.shape[1]))

    # cell pitch on the canvas: the two cloth colours (FLAG graphite / FLAG_LIGHT warm white) meet
    # at every cell boundary, so the distance between those meetings is the pitch. Counting runs
    # instead would be spoiled by the hoist cues (x 18-22) and the rim (x 61).
    CH = [(34, 32, 38), (240, 237, 228)]     # gen-level-textures.py FLAG / FLAG_LIGHT
    col = [tuple(c) for c in after[10:196, 30, :3]]
    row = [tuple(c) for c in after[103, 18:62, :3]]
    col_marks = [i for i in range(1, len(col)) if col[i] in CH and col[i - 1] in CH and col[i] != col[i - 1]]
    row_marks = [i for i in range(1, len(row)) if row[i] in CH and row[i - 1] in CH and row[i] != row[i - 1]]

    def mode(vals):
        return max(set(vals), key=vals.count) if vals else 0

    cell_h = mode([b - a for a, b in zip(col_marks, col_marks[1:])])
    cell_w = mode([b - a for a, b in zip(row_marks, row_marks[1:])])
    print("\ncanvas chequer        cell boundaries down the cloth %s -> pitch %s px" % (col_marks[:8], cell_h))
    print("                      cell boundaries across it    %s -> pitch %s px"
          % ([m + 18 for m in row_marks[:8]], cell_w))

    sprite_scale = BOX[0] * PX_PER_M / after.shape[1]
    print("\nat the level's %.2f px/m (piece %.1f x %.1f px):" % (
        PX_PER_M, after.shape[1] * sprite_scale, after.shape[0] * sprite_scale))
    print("  cloth               %.1f x %.1f px" % (
        (BANNER[2] - BANNER[0] + 1) * sprite_scale, (BANNER[3] - BANNER[1] + 1) * sprite_scale))
    print("  one cell            %.1f x %.1f px  (aspect h/w %.2f)"
          % (cell_w * sprite_scale, cell_h * sprite_scale,
             (cell_h / float(cell_w)) if cell_w else 0.0))

    colours = set(map(tuple, after.reshape(-1, 4).tolist()))
    rb = set(map(tuple, before.reshape(-1, 4).tolist()))
    print("\ndistinct RGBA         %d -> %d" % (len(rb), len(colours)))
    print("file size            %d B -> %d B" % (os.path.getsize(before_path), os.path.getsize(SHIPPED)))


if __name__ == "__main__":
    main()
