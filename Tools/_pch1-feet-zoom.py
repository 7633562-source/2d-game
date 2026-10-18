# -*- coding: utf-8 -*-
"""B-PERCH-1 recon: 18x zoom of the feet strip (seated f120), x 896-930, y 584-616, to see
exactly what the bird's feet touch: wood / leaves / sky, row by row."""
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (896, 584, 930, 616)
Z = 18
a = Image.open(f"{RUNS}\\shot_bBEND1v_tree_120.png").convert("RGB")
im = a.crop(BOX).resize(((BOX[2] - BOX[0]) * Z, (BOX[3] - BOX[1]) * Z), Image.NEAREST)
d = ImageDraw.Draw(im)
for gx in range(900, BOX[2], 10):
    X = (gx - BOX[0]) * Z
    d.line([(X, 0), (X, im.height)], fill=(255, 255, 255), width=1)
    d.text((X + 3, 4), str(gx), fill=(255, 0, 255))
for gy in range(590, BOX[3], 5):
    Y = (gy - BOX[1]) * Z
    d.line([(0, Y), (im.width, Y)], fill=(255, 255, 255), width=1)
    d.text((3, Y + 3), str(gy), fill=(0, 255, 255))
p = RUNS + r"\reviews\bPCH1_feet_seated_18x_mine.png"
im.save(p)
print("saved", p, im.size)
