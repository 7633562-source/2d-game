#!/usr/bin/env python3
"""ART-0 (29th cycle) - what sits ON the oak's trunk, measured on the shipped still.

A vision read of `Trials/Runs/<label>_tree.png` (the oak at the tree lens) named three
things on the trunk: the seams between the rig's own segments, "a coarse horizontal seam
(texture cut line)" near the base, and "identical small green sprites of buds/moss placed
in several places on the trunk". The last one is a claim about ART: every part dressed
with a MATERIAL draws the same window of the same canvas (ArtParts.Dress -> ArtLibrary.Tiled,
one cached Sprite per key+period), so small parts repeat their pixels by construction.

This tool measures the claim instead of repeating it:

  * green clusters (g > r + GREEN_MARGIN and g > b + GREEN_MARGIN) in the tree band, each
    one's bbox and pixel count, and how many of them are PAIRWISE pixel-identical crops
    (same size, same pixels) - that is the repetition the read described;
  * the trunk's own column range at a ladder of rows, from the bark-brown mask, so a crop
    of the trunk can be cut without guessing;
  * a 2x crop of the trunk band next to the clusters' own map, for the eye.

Writes: Trials/Runs/<label>-trunkband.png (the crop) and prints the numbers.
Usage:  python Tools/art0-trunk-band-probe.py [label]
"""
import os
import sys

import numpy as np
from PIL import Image

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(PROJ, "Trials", "Runs")

GREEN_MARGIN = 24
GROUND_ROW = 1010          # below this row is the yard floor, not the tree
MIN_CLUSTER = 12           # px: smaller than this is grain, not a prop


def load(label):
    path = os.path.join(RUNS, label + "_tree.png")
    if not os.path.exists(path):
        raise SystemExit("FAIL no such still: " + path)
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.int16)


def green_mask(rgb):
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    return (g > r + GREEN_MARGIN) & (g > b + GREEN_MARGIN) & (g > 60)


def bark_mask(rgb):
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    return (r > g + 12) & (g > b + 8) & (r > 60) & (r < 210)


def clusters(mask, min_px=MIN_CLUSTER):
    """4-connected components, iterative (a 1.5 M-pixel still can blow the C stack)."""
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    out = []
    ys, xs = np.nonzero(mask)
    for y0, x0 in zip(ys, xs):
        if seen[y0, x0]:
            continue
        stack = [(y0, x0)]
        seen[y0, x0] = True
        cells = []
        while stack:
            y, x = stack.pop()
            cells.append((y, x))
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(cells) >= min_px:
            cy = [c[0] for c in cells]
            cx = [c[1] for c in cells]
            out.append((min(cx), min(cy), max(cx) - min(cx) + 1, max(cy) - min(cy) + 1, len(cells)))
    return out


def crop_signature(rgb, box, size=16):
    """The cluster's own crop, resampled to `size` x `size` so a 1 px bbox difference
    does not hide two identical drawings. Nearest, no smoothing: a resample would make
    different crops look alike."""
    x, y, w, h = box[0], box[1], box[2], box[3]
    img = Image.fromarray(rgb[y:y + h, x:x + w].astype(np.uint8)).resize((size, size), Image.NEAREST)
    return np.asarray(img, dtype=np.uint8).tobytes()


def main():
    label = sys.argv[1] if len(sys.argv) > 1 else "art0leaf28"
    rgb = load(label)
    h, w, _ = rgb.shape
    band = rgb[:GROUND_ROW]

    gm = green_mask(band)
    cl = clusters(gm)
    cl.sort(key=lambda c: (c[1], c[0]))
    print("ART-0-TRUNK label=%s green clusters>=%d px: %d" % (label, MIN_CLUSTER, len(cl)))

    # how many clusters carry a DIFFERENT crop of the material, and how many repeat one
    sigs = {}
    for c in cl:
        key = crop_signature(band, c)
        sigs.setdefault(key, []).append(c)
    repeats = {k: v for k, v in sigs.items() if len(v) > 1}
    print("  distinct crops=%d ; crops drawn more than once=%d ; clusters in a repeat=%d"
          % (len(sigs), len(repeats), sum(len(v) for v in repeats.values())))
    for key, v in sorted(repeats.items(), key=lambda kv: -len(kv[1]))[:6]:
        c = v[0]
        print("    repeat x%d  box %dx%d px at (%d,%d)" % (len(v), c[2], c[3], c[0], c[1]))
    for c in cl[:20]:
        print("    cluster box %dx%d px at (%d,%d)  ink=%d" % (c[2], c[3], c[0], c[1], c[4]))

    bm = bark_mask(band)
    print("ART-0-TRUNK bark rows (x-range of the bark mask, px):")
    for y in (120, 300, 500, 700, 850, 950, 1000):
        row = np.nonzero(bm[y])[0]
        if len(row) == 0:
            print("    row %4d: none" % y)
            continue
        print("    row %4d: x %d..%d  run=%d  px=%d" % (y, row.min(), row.max(), row.max() - row.min() + 1, len(row)))

    x0 = int(np.nonzero(bm[np.any(bm, axis=1)][:, :].any(axis=0))[0].min()) if bm.any() else 0
    print("ART-0-TRUNK bark mask spans x %d..%d" % (x0, int(np.nonzero(bm.any(axis=0))[0].max())))
    out = os.path.join(RUNS, label + "-trunkband.png")
    Image.fromarray(rgb[60:GROUND_ROW, 820:1120].astype(np.uint8)).resize((600, (GROUND_ROW - 60) // 2), Image.NEAREST).save(out)
    print("ART-0-TRUNK crop -> " + out + " (x 820..1120, y 60..%d, 2x wide)" % GROUND_ROW)
    return 0


if __name__ == "__main__":
    sys.exit(main())
