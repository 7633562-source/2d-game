#!/usr/bin/env python3
"""ART-H4d probe: the offline joint A/B - ONE joint drawn from two canvas SETS side by side.

Uses the pose tool's own rig reader / placement / composition (`Tools/art-h1-pose-frames.py`,
imported - no second copy of the geometry), so the strip is exactly what the coverage tool
draws.  Each set is a canvas dir (e.g. Trials/Runs/arth4d-before/canvas).

Usage:
  python Tools/arth4d-ab.py --a <dirA> --b <dirB> --joint knee --deg 60 \
      --out Trials/Runs/arth4d-ab/knee-060-ab.png [--scale 3000 --half 0.30 --bg 140,199,240]
Prints the ink the added paint contributes near the joint (px count) for both sets.
"""
import argparse
import importlib.util
import os
import sys

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def frames_tool():
    path = os.path.join(PROJECT, "Tools", "art-h1-pose-frames.py")
    spec = importlib.util.spec_from_file_location("art_h1_pose_frames", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def joint_frame(pf, cdir, joint, deg, scale, half, only=None):
    pf.ART = cdir
    ns, joints = pf.read_rig()
    for name, pkey, ckey, cvar, pvar, h in pf.JOINTS:
        if name != joint:
            continue
        canvases = {k: pf.load_canvas(v) for k, v in pf.CANVAS.items()}
        canvases = {k: v for k, v in canvases.items() if v is not None}
        spec = joints[cvar]
        layers = [(pkey, spec["anchor_parent"], 0.0), (ckey, spec["anchor"], deg)]
        if only == "parent":
            layers = layers[:1]
        elif only == "child":
            layers = layers[1:]
        return pf.compose(canvases, layers, half, scale)
    raise SystemExit("no such joint: " + joint)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--a", required=True)
    ap.add_argument("--b", required=True)
    ap.add_argument("--joint", default="knee")
    ap.add_argument("--deg", type=float, default=60.0)
    ap.add_argument("--scale", type=float, default=3000.0)
    ap.add_argument("--half", type=float, default=0.30)
    ap.add_argument("--bg", default="140,199,240")
    ap.add_argument("--win", type=float, default=0.13, help="crop half-window, m")
    ap.add_argument("--out", required=True)
    ap.add_argument("--label-a", default="A")
    ap.add_argument("--label-b", default="B")
    args = ap.parse_args()

    bg = tuple(int(v) for v in args.bg.split(","))
    pf = frames_tool()
    ims = [joint_frame(pf, args.a, args.joint, args.deg, args.scale, args.half),
           joint_frame(pf, args.b, args.joint, args.deg, args.scale, args.half)]
    px = args.half * args.scale
    w = int(round(args.win * args.scale))
    c = int(round(px))
    crop = [im.crop((c - w, c - w, c + w, c + w)) for im in ims]

    pad = 10
    strip = Image.new("RGB", (crop[0].width * 2 + pad * 3, crop[0].height + pad * 2), bg)
    for i, cr in enumerate(crop):
        cell = Image.new("RGBA", cr.size, bg + (255,))
        cell.alpha_composite(cr)
        strip.paste(cell.convert("RGB"), (pad + i * (crop[0].width + pad), pad))
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    strip.save(args.out)
    print("ab %s  joint %s %+.0f deg  scale %.0f px/m  window %.2f m  %dx%d" %
          (args.out, args.joint, args.deg, args.scale, args.win, strip.width, strip.height))

    for lbl, im in ((args.label_a, ims[0]), (args.label_b, ims[1])):
        a = np.asarray(im)
        ink = a[:, :, 3] > 16
        print("%s: ink px %d, semi (16<a<240) %d, mean alpha of ink %.1f" %
              (lbl, int(ink.sum()), int(((a[:, :, 3] > 16) & (a[:, :, 3] < 240)).sum()),
               float(a[:, :, 3][ink].mean())))

    # the exposed overlap: the parent's paint where the child leaves it bare (the parent is
    # drawn UNDER the child, so this is what the eye sees over the background)
    for lbl, cdir in ((args.label_a, args.a), (args.label_b, args.b)):
        par = np.asarray(joint_frame(pf, cdir, args.joint, args.deg, args.scale, args.half,
                                     only="parent"))
        chi = np.asarray(joint_frame(pf, cdir, args.joint, args.deg, args.scale, args.half,
                                     only="child"))
        expo = (par[:, :, 3] > 16) & (chi[:, :, 3] <= 16)
        al = par[:, :, 3][expo]
        print("%s exposed: %d px (strong a>200: %d, mid 100-200: %d), mean alpha %.0f"
              % (lbl, int(expo.sum()), int((al > 200).sum()), int(((al > 100) & (al <= 200)).sum()),
                 float(al.mean()) if al.size else 0.0))
    return 0


if __name__ == "__main__":
    sys.exit(main())
