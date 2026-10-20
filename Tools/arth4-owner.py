#!/usr/bin/env python3
"""ART-H4 instrument: WHO paints the seam, and how strong the step is at the boundary.

The H3 numbers say HOW strong the seam line is; Tools/arth4-seam-where.py says where; this one
says WHICH canvas owns each visible pixel of a composed joint frame and measures the tone step
between pixels painted by DIFFERENT canvases - the read the eye calls "an arc across the joint".

Composes each layer on its own (same placement as Tools/art-h1-pose-frames.py - imported, no
geometry typed twice), so the ownership is the real z-order: the top layer wins where its alpha
is >= OPAQUE.

Writes <out>/<joint>-<deg>-owner.png (parent red, child blue, none white) and prints the step
statistics over the parent|child boundary and the parent|background edge.

Usage: python Tools/arth4-owner.py --joint knee --deg 60 [--scale 1000] [--half 0.30]
       python Tools/arth4-owner.py --joint ankle --deg 30
"""
import argparse
import math
import importlib.util
import os
import sys

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OPAQUE = 200

# joint -> parent canvas key, child canvas key, child rig var, frame half-size (m),
# parent end, child end (the ends the hinge joins - art-h1-joint-fit.py's own names)
JOBS = {
    "knee": ("thigh", "shin", "shin", 0.30, "bottom", "top"),
    "ankle": ("shin", "heel", "heel", 0.22, "bottom", "top"),
    "hip": ("pelvis", "thigh", "thigh", 0.34, "bottom", "top"),
    "elbow": ("upper", "lower", "lower", 0.24, "bottom", "top"),
    "shoulder": ("torso", "upper", "upper", 0.30, "side", "top"),
}


