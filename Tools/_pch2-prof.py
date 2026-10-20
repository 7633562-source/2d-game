# -*- coding: utf-8 -*-
"""B-PERCH-2 probe 3: wood-top column profile around slot 7 (static twin bPCH1m2v_tree).

Prints the topmost wood pixel y for every 2nd column 860..1000 in the seat frame (f260),
the pre frame (f240) and the unloaded frame (f700). '-' = no wood in 500..720.
Wood: r>g>b, r-b>=30, r>=80 ; also prints 'dark' (bird/trunk) topmost y per column.
Usage: python Tools/_pch2-prof.py <frame> [<frame> ...]
"""
import sys
import numpy as np
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = "bPCH1m2v_tree"


def load(i):
    return np.asarray(Image.open(f"{RUNS}\\shot_{TAKE}_{i}.png").convert("RGB")).astype(np.int16)


for i in [int(x) for x in sys.argv[1:]] or [240, 260, 700]:
    a = load(i)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    wood = (r > g) & (g >= b) & ((r - b) >= 30) & (r >= 80)
    dark = (a < 110).all(axis=2)
    out = []
    for x in range(860, 1001, 10):
        col = wood[500:720, x]
        ys = np.where(col)[0]
        wtop = int(ys.min()) + 500 if len(ys) else -1
        cd = dark[500:720, x]
        zs = np.where(cd)[0]
        dtop = int(zs.min()) + 500 if len(zs) else -1
        out.append(f"{x}:{wtop}/{dtop}")
    print(f"f{i} t={i/20:.2f}  wood_top/dark_top per column:")
    print("   " + "  ".join(out))
