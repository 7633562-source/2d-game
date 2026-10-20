#!/usr/bin/env python3
"""ART-H4e probe: the far-side A/B - the same crop of the same shot, two render tints.

Panels divided by a pink band, the way the pick question expects.  (1) opaque far side (the
shipped HumanArt.FarTone, from the arth4e take) vs (2) the old translucent far side (the
arth4c take).  Two output files: the pair and its twin with the panels SWAPPED, so a pick can
be checked against a position bias.

Usage: python Tools/arth4e-ab.py <before.png> <after.png> <out.png> x0 x1 y0 y1
  The strip = panel1 BEFORE | panel2 AFTER; "<out>-swap.png" = panel1 AFTER | panel2 BEFORE.
"""
import sys

from PIL import Image


def main(argv):
    before, after, out = argv[1], argv[2], argv[3]
    x0, x1, y0, y1 = [int(v) for v in argv[4:8]]
    a = Image.open(before).convert("RGB").crop((x0, y0, x1, y1))
    b = Image.open(after).convert("RGB").crop((x0, y0, x1, y1))
    w, h = a.size
    for name, p1, p2 in ((out, a, b), (out.replace(".png", "-swap.png"), b, a)):
        strip = Image.new("RGB", (w * 2 + 8, h), (255, 0, 255))
        strip.paste(p1, (0, 0))
        strip.paste(p2, (w + 8, 0))
        strip.save(name)
        print("strip %s  (%dx%d)" % (name, strip.width, strip.height))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
