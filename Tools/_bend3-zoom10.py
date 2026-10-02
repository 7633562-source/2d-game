# -*- coding: utf-8 -*-
"""B-BEND-3: two separate zoomed views of the perch wood region for my own inspection.
Panel 1: UNLOADED (f800) crop x 896-962, y 576-688 at 10x.
Panel 2: SEATED  (f120) same crop at 10x.
Saved as two files (so each can be viewed at full resolution separately).
"""
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (896, 576, 962, 688)
Z = 10


def load(i):
    return Image.open(f"{RUNS}\\shot_bBEND1v_tree_{i}.png").convert("RGB")


for i, name in ((800, "unloaded"), (120, "seated")):
    im = load(i).crop(BOX)
    im = im.resize(((BOX[2] - BOX[0]) * Z, (BOX[3] - BOX[1]) * Z), Image.NEAREST)
    d = ImageDraw.Draw(im)
    for gx in range(900, BOX[2], 10):
        X = (gx - BOX[0]) * Z
        d.line([(X, 0), (X, im.height)], fill=(255, 255, 255), width=1)
        d.text((X + 3, 4), str(gx), fill=(255, 0, 255))
    for gy in range(580, BOX[3], 10):
        Y = (gy - BOX[1]) * Z
        d.line([(0, Y), (im.width, Y)], fill=(255, 255, 255), width=1)
        d.text((3, Y + 3), str(gy), fill=(255, 0, 255))
    p = RUNS + rf"\reviews\bBEND3_zoom10_{name}_mine.png"
    im.save(p)
    print("saved", p, im.size)
