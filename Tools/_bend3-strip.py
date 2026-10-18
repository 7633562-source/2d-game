# -*- coding: utf-8 -*-
"""B-BEND-3 phase-7 strip: five moments of the SAME branch in the SAME crop (static twin
bBEND1v_tree, one camera), x 927-972 y 592-700 at 7x; red dashed guide at source y=608
(the rest height of the wood's top edge). Crop x>=927 excludes the plant's rest-pose joint
seam (which reaches x=926 only) and the fork's inner wedge (x>=985).
Frames: f70 t=3.5 (bird away) | f110 t=5.5 (just landed) | f140 t=7.0 (seated) |
        f180 t=9.0 (seated) | f500 t=25.0 (long after takeoff).
"""
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (927, 592, 972, 700)
GUIDE = 608
Z = 7
FRAMES = [
    (70, "m1: bird away"),
    (110, "m2: just touched down"),
    (140, "m3: bird seated"),
    (180, "m4: still seated"),
    (500, "m5: long after takeoff"),
]


def panel(i, label):
    a = Image.open(f"{RUNS}\\shot_bBEND1v_tree_{i}.png").convert("RGB")
    x0, y0, x1, y1 = BOX
    im = a.crop(BOX).resize(((x1 - x0) * Z, (y1 - y0) * Z), Image.NEAREST)
    d = ImageDraw.Draw(im)
    Y = (GUIDE - y0) * Z
    for X in range(8, im.width - 8, 22):
        d.line([(X, Y), (X + 11, Y)], fill=(255, 30, 30), width=2)
    for gy in range(y0 - y0 % 20 + 20, y1, 20):
        Yy = (gy - y0) * Z
        d.line([(0, Yy), (6, Yy)], fill=(255, 255, 255), width=1)
        d.text((8, Yy + 2), str(gy), fill=(255, 0, 255))
    band = 22
    out = Image.new("RGB", (im.width, im.height + band), (12, 12, 12))
    out.paste(im, (0, band))
    ImageDraw.Draw(out).text((5, 5), label, fill=(255, 230, 80))
    return out


panels = [panel(i, lb) for i, lb in FRAMES]
gap = 8
out = Image.new("RGB", (len(panels) * panels[0].width + (len(panels) - 1) * gap, panels[0].height), (0, 0, 0))
for k, p in enumerate(panels):
    out.paste(p, (k * (p.width + gap), 0))
p = RUNS + r"\reviews\bBEND3_bend_strip5.png"
out.save(p)
print("saved", p, out.size, "| top edge x928:", [611, 618, 641, 632, 611])
