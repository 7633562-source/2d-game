#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R3 - is the cover's residual the CONTROL ART's own bay?

For every joint and side: at v=0.4 across the joint's own angle range, count the disc holes
of the composition the GAME draws (the parent as the dress draws it: the knee/elbow parent
shrunk about its pivot, the hip's pelvis untouched), with the baked cover under both sprites.
Then compare against the control art's OWN holes at rest (v=0, theta=0, the accepted art as
it ships): the report's question is whether the cover ever leaves background that the control
art itself does not already show - a pre-existing bay (the shoulder's armpit bay's kind) is
out of scope and named; anything else is the cover failing.

Usage: python Tools/ah5fr3-bay-check.py
"""
import importlib.util
import os

import numpy as np

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(PROJECT, "Tools", rel))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


BAKE = load("bake", "art-h5f-cover-bake.py")
F = BAKE.F


def main():
    ns, joints = F.read_rig()
    canvases = {k: F.load_canvas(v) for k, v in F.CANVAS.items()}
    canvases = {k: v for k, v in canvases.items() if v is not None}

    for name in ("knee", "elbow", "hip", "shoulder"):
        sides = ("near", "far") if "side_x" in BAKE.JOINTS[name] else ("near",)
        for side in sides:
            spec = BAKE.resolve_spec(name, side)
            fr, rgba, shape = BAKE.bake(spec, canvases, ns, 3000.0, verbose=False)
            cover = rgba.copy()
            r_eye = min(F.ink_half(canvases[spec["parent"]], spec["qp"]),
                        F.ink_half(canvases[spec["child"]], spec["qc"]))
            yy, xx = np.mgrid[0:fr.ny, 0:fr.nx]
            Xm = fr.x0 + (xx + 0.5) / fr.scale
            Ym = fr.y0 + fr.h - (yy + 0.5) / fr.scale
            disc = (Xm * Xm + Ym * Ym) <= r_eye * r_eye
            short = spec.get("short", "parent")

            # the control art's own holes at rest (v=0, theta=0)
            p0 = BAKE.pose(fr, spec, 0.0, 0.0)
            rest = disc & (BAKE.over(p0["par_now"], p0["ch_now"])[..., 3] <= 0.06)
            # the game's own no-cover holes at v=0.4, theta=0: what the cover has to close
            p0g = BAKE.pose(fr, spec, 0.0, 0.4)
            par0g = p0g["par_now"]   # R4: the hinge sits at the shortened box' end
            base = disc & (BAKE.over(par0g, p0g["ch_now"])[..., 3] <= 0.06)

            lo, hi = BAKE.RANGES[name]
            worst_beyond, worst_th, worst_wc, worst_wc_th = 0, 0.0, 0, 0.0
            for i in range(9):
                th = lo + (hi - lo) * i / 8.0
                p = BAKE.pose(fr, spec, th, 0.4)
                par = p0g["par_now"]   # the parent never rotates: theta 0
                game = BAKE.over(BAKE.over(cover, par), p["ch_now"])
                wc = disc & (game[..., 3] <= 0.06)
                beyond = wc & ~rest
                if int(wc.sum()) > worst_wc:
                    worst_wc, worst_wc_th = int(wc.sum()), th
                if int(beyond.sum()) > worst_beyond:
                    worst_beyond, worst_th = int(beyond.sum()), th
            print("%-10s v=0.4: game no-cover %5d px at theta 0 | control-at-rest holes %5d px"
                  " | worst with-cover %5d px @ %+.0f deg, of it beyond the control's own"
                  " holes %d px @ %+.0f deg" % (spec["name"], int(base.sum()), int(rest.sum()),
                                                worst_wc, worst_wc_th, worst_beyond, worst_th))


if __name__ == "__main__":
    main()
