"""ART-0 31st pass: measure the bole at its BASE and at its MODULE joints.

Reads the shipped gallery still (Trials/Runs/art0leaf29_tree.png), finds the
bark column of the oak, and reports, per row, the bark run's left/right edges
and the ink width, so the base gap and the module steps are numbers, not
impressions. Also writes two zooms (the base, one mid-trunk joint) for the
blind pairwise read.

Usage:  python Tools/art0-bole-base-probe.py [still.png]
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STILL = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    ROOT, "Trials", "Runs", "art0leaf29_tree.png")
OUT = os.path.join(ROOT, "Trials", "Runs", "art0bole31")


def is_bark(r, g, b):
    """Bark = brown: red above green, green above blue, not too dark, not grey."""
    return r > 55 and r > g + 12 and g > b + 6 and (r - b) > 24


def main():
    im = Image.open(STILL).convert("RGB")
    w, h = im.size
    px = im.load()
    os.makedirs(OUT, exist_ok=True)

    # The bole column: the column band with the tallest uninterrupted bark run
    # in the lower half of the frame (the ground strip's own brown is excluded
    # by only counting runs that reach 0.55 of the frame height).
    rows = []
    for x in range(w):
        top = None
        run = 0
        best = 0
        for y in range(h - 1, -1, -1):
            if is_bark(*px[x, y]):
                run += 1
                if run > best:
                    best = run
                    top = y - run + 1
            else:
                run = 0
        rows.append((best, top, x))

    band = max(rows)[0]
    cols = [x for (b, t, x) in rows if b >= band * 0.80]
    x0, x1 = min(cols), max(cols)
    tops = [t for (b, t, x) in rows if b >= band * 0.80 and t is not None]
    ytop = min(tops)

    print("STILL %s %dx%d" % (os.path.basename(STILL), w, h))
    print("BOLE  columns %d..%d  width %d px  longest run %d px  top y=%d"
          % (x0, x1, x1 - x0 + 1, band, ytop))

    # Per-row left/right edge of the bark ink inside the column band.
    prev = None
    steps = []
    base_row = None
    for y in range(ytop, h):
        left = None
        right = None
        for x in range(x0, x1 + 1):
            if is_bark(*px[x, y]):
                if left is None:
                    left = x
                right = x
        if left is None:
            continue
        width = right - left + 1
        if base_row is None:
            base_row = y
        if prev is not None:
            dl, dr = left - prev[0], right - prev[1]
            if abs(dl) > 3 or abs(dr) > 3:
                steps.append((y, left, right, width, dl, dr))
        prev = (left, right)

    print("BASE  first bark row of the column y=%d (of %d); last row scanned %d"
          % (base_row, h, h - 1))

    # The bottom of the bole: how many rows of the column band are NOT bark
    # between the bole's foot and the ground strip's own first bark row.
    foot = None
    for y in range(h - 1, ytop, -1):
        if any(is_bark(*px[x, y]) for x in range(x0, x1 + 1)):
            foot = y
            break
    print("FOOT  lowest bark row of the column y=%d" % foot)

    print("STEPS %d rows where the bark column's edge moves more than 3 px" % len(steps))
    for s in steps[:60]:
        print("  y=%4d x %4d..%4d width %3d dLeft %+d dRight %+d" % s)

    # Zooms for the blind read: the base and the largest step.
    def zoom(y0, y1, name, pad=60):
        box = (max(0, x0 - pad), max(0, y0), min(w, x1 + pad), min(h, y1))
        im.crop(box).resize(((box[2] - box[0]) * 3, (box[3] - box[1]) * 3),
                            Image.NEAREST).save(os.path.join(OUT, name))
        print("ZOOM  %s box=%s" % (name, box))

    zoom(foot - 90, foot + 30, "base.png")
    if steps:
        big = max(steps, key=lambda s: abs(s[4]) + abs(s[5]))
        zoom(big[0] - 60, big[0] + 60, "step.png")
        print("BIGSTEP y=%d dLeft %+d dRight %+d" % (big[0], big[4], big[5]))
    print("OUT   %s" % OUT)


if __name__ == "__main__":
    main()
