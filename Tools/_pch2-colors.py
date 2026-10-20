# -*- coding: utf-8 -*-
"""B-PERCH-2 palette probe: top colours in the seat window (bird + perch) of the static twin.

Usage: python Tools/_pch2-colors.py <frame> <x0,y0,x1,y1>
"""
import sys
import numpy as np
from PIL import Image
from collections import Counter

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = "bPCH1m2v_tree"

frame = int(sys.argv[1])
x0, y0, x1, y1 = [int(v) for v in sys.argv[2].split(",")]
a = np.asarray(Image.open(f"{RUNS}\\shot_{TAKE}_{frame}.png").convert("RGB"))[y0:y1, x0:x1]
c = Counter(map(tuple, a.reshape(-1, 3).tolist()))
print(f"f{frame} window {x0},{y0}..{x1},{y1}  ({a.shape[0]}x{a.shape[1]} px)")
for col, n in c.most_common(14):
    print("  RGB%s n=%d" % (col, n))
