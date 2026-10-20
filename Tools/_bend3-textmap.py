# -*- coding: utf-8 -*-
"""B-BEND-3: definitive text map of the perch region, both states, 4x4 blocks.

Symbols: '~' sky, 'w' wood (brown), '*' leaves (green), '#' dark (bird/bark), '.' other.
Region x 866-1014, y 551-735 -> 37 cols x 46 rows per panel.
"""
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
X0, X1 = 866, 1014
Y0, Y1 = 551, 735
B = 4
SKY = np.array([140, 199, 240])


def load(i):
    return np.asarray(Image.open(f"{RUNS}\\shot_bBEND1v_tree_{i}.png").convert("RGB")).astype(int)


def classes(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    out = np.full(r.shape, ".", dtype="<U1")
    out[(np.abs(a - SKY).sum(axis=2) < 45)] = "~"
    out[(g > r + 5) & (g > b + 5)] = "*"
    out[(r > g + 15) & (g >= b) & (r > 50)] = "w"
    out[(r + g + b) < 260] = "#"
    return out


def panel(i, name):
    a = load(i)[Y0:Y1, X0:X1]
    c = classes(a)
    rows = (Y1 - Y0) // B
    cols = (X1 - X0) // B
    print(f"== {name} (f{i}) x{X0}..{X1} y{Y0}..{Y1}, {B}px blocks; header x = X0 + 4*col")
    hdr = "    " + "".join(str((X0 + B * k) // 100 % 10) if (X0 + B * k) % 20 == 2 else " " for k in range(cols))
    hdr2 = "    " + "".join(str((X0 + B * k) // 10 % 10) if (X0 + B * k) % 20 == 2 else " " for k in range(cols))
    print(hdr); print(hdr2)
    for r in range(rows):
        line = []
        for cc in range(cols):
            blk = c[r * B:(r + 1) * B, cc * B:(cc + 1) * B].ravel()
            vals, counts = np.unique(blk, return_counts=True)
            line.append(vals[counts.argmax()])
        print(f"{Y0 + r * B:4d}" + "".join(line))


panel(120, "SEATED")
panel(800, "UNLOADED")
