# ART-BW1 probe 8: the dark-core count, before vs after (same box, same instrument).
# Counts the bird-band pixels under three darkness thresholds in both frames.
import os
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)
PAIRS = (("bW10f2", 93, "before"), ("aBW1f1_wander", 95, "after"))

for label, n, tag in PAIRS:
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
    box = im.crop(BOX)
    bpx = box.load()
    counts = {150: 0, 200: 0, 250: 0}
    darkest = (9999, 0, 0, None)
    for y in range(300):
        for x in range(400):
            r, g, b = bpx[x, y]
            s = r + g + b
            for thr in counts:
                if s < thr:
                    counts[thr] += 1
            if s < darkest[0]:
                darkest = (s, x, y, (r, g, b))
    print("%-6s f%-3d  sum<150:%5d  <200:%5d  <250:%5d   darkest %s at (%d,%d)" %
          (tag, n, counts[150], counts[200], counts[250], darkest[3], darkest[1], darkest[2]))
