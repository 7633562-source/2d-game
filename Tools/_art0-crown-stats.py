"""ART-0 28th pass - is the TREE CROWN actually showing the leaf canvas?

Reads a gallery still and reports, over a crop, how many distinct colours the green pixels
carry and what fraction the modal (most common) colour holds. A TILED canvas gives many
greens and a low modal fraction; a FLAT procedural card gives a handful of colours and a
high modal fraction. Prints the top greens with percentages so the number can be argued
with. No Unity, shipped bytes only.

Run: python Tools/_art0-crown-stats.py <still.png> x0,y0,x1,y1 [label]
"""
import sys
from collections import Counter

from PIL import Image


def main():
    path = sys.argv[1]
    box = [int(v) for v in sys.argv[2].split(",")]
    label = sys.argv[3] if len(sys.argv) > 3 else path
    im = Image.open(path).convert("RGB").crop(tuple(box))
    px = list(im.getdata())
    greens = [p for p in px if p[1] > p[0] + 6 and p[1] > p[2] + 6]
    allc = Counter(px)
    gc = Counter(greens)
    print("%s  crop %s  px %d  green px %d (%.1f%%)" % (label, box, len(px), len(greens),
                                                        100.0 * len(greens) / max(1, len(px))))
    print("  distinct colours: all=%d green=%d" % (len(allc), len(gc)))
    modal_all, mA = allc.most_common(1)[0]
    print("  modal colour (all px) %s x%d = %.3f of crop" % (modal_all, mA, mA / float(len(px))))
    if gc:
        mg, mG = gc.most_common(1)[0]
        print("  modal GREEN %s x%d = %.3f of green px" % (mg, mG, mG / float(len(greens))))
        print("  top greens:")
        for c, n in gc.most_common(10):
            print("    %-16s x%-7d %5.1f%%" % (str(c), n, 100.0 * n / len(greens)))
        # a tiled painted canvas: how much of the green area is NOT in the top-5 colours
        top5 = sum(n for _, n in gc.most_common(5))
        print("  greens outside the top-5 colours: %.1f%%" % (100.0 * (1.0 - top5 / float(len(greens)))))
    return 0


if __name__ == "__main__":
    sys.exit(main())
