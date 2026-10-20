# -*- coding: utf-8 -*-
"""B-PERCH-2 lift A/B: seat -0.075 (default) vs seat -0.037 (lift), same lens, same crop.

Panels: left = bPCH1m2v_tree f260 (default, claws 603-609), right = bPCH2l_tree f262 (lift,
claws 597-603). Red dashed guide at the wood top edge (y=602) in both. Camera identity is
checked first on a static background patch (trunk below the fork).
Usage: python Tools/_pch2-liftab.py
"""
import numpy as np
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
BOX = (900, 580, 950, 625)
Z = 12
GUIDE = 602


def load(take, i):
    return np.asarray(Image.open(f"{RUNS}\\shot_{take}_{i}.png").convert("RGB")).astype(int)


A = load("bPCH1m2v_tree", 260)
B = load("bPCH2l_tree", 262)
bg = np.abs(A[700:900, 900:1100] - B[700:900, 900:1100]).max()
print("camera identity check (trunk patch max diff):", int(bg))


def panel(a, label, sub):
    x0, y0, x1, y1 = BOX
    im = Image.fromarray(a[y0:y1, x0:x1].astype("uint8")).resize(
        ((x1 - x0) * Z, (y1 - y0) * Z), Image.NEAREST)
    d = ImageDraw.Draw(im)
    Y = (GUIDE - y0) * Z
    for X in range(8, im.width - 8, 22):
        d.line([(X, Y), (X + 11, Y)], fill=(255, 30, 30), width=2)
    for gx in range(x0 - x0 % 10 + 10, x1, 10):
        X = (gx - x0) * Z
        d.line([(X, 0), (X, 6)], fill=(255, 255, 255), width=1)
        if gx % 20 == 0:
            d.text((X + 2, 7), str(gx), fill=(255, 0, 255))
    for gy in range(y0 - y0 % 20 + 20, y1, 20):
        Yy = (gy - y0) * Z
        d.line([(0, Yy), (7, Yy)], fill=(255, 255, 255), width=1)
        d.text((9, Yy + 2), str(gy), fill=(0, 255, 255))
    band = 40
    out = Image.new("RGB", (im.width, im.height + band), (12, 12, 12))
    out.paste(im, (0, band))
    dd = ImageDraw.Draw(out)
    dd.text((6, 6), label, fill=(255, 232, 90))
    dd.text((6, 22), sub, fill=(255, 190, 120))
    return out


PA = panel(A, "A: seat -0.075 (current code default)", "claws 603-609; wood top edge 602 (red)")
PB = panel(B, "B: seat -0.037 (lift +6 px)", "claws 597-603; wood top edge 602 (red)")
gap = 14
out = Image.new("RGB", (PA.width + gap + PB.width, PA.height), (0, 0, 0))
out.paste(PA, (0, 0))
out.paste(PB, (PA.width + gap, 0))
p = REV + r"\bPCH2_lift_AB.png"
out.save(p)
print("saved", p, out.size)
