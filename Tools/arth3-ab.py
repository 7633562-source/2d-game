#!/usr/bin/env python3
"""ART-H3 probe: the A|B read of one seam view (before | after) plus a coverage diff.

Writes Trials/Runs/arth3-ab/<name>-ab.png (the two views side by side on a grey ground)
and <name>-diff.png (the raw RGBA difference), and prints how many px changed.

Usage: python Tools/arth3-ab.py ankle-+030.png [knee-+120.png ...]
"""
import os
import sys

import numpy as np
from PIL import Image, ImageChops

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.path.join(PROJECT, "Trials", "Runs")


def strip_pair(im_b, im_a, out_path):
    w, h = im_b.size
    strip = Image.new("RGB", (w * 2 + 8, h), (40, 44, 52))
    for i, im in enumerate((im_b, im_a)):
        bg = Image.new("RGBA", im.size, (40, 44, 52, 255))
        bg.alpha_composite(im)
        strip.paste(bg.convert("RGB"), (i * (w + 8), 0))
    strip.save(out_path)


def main():
    names = sys.argv[1:] or ["ankle-+030.png"]
    out_dir = os.path.join(BASE, "arth3-ab")
    os.makedirs(out_dir, exist_ok=True)
    for name in names:
        pb = os.path.join(BASE, "arth3-seam-before", name)
        pa = os.path.join(BASE, "arth3-seam-after", name)
        if not (os.path.exists(pb) and os.path.exists(pa)):
            print("%s: MISSING before/after" % name)
            continue
        im_b = Image.open(pb).convert("RGBA")
        im_a = Image.open(pa).convert("RGBA")
        w, h = im_b.size
        strip_pair(im_b, im_a, os.path.join(out_dir, name.replace(".png", "-ab.png")))
        d = ImageChops.difference(im_b, im_a)
        d.save(os.path.join(out_dir, name.replace(".png", "-diff.png")))
        arr = np.array(d).astype(np.int64)
        changed = int((arr.sum(axis=2) > 12).sum())
        print("%s: %dx%d  changed px %d (%.2f%%)" % (name, w, h, changed,
                                                     100.0 * changed / (w * h)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
