# -*- coding: utf-8 -*-
"""B-PERCH-6 (bird lane, 26.09): the seat measured on the PIXELS (scene diff).

Usage: python Tools/_pch6scene.py <take_label> [<ref_frame>] [<frames...>]

The camera is static in the owner take, so a frame from before the bird arrives is
the CLEAN scene. Every later frame is diffed against it: the changed pixels are the
bird (plus a little wind sway). For the bird's own columns the script reports, per
column band:
  bird_low  - the lowest bird pixel (the claw tips)
  wood_top  - the scene's first brown pixel going down from the sky (the branch edge
              in that column, i.e. the surface the claws must meet)
The claw tips standing ON the wood read as bird_low ~ wood_top at the same x.
"""
import sys
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1] if len(sys.argv) > 1 else "bPCH6b_tree"
REF = int(sys.argv[2]) if len(sys.argv) > 2 else 200
FRAMES = [int(x) for x in sys.argv[3:]] or [249, 251, 256, 260, 300, 360, 470]


def load(i):
    return np.asarray(Image.open(RUNS + "\\shot_%s_%02d.png" % (TAKE, i)).convert("RGB")).astype(int)


ref = load(REF)
r, g, b = ref[..., 0], ref[..., 1], ref[..., 2]
# brown wood: warmer than the sky and than the leaves
wood = (r > g + 12) & (r > 55) & (b < r - 10)
sky = ref[5, 5]
print("take=%s ref=f%d sky=%s wood_px=%d" % (TAKE, REF, sky.tolist(), int(wood.sum())))

for i in FRAMES:
    a = load(i)
    d = (np.abs(a - ref).sum(axis=2) > 45)
    ys, xs = np.where(d)
    if not len(xs):
        print("f%-4d no diff" % i)
        continue
    x0, x1 = xs.min(), xs.max()
    y0, y1 = ys.min(), ys.max()
    line = "f%-4d bird bbox x%d..%d y%d..%d" % (i, x0, x1, y0, y1)
    for x in list(range(max(0, x0 - 14), x0, 4)) + list(range(min(1299, x1 + 1), min(1300, x1 + 15), 4)):
        col = np.where(wood[:, x])[0]
        col = col[col > 100]
        line += "  x%d:wood=%s" % (x, int(col.min()) if len(col) else "-")
    cbx = (x0 + x1) // 2
    line += "  MIDx%d bird_low=%d" % (cbx, y1)
    # below the bird's own columns the branch may be hidden; report the diff rows too
    col = np.where(wood[:, cbx])[0]
    col = col[col > 100]
    line += " wood_mid=%s" % (int(col.min()) if len(col) else "-")
    print(line)
