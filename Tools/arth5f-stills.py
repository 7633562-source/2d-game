#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f - THE STILLS: the knee's and the shoulder's closeups at the walk's highest
knee-lifts, the control frame beside the foreshortened one.

The two film takes share the walk's own CSV, so a peak time picks the same frame index in
both; the human is found in the frame by the mannequin's own palette (the scene is only
sky and ground, so the mask over the horizon line is the figure).  Usage:
  python Tools/arth5f-stills.py --take-a human_foreshort_vis0 --take-b human_foreshort_vis
"""
import argparse
import csv
import glob
import os

import numpy as np
from PIL import Image, ImageDraw

RUN = "Trials/Runs"
OUT = os.path.join(RUN, "arth5f-foreshorten")
MASK = lambda a: ((a[..., 0] - a[..., 2] > 22) & (a[..., 0] > 160)
                  & (np.abs(a[..., 1] - (a[..., 0] - 28)) < 40) & (a[..., 2] > 70))


def peak_times(csv_path, top, min_gap=0.45, floor=50.0):
    with open(csv_path, newline="") as fh:
        rd = csv.reader(fh, delimiter=";")
        head = next(rd)
        idx = {n: i for i, n in enumerate(head)}
        t_i = idx.get("time", idx.get("t", 0))
        k_i = idx["kneeR"]
        rows = [(float(r[t_i]), float(r[k_i])) for r in rd if len(r) > k_i]
    peaks = []
    for i in range(1, len(rows) - 1):
        t, k = rows[i]
        if k >= floor and k >= rows[i - 1][1] and k > rows[i + 1][1]:
            if not peaks or t - peaks[-1][0] > min_gap:
                peaks.append((t, k))
            elif k > peaks[-1][1]:
                peaks[-1] = (t, k)
    peaks.sort(key=lambda p: -p[1])
    return sorted(peaks[:top])


def figure_box(a, ground_y):
    m = MASK(a)
    m[ground_y:] = False                      # below the horizon the ground shares the tones
    ys, xs = np.nonzero(m)
    if len(xs) < 200:
        return None
    keep = np.zeros_like(m)
    x0, x1 = np.percentile(xs, [1, 99]).astype(int)
    keep[:, max(0, x0 - 30):x1 + 30] = m[:, max(0, x0 - 30):x1 + 30]
    ys, xs = np.nonzero(keep)
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def horizon(a):
    """The last row of the sky, read down the frame's left edge."""
    sky = np.array([140, 199, 240])
    col = a[:, 20].astype(np.int16)
    d = np.abs(col - sky).sum(axis=1)
    rows = np.nonzero(d < 40)[0]
    return int(rows.max()) if len(rows) else a.shape[0] // 2


