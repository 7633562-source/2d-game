#!/usr/bin/env python3
"""ART-H4d probe: the thigh canvas' knee-end (terminal) boundary - alpha and tone as pixels.

Prints, for rows walking from the canvas' bottom tip inward:
  * the ink span (alpha>16) and the row's alpha ramp across BOTH silhouette edges (the
    outermost 16 px of the row: how many px sit between alpha 16..240 - the soft-edge width);
  * where the box edge is (from the .meta pivot + Human.cs' thighSize), and where the joint
    disc' radius (R_eye, from the plan) lands, both measured from the box edge.

Usage: python Tools/arth4d-canvas-end.py [--part thigh] [--rows 140] [--every 4]
"""
import argparse
import json
import os
import re

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "Resources", "Art", "Human")
HUMAN_CS = os.path.join(PROJECT, "Assets", "Human.cs")
PLAN = os.path.join(PROJECT, "Trials", "Runs", "art-h1-joint-plan.json")


def read_meta(path):
    return open(path, encoding="utf-8", errors="replace").read()


def meta_ppu(meta):
    m = re.search(r"spritePixelsToUnits: ([0-9.]+)", meta)
    return float(m.group(1))


def meta_pivot(meta):
    m = re.search(r"spritePivot: \{x: ([0-9.]+), y: ([0-9.]+)\}", meta)
    return float(m.group(1)), float(m.group(2))


def box_size(field):
    text = open(HUMAN_CS, encoding="utf-8", errors="replace").read()
    m = re.search(r"public Vector2 %s\s*=\s*new Vector2\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f" %
                  field, text)
    return float(m.group(1)), float(m.group(2))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--part", default="thigh")
    ap.add_argument("--rows", type=int, default=140)
    ap.add_argument("--every", type=int, default=4)
    ap.add_argument("--field", default="thighSize")
    args = ap.parse_args()

    path = os.path.join(ART, args.part + ".png")
    a = np.asarray(Image.open(path).convert("RGBA")).astype(np.float64)
    meta = read_meta(path + ".meta")
    ppu = meta_ppu(meta)
    pivx, pivy = meta_pivot(meta)
    h, w = a.shape[:2]
    bw, bh = box_size(args.field)
    print("%s %dx%d ppu=%.0f pivot=(%.4f, %.4f) box=%.3fx%.3f m" % (args.part, w, h, ppu, pivx, pivy, bw, bh))
    box_cy = (1.0 - pivy) * h                    # image row of the box centre
    box_bottom = box_cy + bh * ppu / 2.0         # image row of the box' bottom edge
    print("box centre row %.1f, box bottom edge row %.1f (= row 0 of the skirt/skirt start)" %
          (box_cy, box_bottom))

    plan = json.load(open(PLAN, encoding="utf-8"))
    r_eye = None
    for j in plan.get("joints", []):
        if j.get("parent") == args.part and j.get("parent_end") == "bottom":
            r_eye = float(j["r_eye"])
    if r_eye:
        print("plan: R_eye %.4f m = %.1f px -> the disc edge sits %.1f px below the box bottom edge" %
              (r_eye, r_eye * ppu, r_eye * ppu))

    alpha = a[:, :, 3]
    lum = 0.2126 * a[:, :, 0] + 0.7152 * a[:, :, 1] + 0.0722 * a[:, :, 2]
    ink_any = alpha > 16
    rows = np.where(ink_any.any(axis=1))[0]
    print("ink rows %d..%d" % (rows[0], rows[-1]))
    last = rows[-1]
    print("%6s %7s %6s %6s  %-24s %-24s %s" %
          ("row", "frombox_px", "frombox_mm", "ink", "left ramp a>16..255", "right ramp a>16..255", "tone"))
    for d in range(0, args.rows + 1, args.every):
        y = last - d
        if y < 0:
            break
        row = alpha[y]
        idx = np.where(row > 16)[0]
        if len(idx) == 0:
            print("%6d %7.1f %6.1f %6d  (empty)" % (y, box_bottom - y, (box_bottom - y) / ppu * 1000, 0))
            continue
        lo, hi = int(idx[0]), int(idx[-1])
        L = [int(row[x]) for x in range(lo, min(lo + 16, hi + 1))]
        Rr = [int(row[x]) for x in range(hi, max(hi - 16, lo - 1), -1)]
        tone = lum[y, lo:hi + 1][alpha[y, lo:hi + 1] > 200]
        tm = tone.mean() if tone.size else -1
        print("%6d %7.1f %6.1f %6d  %-24s %-24s %.0f" % (
            y, box_bottom - y, (box_bottom - y) / ppu * 1000, len(idx),
            ",".join(str(v) for v in L), ",".join(str(v) for v in Rr), tm))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
