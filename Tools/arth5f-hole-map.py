#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f - WHERE the hole is: an ASCII map of the joint's disc (paint vs background).

Usage: python Tools/arth5f-hole-map.py knee 0.4 centre [--grow 0.036] [--theta 60]
Prints the disc of R_eye as a grid: '#' painted, '.' background inside the disc,
' ' outside the disc; the joint is the centre of the grid.
"""
import argparse
import importlib.util
import os
import sys

import numpy as np

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
spec = importlib.util.spec_from_file_location(
    "arth5f_cov", os.path.join(PROJECT, "Tools", "arth5f-coverage.py"))
C = importlib.util.module_from_spec(spec)
spec.loader.exec_module(C)

ap = argparse.ArgumentParser()
ap.add_argument("joint")
ap.add_argument("v", type=float)
ap.add_argument("anchor")
ap.add_argument("--grow", type=float, default=0.0)
ap.add_argument("--theta", type=float, default=None)
ap.add_argument("--scale", type=float, default=2000.0)
ap.add_argument("--cells", type=int, default=48)
args = ap.parse_args()

ns, joints = C.F.read_rig()
canvases = {k: C.F.load_canvas(v) for k, v in C.F.CANVAS.items()}
canvases = {k: v for k, v in canvases.items() if v is not None}
entry = [j for j in C.F.JOINTS if j[0] == args.joint][0]
_, parent_key, child_key, child_var, parent_var, half = entry
spec_j = joints[child_var]
anchor_child, anchor_parent = spec_j["anchor"], spec_j["anchor_parent"]
lo, hi = spec_j["limits"]
deg = args.theta if args.theta is not None else lo

if parent_key in C.SHRUNK:
    shrunk_side, shortened = "parent", parent_key
else:
    shrunk_side, shortened = "child", child_key
pc, cc = canvases[parent_key], canvases[child_key]
half_p = C.F.ink_half(pc, anchor_parent)
half_c = C.F.ink_half(cc, anchor_child)
r_eye = min(half_p, half_c)
p_top, p_bot = C.ink_extent(pc)
c_top, c_bot = C.ink_extent(cc)
s_top, s_bot = (p_top, p_bot) if shrunk_side == "parent" else (c_top, c_bot)
s_anchor = anchor_parent if shrunk_side == "parent" else anchor_child
box_h = ns[{"thigh": "thighSize", "upper": "upperArmSize"}[shortened]].y
k = 1.0 - args.v
if args.anchor == "centre":
    s = 0.0
elif args.anchor == "down":
    s = -(1.0 - k) * box_h * 0.5
elif args.anchor == "up":
    s = +(1.0 - k) * box_h * 0.5
elif args.anchor == "joint":
    s = (1.0 - k) * s_anchor[1]
else:
    s = float(args.anchor[1:])

n_k, n_shift = 1.0, 0.0
if args.grow > 0.0:
    n_top, n_bot = (c_top, c_bot) if shrunk_side == "parent" else (p_top, p_bot)
    joint_anchor = anchor_child if shrunk_side == "parent" else anchor_parent
    span = n_top - n_bot
    if joint_anchor[1] > 0.0:
        n_k = (span + args.grow) / span
        n_shift = n_bot - n_bot * n_k
    else:
        n_k = (span + args.grow) / span
        n_shift = n_top - n_top * n_k

if shrunk_side == "parent":
    layers = [(pc, anchor_parent, 0.0, k, s), (cc, anchor_child, deg, n_k, n_shift)]
else:
    layers = [(pc, anchor_parent, 0.0, n_k, n_shift), (cc, anchor_child, deg, k, s)]
frame = C.compose(layers, half, args.scale)
hole, total = C.hole_px(frame, r_eye, args.scale)
print("joint %s v=%.2f %s s=%+.4f grow=%.4f theta=%+.1f -> hole %d / %d px (R_eye %.4f m)"
      % (args.joint, args.v, args.anchor, s, args.grow, deg, hole, total, r_eye))
a = np.array(frame)[:, :, 3] > 16
h, w = a.shape
n = args.cells
R = r_eye * args.scale
cx, cy = w / 2.0, h / 2.0
print("   disc map (%.1f mm per cell), '#'=paint '.'=background ' '=outside"
      % (2 * R / n * 1000.0))
for iy in range(n):
    line = ""
    for ix in range(n):
        x = (ix + 0.5) / n * 2 * R + cx - R
        y = (iy + 0.5) / n * 2 * R + cy - R
        inside = (x - cx) ** 2 + (y - cy) ** 2 <= R * R
        if not inside:
            line += " "
        else:
            line += "#" if a[int(y), int(x)] else "."
    print("   " + line)
