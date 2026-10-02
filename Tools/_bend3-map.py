# -*- coding: utf-8 -*-
"""B-BEND-3: a labelled, gridded two-panel map of the perch region (self-view, not a cut for eyes).

Crop x 850-1040, y 555-735 at 5x, both states (seated f120 | unloaded f800), a 10-src-px grid
with x/y labels so the exact geometry (limb, fork, wedge, seam) can be read off the pixels.
"""
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (850, 555, 1040, 735)
Z = 5
GRID = 10


def load(i):
    return np.asarray(Image.open(f"{RUNS}\\shot_bBEND1v_tree_{i}.png").convert("RGB")).astype("uint8")


def panel(a, label):
    c = a[BOX[1]:BOX[3], BOX[0]:BOX[2]]
    im = Image.fromarray(c).resize(((BOX[2] - BOX[0]) * Z, (BOX[3] - BOX[1]) * Z), Image.NEAREST)
    d = ImageDraw.Draw(im)
    x0, y0 = BOX[0], BOX[1]
    for gx in range(x0 - x0 % GRID + GRID, BOX[2], GRID):
        X = (gx - x0) * Z
        d.line([(X, 0), (X, im.height)], fill=(255, 255, 255), width=1)
        if gx % 50 == 0:
            d.text((X + 2, 2), str(gx), fill=(255, 0, 255))
    for gy in range(y0 - y0 % GRID + GRID, BOX[3], GRID):
        Y = (gy - y0) * Z
        d.line([(0, Y), (im.width, Y)], fill=(255, 255, 255), width=1)
        if gy % 50 == 0:
            d.text((2, Y + 2), str(gy), fill=(255, 0, 255))
    band = 26
    out = Image.new("RGB", (im.width, im.height + band), (10, 10, 10))
    out.paste(im, (0, band))
    ImageDraw.Draw(out).text((6, 6), label, fill=(255, 230, 80))
    return out


A = panel(load(120), "SEATED f120 (t=6.0s)")
C = panel(load(800), "UNLOADED f800 (t=40s)")
gap = 10
out = Image.new("RGB", (A.width * 2 + gap, A.height), (0, 0, 0))
out.paste(A, (0, 0))
out.paste(C, (A.width + gap, 0))
p = RUNS + r"\reviews\bBEND3_map_mine.png"
out.save(p)
print("saved", p, out.size)
