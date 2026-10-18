# ART-BW1 probe 6: is the sky uniform? Sample the frame's background (avoiding bird/grass),
# and print raw RGB at a few points; also raw samples along a line through the fan.
import os
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
im = Image.open(os.path.join(RUNS, "shot_bW10f2_93.png")).convert("RGB")
px = im.load()
print("frame", im.size)
for y in (30, 150, 300, 450, 600, 700, 760, 820, 880, 940, 990):
    row = []
    for x in (100, 400, 1420, 1800):
        row.append("%s" % (px[x, y],))
    print("y=%4d  %s" % (y, "  ".join(row)))
print("--- raw samples through the box (frame coords) ---")
for (x, y) in ((760, 760), (780, 780), (800, 800), (820, 820), (840, 840), (860, 860),
               (880, 880), (900, 900), (920, 920), (940, 940), (980, 940), (1000, 940),
               (1020, 940), (1040, 940), (1060, 940)):
    print("  (%4d,%4d) %s" % (x, y, px[x, y]))
