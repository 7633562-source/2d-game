#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R4 - WHERE THE HOLE IS at a joint, read off the check's own frames.

The look camera is centred on the joint's hinge, so the frame's centre pixel is the hinge.
The background is read from the frame's own border (the scene's sky, whatever colour it is),
then counted inside the collider disc (r m) and reported as a place: centroid and bbox
relative to the centre, in millimetres.

Usage: HOLE_PPM=<px/m> python Tools/ah5fr4-hole-locate.py <tag> <radius_m> <png> [<png> ...]
"""
import sys
import os
import numpy as np
from PIL import Image

tag = sys.argv[1]
radius_m = float(sys.argv[2])
for png in sys.argv[3:]:
    path = os.path.join("Trials", "Runs", "cuff-r4", png)
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(np.int16)
    H, W = a.shape[0], a.shape[1]
    cx0, cy0 = W / 2.0, H / 2.0
    # the frame's own background: the median of the border ring
    ring = np.concatenate([a[0:4].reshape(-1, 3), a[-4:].reshape(-1, 3),
                           a[:, 0:4].reshape(-1, 3), a[:, -4:].reshape(-1, 3)])
    bg = np.median(ring, axis=0)
    d = np.abs(a - bg).sum(axis=2)
    sky = d < 24
    ppm = float(os.environ.get("HOLE_PPM", "2347"))
    yy, xx = np.mgrid[0:H, 0:W]
    rr = np.sqrt((xx - cx0) ** 2 + (yy - cy0) ** 2)
    disc = rr <= radius_m * ppm
    hit = sky & disc
    n = int(hit.sum())
    disc_n = int(disc.sum())
    if n:
        ys, xs = np.where(hit)
        print("%-42s bg(%3d,%3d,%3d) hole %6d / %6d  centroid (%+6.1f,%+6.1f)  bbox x %+6.1f..%+6.1f  y %+6.1f..%+6.1f mm"
              % (png, bg[0], bg[1], bg[2], n, disc_n,
                 (xs.mean() - cx0) / ppm * 1000.0, (cy0 - ys.mean()) / ppm * 1000.0,
                 (xs.min() - cx0) / ppm * 1000.0, (xs.max() - cx0) / ppm * 1000.0,
                 (cy0 - ys.max()) / ppm * 1000.0, (cy0 - ys.min()) / ppm * 1000.0))
    else:
        print("%-42s bg(%3d,%3d,%3d) hole %6d / %6d  clean" % (png, bg[0], bg[1], bg[2], n, disc_n))
