#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f - THE BAND SCAN: every column of the joint's crop, the gaps between paints.

The question the eye asks at a joint: "do the two paints touch everywhere, or does a band
of background run between them?"  A single centre column cannot answer it (a tilted limb
moves its paint aside), so this scans EVERY column of the composed frame:

  * the first and last painted pixel of the column define the limb's span there;
  * every transparent run strictly between them is a BAND (background enclosed in the
    figure) - counted in px and in millimetres of height;
  * the scan is run at v=0 and at the tested v, so the change's own cost is readable:
    BAND(v) px vs BAND(0) px.

Usage:
  python Tools/arth5f-gap-scan.py --joint knee,hip --v 0.4 --anchor down --deg 65,-29.3
  python Tools/arth5f-gap-scan.py --joint knee --v 0,0.2,0.4 --anchor centre,down
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


def gaps_of(frame):
    """Per column: the background runs strictly inside the painted span. Returns
    (total px, worst run px, worst run mm, worst column, count of columns with bands)."""
    a = np.array(frame)[:, :, 3] > 16
    h, w = a.shape
    total = 0
    worst = 0
    worst_col = -1
    cols_with = 0
    for x in range(w):
        col = a[:, x]
        ink = np.where(col)[0]
        if len(ink) < 2:
            continue
        top, bot = ink[0], ink[-1]
        gaps = 0
        for r in range(top, bot + 1):
            if not col[r]:
                gaps += 1
        if gaps:
            total += gaps
            cols_with += 1
            if gaps > worst:
                worst = gaps
                worst_col = x
    return total, worst, worst_col


ap = argparse.ArgumentParser()
ap.add_argument("--joint", default="knee,hip,elbow,shoulder")
ap.add_argument("--v", default="0,0.4")
ap.add_argument("--anchor", default="centre,down")
ap.add_argument("--grow", action="append", default=[])
ap.add_argument("--deg", default=None,
                help="comma list of test angles; default = the joint's own range ends")
ap.add_argument("--scale", type=float, default=1000.0)
ap.add_argument("--min-px", type=int, default=3,
                help="ignore runs shorter than this (anti-aliasing)")
args = ap.parse_args()

grows = {}
for item in args.grow:
    jn, ms = item.split("=")
    grows[jn] = float(ms)

ns, joints = C.F.read_rig()
canvases = {k: C.F.load_canvas(v) for k, v in C.F.CANVAS.items()}
canvases = {k: v for k, v in canvases.items() if v is not None}

for name, parent_key, child_key, child_var, parent_var, half in C.F.JOINTS:
    if name not in args.joint.split(","):
        continue
    spec_j = joints.get(child_var)
    if spec_j is None or spec_j["parent"] != parent_var:
        continue
    if parent_key in C.SHRUNK and child_key in C.SHRUNK:
        continue
    if parent_key in C.SHRUNK:
        shrunk_side, shortened = "parent", parent_key
    elif child_key in C.SHRUNK:
        shrunk_side, shortened = "child", child_key
    else:
        continue
    anchor_child, anchor_parent = spec_j["anchor"], spec_j["anchor_parent"]
    lo, hi = spec_j["limits"]
    pc, cc = canvases[parent_key], canvases[child_key]
    p_top, p_bot = C.ink_extent(pc)
    c_top, c_bot = C.ink_extent(cc)
    s_top, s_bot = (p_top, p_bot) if shrunk_side == "parent" else (c_top, c_bot)
    s_anchor = anchor_parent if shrunk_side == "parent" else anchor_child
    box_h = ns[{"thigh": "thighSize", "upper": "upperArmSize"}[shortened]].y
    degs = [float(x) for x in args.deg.split(",")] if args.deg else [lo, 0.0, hi, -29.3, 65.0]

    g = grows.get(name, 0.0)
    n_k, n_shift = 1.0, 0.0
    if g > 0.0:
        n_top, n_bot = (c_top, c_bot) if shrunk_side == "parent" else (p_top, p_bot)
        joint_anchor = anchor_child if shrunk_side == "parent" else anchor_parent
        span = n_top - n_bot
        if joint_anchor[1] > 0.0:
            n_k = (span + g) / span
            n_shift = n_bot - n_bot * n_k
        else:
            n_k = (span + g) / span
            n_shift = n_top - n_top * n_k

    for v in [float(x) for x in args.v.split(",")]:
        k = 1.0 - v
        for anchor_name in args.anchor.split(","):
            if anchor_name == "centre":
                s = 0.0
            elif anchor_name == "down":
                s = -(1.0 - k) * box_h * 0.5
            elif anchor_name == "up":
                s = +(1.0 - k) * box_h * 0.5
            elif anchor_name == "joint":
                s = (1.0 - k) * s_anchor[1]
            else:
                s = float(anchor_name[1:])
            for deg in degs:
                if not (lo - 1e-6 <= deg <= hi + 1e-6):
                    continue
                if shrunk_side == "parent":
                    layers = [(pc, anchor_parent, 0.0, k, s),
                              (cc, anchor_child, deg, n_k, n_shift)]
                else:
                    layers = [(pc, anchor_parent, 0.0, n_k, n_shift),
                              (cc, anchor_child, deg, k, s)]
                frame = C.compose(layers, half, args.scale)
                total, worst, col = gaps_of(frame)
                mm = worst / args.scale * 1000.0
                print("%-9s v=%.2f %-7s deg %+7.1f  band %6d px  worst %4d px (%.1f mm)"
                      % (name, v, anchor_name, deg, total, worst, mm))
print("note: a 1-px run is anti-aliasing; the shipped canvases read band 0 at v=0")
