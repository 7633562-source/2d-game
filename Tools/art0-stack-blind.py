#!/usr/bin/env python3
"""ART-0 cycle: a BLIND two-panel sheet of one catalogue lens, plus the mapping.

A named panel hands the reader the answer it is supposed to find (the fire pass of
19.09 was misled by exactly that), so the order here is shuffled by a seed and the
mapping goes to a file the reader does not open until the pick is written down.

Usage:  python Tools/art0-stack-blind.py <labelA> <labelB> <lens> <out.png> <map.txt> [seed] [scale]

Writes:  <out.png>   the two panels, no captions, left and right
         <map.txt>   which label is on which side
"""
import os
import random
import sys

from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials", "Runs")


def main():
    if len(sys.argv) < 6:
        raise SystemExit(__doc__)
    a, b = sys.argv[1], sys.argv[2]
    lens = sys.argv[3]
    out, mapping = sys.argv[4], sys.argv[5]
    seed = int(sys.argv[6]) if len(sys.argv) > 6 else 1
    scale = int(sys.argv[7]) if len(sys.argv) > 7 else 1
    box = tuple(int(v) for v in sys.argv[8].split(",")) if len(sys.argv) > 8 else None

    panels = [Image.open(os.path.join(RUNS, "%s_%s.png" % (lbl, lens))).convert("RGB")
              for lbl in (a, b)]
    if box is not None:
        panels = [p.crop(box) for p in panels]
    order = [0, 1]
    random.Random(seed).shuffle(order)
    if scale != 1:
        panels = [p.resize((p.width * scale, p.height * scale), Image.NEAREST) for p in panels]

    w, h = panels[0].size
    sheet = Image.new("RGB", (w * 2 + 16, h), (18, 18, 20))
    for i, idx in enumerate(order):
        sheet.paste(panels[idx], (i * (w + 16), 0))
    sheet.save(out)

    with open(mapping, "w", encoding="utf-8") as fh:
        fh.write("lens=%s seed=%d\n" % (lens, seed))
        fh.write("left  = %s\n" % (a if order[0] == 0 else b))
        fh.write("right = %s\n" % (b if order[0] == 0 else a))
    print("wrote %s (%dx%d) and %s - do not open the map before the pick"
          % (out, sheet.width, sheet.height, mapping))


if __name__ == "__main__":
    main()
