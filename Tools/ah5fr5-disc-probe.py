#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R5 - WHO PAINTS THE SHOULDER'S DISC, and where the hole sits inside it.

The sweep counts the sky inside the disc; this probe answers WHERE inside the disc the
hole is, and what the two canvases carry there:

  * the hole's radial extent (min/max distance from the hinge, mm) against R_eye,
  * the parent's and the child's own alpha sampled at the hole's pixels (the patch's
    faded rim reads as low alpha, a real gap reads as zero),
  * the same at the control (v=0) so the R5 bar's own number is explained, not only
    counted.

Usage: python Tools/ah5fr5-disc-probe.py [--joint shoulder] [--side near] [--deg -14]
"""
import argparse
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


def sample_alpha(canvas, X, Y, anchor, theta=0.0, kk=1.0, ss=0.0):
    """The canvas' own alpha at joint-frame points (X, Y) - the bake.place mapping."""
    W, H = canvas["px"]
    ppu = canvas["ppu"]
    pxv = canvas["pivot"][0] * W
    pyv = (1.0 - canvas["pivot"][1]) * H
    th = np.radians(theta)
    ct, st = np.cos(th), np.sin(th)
    u = ct * X + st * Y + anchor[0]
    vv = -st * X + ct * Y + anchor[1]
    v = (vv - ss) / kk
    sx = np.clip(np.round(pxv + u * ppu - 0.5).astype(np.int64), 0, W - 1)
    sy = np.clip(np.round(pyv - v * ppu - 0.5).astype(np.int64), 0, H - 1)
    a = np.asarray(canvas["im"])[:, :, 3].astype(np.float64) / 255.0
    return a[sy, sx]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--joint", default="shoulder")
    ap.add_argument("--side", default="near")
    ap.add_argument("--deg", type=float, default=-14.0)
    ap.add_argument("--scale", type=float, default=3000.0)
    args = ap.parse_args()

    ns, joints = F.read_rig()
    canvases = {k: F.load_canvas(v) for k, v in F.CANVAS.items()}
    canvases = {k: v for k, v in canvases.items() if v is not None}
    spec = BAKE.resolve_spec(args.joint, args.side)
    fr, rgba, shape = BAKE.bake(spec, canvases, ns, args.scale, verbose=False)
    p_half = F.ink_half(canvases[spec["parent"]], spec["qp"])
    c_half = F.ink_half(canvases[spec["child"]], spec["qc"])
    r_eye = min(p_half, c_half)
    print("ink_half %s %.4f m  %s %.4f m  -> R_eye %.4f m"
          % (spec["parent"], p_half, spec["child"], c_half, r_eye))

    yy, xx = np.mgrid[0:fr.ny, 0:fr.nx]
    Xm = fr.x0 + (xx + 0.5) / fr.scale
    Ym = fr.y0 + fr.h - (yy + 0.5) / fr.scale
    rad = np.sqrt(Xm * Xm + Ym * Ym)
    disc = rad <= r_eye
    for v in (0.0, 0.4):
        p = BAKE.pose(fr, spec, args.deg, v)
        ctrl = BAKE.over(p["par_now"], p["ch_now"])
        hole = disc & (ctrl[..., 3] <= 0.06)
        n = int(hole.sum())
        print("%s v=%.2f deg=%+.1f: hole %d px of disc %d" % (args.joint, v, args.deg, n, int(disc.sum())))
        if not n:
            continue
        r_mm = rad[hole] * 1000.0
        print("    radial extent %.1f..%.1f mm (R_eye %.1f) - the hole %s the rim"
              % (r_mm.min(), r_mm.max(), r_eye * 1000.0,
                 "sits on" if r_mm.min() > r_eye * 1000.0 - 12.0 else "reaches inside"))
        Xh, Yh = Xm[hole], Ym[hole]
        pa = sample_alpha(canvases[spec["parent"]], Xh, Yh, spec["qp"])
        ca = sample_alpha(canvases[spec["child"]], Xh, Yh, spec["qc"],
                          theta=args.deg, kk=1.0 - v, ss=0.0)
        print("    parent alpha: >0.06 on %d px, 0.01..0.06 on %d px, <=0.01 on %d px"
              % (int((pa > 0.06).sum()), int(((pa > 0.01) & (pa <= 0.06)).sum()), int((pa <= 0.01).sum())))
        print("    child  alpha: >0.06 on %d px, 0.01..0.06 on %d px, <=0.01 on %d px  (max %.3f)"
              % (int((ca > 0.06).sum()), int(((ca > 0.01) & (ca <= 0.06)).sum()), int((ca <= 0.01).sum()),
                 float(ca.max())))


if __name__ == "__main__":
    main()
