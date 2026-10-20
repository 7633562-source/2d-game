# -*- coding: utf-8 -*-
"""B-PERCH-3 owner A/B (bird lane, 25.09): the owner-suite seat before vs after the adopted
lift. Panel A = bPCH1d_tree (-0.075, the previous code default), panel B = bPCH3d_tree (-0.037,
the adopted default). Same camAuto scene, same frame f268, same crop.
Usage: python Tools/_pch3-owner-ab.py
"""
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
BOX = (740, 140, 960, 320)
ZOOM = 3


def panel(take, label):
    im = Image.open(f"{RUNS}\\shot_{take}_268.png").convert("RGB").crop(BOX)
    im = im.resize((im.width * ZOOM, im.height * ZOOM), Image.NEAREST)
    band = 26
    out = Image.new("RGB", (im.width, im.height + band), (12, 12, 12))
    out.paste(im, (0, band))
    ImageDraw.Draw(out).text((6, 7), label, fill=(255, 232, 90))
    return out


a = panel("bPCH1d_tree", "A: -0.075 (old default)")
b = panel("bPCH3d_tree", "B: -0.037 (adopted)")
w = max(a.width, b.width)
out = Image.new("RGB", (w * 2 + 10, a.height), (0, 0, 0))
out.paste(a, (0, 0))
out.paste(b, (w + 10, 0))
p = REV + r"\bPCH3_owner_AB.png"
out.save(p)
print("saved", p, out.size)
