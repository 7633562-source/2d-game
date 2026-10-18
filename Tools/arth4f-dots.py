#!/usr/bin/env python3
"""ART-H4f probe: find SMALL isolated dark blobs ("dot") floating on the sky in a look still.

The judge's ankle read names "видны точки пивотов/шарниров" and the lane's own eyes saw a
dark dot near the lower foot; the joint markers are gated by -rigOverlay 0, so this tool
answers with pixels: inside the SKY band, any dark pixel whose 21x21 window carries almost
no other dark pixel is a floating dot.  Its bbox, area and darkest pixel are printed.
A dot that belongs to the drawing sits inside the silhouette; a debug dot floats alone.

Usage: python Tools/arth4f-dots.py Trials/Runs/arth4e-still-ankle-bent.png [...]
"""
import sys

import numpy as np
from PIL import Image

DARK = 300      # sum(RGB) below this counts as dark
WIN = 21        # isolation window
NEIGH_MAX = 40  # dark px allowed inside the window (one small dot's own body)


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    for p in argv[1:]:
        img = np.asarray(Image.open(p).convert("RGB")).astype(np.int32)
        h, w, _ = img.shape
        sky = np.median(img[0:40, :, :].reshape(-1, 3), axis=0)
        row_med = np.median(img, axis=1)
        sky_row = (np.abs(row_med - sky[None, :]).sum(axis=1) < 40)
        # the sky band = from top to the last mostly-sky row
        band_end = 0
        for y in range(h):
            if sky_row[max(0, y - 3):y + 1].all():
                band_end = y
        s = img.sum(axis=2)
        dark = (s < DARK) & (np.arange(h)[:, None] < band_end)
        # windowed dark count
        c = np.cumsum(np.cumsum(dark.astype(np.int64), axis=0), axis=1)
        pad = WIN // 2

        def wcount(y, x):
            y0, y1 = max(0, y - pad), min(h, y + pad + 1)
            x0, x1 = max(0, x - pad), min(w, x + pad + 1)
            tot = c[y1 - 1, x1 - 1]
            if y0:
                tot -= c[y0 - 1, x1 - 1]
            if x0:
                tot -= c[y1 - 1, x0 - 1]
            if y0 and x0:
                tot += c[y0 - 1, x0 - 1]
            return int(tot)

        ys, xs = np.where(dark)
        cand = [(int(y), int(x)) for y, x in zip(ys, xs) if wcount(y, x) <= NEIGH_MAX]
        print("%s  %dx%d  sky=%s  sky band rows 0..%d  dark px=%d  isolated cand=%d"
              % (p, w, h, tuple(int(v) for v in sky), band_end, int(dark.sum()), len(cand)))
        # cluster the candidates: chain when within 16 px
        used = [False] * len(cand)
        for i, (y, x) in enumerate(cand):
            if used[i]:
                continue
            group = [(y, x)]
            used[i] = True
            j = 0
            while j < len(group):
                gy, gx = group[j]
                j += 1
                for k2 in range(len(cand)):
                    if used[k2]:
                        continue
                    cy, cx = cand[k2]
                    if abs(cy - gy) <= 16 and abs(cx - gx) <= 16:
                        used[k2] = True
                        group.append((cy, cx))
            gys = np.array([g[0] for g in group])
            gxs = np.array([g[1] for g in group])
            if len(group) > 260:
                continue
            sums = img[gys, gxs].sum(axis=1)
            k = int(np.argmin(sums))
            print("   dot @ x %d..%d y %d..%d area %d  minRGB %s (sum %d)"
                  % (gxs.min(), gxs.max(), gys.min(), gys.max(), len(group),
                     tuple(int(v) for v in img[gys[k], gxs[k]]), int(sums[k])))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
