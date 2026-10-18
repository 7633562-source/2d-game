# -*- coding: utf-8 -*-
"""B-PERCH-2 text map: perch region of the static twin bPCH1m2v_tree.

Symbols: '~' sky, 'w' wood (brown), '*' leaves (green), '#' dark (bird/bark), '.' other.
Region x 866-1014, y 551-735, 4x4 blocks -> 37 cols x 46 rows per panel.
Usage: python Tools/_pch2-map.py <frame> [<frame> ...]
"""
import sys
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = "bPCH1m2v_tree"
X0, X1 = 866, 1014
Y0, Y1 = 551, 735
B = 4
SKY = np.array([140, 199, 240])


def load(i):
    return np.asarray(Image.open(f"{RUNS}\\shot_{TAKE}_{i}.png").convert("RGB")).astype(int)


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
    print(f"== {name} (f{i}) x{X0}..{X1} y{Y0}..{Y1}, {B}px blocks")
    hdr = "    " + "".join(str((X0 + B * k) // 100 % 10) if (X0 + B * k) % 20 == 2 else " " for k in range(cols))
    hdr2 = "    " + "".join(str((X0 + B * k) // 10 % 10) if (X0 + B * k) % 20 == 2 else " " for k in range(cols))
    print(hdr)
    print(hdr2)
    for r in range(rows):
        line = []
        for cc in range(cols):
            blk = c[r * B:(r + 1) * B, cc * B:(cc + 1) * B].ravel()
            vals, counts = np.unique(blk, return_counts=True)
            line.append(vals[counts.argmax()])
        print(f"{Y0 + r * B:4d}" + "".join(line))


for i in [int(x) for x in sys.argv[1:]]:
    panel(i, f"t={i/20:.1f}s")
