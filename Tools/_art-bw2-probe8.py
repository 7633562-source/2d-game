# ART-BW2 probe 8: what tones live in the mic crop? (define the fan mask on real pixels.)
# Read-only, on the ART-BW1 kept take.
import os
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)
label, n = "aBW1f2_wander", 95

im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
crop = im.crop(BOX)
px = crop.load()
W, H = crop.size
hist = {}
for y in range(H):
    for x in range(W):
        r, g, b = px[x, y]
        key = (r // 12 * 12, g // 12 * 12, b // 12 * 12)
        hist[key] = hist.get(key, 0) + 1
top = sorted(hist.items(), key=lambda kv: -kv[1])[:18]
print("== the crop's most common quantized tones ==")
for k, c in top:
    # where does it live?
    xs = []
    for y in range(0, H, 4):
        for x in range(0, W, 4):
            r, g, b = px[x, y]
            if (r // 12 * 12, g // 12 * 12, b // 12 * 12) == k:
                xs.append((x, y))
    if xs:
        x0 = min(p[0] for p in xs); x1 = max(p[0] for p in xs)
        y0 = min(p[1] for p in xs); y1 = max(p[1] for p in xs)
        print("%14s  n=%6d  bbox x%3d..%3d y%3d..%3d" % (str(k), c, x0, x1, y0, y1))
