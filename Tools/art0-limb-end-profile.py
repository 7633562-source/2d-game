"""ART-0 28th pass probe - the SHAPE of a limb canvas' two ends.

The four limb canvases carry slack (the ink is 28-44 % shorter than its bone), and the
placement of that slack is a choice. This probe answers the question that decides it:
is the ink's PROXIMAL end (the end that belongs on the parent joint) a distinctive cap -
a shoulder / hip / elbow / knee - or a plain tube cut? Reads the shipped bytes
(Resources/Art/Human/*.png) and the legacy source, prints the silhouette width at 11
slices from proximal (row 0 = top = the joint) to distal, in px and as a fraction of the
widest row.
"""

import pathlib
import sys

from PIL import Image

PROJECT = pathlib.Path(__file__).resolve().parent.parent
ART = PROJECT / "Assets" / "Resources" / "Art" / "Human"
LEGACY = PROJECT / "Assets" / "LegacyArt" / "Human"

PARTS = ["upper", "lower", "thigh", "shin", "hand", "head", "torso", "foot"]
SLICES = 11


def profile(path, label):
    im = Image.open(path).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 40 else 0)
    bb = a.getbbox()
    if bb is None:
        print("  %s: no ink" % label)
        return
    x0, y0, x1, y1 = bb
    w = x1 - x0
    h = y1 - y0
    widths = []
    px = a.load()
    for i in range(SLICES):
        ya = y0 + int(h * i / SLICES)
        yb = y0 + int(h * (i + 1) / SLICES)
        best = 0
        for y in range(ya, max(ya + 1, yb)):
            n = 0
            for x in range(x0, x1):
                if px[x, y]:
                    n += 1
            best = max(best, n)
        widths.append(best)
    top = max(1, widths[0])
    bot = max(1, widths[-1])
    peak = max(widths) or 1
    print("  %-6s ink %dx%d px  canvas %dx%d  top row %d px (%.2f of peak)  bottom row %d px (%.2f of peak)"
          % (label, w, h, im.width, im.height, top, top / float(peak), bot, bot / float(peak)))
    print("          proximal->distal width %% of peak: %s"
          % " ".join("%3.0f" % (100.0 * v / peak) for v in widths))
    print("          widths px: %s" % " ".join("%3d" % v for v in widths))


def main():
    only = sys.argv[1:] or PARTS
    for p in only:
        print("== %s ==" % p)
        profile(ART / (p + ".png"), "ship")
        src = LEGACY / (p + ".png")
        if src.exists():
            profile(src, "legacy")
        else:
            print("  legacy: (no source file)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
