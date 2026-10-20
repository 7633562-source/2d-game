# -*- coding: utf-8 -*-
"""B-BEND-3: three candidate phase-7 cut crops (two panels each: seated | unloaded),
from the STATIC twin bBEND1v_tree (f120 seated, f800 unloaded - the pixel-identical controls).
Each: identical crop in both panels, red dashed guide at the same source y.
For my own inspection first (choose the one that excludes the seam/wedge), then the eyes.
"""
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
Z = 10


def load(i):
    return Image.open(f"{RUNS}\\shot_bBEND1v_tree_{i}.png").convert("RGB")


def panel(a, box, guide_y, label):
    x0, y0, x1, y1 = box
    im = a.crop(box).resize(((x1 - x0) * Z, (y1 - y0) * Z), Image.NEAREST)
    d = ImageDraw.Draw(im)
    Y = (guide_y - y0) * Z
    for X in range(0, im.width, 24):
        d.line([(X, Y), (min(X + 12, im.width), Y)], fill=(255, 40, 40), width=2)
    # 10-px ticks along the top and left edges
    for gx in range(x0 - x0 % 10 + 10, x1, 10):
        Xx = (gx - x0) * Z
        d.line([(Xx, 0), (Xx, 8)], fill=(255, 255, 255), width=1)
    band = 22
    out = Image.new("RGB", (im.width, im.height + band), (10, 10, 10))
    out.paste(im, (0, band))
    ImageDraw.Draw(out).text((6, 5), label, fill=(255, 230, 80))
    return out


def build(name, box, guide):
    A = load(120)
    C = load(800)
    p1 = panel(A, box, guide, "SEATED (bird on the branch)")
    p2 = panel(C, box, guide, "UNLOADED (bird gone)")
    gap = 12
    out = Image.new("RGB", (p1.width * 2 + gap, p1.height), (0, 0, 0))
    out.paste(p1, (0, 0))
    out.paste(p2, (p1.width + gap, 0))
    p = RUNS + rf"\reviews\{name}.png"
    out.save(p)
    print("saved", p, out.size)


build("bBEND3_candA", (894, 584, 954, 668), 601)
build("bBEND3_candB", (912, 584, 966, 676), 601)
build("bBEND3_candC", (920, 586, 968, 682), 599)
build("bBEND3_candD", (925, 588, 970, 704), 601)
