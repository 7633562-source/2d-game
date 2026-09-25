# ART-BW2 probe 13: the tone-step width - the plate boundaries' gradient length in the take's own
# pixels. For each row of the fan's zone: a "flat" pixel starts a horizontal run of equal tone;
# a transition is the stretch of non-flat pixels between two flats. Mean length, px (1 px = 1.57 mm).
import os
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)
CASES = [("aBW1f2_wander", 95, "before"), ("aBW2f2_wander", 94, "after")]
X0, X1, Y0, Y1 = 72, 330, 92, 290


def flat(t, i, n):
    if i + 2 >= n:
        return False
    return t[i] == t[i + 1] == t[i + 2]


for label, n, tag in CASES:
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
    crop = im.crop(BOX)
    px = crop.load()
    lens = []
    nsteps = 0
    for y in range(Y0, Y1, 4):
        t = [px[x, y][0] for x in range(X0, X1)]
        nn = len(t)
        f = [flat(t, i, nn) for i in range(nn)]
        i = 0
        while i < nn:
            if f[i]:
                i += 1
                continue
            j = i
            while j < nn and not f[j]:
                j += 1
            run = j - i
            if run >= 1 and t[max(0, i - 1)] != t[min(nn - 1, j)]:
                lens.append(run)
                nsteps += 1
            i = j
    lens.sort()
    mean = sum(lens) / max(1, len(lens))
    med = lens[len(lens) // 2] if lens else 0
    print("%-6s transitions %4d  mean %.2f px (%.1f mm)  median %d  max %d  >=4px: %d" %
          (tag, nsteps, mean, mean * 1000 / 635.3, med, lens[-1] if lens else 0,
           sum(1 for v in lens if v >= 4)))
