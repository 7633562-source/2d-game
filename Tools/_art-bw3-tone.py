# ART-BW3 tone probe: ART-BW1/BW2's own ruler on the mic pair's own crop (probe 10's port):
# the dark-core count over the crop's bird region only (y < 300 - the grass strip below is not
# the bird), and the pad's ink (the slate family: b > g > r, b - r <= 104, r <= 128).
# usage: python _art-bw3-tone.py <label> <frame> [<label> <frame> ...]
import sys

from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)
TOP = 300
PX = 635.3


def stats(label, frame):
    box = Image.open(RUNS + "\\shot_%s_%d.png" % (label, frame)).convert("RGB").crop(BOX)
    bpx = box.load()
    W, H = box.size
    out = []
    for thr in (250, 300, 350):
        k = 0
        x0, y0, x1, y1 = W, H, 0, 0
        for y in range(TOP):
            for x in range(W):
                r, g, b = bpx[x, y]
                if r + g + b < thr:
                    k += 1
                    x0, y0, x1, y1 = min(x0, x), min(y0, y), max(x1, x), max(y1, y)
        out.append("sum<%d:%5d%s" % (thr, k, (" bbox x%d..%d y%d..%d" % (x0, x1, y0, y1)) if k else ""))
    dark = (9999, None)
    for y in range(TOP):
        for x in range(W):
            r, g, b = bpx[x, y]
            if r + g + b < dark[0]:
                dark = (r + g + b, (r, g, b))
    n = 0
    mx0, my0, mx1, my1 = W, H, 0, 0
    for y in range(TOP):
        for x in range(W):
            r, g, b = bpx[x, y]
            if b > g > r and b - r <= 104 and r <= 128:
                n += 1
                mx0, my0, mx1, my1 = min(mx0, x), min(my0, y), max(mx1, x), max(my1, y)
    print("%s f%-4d %s darkest %s | ink px %6d bbox x%d..%d y%d..%d (%.0f x %.0f mm)" % (
        label, frame, "  ".join(out), dark[1], n, mx0, mx1, my0, my1,
        (mx1 - mx0) / PX * 1000, (my1 - my0) / PX * 1000))


for i in range(1, len(sys.argv), 2):
    stats(sys.argv[i], int(sys.argv[i + 1]))
