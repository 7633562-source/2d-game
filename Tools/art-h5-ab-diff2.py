#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5x: time-shift-tolerant diff of the -skinJoints A/B pair.

The two runs' shot trains can be offset by one index (163 vs 164 shots).  For each
sampled frame find the shift in {-2..+2} that minimizes the diff; the RESIDUAL at the
best shift is the skin's own delta; print its px count, bbox and save a knee crop pair.
Usage: python Tools/art-h5-ab-diff2.py [--frame 52]
"""
import argparse
import glob
import os

import numpy as np
from PIL import Image

RUN = "Trials/Runs"


def load(path):
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.int16)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--frames", default="13,39,52,65,91,117,156")
    ap.add_argument("--crop", default="420,300,1020,860")
    args = ap.parse_args()

    off = sorted(glob.glob(os.path.join(RUN, "shot_human_dress5x_off_*.png")))
    on = sorted(glob.glob(os.path.join(RUN, "shot_human_dress5x_on_*.png")))
    n = min(len(off), len(on))
    for i in [int(v) for v in args.frames.split(",")]:
        if i + 2 >= n:
            continue
        a = load(off[i])
        best = None
        for s in (-2, -1, 0, 1, 2):
            if not (0 <= i + s < n):
                continue
            b = load(on[i + s])
            c = int((np.abs(a - b).max(axis=2) > 8).sum())
            if best is None or c < best[1]:
                best = (s, c)
        s, c = best
        b = load(on[i + s])
        d = np.abs(a - b).max(axis=2) > 8
        ys, xs = np.nonzero(d)
        bbox = (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())) if len(xs) else None
        print("frame %d: best shift %+d diff>8 px=%d bbox=%s" % (i, s, c, bbox))

    # knee crop pair at the requested frame, best shift applied
    i = int(args.frames.split(",")[0])
    a = load(off[i])
    best = None
    for s in (-2, -1, 0, 1, 2):
        if not (0 <= i + s < n):
            continue
        b = load(on[i + s])
        c = int((np.abs(a - b).max(axis=2) > 8).sum())
        if best is None or c < best[1]:
            best = (s, c)
    s = best[0]
    b = load(on[i + s])
    x0, y0, x1, y1 = [int(v) for v in args.crop.split(",")]
    ca = Image.fromarray(np.clip(a[y0:y1, x0:x1], 0, 255).astype(np.uint8))
    cb = Image.fromarray(np.clip(b[y0:y1, x0:x1], 0, 255).astype(np.uint8))
    w, h = ca.size
    rec = Image.new("RGB", (w * 2 + 8, h), (255, 0, 255))
    rec.paste(ca, (0, 0))
    rec.paste(cb, (w + 8, 0))
    out = os.path.join(RUN, "arth5x-knee-off-on-f%03d-s%+d.png" % (i, s))
    rec.save(out)
    print("pair -> %s (left=off, right=on shift %+d, crop %s)" % (out, s, args.crop))


if __name__ == "__main__":
    main()
