# -*- coding: utf-8 -*-
"""B-PERCH-16: the seated bird's own pixels, 1:1 ASCII class map.
K=black(claw/eye, max<95)  B=bird body (blue-grey: b>r+8 and 60<max<230)  W=wood brown
 (r>g+12, g>=b, 60<r<210)  L=leaf (g>r+10, g>b+10)  .=sky/fill  +=other
Usage: python Tools/_pch16-map.py <take> <frame> [x0 x1 y0 y1]
"""
import sys
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
F = int(sys.argv[2])
X0 = int(sys.argv[3]) if len(sys.argv) > 3 else 825
X1 = int(sys.argv[4]) if len(sys.argv) > 4 else 885
Y0 = int(sys.argv[5]) if len(sys.argv) > 5 else 265
Y1 = int(sys.argv[6]) if len(sys.argv) > 6 else 335

a = np.asarray(Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, F)).convert("RGB")).astype(int)[Y0:Y1, X0:X1]
r, g, b = a[..., 0], a[..., 1], a[..., 2]
mx = np.maximum(np.maximum(r, g), b)
sky = (np.abs(r - 140) < 20) & (np.abs(g - 199) < 22) & (np.abs(b - 240) < 22)
K = (mx < 95) & ~sky
W = (r > g + 12) & (g >= b) & (r > 60) & (r < 215)
B = (b > r + 8) & (mx >= 60) & ~sky & ~W
L = (g > r + 10) & (g > b + 10) & ~sky
ch = np.where(K, "K", np.where(W, "W", np.where(B, "B", np.where(L, "L", np.where(sky, ".", "+")))))
print("take=%s f=%d  x %d..%d  y %d..%d" % (TAKE, F, X0, X1, Y0, Y1))
print("     " + "".join(str(x // 100 % 10) for x in range(X0, X1)))
print("     " + "".join(str(x // 10 % 10) for x in range(X0, X1)))
print("     " + "".join(str(x % 10) for x in range(X0, X1)))
for j in range(Y1 - Y0):
    print("%4d %s" % (Y0 + j, "".join(ch[j])))
