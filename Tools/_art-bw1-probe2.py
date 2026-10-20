# ART-BW1 probe 2: the fan's tone layers only (no grass). Quantized histogram of the
# bluish bird pixels in the mic box + the darkest cluster + row/col extents.
import os
from PIL import Image
from collections import Counter

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)
SHOT = "shot_bW10f2_93.png"

im = Image.open(os.path.join(RUNS, SHOT)).convert("RGB")
box = im.crop(BOX)
W, H = box.size
bpx = box.load()

cnt = Counter()
darkest = (999, 0, 0)
for y in range(0, 300):            # bird band only: exclude the grass strip
    for x in range(W):
        r, g, b = bpx[x, y]
        if b >= r and b >= g and (r + g + b) < 620:      # bluish (bird), not sky/green
            cnt[(r // 8 * 8, g // 8 * 8, b // 8 * 8)] += 1
            if r + g + b < darkest[0]:
                darkest = (r + g + b, x, y)

print("bluish bird pixels:", sum(cnt.values()))
for c, n in cnt.most_common(14):
    print("   ", c, n)

print("darkest bird px:", darkest)

# rings: for each 16-unit blue bucket, print the mean r,g,b so the tone ladder is readable
print("tone ladder (quantized q -> mean rgb, count):")
lad = {}
for y in range(0, 300):
    for x in range(W):
        r, g, b = bpx[x, y]
        if b >= r and b >= g and (r + g + b) < 620:
            q = b // 16 * 16
            s = lad.setdefault(q, [0, 0, 0, 0])
            s[0] += r; s[1] += g; s[2] += b; s[3] += 1
for q in sorted(lad):
    r, g, b, n = lad[q]
    print("    b~%3d -> (%3d,%3d,%3d) n=%d" % (q, r // n, g // n, b // n, n))
