# -*- coding: utf-8 -*-
"""B-PERCH-2 probe 2: wood top edge + bird bbox around the seat (static twin bPCH1m2v_tree).

Wood test: brown = r>g>b, r-b>=30, r>=80.  Bird test: dark = all channels < 110.
Columns 920..965 (the load twig / perch area), rows 520..700.
Usage: python Tools/_pch2-wood.py
"""
import numpy as np
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = "bPCH1m2v_tree"
FRAMES = [100, 240, 250, 255, 258, 259, 260, 261, 262, 265, 270, 280, 300, 500, 700, 900]
X0, X1, Y0, Y1 = 900, 975, 500, 700


def load(i):
    return np.asarray(Image.open(f"{RUNS}\\shot_{TAKE}_{i}.png").convert("RGB")).astype(np.int16)


def wood_mask(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    return (r > g) & (g >= b) & ((r - b) >= 30) & (r >= 80)


def bird_mask(a):
    return (a < 110).all(axis=2)


for i in FRAMES:
    a = load(i)[Y0:Y1, X0:X1]
    w = wood_mask(a)
    bm = bird_mask(a)
    rows = []
    for x in (930, 940, 950, 960):
        col = np.where(w[:, x - X0])[0]
        rows.append(int(col.min()) + Y0 if len(col) else -1)
    ys, xs = np.where(bm)
    bb = (int(xs.min()) + X0, int(ys.min()) + Y0, int(xs.max()) + X0, int(ys.max()) + Y0) if len(xs) else None
    print(f"f{i:4d} t={i/20:5.2f}  woodtop@930/940/950/960 = {rows}   bird_bbox={bb}")
