#!/usr/bin/env python3
"""ART-0 sign cells, pass 4: blind A/B of the sign in two real gallery captures.

Takes the LEVEL still of two capture labels and cuts the same window out of both, so the only
difference the eye can see is the cloth. Panels are named LEFT / RIGHT only and the mapping goes to
`map.txt` next to the sheet - a labelled pair invites the answer it wants.

Usage:  python Tools/art0-sign-frame-ab.py <labelOld> <labelNew> [outDirName]
"""

import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials/Runs")

# the sign sits at the level's right end; the window is wide enough to hold the post plus the
# ground line it stands on, so the eye can judge the piece against the level it lives in
WINDOW_FRAME = (1000, 430, 1140, 700)
MAG = 3


def sign_box(img):
    """Locate the graphite cloth: the darkest pixels with a warm-white neighbour."""
    a = np.asarray(img.convert("RGB")).astype(int)
    dark = (a.sum(axis=2) < 90)
    ys, xs = np.nonzero(dark)
    if len(xs) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def main():
    old, new = sys.argv[1], sys.argv[2]
    out = os.path.join(RUNS, sys.argv[3] if len(sys.argv) > 3 else "art-0-sign-cells")
    os.makedirs(out, exist_ok=True)
    window = WINDOW_FRAME
    if len(sys.argv) > 5:
        window = tuple(int(v) for v in sys.argv[5].split(","))
    frame = sys.argv[4] if len(sys.argv) > 4 else "level"

    panels = {}
    for label in (old, new):
        img = Image.open(os.path.join(RUNS, "%s_%s.png" % (label, frame))).convert("RGB")
        crop = img.crop(window)
        # an integer NEAREST factor is lossless, so the panel is exactly the game's pixels
        crop = crop.resize((crop.size[0] * MAG, crop.size[1] * MAG), Image.NEAREST)
        panels[label] = crop
        print("%s: %s still, window %s, panel %dx%d"
              % (label, frame, window, crop.size[0], crop.size[1]))

    pair = [old, new]
    random.Random(4).shuffle(pair)
    ordered = [panels[p] for p in pair]
    pad, sep = 8, 8
    w = sum(p.size[0] for p in ordered) + sep + 2 * pad
    h = max(p.size[1] for p in ordered) + 2 * pad
    sheet = Image.new("RGB", (w, h), (128, 128, 128))
    d = ImageDraw.Draw(sheet)
    x = pad
    for p in ordered:
        sheet.paste(p, (x, pad))
        x += p.size[0] + sep
    d.rectangle([pad + ordered[0].size[0] + 1, 0, pad + ordered[0].size[0] + sep - 2, h], fill=(255, 0, 255))
    path = os.path.join(out, "frame_%s_%d_%d_%s_vs_%s.png" % (frame, window[0], window[1], old, new))
    sheet.save(path)
    with open(os.path.join(out, "map_frame.txt"), "a", encoding="utf-8", newline="\n") as fh:
        fh.write("%s : LEFT=%s RIGHT=%s\n" % (os.path.basename(path), pair[0], pair[1]))
        fh.write("  both panels: the same window %s of the %s still of the two captures, %dx NEAREST.\n"
                 "  Same camera, same moment in the capture schedule.\n" % (window, frame, MAG))
    print("wrote %s" % path)
    print("mapping: LEFT=%s RIGHT=%s" % (pair[0], pair[1]))


if __name__ == "__main__":
    main()
