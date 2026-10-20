# ART-BW1 probe 4: where each quantized tone lives (centroid + bbox) in the mic box,
# so the fan's own layers can be told from the body's and the bones'.
import os
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)
SHOT = "shot_bW10f2_93.png"
TARGETS = [((140, 199, 240), "sky"), ((136, 192, 240), "sky-q"),
           ((104, 152, 184), "?"), ((86, 122, 149), "?"),
           ((112, 152, 192), "?"), ((109, 156, 189), "?"),
           ((80, 120, 144), "?"), ((29, 35, 46), "core")]

im = Image.open(os.path.join(RUNS, SHOT)).convert("RGB")
box = im.crop(BOX)
W, H = box.size
bpx = box.load()

for tc, name in TARGETS:
    xs, ys, n = 0, 0, 0
    x0, y0, x1, y1 = W, H, 0, 0
    for y in range(H):
        for x in range(W):
            r, g, b = bpx[x, y]
            if abs(r - tc[0]) <= 4 and abs(g - tc[1]) <= 4 and abs(b - tc[2]) <= 4:
                n += 1
                xs += x
                ys += y
                x0, y0, x1, y1 = min(x0, x), min(y0, y), max(x1, x), max(y1, y)
    if n:
        print("%-14s n=%6d centroid (%3d,%3d) bbox x %3d..%3d y %3d..%3d" %
              (name + str(tc), n, xs // n, ys // n, x0, x1, y0, y1))
    else:
        print("%-14s n=0" % (name + str(tc)))
