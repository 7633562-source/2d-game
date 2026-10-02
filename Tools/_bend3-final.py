# -*- coding: utf-8 -*-
"""B-BEND-3 final phase-7 cut: crop x 927-972 (fully right of the plant seam, which reaches
x=926 only), y 592-700, 10x, red dashed guide at source y=601 (same in both panels).
Panels: [SEATED f120 | UNLOADED f800] on the static twin bBEND1v_tree. No source edit.
"""
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (927, 592, 972, 700)
GUIDE = 608
Z = 10


def load(i):
    return Image.open(f"{RUNS}\\shot_bBEND1v_tree_{i}.png").convert("RGB")


def panel(a, label):
    x0, y0, x1, y1 = BOX
    im = a.crop(BOX).resize(((x1 - x0) * Z, (y1 - y0) * Z), Image.NEAREST)
    d = ImageDraw.Draw(im)
    Y = (GUIDE - y0) * Z
    for X in range(10, im.width - 10, 26):
        d.line([(X, Y), (X + 13, Y)], fill=(255, 30, 30), width=2)
    for gx in range(x0 - x0 % 10 + 10, x1, 10):
        Xx = (gx - x0) * Z
        d.line([(Xx, 0), (Xx, 7)], fill=(255, 255, 255), width=1)
        if gx % 20 == 0:
            d.text((Xx + 2, 8), str(gx), fill=(255, 0, 255))
    for gy in range(y0 - y0 % 20 + 20, y1, 20):
        Yy = (gy - y0) * Z
        d.line([(0, Yy), (7, Yy)], fill=(255, 255, 255), width=1)
        d.text((9, Yy + 2), str(gy), fill=(255, 0, 255))
    band = 24
    out = Image.new("RGB", (im.width, im.height + band), (12, 12, 12))
    out.paste(im, (0, band))
    ImageDraw.Draw(out).text((6, 6), label, fill=(255, 230, 80))
    return out


A = panel(load(120), "LEFT: branch AS THE BIRD SITS ON IT (loaded, bird just left of frame)")
C = panel(load(800), "RIGHT: same branch, bird gone for a while (unloaded, rested)")
gap = 14
out = Image.new("RGB", (A.width * 2 + gap, A.height), (0, 0, 0))
out.paste(A, (0, 0))
out.paste(C, (A.width + gap, 0))
p = RUNS + r"\reviews\bBEND3_bend_load_pair.png"
out.save(p)
print("saved", p, out.size)
