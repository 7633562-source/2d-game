# ART-BW3 teeth probe: the visible gaps (the notches between the fan's plate tips) measured off
# the mic crop - per row, a gap = a run of uncovered pixels flanked by covered runs on BOTH
# sides (so the outer sky and the grass are excluded). The row's rhythm shows in the spread of
# the gap widths and in how many same-size gaps repeat.
# usage: python _art-bw3-teeth.py <label> <frame> [<label> <frame> ...]
import statistics
import sys

from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (690, 700, 1090, 1060)
TOP = 300
COVERED_SUM = 530


def covered_row(px, y, w):
    return [1 if sum(px[x, y]) < COVERED_SUM else 0 for x in range(w)]


def report(label, frame):
    im = Image.open(RUNS + "\\shot_%s_%d.png" % (label, frame)).convert("RGB").crop(BOX)
    px = im.load()
    w, h = im.size
    gaps = []
    rowsc = 0
    for y in range(40, TOP):
        row = covered_row(px, y, w)
        if 1 not in row:
            continue
        rowsc += 1
        x = 0
        while x < w:
            if row[x] == 0:
                x0 = x
                while x < w and row[x] == 0:
                    x += 1
                x1 = x
                if 0 < x0 and x1 < w and row[x0 - 1] == 1 and row[x1] == 1 and (x1 - x0) <= 40:
                    gaps.append(x1 - x0)
            else:
                x += 1
    if not gaps:
        print("%s f%d: no interior gaps" % (label, frame))
        return
    hist = {}
    for g in gaps:
        hist[g] = hist.get(g, 0) + 1
    print("%s f%-4d rows %d  gaps %4d  width mean %.2f sd %.2f min %d max %d | hist %s" % (
        label, frame, rowsc, len(gaps), statistics.mean(gaps), statistics.pstdev(gaps),
        min(gaps), max(gaps), " ".join("%d:%d" % kv for kv in sorted(hist.items()))))


for i in range(1, len(sys.argv), 2):
    report(sys.argv[i], int(sys.argv[i + 1]))
