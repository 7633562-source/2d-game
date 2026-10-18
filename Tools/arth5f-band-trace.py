#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f - the ALPHA along the limb's centre line through a joint, in metres.

For a joint that the foreshortening can open (the hip: the thigh's paint pulls down from
the pelvis's skirt; the shoulder: same against the torso), this prints the ink runs on
the column through the joint, so a band of background between two paints shows as a
number in millimetres - the disc test (R_eye) only watches the joint's own disc.

Usage: python Tools/arth5f-band-trace.py hip 0.4 -0.086 [--theta -22.5]
"""
import argparse
import importlib.util
import os
import sys

import numpy as np

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(PROJECT, "Tools"))
spec = importlib.util.spec_from_file_location(
    "arth5f_cov", os.path.join(PROJECT, "Tools", "arth5f-coverage.py"))
C = importlib.util.module_from_spec(spec)
spec.loader.exec_module(C)

ap = argparse.ArgumentParser()
ap.add_argument("joint")
ap.add_argument("v", type=float)
ap.add_argument("shift", type=float)
ap.add_argument("--theta", type=float, default=None)
ap.add_argument("--ck", type=float, default=1.0, help="the neighbour layer's own k")
ap.add_argument("--cs", type=float, default=0.0, help="the neighbour layer's shift (m)")
ap.add_argument("--scale", type=float, default=4000.0)
args = ap.parse_args()

ns, joints = C.F.read_rig()
canvases = {k: C.F.load_canvas(v) for k, v in C.F.CANVAS.items()}
canvases = {k: v for k, v in canvases.items() if v is not None}

name = args.joint
entry = [j for j in C.F.JOINTS if j[0] == name][0]
_, parent_key, child_key, child_var, parent_var, half = entry
spec_j = joints[child_var]
anchor_child, anchor_parent = spec_j["anchor"], spec_j["anchor_parent"]
lo, hi = spec_j["limits"]
deg = args.theta if args.theta is not None else lo

k = 1.0 - args.v
if parent_key in C.SHRUNK:
    layers = [(canvases[parent_key], anchor_parent, 0.0, k, args.shift),
              (canvases[child_key], anchor_child, deg, args.ck, args.cs)]
elif child_key in C.SHRUNK:
    layers = [(canvases[parent_key], anchor_parent, 0.0, args.ck, args.cs),
              (canvases[child_key], anchor_child, deg, k, args.shift)]
frame = C.compose(layers, half, args.scale)
a = np.array(frame)[:, :, 3] > 16
h, w = a.shape
mid = w // 2
col = a[:, mid]
runs = []
start = None
for r in range(h):
    if col[r] and start is None:
        start = r
    elif not col[r] and start is not None:
        runs.append((start, r - 1))
        start = None
if start is not None:
    runs.append((start, h - 1))
print("joint %s v=%.2f shift=%+.4f theta=%+.1f  frame %d px, %.3f m (col x=%d)"
      % (name, args.v, args.shift, deg, w, w / args.scale, mid))
for (r0, r1) in runs:
    y0 = (h / 2.0 - (r0 + 0.5)) / args.scale
    y1 = (h / 2.0 - (r1 + 0.5)) / args.scale
    print("  ink run: y %+8.4f .. %+8.4f m  (rows %d..%d)" % (y0, y1, r0, r1))
gaps = []
for i in range(len(runs) - 1):
    g0 = (h / 2.0 - (runs[i][1] + 0.5)) / args.scale
    g1 = (h / 2.0 - (runs[i + 1][0] + 0.5)) / args.scale
    gaps.append((g1, g0))
for g in gaps:
    print("  BACKGROUND BAND: y %+8.4f .. %+8.4f m  (%.1f mm)"
          % (g[0], g[1], (g[1] - g[0]) * 1000.0))
