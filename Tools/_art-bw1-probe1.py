# ART-BW1 probe 1: the tone layers of the bird's fan in the kept take (bW10f2 f93).
# Prints: image size, sky/grass samples, darkest-pixel clusters (the overlap core),
# and a luminance grid over the mic box (690,700)-(1090,1060).
import os
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)
SHOT = "shot_bW10f2_93.png"

im = Image.open(os.path.join(RUNS, SHOT)).convert("RGB")
print("size", im.size)
px = im.load()

# sky: top-left corner of the frame; grass: bottom strip
print("sky", px[60, 60], px[600, 300])
print("grass", px[60, im.size[1] - 30], px[900, im.size[1] - 60])

box = im.crop(BOX)
W, H = box.size
bpx = box.load()

# darkest cluster: find the darkest 1% of pixels and their centroid
vals = []
for y in range(H):
    for x in range(W):
        r, g, b = bpx[x, y]
        vals.append((r + g + b, x, y))
vals.sort()
n = len(vals)
dark = vals[: max(1, n // 100)]
cx = sum(v[1] for v in dark) / len(dark)
cy = sum(v[2] for v in dark) / len(dark)
print("darkest1pct: sum<=%d centroid in-box (%.0f,%.0f) samples:" % (dark[-1][0], cx, cy))
for v in (dark[0], dark[len(dark) // 2], dark[-1]):
    print("   ", v, bpx[v[1], v[2]])

# the fan core sample: 5 points near the centroid
print("core samples:")
for dx, dy in ((0, 0), (-20, 0), (20, 0), (0, -20), (0, 20)):
    x, y = int(cx) + dx, int(cy) + dy
    if 0 <= x < W and 0 <= y < H:
        print("    (%d,%d) %s" % (x, y, bpx[x, y]))

# luminance grid 40 x 18
print("lum grid (0=white . = light, 9=black #):")
GX, GY = 50, 20
for gy in range(GY):
    row = ""
    for gx in range(GX):
        x = int((gx + 0.5) * W / GX)
        y = int((gy + 0.5) * H / GY)
        r, g, b = bpx[x, y]
        lum = (r * 299 + g * 587 + b * 114) // 1000
        idx = min(9, (255 - lum) * 10 // 256)
        row += "." if idx == 0 else str(idx)
    print("  " + row)
