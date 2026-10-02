#!/usr/bin/env python3
"""ART-H4e probe: count the "sky-through-the-limb" ghost pixels of a still.

The far limb used to be drawn with ArtLibrary.DimFar (RGB x0.70, alpha 0.62), so in the
game's captures its pixels were a BLEND of the drawing and the background: near-NEUTRAL grey
(R ~ G ~ B), luminance mid-band.  After ART-H4e (HumanArt.FarTone: the same 0.70 tone, alpha
pinned to 1) the far limb's pixels are the drawing's own warm tones instead.

The instrument: a pixel is GHOST when its channels are near-equal (max-min < TOL) and its
luminance sits in the ghost band [LO..HI].  For reference the same frame also counts
SKY (clearly blue: B - R > 25) and WARM picture pixels (R - B > 20).

Usage: python Tools/arth4e-ghost-px.py Trials/Runs/arth4e-still-ankle-bent.png [...]
Options: --tol 14 --lo 120 --hi 200   (the ghost signature's tolerances)
"""
import argparse

import numpy as np
from PIL import Image


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("pngs", nargs="+")
    ap.add_argument("--tol", type=int, default=14)
    ap.add_argument("--lo", type=int, default=120)
    ap.add_argument("--hi", type=int, default=200)
    ap.add_argument("--map", type=int, default=0, help="print a NxN/2 class grid: S sky, W warm, N neutral, D dark, ? other")
    ap.add_argument("--where", action="store_true", help="print the ghost px bbox + the mean RGB they carry")
    ap.add_argument("--wash", action="store_true", help="count the far limb's blue-grey wash px")
    args = ap.parse_args()

    for p in args.pngs:
        a = np.asarray(Image.open(p).convert("RGB")).astype(np.int16)
        r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
        mx = np.maximum(np.maximum(r, g), b)
        mn = np.minimum(np.minimum(r, g), b)
        lum = 0.2126 * r + 0.7152 * g + 0.0722 * b
        neutral = (mx - mn) < args.tol
        ghost = neutral & (lum >= args.lo) & (lum <= args.hi)
        sky = (b - r) > 25
        warm = (r - b) > 20
        total = a.shape[0] * a.shape[1]
        print("%s  %dx%d" % (p, a.shape[1], a.shape[0]))
        print("  ghost(neutral %d..%d) px=%d (%.2f%%)   sky px=%.2f%%   warm px=%.2f%%"
              % (args.lo, args.hi, int(ghost.sum()), 100.0 * ghost.sum() / total,
                 100.0 * sky.sum() / total, 100.0 * warm.sum() / total))
        if args.where and ghost.any():
            ys, xs = np.where(ghost)
            print("  ghost bbox x %d..%d y %d..%d  meanRGB=(%.0f, %.0f, %.0f)  r-b mean %.1f"
                  % (xs.min(), xs.max(), ys.min(), ys.max(),
                     r[ghost].mean(), g[ghost].mean(), b[ghost].mean(),
                     float((r[ghost] - b[ghost]).mean())))
            for k in range(0, len(ys), max(1, len(ys) // 8)):
                y, x = ys[k], xs[k]
                print("    y=%d x=%d rgb=(%d,%d,%d)" % (y, x, a[y, x, 0], a[y, x, 1], a[y, x, 2]))
        if args.wash:
            # the far limb as DimFar drew it: the drawing blended 62% over the sky = a blue-grey
            # wash - b-r clearly positive but far weaker than the sky's own blue (the sky is
            # ~(140,199,240): b-r ~ 100; a wash pixel like (134,153,173): b-r ~ 39).
            wash = ((b - r) > 10) & ((b - r) < 70) & (lum >= 130) & (lum <= 195)
            print("  wash px (10 < b-r < 70, lum 130..195): %d (%.2f%%)  meanRGB=(%.0f, %.0f, %.0f)"
                  % (int(wash.sum()), 100.0 * wash.sum() / total,
                     r[wash].mean() if wash.any() else 0,
                     g[wash].mean() if wash.any() else 0,
                     b[wash].mean() if wash.any() else 0))
        if args.map:
            n = args.map
            h, w = a.shape[0], a.shape[1]
            for i in range(n):
                line = []
                for j in range(n):
                    y0, y1 = i * h // n, (i + 1) * h // n
                    x0, x1 = j * w // n, (j + 1) * w // n
                    d = a[y0:y1, x0:x1]
                    dl = lum[y0:y1, x0:x1]
                    dn = neutral[y0:y1, x0:x1]
                    ds = sky[y0:y1, x0:x1]
                    dw = warm[y0:y1, x0:x1]
                    c = d.reshape(-1, 3).mean(axis=0)
                    if ds.mean() > 0.5:
                        line.append("S")
                    elif dw.mean() > 0.4:
                        line.append("W" if c[0] < 190 else "w")
                    elif dn.mean() > 0.4:
                        line.append("N" if dl.mean() >= args.lo else "D")
                    elif dl.mean() < 90:
                        line.append("D")
                    else:
                        line.append("?")
                print("   %2d %s" % (i, "".join(line)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
