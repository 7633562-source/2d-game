#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R4 - THE NEIGHBOUR'S OWN DELTA: what the cuff moves against the control.

Two takes on ONE build and ONE walk line: r4_ctl0 (foreshorten 0 - the control picture,
no covers) and r4_on (foreshorten 0.4, covers on).  The R4 rule is that the NEIGHBOUR is
not touched: the shin / the lower arm keep their own canvas at its own size, so in these
frames every changed pixel must sit in a joint's own zone (the parent's end + its cover)
and NOTHING outside them may move.  The tool prints the per-frame delta in total and per
zone, and writes a 3-panel (control | v0.4 | the delta in red over the v0.4 frame).

Usage: python Tools/ah5fr4-neighbour-delta.py [--times 15.43,28.38,30.00,33.22]
"""
import argparse
import importlib.util
import os

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("stills", os.path.join(HERE, "arth5f-stills.py"))
ST = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ST)

OUT = os.path.join(ST.RUN, "arth5f-foreshorten")


def zones(y0, y1, cx):
    """The four joints' bands in the frame's own figure box (the stills tool's spans +
    the shoulder the R3 tool left out)."""
    hgt = y1 - y0
    out = {}
    for name, (ty0, ty1, halfw) in {
        "knee": (y1 - int(hgt * 0.62), y1 - int(hgt * 0.02), int(hgt * 0.30)),
        "elbow": (y0 + int(hgt * 0.28), y0 + int(hgt * 0.50), int(hgt * 0.22)),
        "hip": (y0 + int(hgt * 0.42), y0 + int(hgt * 0.64), int(hgt * 0.22)),
        "shoulder": (y0 - int(hgt * 0.06), y0 + int(hgt * 0.34), int(hgt * 0.24)),
    }.items():
        out[name] = (cx - halfw, ty0, cx + halfw, ty1)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--times", default="15.43,28.38,30.00,33.22")
    ap.add_argument("--take-a", default="r4_ctl0")
    ap.add_argument("--take-b", default="r4_on")
    ap.add_argument("--rate", type=float, default=25.0)
    ap.add_argument("--thr", type=int, default=24, help="per-pixel |dRGB| sum counted as changed")
    args = ap.parse_args()

    lines = []
    for t in [float(x) for x in args.times.split(",") if x.strip()]:
        n = int(t * args.rate)
        fa = os.path.join(ST.RUN, "shot_%s_%02d.png" % (args.take_a, n))
        fb = os.path.join(ST.RUN, "shot_%s_%02d.png" % (args.take_b, n))
        if not (os.path.exists(fa) and os.path.exists(fb)):
            lines.append("t=%.2f frame %02d: MISSING %s %s" % (t, n, os.path.exists(fa), os.path.exists(fb)))
            continue
        ia = Image.open(fa).convert("RGB")
        ib = Image.open(fb).convert("RGB")
        aa = np.asarray(ia).astype(np.int16)
        ab = np.asarray(ib).astype(np.int16)
        box = ST.figure_box(aa, ST.horizon(aa))
        if box is None:
            lines.append("t=%.2f frame %02d: no figure" % (t, n))
            continue
        dx, dy = ST.align_shift(aa, ab, box)
        if (dx, dy) != (0, 0):
            ib = Image.fromarray(np.roll(ab, (dy, dx), axis=(0, 1)).astype(np.uint8))
            ab = np.asarray(ib).astype(np.int16)
        d = np.abs(aa - ab).sum(axis=2)
        m = d > args.thr
        x0, y0, x1, y1 = box
        cx = (x0 + x1) // 2
        zz = zones(y0, y1, cx)
        per = {}
        for name, (zx0, zy0, zx1, zy1) in zz.items():
            per[name] = int(m[max(0, zy0):zy1, max(0, zx0):zx1].sum())
        inside = np.zeros_like(m)
        for (zx0, zy0, zx1, zy1) in zz.values():
            inside[max(0, zy0):zy1, max(0, zx0):zx1] = True
        outside = int((m & ~inside).sum())
        ys, xs = np.nonzero(m)
        bb = (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())) if len(xs) else None
        lines.append("t=%5.2f frame %02d: changed %6d px (scene offset dx=%d dy=%d) | "
                     "knee %5d elbow %5d hip %5d shoulder %5d | OUTSIDE the four joint zones %5d | bbox %s"
                     % (t, n, int(m.sum()), dx, dy, per["knee"], per["elbow"], per["hip"],
                        per["shoulder"], outside, bb))
        # 3-panel sheet: control | v0.4 | the delta in red over the v0.4 frame
        over = np.asarray(ib).copy()
        over[m] = [255, 40, 40]
        panel = Image.new("RGB", (1920 * 3 + 16, 1080 + 30), (24, 24, 28))
        panel.paste(ia, (0, 30))
        panel.paste(ib, (1920 + 8, 30))
        panel.paste(Image.fromarray(over), (2 * 1920 + 16, 30))
        dr = ImageDraw.Draw(panel)
        dr.text((4, 6), "t=%.2f  control (foreshorten 0)  [%s]" % (t, args.take_a), fill=(240, 240, 240))
        dr.text((1928, 6), "v=0.4 + covers  [%s]" % args.take_b, fill=(255, 220, 120))
        dr.text((2 * 1920 + 24, 6), "changed pixels in red (%d px)" % int(m.sum()), fill=(255, 120, 120))
        name = "r4-neighbour-delta_t%05.2f_f%02d.png" % (t, n)
        panel.save(os.path.join(OUT, name))
        lines.append("    wrote %s" % name)
    txt = "\n".join(lines)
    print(txt)
    os.makedirs(os.path.join(ST.RUN, "cuff-r4"), exist_ok=True)
    with open(os.path.join(ST.RUN, "cuff-r4", "r4-neighbour-delta.txt"), "w", encoding="utf-8") as fh:
        fh.write("# ART-H5f R4 neighbour delta - Tools/ah5fr4-neighbour-delta.py\n" + txt + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
