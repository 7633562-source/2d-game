#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5 R2: are the margin 'digits' real ink or transparent-RGB ghosts?

Vision reads report white digits/letters (1/2/S) along a canvas' left margin. A real opaque
digit would sit ON the character in a dressed capture; the R2 fullbody look still shows none.
This probe answers with the pixels:

  --mode census   white-ish px split by alpha class in the left margin (default)
  --mode ascii    ASCII alpha map of a box, plus connected white clusters with their mean
                  alpha and RGB - so the digit, if it exists, is read from numbers alone

Usage:
  python Tools/art-h5-r2-margin-probe.py --keys thigh,shin
  python Tools/art-h5-r2-margin-probe.py --keys thigh --mode ascii --box 0,520,90,660
"""
import argparse
import os

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")
OUT = os.path.join(PROJECT, "Trials", "Runs", "arth5-r2")


def load(key):
    im = Image.open(os.path.join(ART, key + ".png")).convert("RGBA")
    return np.asarray(im).astype(np.uint8)


def census(key, a):
    alpha = a[:, :, 3].astype(np.int32)
    rgb = a[:, :, :3]
    h, w = alpha.shape
    left = int(w * 0.40)
    whiteish = (rgb[:, :, 0] > 190) & (rgb[:, :, 1] > 190) & (rgb[:, :, 2] > 190)
    margin = np.zeros_like(alpha, dtype=bool)
    margin[:, :left] = True
    for name, sel in (("alpha=0   ", margin & whiteish & (alpha == 0)),
                      ("alpha 1-31", margin & whiteish & (alpha > 0) & (alpha < 32)),
                      ("alpha 32+ ", margin & whiteish & (alpha >= 32))):
        n = int(sel.sum())
        if n:
            ys, xs = np.nonzero(sel)
            vals = alpha[sel]
            print("%s %s: n=%d  bbox x[%d..%d] y[%d..%d]  alpha mean %.1f max %d"
                  % (key, name, n, xs.min(), xs.max(), ys.min(), ys.max(),
                     float(vals.mean()), int(vals.max())))
        else:
            print("%s %s: n=0" % (key, name))


def clusters(key, a, x0, y0, x1, y1):
    alpha = a[:, :, 3].astype(np.int32)
    rgb = a[:, :, :3].astype(np.int32)
    sub_a = alpha[y0:y1, x0:x1]
    sub_c = rgb[y0:y1, x0:x1]
    ink = sub_a > 0
    # connected components (4-neighbour) over ink px
    from collections import deque
    seen = np.zeros_like(ink, dtype=bool)
    comps = []
    H, W = ink.shape
    for yy in range(H):
        for xx in range(W):
            if ink[yy, xx] and not seen[yy, xx]:
                q = deque([(yy, xx)])
                seen[yy, xx] = True
                members = []
                while q:
                    cy, cx = q.popleft()
                    members.append((cy, cx))
                    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        ny, nx = cy + dy, cx + dx
                        if 0 <= ny < H and 0 <= nx < W and ink[ny, nx] and not seen[ny, nx]:
                            seen[ny, nx] = True
                            q.append((ny, nx))
                if len(members) >= 12:
                    ys = np.array([m[0] for m in members]) + y0
                    xs = np.array([m[1] for m in members]) + x0
                    as_ = np.array([alpha[m[0] + y0, m[1] + x0] for m in members])
                    cs = np.array([rgb[m[0] + y0, m[1] + x0] for m in members])
                    comps.append((len(members), xs.min(), xs.max(), ys.min(), ys.max(),
                                  float(as_.mean()), cs.mean(axis=0)))
    comps.sort(reverse=True)
    print("== %s box x[%d..%d] y[%d..%d]: %d ink clusters >=12 px" % (key, x0, x1, y0, y1, len(comps)))
    for n, xa, xb, ya, yb, am, cm in comps[:12]:
        print("   cluster n=%4d  x[%3d..%3d] y[%3d..%3d]  alpha mean %5.1f  rgb (%3.0f,%3.0f,%3.0f)"
              % (n, xa, xb, ya, yb, am, cm[0], cm[1], cm[2]))


def ascii_map(key, a, x0, y0, x1, y1, scale=1):
    alpha = a[:, :, 3].astype(np.int32)
    chars = " .:+*#@"
    print("== %s ascii alpha map x[%d..%d] y[%d..%d]  (chars: %r)" % (key, x0, x1, y0, y1, chars))
    for yy in range(y0, y1, scale):
        row = []
        for xx in range(x0, x1, scale):
            v = alpha[yy, xx]
            row.append(chars[min(6, v * 7 // 256)])
        print("y%03d |%s|" % (yy, "".join(row)))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--keys", default="thigh,shin")
    ap.add_argument("--mode", default="census")
    ap.add_argument("--box", default="0,150,110,660")
    args = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    box = [int(v) for v in args.box.split(",")]
    for key in args.keys.split(","):
        a = load(key)
        if args.mode == "census":
            census(key, a)
        else:
            clusters(key, a, *box)
            ascii_map(key, a, *box, scale=2)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