def frames_tool():
    path = os.path.join(PROJECT, "Tools", "art-h1-pose-frames.py")
    spec = importlib.util.spec_from_file_location("art_h1_pose_frames", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def boundary_steps(owner, lum, o1, o2):
    """|dL| over every 4-neighbour pixel pair owned by o1 and o2."""
    parts = []
    for (a_owner, b_owner, a_lum, b_lum) in (
            (owner[:, :-1], owner[:, 1:], lum[:, :-1], lum[:, 1:]),
            (owner[:-1, :], owner[1:, :], lum[:-1, :], lum[1:, :])):
        m = ((a_owner == o1) & (b_owner == o2)) | ((a_owner == o2) & (b_owner == o1))
        if m.any():
            parts.append(np.abs(a_lum - b_lum)[m])
    if not parts:
        return np.zeros(0)
    return np.concatenate(parts)


def describe(tag, v):
    if v.size == 0:
        print("  %-13s no such boundary" % tag)
        return
    print("  %-13s pairs=%6d  mean=%6.1f  p90=%6.1f  max=%6.1f  >40: %d  >80: %d"
          % (tag, v.size, v.mean(), np.percentile(v, 90), v.max(),
             int((v > 40).sum()), int((v > 80).sum())))


def layer_geometry(canvas, anchor, theta, scale, px):
    """The inverse of art-h1-pose-frames' place(): frame px -> the canvas' own px."""
    w, h = canvas["px"]
    ppu = canvas["ppu"]
    f = scale / ppu
    ax = canvas["pivot"][0] * w * f + anchor[0] * scale
    ay = (1.0 - canvas["pivot"][1]) * h * f - anchor[1] * scale
    th = math.radians(theta)
    ct, st = math.cos(th), math.sin(th)
    jx = jy = px / 2.0
    a = ct
    b = -st
    c = ax + (0.5 - jx) * ct + (jy - 0.5) * st
    d = st
    e = ct
    g = ay + (0.5 - jx) * st + (0.5 - jy) * ct
    ax0 = canvas["pivot"][0] * w + anchor[0] * ppu
    ay0 = (1.0 - canvas["pivot"][1]) * h - anchor[1] * ppu
    return a, b, c, d, e, g, f, ax0, ay0, ppu


def depth_map(geom, end, px, scale):
    """Signed depth beyond the box edge, in metres, for every frame pixel (down the canvas)."""
    a, b, c, d, e, g, f, ax0, ay0, ppu = geom
    yy, xx = np.mgrid[0:px, 0:px]
    xi = (a * xx + b * yy + c) / f
    yi = (d * xx + e * yy + g) / f
    out = (yi - ay0) / ppu
    if end == "top":
        out = -out
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--joint", required=True)
    ap.add_argument("--deg", type=float, required=True)
    ap.add_argument("--scale", type=float, default=1000.0)
    ap.add_argument("--half", type=float, default=0.0, help="frame half-size override (m)")
    ap.add_argument("--out", default="Trials/Runs/arth4-owner")
    ap.add_argument("--tag", default="")
    ap.add_argument("--dump", default=None, help="x0,y0,x1,y1 - print owner + luminance maps")
    ap.add_argument("--depth", action="store_true", help="bin the visible px by depth into each canvas")
    args = ap.parse_args()

    pkey, ckey, child_var, half, pend, cend = JOBS[args.joint]
    if args.half > 0:
        half = args.half
    pf = frames_tool()
    ns, joints = pf.read_rig()
    canvases = {k: pf.load_canvas(v) for k, v in pf.CANVAS.items()}
    spec = joints[child_var]
    anchor_child, anchor_parent = spec["anchor"], spec["anchor_parent"]

    px = max(64, int(round(2 * half * args.scale)))
    layers = [(pkey, anchor_parent, 0.0), (ckey, anchor_child, args.deg)]
    frame = pf.compose(canvases, layers, half, args.scale)
    arr = np.array(frame).astype(np.float64)

    alphas = {}
    for key, anchor, theta in layers:
        one = pf.compose(canvases, [(key, anchor, theta)], half, args.scale)
        alphas[key] = np.array(one)[:, :, 3]
    pa, ca = alphas[pkey], alphas[ckey]

    lum = 0.2126 * arr[:, :, 0] + 0.7152 * arr[:, :, 1] + 0.0722 * arr[:, :, 2]
    owner = np.zeros(pa.shape, np.uint8)          # 0 none, 1 parent, 2 child
    owner[pa >= OPAQUE] = 1
    owner[ca >= OPAQUE] = 2                       # the child draws on top

    print("%s %+.0f deg  scale=%.0f  frame %dx%d  parent px=%d child px=%d none px=%d"
          % (args.joint, args.deg, args.scale, px, px,
             int((owner == 1).sum()), int((owner == 2).sum()), int((owner == 0).sum())))
    describe("child-parent", boundary_steps(owner, lum, 1, 2))
    describe("parent-empty", boundary_steps(owner, lum, 1, 0))
    describe("child-empty", boundary_steps(owner, lum, 2, 0))

    # the strongest child|parent steps, with their colours - WHAT paints the arc
    wins = []
    for (a_owner, b_owner, a_lum, b_lum, dx, dy) in (
            (owner[:, :-1], owner[:, 1:], lum[:, :-1], lum[:, 1:], 1, 0),
            (owner[:-1, :], owner[1:, :], lum[:-1, :], lum[1:, :], 0, 1)):
        m = ((a_owner == 1) & (b_owner == 2)) | ((a_owner == 2) & (b_owner == 1))
        ys, xs = np.where(m)
        for y, x in zip(ys.tolist(), xs.tolist()):
            wins.append((abs(a_lum[y, x] - b_lum[y, x]), x, y, dx, dy,
                         int(owner[y, x]), int(owner[y + dy, x + dx])))
    wins.sort(reverse=True)
    cx = px / 2.0
    print("top child|parent steps (which side owns which colour):")
    for dv, x, y, dx, dy, o1, o2 in wins[:10]:
        r = ((x + 0.5 - cx) ** 2 + (y + 0.5 - cx) ** 2) ** 0.5 / args.scale * 1000
        print("   dL=%6.1f at (%4d,%4d) r=%5.1f mm  %s rgb%s  |  %s rgb%s"
              % (dv, x, y, r, "PAR" if o1 == 1 else "CHI",
                 tuple(arr[y, x, :3].astype(int)),
                 "PAR" if o2 == 1 else "CHI",
                 tuple(arr[y + dy, x + dx, :3].astype(int))))

    # the visible sliver of the parent: how deep into each canvas it comes from
    if args.depth:
        bins = [(-9, 0.0), (0.0, 0.005), (0.005, 0.015), (0.015, 0.030),
                (0.030, 0.050), (0.050, 0.090), (0.090, 9.0)]
        for key, anchor, theta, end in ((pkey, anchor_parent, 0.0, pend),
                                       (ckey, anchor_child, args.deg, cend)):
            if end not in ("top", "bottom"):
                print("  depth: %s end '%s' is not an axis end - skipped" % (key, end))
                continue
            geom = layer_geometry(canvases[key], anchor, theta, args.scale, px)
            depth = depth_map(geom, end, px, args.scale)
            owned = (owner == (1 if key == pkey else 2)) & (alphas[key] >= OPAQUE)
            print("  depth of the VISIBLE %s px (%s end '%s'): art = depth <= 0"
                  % (key, args.joint, end))
            for lo, hi in bins:
                m = owned & (depth > lo) & (depth <= hi)
                if not m.any():
                    print("     [%+7.1f..%+7.1f] mm: 0 px" % (lo * 1000, hi * 1000))
                    continue
                print("     [%+7.1f..%+7.1f] mm: %6d px  meanLum %5.1f  frac of layer %.2f"
                      % (lo * 1000, hi * 1000, int(m.sum()), float(lum[m].mean()),
                         m.sum() / max(1, int(owned.sum()))))

    out = args.out if os.path.isabs(args.out) else os.path.join(PROJECT, args.out)
    vis = np.zeros((owner.shape[0], owner.shape[1], 3), np.uint8)
    vis[:] = 255
    vis[owner == 1] = (200, 60, 60)
    vis[owner == 2] = (60, 90, 200)
    vis[(owner == 1) & (ca > 16)] = (170, 90, 170)      # parent seen through a soft child
    name = "%s-%+04.0f%s-owner.png" % (args.joint, args.deg, args.tag)
    if args.dump:
        x0, y0, x1, y1 = [int(v) for v in args.dump.split(",")]
        ramp = "@%#*+=-:."
        print("owner codes in %d,%d..%d,%d  (P parent-only, C child-opaque, c child-soft-over-parent, . empty):"
              % (x0, y0, x1, y1))
        for y in range(y0, y1):
            line = []
            for x in range(x0, x1):
                if owner[y, x] == 2:
                    line.append("C")
                elif owner[y, x] == 1:
                    line.append("c" if ca[y, x] > 16 else "P")
                else:
                    line.append(".")
            print("   " + "".join(line))
        print("luminance of the same window:")
        for y in range(y0, y1):
            line = []
            for x in range(x0, x1):
                if owner[y, x] == 0 and ca[y, x] < 16 and pa[y, x] < 16:
                    line.append(" ")
                    continue
                k = int(np.clip(lum[y, x] / 255.0 * (len(ramp) - 1), 0, len(ramp) - 1))
                line.append(ramp[k])
            print("   " + "".join(line))
    if args.dump:
        print("(not writing the owner map)")
        return 0
    os.makedirs(out, exist_ok=True)
    Image.fromarray(vis).save(os.path.join(out, name))
    print("owner map -> %s" % os.path.join(out, name))
    return 0


if __name__ == "__main__":
    sys.exit(main())
