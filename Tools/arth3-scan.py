#!/usr/bin/env python3
"""ART-H3 probe: the ankle seam's own column scan (offline, shipped canvases).

Composes the shin+foot pair exactly as the dress does (via art-h1-pose-frames.py) at a big
scale, then prints the ALPHA + RGB profile of the composite along the limb's axis near the
seam, so a "hard overlay line" can be read as numbers before/after the ART-H3 profile:
a drawn dark rim or a tone step shows up as a dark band right above the hinge.

Usage: python Tools/arth3-scan.py [--jdir Trials/Runs/arth3-seam-before] [--joint ankle] \
       [--deg 0] [--scale 4000]
"""
import argparse
import importlib.util
import os
import sys

import numpy as np

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def frames_tool():
    path = os.path.join(PROJECT, "Tools", "art-h1-pose-frames.py")
    spec = importlib.util.spec_from_file_location("art_h1_pose_frames", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


JOINTS = {
    "ankle": ("shin", "heel", "heel", "shin", 0.22),
    "knee": ("thigh", "shin", "shin", "thigh", 0.30),
    "hip": ("pelvis", "thigh", "thigh", "pelvis", 0.34),
    "shoulder": ("torso", "upper", "upper", "torso", 0.30),
    "elbow": ("upper", "lower", "lower", "upper", 0.24),
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--joint", default="ankle")
    ap.add_argument("--deg", type=float, default=0.0)
    ap.add_argument("--scale", type=float, default=4000.0)
    ap.add_argument("--half", type=float, default=None)
    ap.add_argument("--out", default=None, help="save the composite PNG here")
    ap.add_argument("--save-png", default=None)
    ap.add_argument("--y0", type=int, default=-160, help="first row offset from the joint centre")
    ap.add_argument("--y1", type=int, default=40, help="last row offset")
    ap.add_argument("--step", type=int, default=4)
    ap.add_argument("--solo", default="both", choices=["both", "parent", "child"],
                    help="compose only one of the pair (whose content is this row?)")
    args = ap.parse_args()

    pf = frames_tool()
    ns, joints = pf.read_rig()
    pkey, ckey, cvar, pvar, half = JOINTS[args.joint]
    if args.half:
        half = args.half
    canvases = {k: pf.load_canvas(v) for k, v in pf.CANVAS.items()}
    canvases = {k: v for k, v in canvases.items() if v is not None}
    spec = joints[cvar]
    anchor_child = spec["anchor"]
    anchor_parent = spec["anchor_parent"]
    layers = [(pkey, anchor_parent, 0.0), (ckey, anchor_child, args.deg)]
    if args.solo == "parent":
        layers = layers[:1]
    elif args.solo == "child":
        layers = layers[1:]
    frame = pf.compose(canvases, layers, half, args.scale)
    if args.save_png:
        p = args.save_png if os.path.isabs(args.save_png) else os.path.join(PROJECT, args.save_png)
        frame.save(p)
        print("saved", p)
    arr = np.array(frame)
    h, w = arr.shape[0], arr.shape[1]
    cx = w // 2
    cy = h // 2
    # which end of the limb is the parent?  the parent's canvas is placed at rest; its box
    # centre is towards the canvas' own inward direction.  Print both columns above and below
    # the joint centre: the seam's rows are the ones just above the hinge for the ankle.
    print("frame %dx%d, joint centre at (%d,%d), scale %.0f px/m" % (w, h, cx, cy, args.scale))
    print("row   y_off  alpha   R   G   B    lum")
    rows = range(cy + args.y0, cy + args.y1 + 1, args.step)
    for y in rows:
        px = arr[y, cx]
        a = int(px[3])
        if a < 8:
            print("%4d %+6d %6d    .   .   ." % (y, y - cy, a))
            continue
        lum = 0.2126 * px[0] + 0.7152 * px[1] + 0.0722 * px[2]
        print("%4d %+6d %6d %4d %4d %4d  %6.1f" % (y, y - cy, a, px[0], px[1], px[2], lum))
    return 0


if __name__ == "__main__":
    sys.exit(main())
