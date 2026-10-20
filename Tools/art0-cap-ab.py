"""ART-0 before/after sheet for one ground strip.

Both captures framed the same camera (same still definition, same ortho), so a crop
of the same box shows the same world metres before and after a repaint. The crop is
upscaled with NEAREST on purpose: a filter would hide the very pixel pattern the
strip is judged by.

Usage:  python Tools/art0-cap-ab.py <labelOld> <labelNew> <still> <x0> <y0> <x1> <y1> [zoom]
Writes  Trials/Runs/<labelNew>_<still>_<x0>_<y0>_ab.png   (old on top, new below)
"""
import os
import sys

from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials/Runs")


def main(old, new, still, x0, y0, x1, y1, zoom=3):
    a = Image.open(os.path.join(RUNS, "%s_%s.png" % (old, still))).convert("RGB").crop((x0, y0, x1, y1))
    b = Image.open(os.path.join(RUNS, "%s_%s.png" % (new, still))).convert("RGB").crop((x0, y0, x1, y1))
    w, h = a.size
    a = a.resize((w * zoom, h * zoom), Image.NEAREST)
    b = b.resize((w * zoom, h * zoom), Image.NEAREST)
    sheet = Image.new("RGB", (a.width, a.height * 2 + 4), (255, 0, 255))
    sheet.paste(a, (0, 0))
    sheet.paste(b, (0, a.height + 4))
    out = os.path.join(RUNS, "%s_%s_%d_%d_ab.png" % (new, still, x0, y0))
    sheet.save(out)
    print("AB %s  crop %dx%d at (%d,%d) zoom %dx  old=%s new=%s"
          % (out, x1 - x0, y1 - y0, x0, y0, zoom, old, new))
    return 0


if __name__ == "__main__":
    args = sys.argv[1:8]
    main(args[0], args[1], args[2], *[int(v) for v in args[3:]],
         **({"zoom": int(sys.argv[8])} if len(sys.argv) > 8 else {}))
