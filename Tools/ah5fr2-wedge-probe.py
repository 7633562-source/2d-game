#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f-R2 - WHERE the wedge is, per column, at the game's own numbers.

Composes the joint's two canvases the way the game draws them (foreshorten v, centre
anchor = the sprite scales about its pivot; the neighbour grown by the game's own
`missing = max(0, half - inkBot*k) + 0.005`), then reports, per column across the limb:

  yT  = the thigh's lowest painted edge (m, joint frame)
  yS  = the shin's highest painted edge
  brk = painted - background - painted in that column (a true break)

and the hole count inside R_eye.  Also writes the composed frame as a PNG.

Usage: python Tools/ah5fr2-wedge-probe.py --joint knee --v 0.4 --theta 0 [--cell 2]
"""
import argparse
import importlib.util
import os

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PROJECT, "Trials", "Runs", "arth5f-foreshorten")


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(PROJECT, "Tools", rel))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


C = load("cov", "arth5f-coverage.py")
F = C.F

ap = argparse.ArgumentParser()
ap.add_argument("--joint", default="knee")
ap.add_argument("--v", type=float, default=0.4)
ap.add_argument("--theta", type=float, default=0.0)
ap.add_argument("--scale", type=float, default=2000.0)
ap.add_argument("--cell", type=float, default=2.0, help="probe column step, mm")
ap.add_argument("--save", default="")
ap.add_argument("--grow", type=float, default=None, help="override the game's missing, m")
args = ap.parse_args()

ns, joints = F.read_rig()
canvases = {k: F.load_canvas(v) for k, v in F.CANVAS.items()}
canvases = {k: v for k, v in canvases.items() if v is not None}
entry = [j for j in F.JOINTS if j[0] == args.joint][0]
_, parent_key, child_key, child_var, parent_var, half_m = entry
spec_j = joints[child_var]
anchor_child, anchor_parent = spec_j["anchor"], spec_j["anchor_parent"]
pc, cc = canvases[parent_key], canvases[child_key]

# the game's own arithmetic (HumanArt.ForeshortenVisuals)
p_top, p_bot = C.ink_extent(pc)
c_top, c_bot = C.ink_extent(cc)
parent_shrunk = parent_key in C.SHRUNK
k = 1.0 - args.v
if parent_shrunk:
    box = pc["world"]          # canvas bounds size, m (the game's Sprite.bounds)
    pivotY = pc["pivot"][1]
    inkBot = pivotY * box[1]
    size_half = ns[{"thigh": "thighSize", "upper": "upperArmSize"}[parent_key]].y * 0.5
    missing = max(0.0, size_half - inkBot * k) + 0.005
    if args.grow is not None:
        missing = args.grow
    base = [(pc, anchor_parent, 0.0, k, 0.0), (cc, anchor_child, args.theta, 0.0, 0.0)]
else:
    box = cc["world"]
    pivotY = cc["pivot"][1]
    inkBot = pivotY * box[1]
    size_half = ns[{"thigh": "thighSize", "upper": "upperArmSize"}[child_key]].y * 0.5
    missing = max(0.0, size_half - inkBot * k) + 0.005
    if args.grow is not None:
        missing = args.grow
    base = [(pc, anchor_parent, 0.0, 0.0, 0.0), (cc, anchor_child, args.theta, k, 0.0)]

# the neighbour grows so its ink edge facing the joint moves toward it by `missing`
n_top, n_bot = (c_top, c_bot) if parent_shrunk else (p_top, p_bot)
joint_anchor = anchor_child if parent_shrunk else anchor_parent
span = n_top - n_bot
n_k = (span + missing) / span
n_shift = (n_bot - n_bot * n_k) if joint_anchor[1] > 0.0 else (n_top - n_top * n_k)

layers = []
for (c, a, th, kk, ss) in base:
    if c is cc and kk == 0.0:
        layers.append((c, a, th, n_k, n_shift))
    else:
        layers.append((c, a, th, kk, ss))

half_frame = 0.12
frame = C.compose(layers, half_frame, args.scale)
r_eye = min(F.ink_half(pc, anchor_parent), F.ink_half(cc, anchor_child))
hole, total = C.hole_px(frame, r_eye, args.scale)
print("joint %s v=%.2f theta=%.1f  missing=%.5f m  R_eye=%.4f m  hole %d / %d px"
      % (args.joint, args.v, args.theta, missing, r_eye, hole, total))

a = np.array(frame)[:, :, 3] > 16
H, W = a.shape
cx = cy = W / 2.0
def col_px(xm):
    return int(round(cx + xm * args.scale))
def row_px(ym):
    return int(round(cy - ym * args.scale))     # rows top-down

xlim = 0.088
step = args.cell / 1000.0
xs = np.arange(-xlim, xlim + 1e-9, step)
print("cols x(mm): " + " ".join("%+d" % round(x * 1000) for x in xs[::5]))
for x in xs:
    px = col_px(x)
    if px < 0 or px >= W:
        continue
    col = a[:, px]
    ys = np.nonzero(col)[0]
    if not len(ys):
        print("%+6.1f mm: EMPTY" % (x * 1000))
        continue
    y_top = (cy - ys.min()) / args.scale
    y_bot = (cy - ys.max()) / args.scale
    # break: background between two painted runs inside the limb window
    win0 = row_px(0.07); win1 = row_px(-0.07)
    seg = col[win0:win1 + 1]
    runs = np.diff(np.concatenate(([0], seg.astype(np.int8), [0])))
    starts = np.nonzero(runs == 1)[0]; ends = np.nonzero(runs == -1)[0]
    brk = ""
    if len(starts) >= 2:
        gaps = [(win0 + starts[i + 1], win0 + ends[i]) for i in range(len(starts) - 1)]
        brk = " BREAK " + ",".join("%.1f..%.1fmm" % ((cy - b) / args.scale * 1000,
                                                     (cy - g) / args.scale * 1000)
                                   for g, b in gaps)
    if abs(x * 1000) % 10 < step * 1000 * 0.5:
        print("%+6.1f mm: top %+6.1f bot %+6.1f mm%s" % (x * 1000, y_top * 1000, y_bot * 1000, brk))

if args.save:
    path = os.path.join(OUT, args.save)
    frame.convert("RGB").save(path)
    print("saved", path)
