# -*- coding: utf-8 -*-
"""B-BEND-3: wide anatomy view of the tree region (self-view). x 620-1260, y 260-960 at 2x,
seated + unloaded stacked, so the limb / fork / twig geometry is identifiable.
"""
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (620, 260, 1260, 960)
Z = 2


def load(i):
    return Image.open(f"{RUNS}\\shot_bBEND1v_tree_{i}.png").convert("RGB")


def panel(i, label):
    im = load(i).crop(BOX)
    im = im.resize(((BOX[2] - BOX[0]) * Z, (BOX[3] - BOX[1]) * Z), Image.NEAREST)
    band = 24
    out = Image.new("RGB", (im.width, im.height + band), (10, 10, 10))
    out.paste(im, (0, band))
    d = ImageDraw.Draw(out)
    d.text((6, 6), label, fill=(255, 230, 80))
    # 50-px grid
    for gx in range(BOX[0] - BOX[0] % 50 + 50, BOX[2], 50):
        X = (gx - BOX[0]) * Z
        d.line([(X, band), (X, out.height)], fill=(120, 120, 120), width=1)
        d.text((X + 2, band + 2), str(gx), fill=(255, 0, 255))
    for gy in range(BOX[1] - BOX[1] % 50 + 50, BOX[3], 50):
        Y = (gy - BOX[1]) * Z + band
        d.line([(0, Y), (out.width, Y)], fill=(120, 120, 120), width=1)
        d.text((2, Y + 2), str(gy), fill=(255, 0, 255))
    return out


A = panel(120, "SEATED f120 t=6.0s")
C = panel(800, "UNLOADED f800 t=40s")
out = Image.new("RGB", (A.width, A.height + C.height + 8), (0, 0, 0))
out.paste(A, (0, 0))
out.paste(C, (0, A.height + 8))
p = RUNS + r"\reviews\bBEND3_wide_mine.png"
out.save(p)
print("saved", p, out.size)
