"""ART-0 (18th pass): the blind pairwise read taken from the SHIPPED FRAME, not from the asset.

The asset sheet (`Tools/art0-fill-strip-ab.py`) shows the tile; the acceptance this lane is judged by
is a PICTURE - the same camera, the same moment, the game's own renderer. So this cuts the band the
ground fill draws in out of the same still of two capture labels (find the band with
`Tools/art0-still-row-split.py`) and stacks the two builds - one above the other, at 1:1 so no
resampling invents or hides a stone - with which build is on TOP random and written to `map.txt`,
which is opened after the pick. Stacking rather than putting them side by side keeps the pixels at
their real size (a 1920-wide side-by-side pair is downscaled by any reader, and a downscale is
exactly the operation that can hide a pebble).

Usage:
  python Tools/art0-frame-band-pair.py <labelA> <labelB> <still> <y0> <y1> <x0> <x1> <seed>
  python Tools/art0-frame-band-pair.py art0wrap17 art0fill18 level 660 810 380 1780 5
"""
import os
import random
import sys

from PIL import Image

ROOT = r"E:/Games/2D Game/My project"
RUNS = os.path.join(ROOT, "Trials", "Runs")
OUT = os.path.join(RUNS, "art-0-fill-18th-frames")
GAP = 8
NEUTRAL = (110, 110, 110)


def band(label, still, y0, y1, x0, x1):
    p = os.path.join(RUNS, "%s_%s.png" % (label, still))
    return Image.open(p).convert("RGB").crop((x0, y0, x1, y1))


def main():
    if len(sys.argv) < 9:
        print(__doc__)
        return 2
    a, b, still = sys.argv[1], sys.argv[2], sys.argv[3]
    y0, y1, x0, x1, seed = (int(sys.argv[4]), int(sys.argv[5]), int(sys.argv[6]),
                            int(sys.argv[7]), int(sys.argv[8]))
    os.makedirs(OUT, exist_ok=True)
    order = [a, b]
    random.Random(seed).shuffle(order)
    top, bottom = band(order[0], still, y0, y1, x0, x1), band(order[1], still, y0, y1, x0, x1)
    out = Image.new("RGB", (top.size[0], top.size[1] * 2 + GAP), NEUTRAL)
    out.paste(top, (0, 0))
    out.paste(bottom, (0, top.size[1] + GAP))
    name = "stack_%s_%d-%d_x%d-%d_s%d.png" % (still, y0, y1, x0, x1, seed)
    out.save(os.path.join(OUT, name), "PNG", optimize=True)
    with open(os.path.join(OUT, "map.txt"), "a", encoding="utf-8", newline="\n") as fh:
        fh.write("%-40s rows %d-%d x %d-%d  %dx%d  OLD(%s)=%s  NEW(%s)=%s\n"
                 % (name, y0, y1, x0, x1, out.size[0], out.size[1], a,
                    "TOP" if order[0] == a else "BOTTOM", b,
                    "TOP" if order[0] == b else "BOTTOM"))
    print("wrote %s %dx%d (mapping withheld, TOP=%s)" % (name, out.size[0], out.size[1], order[0]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
