# ART-BW2 probe 11b: the fan's outer end (the left edge of the pad) - geometry or faintness?
# Per column: the count of slate-ink pixels (r <= 128, b > g > r) and the column's darkest r.
import os
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)
CASES = [("aBW1f2_wander", 95, "before"), ("aBW2f2_wander", 94, "after")]

for label, n, tag in CASES:
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
    crop = im.crop(BOX)
    px = crop.load()
    line = []
    for x in range(70, 100):
        k = 0
        mn = 255
        for y in range(300):
            r, g, b = px[x, y]
            if b > g > r and r <= 128:
                k += 1
                if r < mn:
                    mn = r
        line.append("%d:n%d,r%s" % (x, k, mn if mn < 255 else "-"))
    print(tag, " ".join(line))
