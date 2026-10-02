# -*- coding: utf-8 -*-
"""B-PERCH-16: the seated bird's own pixels by differencing against a NO-BIRD frame with
the SAME camera (verified by the trunk region), plus the wood's top silhouette on the
no-bird frame and its difference against the loaded frame (the bend).
Usage: python Tools/_pch16-birdmask.py <take> <fSeated> <fNoBird> [x0 x1 y0 y1] [thr]
"""
import sys
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
FB = int(sys.argv[2])
FN = int(sys.argv[3])
X0 = int(sys.argv[4]) if len(sys.argv) > 4 else 830
X1 = int(sys.argv[5]) if len(sys.argv) > 5 else 890
Y0 = int(sys.argv[6]) if len(sys.argv) > 6 else 240
Y1 = int(sys.argv[7]) if len(sys.argv) > 7 else 340
THR = int(sys.argv[8]) if len(sys.argv) > 8 else 24


def path(f):
    return RUNS + r"\shot_%s_%s.png" % (TAKE, ("%02d" % f) if f < 100 else str(f))


def L(f):
    return np.asarray(Image.open(path(f)).convert("RGB")).astype(int)


a_all, n_all = L(FB), L(FN)
# camera check on a wood-only region
reg = (slice(450, 650), slice(1000, 1120))
print("camera check f%d vs f%d : max|diff| = %d" % (FB, FN, int(np.abs(a_all[reg] - n_all[reg]).sum(axis=2).max())))
A = a_all[Y0:Y1, X0:X1]
N = n_all[Y0:Y1, X0:X1]
diff = np.abs(A - N).sum(axis=2) >= THR
r, g, b = N[..., 0], N[..., 1], N[..., 2]
mx = np.maximum(np.maximum(r, g), b)
sky = (np.abs(r - 140) < 22) & (np.abs(g - 199) < 24) & (np.abs(b - 240) < 24)
wood = (r > g + 12) & (g >= b) & (r > 60) & ~sky
sil = {}
for x in range(wood.shape[1]):
    for y in range(wood.shape[0] - 2):
        if wood[y, x] and wood[y + 1, x] and wood[y + 2, x]:
            sil[x] = y + Y0
            break
print("take=%s  seated f%d / no-bird f%d   changed px=%d" % (TAKE, FB, FN, int(diff.sum())))
print("     " + "".join(str(x // 10 % 10) for x in range(X0, X1, 2)))
print("     (B=bird-changed pixel, #=wood top silhouette on the NO-BIRD frame)")
for j in range(0, Y1 - Y0, 2):
    row = ""
    for i in range(0, X1 - X0, 2):
        ch = "."
        if diff[j:j + 2, i:i + 2].any():
            ch = "B"
        x = i + X0
        if x in sil and sil[x] - Y0 in (j, j + 1):
            ch = "#" if ch == "." else "$"
        row += ch
    print("%4d %s" % (Y0 + j, row))
# the silhouette under the bird's own columns
cols = [x for x in range(X0, X1) if diff[:, x - X0].any()]
if cols:
    print("bird columns %d..%d ; wood top silhouette at those columns:" % (min(cols), max(cols)))
    print("  " + " ".join("%d:%s" % (x, (sil.get(x, 0) if x in sil else "-")) for x in cols))
