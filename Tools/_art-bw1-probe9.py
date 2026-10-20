# ART-BW1 probe 9: how big is the residual dark zone after the change, and where.
import os
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)

for label, n, tag in (("bW10f2", 93, "before"), ("aBW1f1_wander", 95, "after")):
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
    box = im.crop(BOX)
    bpx = box.load()
    for thr in (250, 300, 350, 400):
        xs = ys = k = 0
        x0, y0, x1, y1 = 400, 300, 0, 0
        for y in range(300):
            for x in range(400):
                r, g, b = bpx[x, y]
                if r + g + b < thr:
                    k += 1
                    xs += x
                    ys += y
                    x0, y0, x1, y1 = min(x0, x), min(y0, y), max(x1, x), max(y1, y)
        if k:
            print("%-6s sum<%3d n=%5d centroid(%3d,%3d) bbox x%3d..%3d y%3d..%3d" %
                  (tag, thr, k, xs // k, ys // k, x0, x1, y0, y1))
        else:
            print("%-6s sum<%3d n=0" % (tag, thr))
    print()
