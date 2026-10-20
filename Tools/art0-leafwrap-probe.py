#!/usr/bin/env python3
"""ART-0 leaf wrap probe: measure a tiled material's wrap seam independently of the generator.

Reports, per image and per axis:
  cut / inner   - |first edge pixel - last edge pixel| vs |neighbour| in 0-255 levels.
  seam rank     - where the wrap step sits in the ranked list of ALL the steps the tiled
                  sheet shows (a 3 x 3 repeat). Rank 100 % means "the worst step in the whole
                  sheet is at the seam", i.e. a line the eye can find; a rank inside the
                  interior distribution means the seam is just another neighbour step.
                  This is the metric the ART-0 card's rolled-crop trap needed: a mean cannot
                  see a step that has moved inside, and a rank cannot be gamed by moving it.

Also writes the blind A/B sheets the eye reads:
  <out>/<name>_3x3_blind.png  - two 3 x 3 repeats side by side, native 1:1
  <out>/<name>_game_blind.png - the same two, each tile drawn 64 px (the swatch scale)

Run: python Tools/art0-leafwrap-probe.py OLD.png NEW.png OUTDIR [name]
"""

import os
import sys

import numpy as np
from PIL import Image


def cut_inner(a, axis):
    cut = np.abs(a.take(0, axis=axis).astype(np.int16) - a.take(-1, axis=axis).astype(np.int16))
    inner = np.abs(np.diff(a.astype(np.int16), axis=axis))
    return float(cut.mean()), float(inner.mean())


def seam_rank(a, axis, repeat=3):
    """Rank (0-100 %) of the wrap step among every step of a `repeat` x `repeat` tiling."""
    sheet = np.concatenate([np.concatenate([a] * repeat, axis=1)] * repeat, axis=0)
    steps = np.abs(np.diff(sheet.astype(np.int16), axis=axis)).mean(axis=2)     # (rows, cols-1)
    n = a.shape[axis]
    seam_idx = [(k + 1) * n - 1 for k in range(repeat - 1)]        # step that crosses a wrap
    seam = np.array([steps.take(i, axis=axis).mean() for i in seam_idx])
    keep = np.setdiff1d(np.arange(steps.shape[axis]), np.array(seam_idx))
    flat = np.sort(steps.take(keep, axis=axis).ravel())
    pct = [float((flat < s).mean() * 100.0) for s in seam]
    return float(np.mean(seam)), float(np.max(pct)), float(np.median(pct))


def sheet(img, tile, tiles_x, tiles_y):
    small = img.resize((tile, tile), Image.LANCZOS)
    out = Image.new("RGB", (tile * tiles_x, tile * tiles_y))
    for y in range(tiles_y):
        for x in range(tiles_x):
            out.paste(small, (x * tile, y * tile))
    return out


def main():
    old_path, new_path, outdir = sys.argv[1], sys.argv[2], sys.argv[3]
    name = sys.argv[4] if len(sys.argv) > 4 else os.path.splitext(os.path.basename(new_path))[0]
    os.makedirs(outdir, exist_ok=True)
    old = Image.open(old_path).convert("RGB")
    new = Image.open(new_path).convert("RGB")
    print("old %s  new %s  size %s" % (old_path, new_path, old.size))
    for label, img in (("OLD", old), ("NEW", new)):
        a = np.asarray(img).astype(np.float64)
        for axis, ax_name in ((1, "X"), (0, "Y")):
            cut, inner = cut_inner(a, axis)
            seam, pct_max, pct_med = seam_rank(a, axis)
            print("%s %s: cut %.2f inner %.2f ratio %.2f | seam step %.2f rank max %.1f%% median %.1f%%"
                  % (label, ax_name, cut, inner, cut / inner, seam, pct_max, pct_med))

    ob, nb = np.asarray(old).astype(np.int16), np.asarray(new).astype(np.int16)
    changed = np.abs(ob - nb).max(axis=2) > 0
    band = 32
    interior = changed.copy()
    interior[:, -band:] = False
    interior[-band:, :] = False
    if changed.any():
        ys, xs = np.nonzero(changed)
        print("old vs new: %d px of %d (%.2f %%), bbox x %d..%d y %d..%d | outside the %d px tail "
              "bands: %d px" % (int(changed.sum()), changed.size, 100.0 * changed.mean(),
                                xs.min(), xs.max(), ys.min(), ys.max(), band, int(interior.sum())))

    side = 900
    pair = Image.new("RGB", (side * 2 + 12, side), (24, 24, 28))
    for i, img in enumerate((old, new)):
        pair.paste(sheet(img, side // 3, 3, 3), (i * (side + 12), 0))
    p1 = os.path.join(outdir, name + "_3x3_blind.png")
    pair.save(p1)

    game = Image.new("RGB", (64 * 12 * 2 + 24, 64 * 5), (24, 24, 28))
    for i, img in enumerate((old, new)):
        game.paste(sheet(img, 64, 12, 5), (i * (64 * 12 + 24), 0))
    p2 = os.path.join(outdir, name + "_game_blind.png")
    game.save(p2)
    print("wrote %s and %s" % (p1, p2))


if __name__ == "__main__":
    main()
