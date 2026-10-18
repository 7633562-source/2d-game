# ART-BW1 probe 5: tone map of the mic box - which tone sits on which part.
# One character per sampled pixel: sky ' ', then the 8 tone clusters, plus 'other'.
import os
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)
SHOT = "shot_bW10f2_93.png"
REF = [((140, 199, 240), " "), ((29, 35, 46), "0"), ((41, 47, 58), "1"),
       ((49, 56, 67), "2"), ((54, 72, 87), "3"), ((62, 83, 101), "4"),
       ((68, 96, 118), "5"), ((86, 122, 149), "6"), ((109, 156, 189), "7"),
       ((112, 152, 192), "8"), ((66, 117, 15), "g")]

im = Image.open(os.path.join(RUNS, SHOT)).convert("RGB")
box = im.crop(BOX)
W, H = box.size
bpx = box.load()

GX, GY = 100, 44
print("legend: ' '=sky 0=darkest .. 8=lightest q=other g=green")
for gy in range(GY):
    row = ""
    for gx in range(GX):
        x = int((gx + 0.5) * W / GX)
        y = int((gy + 0.5) * H / GY)
        r, g, b = bpx[x, y]
        best, bd = "?", 9e9
        for (tr, tg, tb), ch in REF:
            d = abs(r - tr) + abs(g - tg) + abs(b - tb)
            if d < bd:
                bd, best = d, ch
        row += best if bd < 40 else "q"
    print("  " + row)
