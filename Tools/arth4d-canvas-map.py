#!/usr/bin/env python3
"""ART-H4d probe 2: the thigh canvas' knee end as a coarse ALPHA map (2 px per char).

Rows walk from the canvas' last ink row upward; the map shows alpha as digits (0 none .. 9
opaque, ' ' = 0) so the terminal fade/silhouette can be read as pixels.  Also marks the box
bottom edge row and the disc edge row (box edge + R_eye*ppu).

Usage: python Tools/arth4d-canvas-map.py [--part thigh] [--rows 120] [--step 3]
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


def meta_ppu(meta):
    return float(re.search(r"spritePixelsToUnits: ([0-9.]+)", meta).group(1))


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
    ap.add_argument("--rows", type=int, default=120)
    ap.add_argument("--step", type=int, default=3)
    ap.add_argument("--field", default="thighSize")
    args = ap.parse_args()

    path = os.path.join(ART, args.part + ".png")
    a = np.asarray(Image.open(path).convert("RGBA")).astype(np.float64)
    meta = open(path + ".meta", encoding="utf-8", errors="replace").read()
    ppu = meta_ppu(meta)
    pivx, pivy = meta_pivot(meta)
    h, w = a.shape[:2]
    bw, bh = box_size(args.field)
    box_cy = (1.0 - pivy) * h
    box_bottom = box_cy + bh * ppu / 2.0
    plan = json.load(open(PLAN, encoding="utf-8"))
    r_eye = None
    for j in plan.get("joints", []):
        if j.get("parent") == args.part and j.get("parent_end") == "bottom":
            r_eye = float(j["r_eye"])
    print("%s %dx%d ppu=%.0f; box bottom edge row %.1f; disc edge row %.1f (R_eye %.1f px)"
          % (args.part, w, h, ppu, box_bottom, box_bottom + (r_eye or 0) * ppu, (r_eye or 0) * ppu))
    alpha = a[:, :, 3]
    ink = alpha > 16
    rows = np.where(ink.any(axis=1))[0]
    last = int(rows[-1])
    print("last ink row %d (%.1f px below the box edge)".replace("%.1f", "%.1f") % (last, last - box_bottom))
    print("legend: digit = alpha/25.6 (rounded): 0=0..25 .. 9=230..255, ' ' = alpha<=16")
    half = w // 2
    hdr = "row   px>box  " + "".join(str((x // 10) % 10) if x % 20 == 0 else " " for x in range(0, w, 2))
    print(hdr)
    for y in range(last, max(-1, last - args.rows), -args.step):
        line = []
        for x0 in range(0, w, 2):
            block = alpha[y, x0:x0 + 2]
            v = int(block.max())
            if v <= 16:
                line.append(" ")
            else:
                line.append(str(int(np.clip(v / 25.6, 0, 9))))
        mark = ""
        if abs(y - (box_bottom + r_eye * ppu)) < args.step:
            mark = " <- disc edge"
        if abs(y - box_bottom) < args.step:
            mark = " <- BOX EDGE"
        print("%4d %8.1f  %s%s" % (y, y - box_bottom, "".join(line), mark))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
