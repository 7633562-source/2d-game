# ART-BW3 film-side row probe: the saw's own statistics off the mic crop (same BOX as the
# instrument). The fan's top rim is traced as y(x); a median filter gives the arc; the residual
# is the saw - its protrusions (tips) and their widths. Prints, per frame: tip count, the tip
# widths at the rim and 6 / 12 px inside, and the spread of those widths (the row's variety).
# usage: python _art-bw3-row.py <label> <frame> [<label2> <frame2> ...]
import statistics
import sys

from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)


def fan_mask(im):
    w, h = im.size
    px = im.load()
    m = [[0] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            # the fan/bird slate blends: darker than sky on all three channels, bluer than grass
            if r < 135 and g < 192 and b < 232 and b > 90:
                m[y][x] = 1
    return m


def top_profile(m, x0, x1):
    h = len(m)
    prof = []
    for x in range(x0, x1):
        yt = None
        for y in range(h):
            if m[y][x]:
                yt = y
                break
        prof.append(yt)
    return prof


def median_filter(vals, k):
    out = []
    n = len(vals)
    for i in range(n):
        lo = max(0, i - k)
        hi = min(n, i + k + 1)
        seg = [v for v in vals[lo:hi] if v is not None]
        out.append(statistics.median(seg) if seg else None)
    return out


def report(label, frame):
    im = Image.open(RUNS + "\\shot_%s_%d.png" % (label, frame)).convert("RGB").crop(BOX)
    m = fan_mask(im)
    w, h = im.size
    # the fan's own columns: those with any fan pixel in the upper half
    cols = [x for x in range(w) if any(m[y][x] for y in range(0, h // 2))]
    if not cols:
        print("%s f%d: no fan pixels" % (label, frame))
        return
    x0, x1 = min(cols), max(cols) + 1
    prof = top_profile(m, x0, x1)
    arc = median_filter(prof, 6)
    res = [None if (p is None or a is None) else a - p for p, a in zip(prof, arc)]
    # tips = negative residuals (the rim reaches higher than the arc), notches = positive
    tips = []
    i = 0
    n = len(res)
    while i < n:
        if res[i] is not None and res[i] < -1.0:
            j = i
            while j < n and res[j] is not None and res[j] < -1.0:
                j += 1
            seg = [res[k] for k in range(i, j)]
            tips.append((x0 + (i + j - 1) / 2.0, -min(seg), j - i))
            i = j
        else:
            i += 1
    # the tooth width 12 px inside the rim: count fan pixels in the column band at the tip
    widths = []
    for tx, amp, wdt in tips:
        xi = int(round(tx))
        if not (0 <= xi < w):
            continue
        yt = None
        for y in range(h):
            if m[y][xi]:
                yt = y
                break
        if yt is None:
            continue
        yy = yt + 12
        if yy >= h:
            continue
        run = 0
        x = xi
        while x > 0 and m[yy][x - 1]:
            x -= 1
        while x < w and m[yy][x]:
            run += 1
            x += 1
        widths.append(run)
    amp = [t[1] for t in tips]
    lw = [t[2] for t in tips]
    sp = [tips[i + 1][0] - tips[i][0] for i in range(len(tips) - 1)]
    print("%s f%-4d cols %d..%d tips %2d | amp mean %.1f max %.1f | lobe-width cols mean %.1f sd %.1f min %d max %d | spacing mean %.1f sd %.1f" % (
        label, frame, x0, x1, len(tips),
        statistics.mean(amp) if amp else 0, max(amp) if amp else 0,
        statistics.mean(lw) if lw else 0, statistics.pstdev(lw) if len(lw) > 1 else 0,
        min(lw) if lw else 0, max(lw) if lw else 0,
        statistics.mean(sp) if sp else 0, statistics.pstdev(sp) if len(sp) > 1 else 0))


for i in range(1, len(sys.argv), 2):
    report(sys.argv[i], int(sys.argv[i + 1]))
