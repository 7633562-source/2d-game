#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R5 - the torso's own shoulder patch, mapped in the joint frame.

Prints an ASCII map of the torso canvas' alpha around the shoulder's hinge (x across,
y up, mm) with the disc of R_eye drawn as 'o' - so the patch's centre, radius and the
fade rim can be READ, not guessed.  Second map: the upper-arm canvas at rest.

Usage: python Tools/ah5fr5-patch-map.py [--joint shoulder] [--side near] [--step 6]
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
    ap.add_argument("--step", type=float, default=6.0)
    args = ap.parse_args()

    ns, joints = F.read_rig()
    canvases = {k: F.load_canvas(v) for k, v in F.CANVAS.items()}
    canvases = {k: v for k, v in canvases.items() if v is not None}
    spec = BAKE.resolve_spec(args.joint, args.side)
    r_eye = min(F.ink_half(canvases[spec["parent"]], spec["qp"]),
                F.ink_half(canvases[spec["child"]], spec["qc"]))
    print("joint %s side %s  qp=(%.4f,%.4f) qc=(%.4f,%.4f)  R_eye %.4f m"
          % (args.joint, args.side, spec["qp"][0], spec["qp"][1], spec["qc"][0], spec["qc"][1], r_eye))

    xs = np.arange(-80.0, 40.0 + 1e-9, args.step)   # mm
    ys = np.arange(60.0, -60.0 - 1e-9, -args.step)
    for label, key, anchor, theta, kk in (
            ("%s (parent)" % spec["parent"], spec["parent"], spec["qp"], 0.0, 1.0),
            ("%s (child, rest)" % spec["child"], spec["child"], spec["qc"], 0.0, 1.0)):
        X, Y = np.meshgrid(xs / 1000.0, ys / 1000.0)
        al = sample_alpha(canvases[key], X, Y, anchor, theta=theta, kk=kk)
        print("\n%s alpha map (x -80..40 mm, y 60..-60 mm, step %.0f mm; 'o' = disc rim,"
              " '.'=nothing, '.'..'@' = alpha)" % (label, args.step))
        hdr = "        " + "".join("%5.0f" % x for x in xs)
        print(hdr)
        for j, y in enumerate(ys):
            row = []
            for i, x in enumerate(xs):
                a = al[j, i]
                r = np.hypot(x, y)
                ch = " "
                if a > 0.6:
                    ch = "#"
                elif a > 0.2:
                    ch = "+"
                elif a > 0.06:
                    ch = ":"
                elif a > 0.01:
                    ch = "."
                if abs(r - r_eye * 1000.0) <= args.step * 0.5:
                    ch = "o" if ch == " " else "O"
                row.append("%4s " % ch if ch != " " else "    .")
            print("y%+5.0f  %s" % (y, "".join(row)))


if __name__ == "__main__":
    main()
