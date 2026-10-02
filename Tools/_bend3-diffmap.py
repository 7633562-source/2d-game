# -*- coding: utf-8 -*-
"""B-BEND-3: movement map seated(f120) vs unloaded(f800), region x 860-1010, y 548-756, 2px blocks.
'.' identical, 'x' differs, 'B' both bird-dark (seated only bird?), 'S' sky in both.
"""
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
X0, X1, Y0, Y1, B = 860, 1010, 548, 756, 2
SKY = np.array([140, 199, 240])


def load(i):
    return np.asarray(Image.open(f"{RUNS}\\shot_bBEND1v_tree_{i}.png").convert("RGB")).astype(int)


A = load(120)
C = load(800)
d = (np.abs(A - C).sum(axis=2) > 12)

cols = (X1 - X0) // B
rows = (Y1 - Y0) // B
print(f"diff map x{X0}..{X1} y{Y0}..{Y1}, {B}px blocks  (x=changed, .=same)")
hdr = "    " + "".join(str((X0 + B * k) // 100 % 10) if (X0 + B * k) % 20 == 0 else " " for k in range(cols))
hdr2 = "    " + "".join(str((X0 + B * k) // 10 % 10) if (X0 + B * k) % 20 == 0 else " " for k in range(cols))
print(hdr); print(hdr2)
for r in range(rows):
    line = []
    for cc in range(cols):
        blk = d[Y0 + r * B:Y0 + (r + 1) * B, X0 + cc * B:X0 + (cc + 1) * B]
        line.append("x" if blk.any() else ".")
    print(f"{Y0 + r * B:4d}" + "".join(line))
