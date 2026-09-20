"""ART-0 31st pass: the two backdrop slits the hole probe found, as pixels.

`Tools/art0-bole-hole-probe.py` found, on the shipped still, a 1 px backdrop band
x 867..895 at y=1045 - i.e. exactly the row between the bole's own last ink row
and the ground strip's first one - plus three 3-7 px wide vertical pockets inside
the bole (x 901..903 y 831..885, x 860..866 y 544..593, x 859..861 y 861..890).

This probe prints the raw RGB of those places and writes a 6x nearest zoom of
each, so the numbers and the picture can be compared against the backdrop's own
colour sampled far away from the tree.

Usage:  python Tools/art0-bole-slit-probe.py [still.png]
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STILL = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    ROOT, "Trials", "Runs", "art0leaf29_tree.png")
OUT = os.path.join(ROOT, "Trials", "Runs", "art0bole31")
PX_PER_M = 414.21

CASES = [
    ("base_slit", 840, 1030, 925, 1062),
    ("pocket_a", 888, 818, 918, 898),
    ("pocket_b", 848, 532, 880, 606),
    ("pocket_c", 846, 850, 872, 902),
]


def main():
    im = Image.open(STILL).convert("RGB")
    px = im.load()
    os.makedirs(OUT, exist_ok=True)

    print("BACKDROP samples away from the tree:")
    for (sx, sy) in ((50, 50), (1800, 60), (960, 20), (300, 900)):
        print("  (%4d,%4d) = %s" % (sx, sy, px[sx, sy]))

    print("\nBASE - rows 1040..1050, columns 860..900 (RGB, one row per line):")
    for y in range(1040, 1051):
        row = []
        for x in range(860, 901, 4):
            r, g, b = px[x, y]
            row.append("%d:%02x%02x%02x" % (x, r, g, b))
        print("  y=%4d %s" % (y, " ".join(row)))

    print("\nTRUNK FOOT - for each column, the last ink row before y=1046:")
    for x in range(855, 906, 2):
        last = None
        for y in range(1000, 1046):
            r, g, b = px[x, y]
            if not (abs(r - g) < 14 and abs(g - b) < 20 and abs(r - b) < 22):
                last = y
        r, g, b = px[x, 1045]
        print("  x=%4d lastInkRow=%s  row1045=%02x%02x%02x%s"
              % (x, last, r, g, b,
                 "  <-- backdrop" if abs(r - g) < 14 and abs(g - b) < 20 and abs(r - b) < 22 else ""))

    for (name, x0, y0, x1, y1) in CASES:
        crop = im.crop((x0, y0, x1, y1))
        z = 6
        crop.resize(((x1 - x0) * z, (y1 - y0) * z), Image.NEAREST).save(
            os.path.join(OUT, name + ".png"))
        print("\n%s window x %d..%d y %d..%d -> %s.png  (%.3f x %.3f m)"
              % (name, x0, x1, y0, y1, name, (x1 - x0) / PX_PER_M, (y1 - y0) / PX_PER_M))

    print("\nOUT %s" % OUT)


if __name__ == "__main__":
    main()
