# -*- coding: utf-8 -*-
"""B-PERCH-1: A/B of the seat — PRE (bBEND1v_tree f120) vs POST seat offset -0.075
(bPCH1m1v_tree f360 t=18.0). Same crop x 880-975, y 552-700 at 7x, one camera."""
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (880, 975, 552, 700)  # x0,x1,y0,y1
Z = 7


def panel(path, label):
    a = Image.open(path).convert("RGB")
    im = a.crop((BOX[0], BOX[2], BOX[1], BOX[3])).resize(
        ((BOX[1] - BOX[0]) * Z, (BOX[3] - BOX[2]) * Z), Image.NEAREST)
    d = ImageDraw.Draw(im)
    for gx in range(900, BOX[1], 10):
        X = (gx - BOX[0]) * Z
        d.line([(X, 0), (X, 6)], fill=(255, 255, 255), width=1)
        if gx % 20 == 0:
            d.text((X + 2, 7), str(gx), fill=(255, 0, 255))
    for gy in range(560, BOX[3], 10):
        Y = (gy - BOX[2]) * Z
        d.line([(0, Y), (6, Y)], fill=(255, 255, 255), width=1)
        if gy % 20 == 0:
            d.text((8, Y + 2), str(gy), fill=(0, 255, 255))
    band = 22
    out = Image.new("RGB", (im.width, im.height + band), (12, 12, 12))
    out.paste(im, (0, band))
    ImageDraw.Draw(out).text((5, 5), label, fill=(255, 230, 80))
    return out


A = panel(f"{RUNS}\\shot_bBEND1v_tree_120.png", "PRE: seat 0.000 (bird floats on the leaf pads)")
B = panel(f"{RUNS}\\shot_bPCH1m1v_tree_360.png", "POST: seatDy -0.075 (feet lowered to the wood)")
gap = 12
out = Image.new("RGB", (A.width + gap + B.width, A.height), (0, 0, 0))
out.paste(A, (0, 0))
out.paste(B, (A.width + gap, 0))
p = RUNS + r"\reviews\bPCH1_seat_ab_mine.png"
out.save(p)
print("saved", p, out.size)
