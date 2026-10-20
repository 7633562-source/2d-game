#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R3 - the cover's tone against the limb's paint around it, in the game's own frame.
The eye's question: at v=0.4 does the covered joint read as a PALER / FLATTER patch than the
limb's own paint?  The instrument: the off/on delta (the cover's own pixels) and - as the
reference - the skin-toned paint around the joint in the same ON frame (the stills tool's
own MASK); all unlit (SceneLighting.Enabled = false in this trial, so every human pixel is
the Unlit sprite material and the comparison is apples to apples).

Usage: python Tools/ah5fr3-cover-tone.py [--frame 145]
"""
import argparse
import importlib.util
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("stills", os.path.join(HERE, "arth5f-stills.py"))
ST = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ST)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--frame", type=int, default=145)
    ap.add_argument("--take-a", default="hcov_off")
    ap.add_argument("--take-b", default="hcov_on")
    ap.add_argument("--win", type=int, default=40)
    args = ap.parse_args()

    pa = os.path.join(ST.RUN, "shot_%s_%02d.png" % (args.take_a, args.frame))
    pb = os.path.join(ST.RUN, "shot_%s_%02d.png" % (args.take_b, args.frame))
    a = np.asarray(Image.open(pa).convert("RGB"), dtype=np.int16)
    b = np.asarray(Image.open(pb).convert("RGB"), dtype=np.int16)
    d = np.abs(a - b).max(axis=2) > 8
    ys, xs = np.nonzero(d)
    if len(xs) == 0:
        print("frame %d: off and on are identical" % args.frame)
        return
    x0, x1, y0, y1 = int(xs.min()), int(xs.max()), int(ys.min()), int(ys.max())
    print("frame %d: the cover's own pixels = the off/on delta: %d px, bbox x %d..%d y %d..%d"
          % (args.frame, int(d.sum()), x0, x1, y0, y1))

    win = np.zeros(b.shape[:2], bool)
    xa, xb = max(0, x0 - args.win), min(b.shape[1], x1 + args.win + 1)
    ya, yb = max(0, y0 - args.win), min(b.shape[0], y1 + args.win + 1)
    win[ya:yb, xa:xb] = True
    paint = ST.MASK(b) & win & ~d
    if paint.sum() < 100:
        print("  no paint found around the joint - widen --win")
        return
    cover = b[d].reshape(-1, 3)
    near = b[paint].reshape(-1, 3)
    lum_c, lum_n = cover.mean(axis=1), near.mean(axis=1)
    print("  the cover's own pixels : rgb %s  luma %.1f  (sd %.1f)"
          % (np.round(cover.mean(axis=0), 1), lum_c.mean(), lum_c.std()))
    print("  the paint around it    : rgb %s  luma %.1f  (sd %.1f)"
          % (np.round(near.mean(axis=0), 1), lum_n.mean(), lum_n.std()))
    print("  cover - paint luma %.1f   (the eye flags a patch beyond ~ +/-12)"
          % (lum_c.mean() - lum_n.mean()))
    print("  what the cover replaced (off frame, same pixels): rgb %s  luma %.1f"
          % (np.round(a[d].reshape(-1, 3).mean(axis=0), 1), a[d].reshape(-1, 3).mean(axis=1).mean()))


if __name__ == "__main__":
    main()
