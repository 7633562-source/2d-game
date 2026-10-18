# -*- coding: utf-8 -*-
"""B-PERCH-2 fine map (1px) of the foot/wood contact window.

Symbols: '~' sky, '*' leaf (green), 'w' wood (brown), 'b' bird steel-blue, 'c' light (claw/beak),
'#' dark, '.' other. Window is given on the command line.
Usage: python Tools/_pch2-fine.py <frame> <x0,y0,x1,y1> [take]
"""
import sys
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[3] if len(sys.argv) > 3 else "bPCH1m2v_tree"
frame = int(sys.argv[1])
x0, y0, x1, y1 = [int(v) for v in sys.argv[2].split(",")]
a = np.asarray(Image.open(f"{RUNS}\\shot_{TAKE}_{frame}.png").convert("RGB")).astype(int)[y0:y1, x0:x1]
r, g, b = a[..., 0], a[..., 1], a[..., 2]
c = np.full(r.shape, ".", dtype="<U1")
c[(np.abs(a - np.array([140, 199, 240])).sum(axis=2) < 45)] = "~"
c[(g > r + 5) & (g > b + 5)] = "*"
c[(r > g + 15) & (g >= b) & (r > 50)] = "w"
c[(b > r + 15) & (b > 90) & (b < 225)] = "b"
c[(r > 150) & (g > 140) & (b > 120) & (np.abs(r - g) < 45) & (np.abs(g - b) < 60)] = "c"
c[(r + g + b) < 200] = "#"
print(f"{TAKE} f{frame} t={frame/20:.2f}s  window x{x0}..{x1} y{y0}..{y1}")
print("     " + "".join(str((x0 + k) // 10 % 10) if (x0 + k) % 10 == 0 else " " for k in range(x1 - x0)))
for j in range(y1 - y0):
    print("%4d " % (y0 + j) + "".join(c[j]))
