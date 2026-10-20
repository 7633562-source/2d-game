#!/usr/bin/env python3
"""ART-H3 instrument: the seams' own microscope (offline, from the SHIPPED canvases).

Two readings, both on frames composed exactly the way the dress composes them (via
Tools/art-h1-pose-frames.py's rig reader and placement - no geometry is typed here twice):

  * VIEWS: <out>/<joint>-<deg>.png close-ups (scale 2000 px/m) at the joints' own max-flexion
    angles, plus <out>/<joint>-rest.png - the deliverable read for "does the joint read as a
    limb" before/after the ART-H3 profile.
  * NUMBERS:
      edge-hardness  - a hard silhouette cut has alpha jumping 255 -> 0 in ~1 px. Count
                       adjacent px pairs crossing the silhouette (alpha >= 240 next to
                       alpha <= 16) and px in the 17..239 band, over the frame.
      seam-step      - the biggest luminance step between vertically adjacent OPAQUE px in
                       the seam zone (the overlap band around the joint), i.e. how strong a
                       line the overlap paints. Lower = the overlay line is gone/softened.
      coverage       - transparent px inside the joint's own disc (radius = the narrower
                       neighbour's ink half at the hinge, the H1 model) - the standing bar.

Usage:  python Tools/arth3-seam.py --out Trials/Runs/arth3-seam-before
        python Tools/arth3-seam.py --out Trials/Runs/arth3-seam-after
"""
import argparse
import importlib.util
import os
import sys

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# joint, canvas keys (parent, child), the frame half-size in metres, the angles to render
JOBS = [
    ("knee", "thigh", "shin", 0.30, [0, 120]),
    ("hip", "pelvis", "thigh", 0.34, [0, -90]),
    ("shoulder", "torso", "upper", 0.30, [0, -160]),
    ("ankle", "shin", "heel", 0.22, [0, 30, 45]),
    ("elbow", "upper", "lower", 0.24, [0, -120]),
]


def frames_tool():
    path = os.path.join(PROJECT, "Tools", "art-h1-pose-frames.py")
    spec = importlib.util.spec_from_file_location("art_h1_pose_frames", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def luminance(rgba):
    f = rgba.astype(np.float64) / 255.0
    return 0.2126 * f[:, :, 0] + 0.7152 * f[:, :, 1] + 0.0722 * f[:, :, 2]


def edge_stats(arr):
    """(hard crossings, soft-band px) over the frame's silhouette."""
    a = arr[:, :, 3]
    hard = ((a[:, :-1] >= 240) & (a[:, 1:] <= 16)).sum() + \
           ((a[:, :-1] <= 16) & (a[:, 1:] >= 240)).sum() + \
           ((a[:-1, :] >= 240) & (a[1:, :] <= 16)).sum() + \
           ((a[:-1, :] <= 16) & (a[1:, :] >= 240)).sum()
    soft = int(((a > 16) & (a < 240)).sum())
    return int(hard), soft


def seam_step(arr, radius_px):
    """Biggest |dL| between vertically adjacent opaque px inside the central disc."""
    lum = luminance(arr)
    a = arr[:, :, 3]
    h, w = a.shape
    cy, cx = h // 2, w // 2
    yy, xx = np.mgrid[0:h, 0:w]
    disc = ((xx + 0.5 - cx) ** 2 + (yy + 0.5 - cy) ** 2) <= radius_px * radius_px
    ok = disc & (a >= 200)
    pair = ok[:-1, :] & ok[1:, :]
    if not pair.any():
        return 0.0
    d = np.abs(lum[1:, :] - lum[:-1, :])
    return float(d[pair].max() * 255.0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--scale", type=float, default=1000.0,
                    help="metrics at this scale (1000 = the H1 tool's own instrument)")
    ap.add_argument("--view-scale", type=float, default=2000.0,
                    help="the saved close-ups' scale (0 = same as --scale)")
    ap.add_argument("--only", default=None)
    args = ap.parse_args()

    pf = frames_tool()
    ns, joints = pf.read_rig()
    canvases = {k: pf.load_canvas(v) for k, v in pf.CANVAS.items()}
    canvases = {k: v for k, v in canvases.items() if v is not None}
    out = args.out if os.path.isabs(args.out) else os.path.join(PROJECT, args.out)
    os.makedirs(out, exist_ok=True)

    print("%-9s %6s %8s %8s %10s %9s" % ("joint", "deg", "hard", "softpx", "seamstep", "R_eye m"))
    for name, pkey, ckey, half, angles in JOBS:
        if args.only and name not in args.only.split(","):
            continue
        if pkey not in canvases or ckey not in canvases:
            print("%-9s CANVAS MISSING (%s/%s)" % (name, pkey, ckey))
            continue
        child_var = {("thigh", "shin"): "shin", ("pelvis", "thigh"): "thigh",
                     ("torso", "upper"): "upper", ("shin", "heel"): "heel",
                     ("upper", "lower"): "lower"}[(pkey, ckey)]
        spec = joints.get(child_var)
        parent_var = {"shin": "thigh", "thigh": "pelvis", "upper": "torso",
                      "heel": "shin", "lower": "upper"}[child_var]
        if spec is None or spec["parent"] != parent_var:
            print("%-9s NO ConnectTo LINE" % name)
            continue
        anchor_child = spec["anchor"]
        anchor_parent = spec["anchor_parent"]
        r_eye = min(pf.ink_half(canvases[pkey], anchor_parent),
                    pf.ink_half(canvases[ckey], anchor_child))
        for deg in angles:
            layers = [(pkey, anchor_parent, 0.0), (ckey, anchor_child, float(deg))]
            frame = pf.compose(canvases, layers, half, args.scale)
            arr = np.array(frame)
            hard, soft = edge_stats(arr)
            step = seam_step(arr, r_eye * args.scale)
            hole, total = pf.measure(frame, r_eye, args.scale)
            view = frame if args.view_scale <= 0 or abs(args.view_scale - args.scale) < 1e-6 \
                else pf.compose(canvases, layers, half, args.view_scale)
            p = os.path.join(out, "%s-%+04d.png" % (name, deg))
            view.save(p)
            print("%-9s %6d %8d %8d %10.1f %9.4f  hole=%d/%d  %s"
                  % (name, deg, hard, soft, step, r_eye, hole, total,
                     os.path.basename(p)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
