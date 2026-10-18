#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R3 - the hip's cover against the GAME's own composition.

The offline model (art-h5f-cover-bake.py pose()) scales the pelvis by k as well at the hip
(the short=child branch applies k to both placements), which the game's dress does NOT do:
HumanArt.ForeshortenVisuals writes only the thigh's / the upper arm's own size, the pelvis
keeps its paint.  This probe composes the hip both ways - the model's pose and the game's
pose - and counts the disc holes with the baked cover, printing the residual's bbox so a
hole that exists only in the model can be told from one that exists in the game.

Usage: python Tools/ah5fr3-hip-parity.py
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

    for side in ("near", "far"):
        spec = BAKE.resolve_spec("hip", side)
        fr, rgba, shape = BAKE.bake(spec, canvases, ns, 3000.0, verbose=False)
        cover = rgba.copy()
        r_eye = min(F.ink_half(canvases[spec["parent"]], spec["qp"]),
                    F.ink_half(canvases[spec["child"]], spec["qc"]))
        yy, xx = np.mgrid[0:fr.ny, 0:fr.nx]
        Xm = fr.x0 + (xx + 0.5) / fr.scale
        Ym = fr.y0 + fr.h - (yy + 0.5) / fr.scale
        disc = (Xm * Xm + Ym * Ym) <= r_eye * r_eye
        par_game = fr.place(spec["parent"], spec["qp"], 0.0, 1.0, 0.0)
        for v in (0.0, 0.2, 0.4):
            for th in (-33.0, -7.5, 5.2, 18.0):
                p = BAKE.pose(fr, spec, th, v)
                for lab, par in (("model par*k", p["par_now"]), ("game  par*1", par_game)):
                    game = BAKE.over(BAKE.over(cover, par), p["ch_now"])
                    ctrl = BAKE.over(par, p["ch_now"])
                    wc = int((disc & (game[..., 3] <= 0.06)).sum())
                    nc = int((disc & (ctrl[..., 3] <= 0.06)).sum())
                    print("%s v=%.1f %s theta %+6.1f deg: with-cover %5d (no-cover %5d)"
                          % (spec["name"], v, lab, th, wc, nc))
                res = disc & (BAKE.over(BAKE.over(cover, par_game), p["ch_now"])[..., 3] <= 0.06)
                if res.any():
                    rr, cc = np.nonzero(res)
                    print("    game v=%.1f residual %d px bbox x %+.1f..%+.1f mm y %+.1f..%+.1f mm"
                          % (v, res.sum(), fr.xm(cc.min()) * 1000, fr.xm(cc.max()) * 1000,
                             fr.ym(rr.max()) * 1000, fr.ym(rr.min()) * 1000))


if __name__ == "__main__":
    main()
