#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R3 - the covers' own in-game delta.  Two takes on ONE build and ONE walk line
differing only by -coverJoints: hcov_off (v=0.4, no cover nodes) and hcov_on (v=0.4, the
covers draw).  Everything the covers paint is the difference, and nothing else in the
picture may move: the tool prints the per-frame delta in total and per joint zone, and
writes a 3-panel (off | on | the delta in red over on) for the report.

Usage: python Tools/ah5fr3-offon-delta.py [--times 15.43,28.38,30.00,33.22]
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


def zones(box, y0, y1):
    hgt = y1 - y0
    cx = (box[0] + box[2]) // 2
    out = {}
    for name, (ty0, ty1, halfw) in {
        "knee": (y1 - int(hgt * 0.62), y1 - int(hgt * 0.02), int(hgt * 0.30)),
        "elbow": (y0 + int(hgt * 0.28), y0 + int(hgt * 0.50), int(hgt * 0.22)),
        "hip": (y0 + int(hgt * 0.42), y0 + int(hgt * 0.64), int(hgt * 0.22)),
    }.items():
        out[name] = (cx - halfw, ty0, cx + halfw, ty1)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--times", default="15.43,28.38,30.00,33.22")
    ap.add_argument("--take-a", default="hcov_off")
    ap.add_argument("--take-b", default="hcov_on")
    ap.add_argument("--rate", type=float, default=25.0)
    ap.add_argument("--zoom", type=int, default=2)
    args = ap.parse_args()

    for t in [float(x) for x in args.times.split(",") if x.strip()]:
        n = int(t * args.rate)
        pa = os.path.join(ST.RUN, "shot_%s_%02d.png" % (args.take_a, n))
        pb = os.path.join(ST.RUN, "shot_%s_%02d.png" % (args.take_b, n))
        if not (os.path.exists(pa) and os.path.exists(pb)):
            print("frame %02d: missing shot (%s / %s)" % (n, os.path.basename(pa), os.path.basename(pb)))
            continue
        a = np.asarray(Image.open(pa).convert("RGB"), dtype=np.int16)
        b = np.asarray(Image.open(pb).convert("RGB"), dtype=np.int16)
        box = ST.figure_box(b, ST.horizon(b))
        if box is None:
            print("frame %02d: no figure box" % n)
            continue
        dx, dy = ST.align_shift(a, b, box)
        a = np.roll(a, (dy, dx), axis=(0, 1))
        d = np.abs(a - b).max(axis=2) > 8
        zs = zones(box, box[1], box[3])
        counts = {k: int(d[z[1]:z[3], z[0]:z[2]].sum()) for k, z in zs.items()}
        covered = np.zeros_like(d)
        for k, z in zs.items():
            covered[z[1]:z[3], z[0]:z[2]] = True
        other = int((d & ~covered).sum())
        ys, xs = np.nonzero(d)
        bb = (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())) if len(xs) else None
        print("frame %02d t=%.2f shift=(%d,%d) delta %d px | knee %d elbow %d hip %d | other %d | bbox %s"
              % (n, t, dx, dy, int(d.sum()), counts["knee"], counts["elbow"], counts["hip"], other, bb))

        # the 3-panel: off | on | delta in red over the 'on' frame, cropped to the figure
        pad = 30
        x0 = max(0, box[0] - pad); x1 = min(b.shape[1], box[2] + pad)
        y0 = max(0, box[1] - pad); y1 = min(b.shape[0], box[3] + pad)
        za = args.zoom
        def crop(img):
            c = img[y0:y1, x0:x1].astype(np.uint8)
            return np.kron(c, np.ones((za, za, 1), dtype=np.uint8))
        pa_img = Image.fromarray(crop(a))
        pb_img = Image.fromarray(crop(b))
        base = crop(b).astype(np.int16)
        mask = np.kron(d[y0:y1, x0:x1], np.ones((za, za), bool))
        red = np.clip(base + mask[..., None] * np.array([110, -20, -30]), 0, 255).astype(np.uint8)
        pr_img = Image.fromarray(red)
        padp = 6
        w = pa_img.width + pb_img.width + pr_img.width + padp * 4
        h = pa_img.height + 40
        sheet = Image.new("RGB", (w, h), (18, 18, 22))
        dr = ImageDraw.Draw(sheet)
        sheet.paste(pa_img, (padp, 34))
        sheet.paste(pb_img, (padp * 2 + pa_img.width, 34))
        sheet.paste(pr_img, (padp * 3 + pa_img.width + pb_img.width, 34))
        dr.text((padp, 8), "%s  v=0.4  frame %02d (t=%.2f)" % (args.take_a, n, t), fill=(235, 235, 235))
        dr.text((padp * 2 + pa_img.width, 8), "%s  v=0.4  the covers draw" % args.take_b, fill=(255, 220, 120))
        dr.text((padp * 3 + pa_img.width + pb_img.width, 8),
                "delta %d px (knee %d, elbow %d, hip %d, other %d)"
                % (int(d.sum()), counts["knee"], counts["elbow"], counts["hip"], other), fill=(255, 120, 120))
        out = os.path.join(ST.OUT, "r3-offon-f%02d-t%05.2f.png" % (n, t))
        sheet.save(out)
        print("wrote", out)


if __name__ == "__main__":
    main()
