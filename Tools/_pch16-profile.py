# -*- coding: utf-8 -*-
"""B-PERCH-16 (bird lane, 26.09): THE FEET'S OWN PIXELS - per-column profile around the
seated bird. For each column x in the window: the rows of the CLAW/DARK blob (the bird's
feet sprite), the first WOOD row (brown) below the window top, and the first SKY row.
Usage: python Tools/_pch16-profile.py <take> <frame> [x0 x1 y0 y1]
"""
import sys
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
FRAME = int(sys.argv[2])
X0 = int(sys.argv[3]) if len(sys.argv) > 3 else 820
X1 = int(sys.argv[4]) if len(sys.argv) > 4 else 900
Y0 = int(sys.argv[5]) if len(sys.argv) > 5 else 240
Y1 = int(sys.argv[6]) if len(sys.argv) > 6 else 360

a = np.asarray(Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, FRAME)).convert("RGB")).astype(int)
w = a[Y0:Y1, X0:X1]
r, g, b = w[..., 0], w[..., 1], w[..., 2]
sky = (np.abs(r - 140) < 18) & (np.abs(g - 199) < 18) & (np.abs(b - 240) < 18)
wood = (r > g + 15) & (g >= b) & (r > 50)
dark = (np.maximum(np.maximum(r, g), b) < 90) & ~wood
leaf = (g > r + 8) & (g > b + 8)

print("take=%s frame=%d window x%d-%d y%d-%d" % (TAKE, FRAME, X0, X1, Y0, Y1))
print("col  dark_top dark_bot  wood_top  sky_top  cls")
for x in range(X0, X1, 4):
    col = x - X0
    dc = np.where(dark[:, col])[0]
    wd = np.where(wood[:, col])[0]
    sk = np.where(sky[:, col])[0]
    if len(dc):
        dt, db = dc.min() + Y0, dc.max() + Y0
    else:
        dt = db = None
    wt = (wd.min() + Y0) if len(wd) else None
    st = (sk.min() + Y0) if len(sk) else None
    seq = "".join(("D" if dark[j, col] else ("W" if wood[j, col] else ("L" if leaf[j, col] else ("." if sky[j, col] else "+")))) for j in range(0, Y1 - Y0, 3))
    print("%4d %8s %8s %8s %8s  %s" % (x, dt, db, wt, st, seq))
