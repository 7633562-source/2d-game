#!/usr/bin/env python3
"""ART-0 32nd pass: an A/B sheet for TWO canvases (not two shipped stills).

`Tools/art0-ab-tight.py` compares two frames of the catalogue. The question this pass asks -
"is the toe row a rhythm or a stamped fence, and is the grain tied to the toes" - is a
question about the CANVAS, and at the shipped lens the foot is 114 x 32 px of a 1920 x 1080
frame: the judge's own downscale then eats the very detail under review. So this tool puts
the two canvases side by side at a fixed NEAREST zoom, no words on the picture, and writes
the mapping to a separate `-map.txt` to be opened AFTER a side has been named.

    python Tools/art0-canvas-ab.py <pngA> <pngB> <zoom> <seed> [tag] [layout]

`layout` is `side` (default, 2 wide) or `stack` (2 tall): a vision judge downscales a very
wide sheet to fit its own window, so the aspect of the sheet is a real variable of the read.

The scale bar is NOT drawn: both panels are the same canvas size by construction (288 x 80),
and the tool refuses to compare two different sizes - that is what would make a zoom lie.
"""
import os
import random
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Trials", "Runs", "art-0-ab")
SEP = (255, 0, 255)


def main():
    if len(sys.argv) < 5:
        print(__doc__)
        return 2
    a_path, b_path = sys.argv[1], sys.argv[2]
    zoom = int(sys.argv[3])
    seed = int(sys.argv[4])
    tag = sys.argv[5] if len(sys.argv) > 5 else "canvas"

    imgs = {}
    for label, path in (("A", a_path), ("B", b_path)):
        im = Image.open(path).convert("RGBA")
        imgs[label] = im
    if imgs["A"].size != imgs["B"].size:
        print("REFUSED: canvases differ in size %s vs %s - a zoom would be a lie"
              % (imgs["A"].size, imgs["B"].size))
        return 1

    # the panels are pasted on the gallery's own backdrop, so the transparent background of a
    # canvas panel and the separator cannot be confused
    bg = Image.new("RGB", imgs["A"].size, (76, 82, 92))
    panels = {}
    for label in ("A", "B"):
        p = bg.copy()
        a = np.asarray(imgs[label].convert("RGBA"))
        base = np.asarray(p).astype(np.float64)
        al = (a[:, :, 3:4].astype(np.float64)) / 255.0
        base = a[:, :, :3] * al + base * (1.0 - al)
        p = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGB")
        panels[label] = p.resize((p.width * zoom, p.height * zoom), Image.NEAREST)

    rng = random.Random(seed)
    first, second = ("A", "B") if rng.random() < 0.5 else ("B", "A")
    left, right = panels[first], panels[second]
    layout = sys.argv[6] if len(sys.argv) > 6 else "side"
    if layout == "stack":
        sheet = Image.new("RGB", (max(left.width, right.width), left.height + 6 + right.height),
                          SEP)
        sheet.paste(left, (0, 0))
        sheet.paste(right, (0, left.height + 6))
    else:
        sheet = Image.new("RGB", (left.width + 6 + right.width, max(left.height, right.height)),
                          SEP)
        sheet.paste(left, (0, 0))
        sheet.paste(right, (left.width + 6, 0))

    os.makedirs(OUT_DIR, exist_ok=True)
    out = os.path.join(OUT_DIR, "%s_seed%d.png" % (tag, seed))
    sheet.save(out)
    with open(out.replace(".png", "-map.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("panel 1 (LEFT)  = %s\npanel 2 (RIGHT) = %s\n\n"
                 "read the picture first, then this file\n"
                 % (os.path.basename(a_path) if first == "A" else os.path.basename(b_path),
                    os.path.basename(b_path) if second == "B" else os.path.basename(a_path)))
    print("A=%s\nB=%s" % (a_path, b_path))
    print("crop none, zoom=%dx  panels %dx%d each, sheet %dx%d"
          % (zoom, left.width, left.height, sheet.width, sheet.height))
    print("wrote %s + %s" % (out.replace("\\", "/"),
                             os.path.basename(out).replace(".png", "-map.txt")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
