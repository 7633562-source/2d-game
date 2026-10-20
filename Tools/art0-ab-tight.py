#!/usr/bin/env python3
"""A SQUARE pairwise A/B sheet, for the vision judge: two labels, same rectangle, side by side.

    python Tools/art0-ab-tight.py <labelA> <labelB> <still> <zoom> <x0,y0,x1,y1> <seed> [tag]

`Tools/art0-ab-pair.py` stacks the two panels VERTICALLY, which on a tall crop (a stem: 70 x 170 px)
writes a 700 x 3412 px sheet. The vision judge downscales such a sheet to fit its own window, and at
that point a 7 px stem is a 20 px streak in both panels and the read came back "the two panels are
identical" - which the pixels contradict (19.9% of them differ). This tool keeps the sheet SQUARE so
the crop arrives at full resolution: the two panels go SIDE BY SIDE, no words on the picture, and the
mapping is written to a separate `-map.txt` to be opened AFTER a side has been named.

Guarantees carried over from the vertical tool (the same rules):
  * both panels are the SAME rectangle of the SAME still from two builds - the crop is fixed, so the
    camera is the only thing that may differ, and the labels' `buildGuid` are printed to check that;
  * which label is on the LEFT is drawn from the seed and written only to the map file;
  * the separator is a 6 px magenta bar, so a model that reports "grey separator" (seed 137 did) is
    reporting that it did not look.
"""
import os
import random
import sys

import numpy as np
from PIL import Image

RUNS = os.path.join("Trials", "Runs")
SEP = (255, 0, 255)


def main():
    if len(sys.argv) < 7:
        print(__doc__)
        return 2
    a_label, b_label, still = sys.argv[1], sys.argv[2], sys.argv[3]
    zoom = int(sys.argv[4])
    x0, y0, x1, y1 = (int(v) for v in sys.argv[5].split(","))
    seed = int(sys.argv[6])
    tag = sys.argv[7] if len(sys.argv) > 7 else "ab"

    crops = {}
    for label in (a_label, b_label):
        path = os.path.join(RUNS, "%s_%s.png" % (label, still))
        img = Image.open(path).convert("RGB").crop((x0, y0, x1, y1))
        crops[label] = img.resize((img.width * zoom, img.height * zoom), Image.NEAREST)

    rng = random.Random(seed)
    first, second = (a_label, b_label) if rng.random() < 0.5 else (b_label, a_label)
    left, right = crops[first], crops[second]
    sheet = Image.new("RGB", (left.width + 6 + right.width, max(left.height, right.height)), SEP)
    sheet.paste(left, (0, 0))
    sheet.paste(right, (left.width + 6, 0))

    out_dir = os.path.join(RUNS, "art-0-ab")
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, "%s_%s_seed%d.png" % (still, tag, seed))
    sheet.save(out)
    with open(out.replace(".png", "-map.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("panel 1 (LEFT)  = %s\npanel 2 (RIGHT) = %s\n\nread the picture first, "
                 "then this file\n" % (first, second))

    for label in (a_label, b_label):
        print("%-14s %s build=%s" % (label, "%s_%s.png" % (label, still), build_guid(label)))
    print("crop=(%d, %d, %d, %d) zoom=%dx  panels %dx%d each" % (x0, y0, x1, y1, zoom,
                                                                 left.width, left.height))
    print("wrote %s (%dx%d) + %s" % (out.replace("\\", "/"), sheet.width, sheet.height,
                                     os.path.basename(out).replace(".png", "-map.txt")))
    return 0


def build_guid(label):
    import json
    path = os.path.join(RUNS, "%s-gallery.json" % label)
    if not os.path.exists(path):
        return "?"
    try:
        return json.load(open(path)).get("buildGuid", "?")
    except Exception:
        return "?"


if __name__ == "__main__":
    sys.exit(main())
