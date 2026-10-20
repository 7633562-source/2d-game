#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5x: 4x knee zoom of the frame-0 A/B pair + a diff-mask panel. Usage:
python Tools/art-h5-ab-zoom.py [--box 890,590,1030,730]"""
import argparse
import glob
import os

import numpy as np
from PIL import Image

RUN = "Trials/Runs"
ap = argparse.ArgumentParser()
ap.add_argument("--box", default="890,590,1030,730")
ap.add_argument("--scale", type=int, default=4)
ap.add_argument("--frame", type=int, default=0)
args = ap.parse_args()

off = sorted(glob.glob(os.path.join(RUN, "shot_human_dress5x_off_*.png")))
on = sorted(glob.glob(os.path.join(RUN, "shot_human_dress5x_on_*.png")))
a = np.asarray(Image.open(off[args.frame]).convert("RGB"), dtype=np.int16)
b = np.asarray(Image.open(on[args.frame]).convert("RGB"), dtype=np.int16)
x0, y0, x1, y1 = [int(v) for v in args.box.split(",")]
ca = a[y0:y1, x0:x1]
cb = b[y0:y1, x0:x1]
d = (np.abs(ca - cb).max(axis=2) > 8)
mask = np.zeros_like(ca, dtype=np.uint8)
mask[..., 0] = np.where(d, 255, 0)
base = ((ca * 0.35) + (cb * 0.35)).astype(np.uint8)
overlay = np.clip(base + mask * 0.65, 0, 255).astype(np.uint8)
s = args.scale
panels = [Image.fromarray(np.clip(ca, 0, 255).astype(np.uint8)),
          Image.fromarray(np.clip(cb, 0, 255).astype(np.uint8)),
          Image.fromarray(overlay)]
panels = [p.resize((p.width * s, p.height * s), Image.NEAREST) for p in panels]
w, h = panels[0].size
rec = Image.new("RGB", (w * 3 + 16, h), (255, 0, 255))
for k, p in enumerate(panels):
    rec.paste(p, (k * (w + 8), 0))
out = os.path.join(RUN, "arth5x-zoom-f%03d-offon-red.png" % args.frame)
rec.save(out)
print("off | on | red=diff>8  (4x of %s) -> %s" % (args.box, out))
print("diff px in this box: %d" % int(d.sum()))