def align_shift(a, b, box, span=6, step=4):
    """The camera's follow is smoothed on the wall clock, so two takes of one walk can
    sit a few pixels apart.  Measure that offset on the scene (everything but the figure)
    and hand it back, so the pair below differs only by the picture under test."""
    h, w, _ = a.shape
    x0, y0, x1, y1 = box
    m = 8
    keep = np.ones((h, w), bool)
    keep[max(0, y0 - m):min(h, y1 + m), max(0, x0 - m):min(w, x1 + m)] = False
    ys, xs = slice(0, h, step), slice(0, w, step)
    ga, gm = a[ys, xs], keep[ys, xs]
    best, best_v = (0, 0), None
    for dy in range(-span, span + 1):
        for dx in range(-span, span + 1):
            gb = np.roll(b, (dy, dx), axis=(0, 1))[ys, xs]
            v = float(np.abs(ga - gb).sum(axis=2)[gm].mean())
            if best_v is None or v < best_v:
                best, best_v = (dx, dy), v
    return best


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv", default=os.path.join(RUN, "human_foreshort_vis.csv"))
    ap.add_argument("--take-a", default="human_foreshort_vis0")
    ap.add_argument("--take-b", default="human_foreshort_vis")
    ap.add_argument("--label-a", default="foreshorten 0")
    ap.add_argument("--label-b", default="foreshorten 0.4")
    ap.add_argument("--rate", type=float, default=25.0)
    ap.add_argument("--top", type=int, default=4)
    ap.add_argument("--zoom", type=float, default=3.0)
    ap.add_argument("--parts", default="knee,shoulder",
                    help="which joints' crops to write (knee, shoulder, elbow, hip)")
    ap.add_argument("--times", default="",
                    help="comma list of fixed peak times - skips the detection (R3's four)")
    ap.add_argument("--tag", default="",
                    help="prefix for the written names (R3 keeps the R1 set intact)")

    args = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)

    if args.times:
        # R3: the ACCEPTED line's own four lift peaks, fixed by the brief; the knee value
        # is read back from the row so the caption stays honest.
        want = [float(x) for x in args.times.split(",") if x.strip()]
        with open(args.csv, newline="") as fh:
            rd = csv.reader(fh, delimiter=";")
            head = next(rd)
            idx = {n: i for i, n in enumerate(head)}
            t_i = idx.get("time", idx.get("t", 0))
            k_i = idx["kneeR"]
            rows = [(float(r[t_i]), float(r[k_i])) for r in rd if len(r) > k_i]
        peaks = [(t, min(rows, key=lambda r: abs(r[0] - t))[1]) for t in want]
    else:
        peaks = peak_times(args.csv, args.top)
    print("peaks:", [(round(t, 2), round(k, 1)) for t, k in peaks])
    for t, k in peaks:
        n = int(t * args.rate)
        fa = os.path.join(RUN, "shot_%s_%02d.png" % (args.take_a, n))
        fb = os.path.join(RUN, "shot_%s_%02d.png" % (args.take_b, n))
        if not (os.path.exists(fa) and os.path.exists(fb)):
            print("missing frame", n, os.path.exists(fa), os.path.exists(fb))
            continue
        ia = Image.open(fa).convert("RGB")
        ib = Image.open(fb).convert("RGB")
        aa = np.asarray(ia).astype(np.int16)
        box = figure_box(aa, horizon(aa))
        if box is None:
            print("no figure at frame", n)
            continue
        ab = np.asarray(ib).astype(np.int16)
        dx, dy = align_shift(aa, ab, box)
        if (dx, dy) != (0, 0):
            ib = Image.fromarray(np.roll(ab, (dy, dx), axis=(0, 1)).astype(np.uint8))
            print("frame %d: scene offset dx=%d dy=%d (the camera's own jitter)" % (n, dx, dy))
        x0, y0, x1, y1 = box
        cx = (x0 + x1) // 2
        hgt = y1 - y0
        spans = {
            "knee": (y1 - int(hgt * 0.62), y1 - int(hgt * 0.02), int(hgt * 0.30)),
            "shoulder": (y0 - int(hgt * 0.06), y0 + int(hgt * 0.34), int(hgt * 0.24)),
            # ART-H5f R3: the covers' own joints.  The elbow sits just above the hip;
            # both bands ride the waist between the shoulder's and the knee's own.
            "elbow": (y0 + int(hgt * 0.26), y0 + int(hgt * 0.50), int(hgt * 0.22)),
            "hip": (y0 + int(hgt * 0.40), y0 + int(hgt * 0.64), int(hgt * 0.22)),
        }
        for part in [p for p in args.parts.split(",") if p in spans]:
            ty0, ty1, halfw = spans[part]
            bx = (cx - halfw - 20, max(0, ty0 - 10), cx + halfw + 20, min(1080, ty1 + 10))
            w = bx[2] - bx[0]
            zoom = args.zoom
            ca = ia.crop((bx[0], bx[1], min(1920, bx[2]), bx[3]))
            cb = ib.crop((bx[0], bx[1], min(1920, bx[2]), bx[3]))
            ca = ca.resize((int(ca.width * zoom), int(ca.height * zoom)), Image.NEAREST)
            cb = cb.resize((int(cb.width * zoom), int(cb.height * zoom)), Image.NEAREST)
            pad, strip = 8, 34
            out = Image.new("RGB", (ca.width + cb.width + pad, ca.height + strip), (24, 24, 28))
            out.paste(ca, (0, strip))
            out.paste(cb, (ca.width + pad, strip))
            dr = ImageDraw.Draw(out)
            dr.text((4, 4), "%s  t=%.2f  knee=%.0f  [%s]" % (part, t, k, args.label_a), fill=(240, 240, 240))
            dr.text((ca.width + pad + 4, 4), "%s  frame %02d  [%s]" % (part, n, args.label_b), fill=(255, 220, 120))
            name = "%s%s_t%05.2f_f%02d_ab.png" % (args.tag, part, t, n)
            out.save(os.path.join(OUT, name))
            print("wrote", name, out.size, "box", bx)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
