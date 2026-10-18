#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5x: diff two same-build takes that differ only in -skinJoints.

The delta between a frame pair IS the skin's own draw.  Prints per-frame px-diff counts
(>8), saves a mid-frame side-by-side for the eyes.  Usage: python Tools/art-h5-ab-diff.py
"""
import glob
import os

import numpy as np
from PIL import Image

RUN = "Trials/Runs"
off = sorted(glob.glob(os.path.join(RUN, "shot_human_dress5x_off_*.png")))
on = sorted(glob.glob(os.path.join(RUN, "shot_human_dress5x_on_*.png")))
n = min(len(off), len(on))
print("frames: off %d on %d (paired %d)" % (len(off), len(on), n))
if n == 0:
    raise SystemExit("no pairs")
step = max(1, n // 12)
tot = []
for i in range(0, n, step):
    a = np.asarray(Image.open(off[i]).convert("RGB"), dtype=np.int16)
    b = np.asarray(Image.open(on[i]).convert("RGB"), dtype=np.int16)
    if a.shape != b.shape:
        print("frame %d shape differs %s vs %s" % (i, a.shape, b.shape))
        continue
    d = np.abs(a - b).max(axis=2)
    c = int((d > 8).sum())
    tot.append(c)
    print("frame %d: diff>8 px = %d" % (i, c))
print("summary: frames sampled %d | max diff px %d | frames with any diff %d"
      % (len(tot), max(tot) if tot else -1, sum(1 for c in tot if c > 0)))
k = n // 2
a = Image.open(off[k]).convert("RGB")
b = Image.open(on[k]).convert("RGB")
w, h = a.size
rec = Image.new("RGB", (w * 2 + 8, h), (255, 0, 255))
rec.paste(a, (0, 0))
rec.paste(b, (w + 8, 0))
out = os.path.join(RUN, "arth5x-ab-frame%03d.png" % k)
rec.save(out)
print("pair -> %s" % out)
