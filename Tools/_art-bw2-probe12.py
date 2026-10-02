# ART-BW2 probe 12: is the feathered margin IN the take pixels? Print the red channel (the
# coverage proxy: sky r=132, deeper = more cover) along scanlines across the fan's left lobe,
# before vs after, same rows.
import os
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)
CASES = [("aBW1f2_wander", 95, "before"), ("aBW2f2_wander", 94, "after")]

for label, n, tag in CASES:
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
    crop = im.crop(BOX)
    px = crop.load()
    for y in (110, 130, 150, 170):
        vals = [px[x, y][0] for x in range(80, 132)]
        print("%-6s y%-4d %s" % (tag, y, " ".join("%3d" % v for v in vals)))
    print()
