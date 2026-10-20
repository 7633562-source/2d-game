#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R3 - THE COVER'S OWN SWEEP: the joint's disc holes across the walk's own
angle range at v=0.4, without and with the baked cover (the report's table).

The instrument is the bake's own lab measure (Tools/art-h5f-cover-bake.py --lab): the
disc of radius R_eye (the narrower neighbour's ink half-width where the two meet) and
the rest-limb shape, both counted for open (background) pixels in the composition the
game draws - the cover under both sprites, the parent shrunk about its pivot, the child
grown with its far edge held.  Angle samples = the joint's own range on the accepted
walk (bake.RANGES).

Usage: python Tools/ah5fr3-cover-sweep.py [--v 0.4] [--steps 9] [--joint knee,elbow,hip]
"""
import argparse
import importlib.util
import os

import numpy as np

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PROJECT, "Trials", "Runs", "arth5f-foreshorten")


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(PROJECT, "Tools", rel))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


BAKE = load("bake", "art-h5f-cover-bake.py")
F = BAKE.F


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--v", type=float, default=0.4)
    ap.add_argument("--steps", type=int, default=9)
    ap.add_argument("--joint", default="knee,elbow,hip")
    ap.add_argument("--scale", type=float, default=3000.0)
    args = ap.parse_args()

    ns, joints = F.read_rig()
    canvases = {k: F.load_canvas(v) for k, v in F.CANVAS.items()}
    canvases = {k: v for k, v in canvases.items() if v is not None}

    lines = []
    for name in args.joint.split(","):
        for side in ("near", "far"):
            if "side_x" not in BAKE.JOINTS[name] and side == "far":
                continue
            spec = BAKE.resolve_spec(name, side)
            fr, rgba, shape = BAKE.bake(spec, canvases, ns, args.scale, verbose=False)
            cover = np.zeros_like(rgba)
            cover[..., :3] = rgba[..., :3]
            cover[..., 3] = rgba[..., 3]
            lo, hi = BAKE.RANGES[name]
            thetas = [lo + (hi - lo) * i / (args.steps - 1) for i in range(args.steps)]
            r_eye = min(F.ink_half(canvases[spec["parent"]], spec["qp"]),
                        F.ink_half(canvases[spec["child"]], spec["qc"]))
            yy, xx = np.mgrid[0:fr.ny, 0:fr.nx]
            Xm = fr.x0 + (xx + 0.5) / fr.scale
            Ym = fr.y0 + fr.h - (yy + 0.5) / fr.scale
            disc = (Xm * Xm + Ym * Ym) <= r_eye * r_eye
            worst_nc, worst_t, tot_wc, worst_wc = 0, 0.0, 0, 0
            rows = []
            for th in thetas:
                p = BAKE.pose(fr, spec, th, args.v)
                game = BAKE.over(BAKE.over(cover, p["par_now"]), p["ch_now"])
                ctrl = BAKE.over(p["par_now"], p["ch_now"])
                nc = int((disc & (ctrl[..., 3] <= 0.06)).sum())
                wc = int((disc & (game[..., 3] <= 0.06)).sum())
                if nc > worst_nc:
                    worst_nc, worst_t = nc, th
                tot_wc += wc
                worst_wc = max(worst_wc, wc)
                rows.append((th, nc, wc))
            lines.append("%s v=%.2f: disc R_eye %.4f m, %d angles %.0f..%.0f deg | "
                         "no-cover worst %d px @ %+.0f deg -> with-cover total %d, worst %d"
                         % (spec["name"], args.v, r_eye, len(thetas), lo, hi,
                            worst_nc, worst_t, tot_wc, worst_wc))
            for th, nc, wc in rows:
                lines.append("    theta %+6.1f: no-cover %5d  with-cover %d" % (th, nc, wc))
    txt = "\n".join(lines)
    print(txt)
    path = os.path.join(OUT, "r3-cover-sweep.txt")
    with open(path, "w", encoding="ascii") as fh:
        fh.write("# ART-H5f R3 cover sweep - Tools/ah5fr3-cover-sweep.py\n" + txt + "\n")
    print("saved", path)


if __name__ == "__main__":
    main()
