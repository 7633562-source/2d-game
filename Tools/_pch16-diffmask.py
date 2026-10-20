# -*- coding: utf-8 -*-
"""B-PERCH-16: the seated bird's own pixels by frame differencing (the camera is static
through the landing + hold: verified). diff(frame_noBird, frame_seated) marks the bird;
diff(seated, seated+dt) marks the wind-swayed background. Prints both masks.
Usage: python Tools/_pch16-diffmask.py <take> <fBird> [fNoBird] [x0 x1 y0 y1] [thr]
"""
import sys
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
FB = int(sys.argv[2])
FN = int(sys.argv[3]) if len(sys.argv) > 3 else 240
X0 = int(sys.argv[4]) if len(sys.argv) > 4 else 810
X1 = int(sys.argv[5]) if len(sys.argv) > 5 else 900
Y0 = int(sys.argv[6]) if len(sys.argv) > 6 else 240
Y1 = int(sys.argv[7]) if len(sys.argv) > 7 else 340
THR = int(sys.argv[8]) if len(sys.argv) > 8 else 24


def L(f):
    return np.asarray(Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, f)).convert("RGB")).astype(int)[Y0:Y1, X0:X1]


A, B, C = L(FN), L(FB), L(FB + 25)
m1 = np.abs(A - B).sum(axis=2) >= THR      # bird (vs no-bird)
m2 = np.abs(C - B).sum(axis=2) >= THR      # background motion (wind) at the seated pose
print("take=%s  bird-diff f%d vs f%d : %d px ; motion f%d vs f%d : %d px" % (TAKE, FB, FN, int(m1.sum()), FB + 25, FB, int(m2.sum())))
print("     " + "".join(str(x // 10 % 10) for x in range(X0, X1, 2)))
for j in range(0, Y1 - Y0, 2):
    row = ""
    for i in range(0, X1 - X0, 2):
        b1 = m1[j:j + 2, i:i + 2].any()
        b2 = m2[j:j + 2, i:i + 2].any()
        row += ("X" if (b1 and b2) else ("B" if b1 else ("m" if b2 else ".")))
    print("%4d %s" % (Y0 + j, row))
